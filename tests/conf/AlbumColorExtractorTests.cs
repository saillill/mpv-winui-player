using System;
using mpv_winui.Domains.Playback.Audio;

namespace mpv_winui_test;

/// <summary>
/// The cover-colour arithmetic, by number.
///
/// The question this guards is not "does it produce a colour" - it always will -
/// but "does it produce the colour a person would name if asked". Every case here
/// is a picture whose answer is unambiguous, so a regression shows up as the wrong
/// hue rather than as a plausible-looking different one.
/// </summary>
[TestFixture]
public class AlbumColorExtractorTests
{
    /// <summary>A solid BGRA8 image.</summary>
    private static byte[] Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var pixels = new byte[width * height * 4];

        for (var i = 0; i < width * height; i++)
        {
            pixels[(i * 4) + 0] = b;
            pixels[(i * 4) + 1] = g;
            pixels[(i * 4) + 2] = r;
            pixels[(i * 4) + 3] = a;
        }

        return pixels;
    }

    /// <summary>Hue of a packed ARGB colour, in degrees.</summary>
    private static double HueOf(uint argb) =>
        HueOf((byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    /// <summary>Hue of an RGB triple, in degrees. Same formula on both sides of an assertion.</summary>
    private static double HueOf(byte r, byte g, byte b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        var delta = (double)(max - min);

        if (delta == 0)
        {
            return 0;
        }

        double hue;

        if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        return hue < 0 ? hue + 360 : hue;
    }

    [TestCase(220, 30, 30)]     // red
    [TestCase(30, 200, 40)]     // green
    [TestCase(40, 60, 210)]     // blue
    [TestCase(190, 170, 40)]    // yellow
    [TestCase(160, 50, 190)]    // purple
    public void ASolidColourKeepsItsHue(byte r, byte g, byte b)
    {
        var palette = AlbumColorExtractor.Extract(Solid(64, 64, r, g, b), 64, 64);

        Assert.That(palette.Accent, Is.Not.EqualTo(0u), "a saturated image must yield a palette");

        // Against the SOURCE hue, not a number typed by hand. The extractor is
        // allowed to change saturation and lightness - that is its job - but the
        // hue must come back intact.
        var expected = HueOf(r, g, b);
        var actual = HueOf(palette.Accent);

        Assert.That(actual, Is.EqualTo(expected).Within(6.0),
            $"accent #{palette.Accent:X8} has hue {actual:F1}, source is {expected:F1}");
    }

    [Test]
    public void TheDominantColourWinsOverASmallerAccent()
    {
        // Three quarters blue, one quarter red: the answer is blue, which is what
        // "dominant" means and what an average would turn into purple.
        var pixels = new byte[64 * 64 * 4];

        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var blue = x >= 16;

                var offset = ((y * 64) + x) * 4;

                pixels[offset + 0] = blue ? (byte)200 : (byte)30;   // B
                pixels[offset + 1] = blue ? (byte)60 : (byte)30;    // G
                pixels[offset + 2] = blue ? (byte)40 : (byte)220;   // R
                pixels[offset + 3] = 255;
            }
        }

        var palette = AlbumColorExtractor.Extract(pixels, 64, 64);

        var hue = HueOf(palette.Accent);

        // The blue region's own hue, not a guess at what blue is.
        var expected = HueOf((byte)40, (byte)60, (byte)200);

        Assert.That(hue, Is.EqualTo(expected).Within(10.0),
            $"expected the blue region (hue {expected:F0}) to dominate, "
            + $"got hue {hue:F0} (#{palette.Accent:X8})");
    }

    [Test]
    public void AGreyscaleCoverYieldsNothingRatherThanAGrey()
    {
        // No hue to extract. Returning a desaturated colour would tint the page
        // for no reason, so the answer is "no palette" and the caller keeps its
        // neutral background.
        var palette = AlbumColorExtractor.Extract(Solid(32, 32, 128, 128, 128), 32, 32);

        Assert.That(palette.Accent, Is.EqualTo(0u));
        Assert.That(palette.Surface, Is.EqualTo(0u));
    }

    [Test]
    public void ABlackCoverYieldsNothing()
    {
        Assert.That(AlbumColorExtractor.Extract(Solid(32, 32, 0, 0, 0), 32, 32).Accent,
            Is.EqualTo(0u));
    }

    [Test]
    public void AWhiteCoverYieldsNothing()
    {
        Assert.That(AlbumColorExtractor.Extract(Solid(32, 32, 255, 255, 255), 32, 32).Accent,
            Is.EqualTo(0u));
    }

    [Test]
    public void TransparentPixelsDoNotCount()
    {
        // A red image that is mostly transparent: the visible part is still red,
        // and the padding must not be read as a colour of its own.
        var pixels = Solid(32, 32, 200, 30, 30);

        for (var i = 0; i < 32 * 32 * 3 / 4; i++)
        {
            pixels[(i * 4) + 3] = 0;
        }

        var palette = AlbumColorExtractor.Extract(pixels, 32, 32);

        Assert.That(HueOf(palette.Accent), Is.EqualTo(0.0).Within(15.0));
    }

    [Test]
    public void TheSurfaceTintIsDarkerThanTheAccent()
    {
        var palette = AlbumColorExtractor.Extract(Solid(32, 32, 40, 160, 200), 32, 32);

        double Luma(uint argb) =>
            (0.299 * ((argb >> 16) & 0xFF)) + (0.587 * ((argb >> 8) & 0xFF)) + (0.114 * (argb & 0xFF));

        Assert.That(Luma(palette.Surface), Is.LessThan(Luma(palette.Accent) * 0.55),
            "the page tint must be dark enough to carry light text over it");
    }

    [Test]
    public void TheSurfaceKeepsTheAccentsHue()
    {
        // The point of extracting at all: the background and the accent agree.
        var palette = AlbumColorExtractor.Extract(Solid(32, 32, 30, 190, 90), 32, 32);

        Assert.That(HueOf(palette.Surface), Is.EqualTo(HueOf(palette.Accent)).Within(8.0),
            "the page tint drifted off the accent's hue");
    }

    [Test]
    public void AStrideWiderThanThePixelsIsHonoured()
    {
        // Decoders hand back padded rows. Reading them as if they were tight
        // shifts the image a little more with every row, which changes the picture
        // without throwing.
        const int width = 16;
        const int height = 16;
        const int stride = 16 * 4 + 12;

        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);

                pixels[offset + 0] = 200;   // B
                pixels[offset + 1] = 50;    // G
                pixels[offset + 2] = 40;    // R
                pixels[offset + 3] = 255;
            }
        }

        var palette = AlbumColorExtractor.Extract(pixels, width, height, stride);

        var expected = HueOf((byte)40, (byte)50, (byte)200);

        Assert.That(HueOf(palette.Accent), Is.EqualTo(expected).Within(10.0),
            "a padded stride was read as if it were tight");
    }

    [Test]
    public void DegenerateSizesReturnNothing()
    {
        Assert.That(AlbumColorExtractor.Extract(ReadOnlySpan<byte>.Empty, 0, 0).Accent, Is.EqualTo(0u));

        // A buffer shorter than the declared size is a caller bug, not a picture.
        Assert.That(AlbumColorExtractor.Extract(new byte[16], 64, 64).Accent, Is.EqualTo(0u));
    }
}
