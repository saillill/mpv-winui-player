using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Player.Audio;

/// <summary>
/// Picks the colour a page should take from its cover art.
///
/// The reference players do this and it is the difference between "a picture on a
/// grey page" and "the page belongs to the album". The arithmetic is here, on
/// plain arrays, so it can be asserted by number; the decoding lives in
/// <c>ArtworkPalette</c>, which is the part that needs WinRT.
///
/// The method is a coarse histogram in a perceptual-ish space rather than a
/// straight average:
///
///  * An average of a photograph is mud. Every hue present, weighted by area,
///    lands on a desaturated brown, and every album ends up the same colour.
///  * So: quantise to 5 bits per channel, count, and take the most populated
///    bucket that is not nearly grey and not nearly black or white. That is the
///    "dominant colour" the eye reports.
///  * Then raise its saturation and pin its lightness into a band that text can
///    sit on. An extracted colour is a *starting point* for a themed surface, not
///    a value to use as-is; unmodified it is either invisible against the page or
///    bright enough to make white text unreadable.
/// </summary>
internal static class AlbumColorExtractor
{
    /// <summary>
    /// A colour and the tint that goes with it.
    /// </summary>
    /// <param name="Accent">Saturated, mid-lightness: safe for a bar or a glow.</param>
    /// <param name="Surface">The same hue, darkened: safe as a page background
    /// behind light text.</param>
    internal readonly record struct Palette(uint Accent, uint Surface);

    /// <summary>What a cover with nothing usable in it falls back to.</summary>
    internal static readonly Palette None = new(0x00000000u, 0x00000000u);

    /// <summary>
    /// The dominant colour of BGRA8 pixels, or <see cref="None"/>.
    /// </summary>
    /// <param name="bgra">Pixels, 4 bytes each, row-major.</param>
    /// <param name="stride">Bytes per row. 0 means <c>width * 4</c>.</param>
    internal static Palette Extract(ReadOnlySpan<byte> bgra, int width, int height, int stride = 0)
    {
        if (width <= 0 || height <= 0)
        {
            return None;
        }

        if (stride <= 0)
        {
            stride = width * 4;
        }

        if (bgra.Length < stride * height)
        {
            return None;
        }

        // 32 levels per channel: 5 bits, which is 32768 buckets. Coarse enough that
        // one bucket is a real region of the picture, fine enough that two albums
        // do not collapse into the same answer.
        var counts = new Dictionary<int, int>();

        long sampled = 0;

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;

            for (var x = 0; x < width; x++)
            {
                var offset = row + (x * 4);

                var b = bgra[offset];
                var g = bgra[offset + 1];
                var r = bgra[offset + 2];
                var a = bgra[offset + 3];

                // Transparent pixels are padding, not colour.
                if (a < 32)
                {
                    continue;
                }

                sampled++;

                if (!IsUsable(r, g, b))
                {
                    continue;
                }

                var key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);

                counts.TryGetValue(key, out var existing);
                counts[key] = existing + 1;
            }
        }

        if (counts.Count == 0)
        {
            // A monochrome, black or white cover: no hue to take. Better to leave
            // the page neutral than to invent a colour for it.
            return None;
        }

        var bestKey = 0;
        var bestCount = -1;

        foreach (var pair in counts)
        {
            if (pair.Value > bestCount)
            {
                bestKey = pair.Key;
                bestCount = pair.Value;
            }
        }

        // Bucket centre, not bucket corner: the corner is up to 7 levels dark.
        var centreR = (((bestKey >> 10) & 0x1F) << 3) + 4;
        var centreG = (((bestKey >> 5) & 0x1F) << 3) + 4;
        var centreB = ((bestKey & 0x1F) << 3) + 4;

        RgbToHsl(centreR, centreG, centreB, out var hue, out _, out _);

        // Saturated and mid-light: bright enough to read as a colour, dark enough
        // that white text on it still contrasts.
        var accent = HslToRgb(hue, 0.62, 0.52);

        // The page tint: the same hue, driven well down. The point is that the
        // background changes with the album, not that it competes with the cover.
        var surface = HslToRgb(hue, 0.38, 0.10);

        return new Palette(Pack(accent), Pack(surface));
    }

    /// <summary>
    /// Rejects pixels that cannot give a usable hue.
    ///
    /// Near-grey has no hue worth extracting, and near-black or near-white has too
    /// little of everything else. Without this filter, a cover that is mostly dark
    /// background - which is most covers - answers "black" every time.
    /// </summary>
    private static bool IsUsable(byte r, byte g, byte b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        var lightness = (max + min) / 2.0 / 255.0;

        if (lightness < 0.12 || lightness > 0.94)
        {
            return false;
        }

        var saturation = max == min
            ? 0.0
            : (max - min) / (255.0 - Math.Abs((max + min) - 255.0));

        return saturation >= 0.16;
    }

    /// <summary>Hue in degrees 0..360, saturation and lightness 0..1.</summary>
    private static void RgbToHsl(int r, int g, int b, out double hue, out double saturation, out double lightness)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));

        var delta = max - min;

        lightness = (max + min) / 2.0;

        if (delta <= 0)
        {
            hue = 0;
            saturation = 0;
            return;
        }

        saturation = delta / (1.0 - Math.Abs((2.0 * lightness) - 1.0));

        if (max == rf)
        {
            hue = 60.0 * (((gf - bf) / delta) % 6.0);
        }
        else if (max == gf)
        {
            hue = 60.0 * (((bf - rf) / delta) + 2.0);
        }
        else
        {
            hue = 60.0 * (((rf - gf) / delta) + 4.0);
        }

        if (hue < 0)
        {
            hue += 360.0;
        }
    }

    private static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
    {
        hue %= 360.0;

        if (hue < 0)
        {
            hue += 360.0;
        }

        var c = (1.0 - Math.Abs((2.0 * lightness) - 1.0)) * saturation;

        var x = c * (1.0 - Math.Abs(((hue / 60.0) % 2.0) - 1.0));

        var m = lightness - (c / 2.0);

        var (r, g, b) = hue switch
        {
            < 60.0 => (c, x, 0.0),
            < 120.0 => (x, c, 0.0),
            < 180.0 => (0.0, c, x),
            < 240.0 => (0.0, x, c),
            < 300.0 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return (ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);

    /// <summary>Opaque ARGB, the form a WinUI Color takes.</summary>
    private static uint Pack((byte R, byte G, byte B) rgb) =>
        0xFF000000u | ((uint)rgb.R << 16) | ((uint)rgb.G << 8) | rgb.B;
}
