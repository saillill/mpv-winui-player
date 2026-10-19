using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace mpv_winui.Modules.Player.Audio;

/// <summary>
/// Decodes a cover into pixels and hands them to the extractor.
///
/// This is the half that needs WinRT, and it is kept apart from
/// <see cref="AlbumColorExtractor"/> on purpose: the arithmetic there is asserted
/// by number, and a decoder in the same file would make those tests need an image.
///
/// Deliberately small: the cover is decoded to a 48x48 thumbnail before the
/// pixels are read. The dominant colour of a 48x48 version of a picture is the
/// dominant colour of the picture, and reading a 1500x1500 cover in full would
/// mean a 9 MB buffer and a visible hitch when a track starts.
/// </summary>
internal static class ArtworkPalette
{
    /// <summary>Thumbnail edge. 48 is 2304 pixels - enough for a stable histogram.</summary>
    private const int ThumbnailSize = 48;

    /// <summary>
    /// The palette of a cover stream, or <see cref="AlbumColorExtractor.None"/>.
    ///
    /// Never throws: a cover that cannot be decoded is a cover without a colour,
    /// and the page keeps its neutral background.
    /// </summary>
    internal static async Task<AlbumColorExtractor.Palette> FromStreamAsync(IRandomAccessStream stream)
    {
        if (stream is null || stream.Size == 0)
        {
            return AlbumColorExtractor.None;
        }

        try
        {
            // Rewound because the caller has just finished reading the stream for
            // the Image itself, and BitmapDecoder starts where the position is.
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);

            var transform = new BitmapTransform
            {
                ScaledWidth = ThumbnailSize,
                ScaledHeight = ThumbnailSize,

                // Fill the square rather than letterbox it: a panoramic cover
                // letterboxed to 48x48 is mostly bars, and the bars would be the
                // dominant "colour".
                InterpolationMode = BitmapInterpolationMode.Fant,
            };

            var pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var bytes = pixels.DetachPixelData();

            return AlbumColorExtractor.Extract(bytes, ThumbnailSize, ThumbnailSize);
        }
        catch (Exception)
        {
            // Unsupported format, corrupt data, decoder refusal: all one outcome.
            return AlbumColorExtractor.None;
        }
    }

    /// <summary>
    /// A brush for a packed ARGB value, at the given opacity.
    ///
    /// Fresh per call rather than cached: a SolidColorBrush is a XAML object with
    /// thread affinity, and the album page and the music page must not share one.
    /// </summary>
    internal static SolidColorBrush Brush(uint argb, double opacity = 1.0)
    {
        var color = Color.FromArgb(
            (byte)(argb >> 24),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));

        return new SolidColorBrush(color) { Opacity = opacity };
    }
}
