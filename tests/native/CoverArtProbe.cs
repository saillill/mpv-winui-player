using System;
using System.IO;
using System.Threading.Tasks;
using mpv_winrt;

namespace mpv_winrt_test;

/// <summary>
/// The cover-art path the music page depends on, driven directly.
///
/// "The cover does not show" is not observable from a layout test, but the data
/// path underneath it is: if TryGetCoverArt returns false, or returns an empty
/// stream, the bug is here and not in the XAML.
///
/// Explicit, not part of the default suite: it needs the native payloads beside
/// the test binary and a tagged media file to read.
/// </summary>
[TestFixture]
[Explicit("Probe: needs the native payloads and a tagged media file.")]
public class CoverArtProbe
{
    /// <summary>
    /// A media file with cover art, supplied by the person running the probe.
    ///
    /// This was a constant pointing at D:\Workspace\WorkBuddy\... - one
    /// developer machine's scratch directory. It never existed on any other
    /// checkout, so the fixture could only ever report "test clip not present"
    /// and the probe has never actually run anywhere. An environment variable
    /// keeps the probe honest: unset still skips (an [Explicit] fixture must not
    /// fail a normal run), but set runs it.
    /// </summary>
    private static readonly string? Clip =
        Environment.GetEnvironmentVariable("MVPW_COVERART_CLIP");

    [Test]
    public async Task CoverArtComesBackAsAJpegStream()
    {
        var clip = Clip;
        if (string.IsNullOrEmpty(clip) || !File.Exists(clip))
        {
            Assert.Ignore("set MVPW_COVERART_CLIP to a media file with cover art");
            return;
        }

        var player = new MpvPlayer();
        player.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

        player.LoadFile(clip, 0);

        // LoadFile is asynchronous inside mpv; the path property is what the
        // cover getter keys off, so wait for it rather than for a fixed delay.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5) && string.IsNullOrEmpty(player.GetCurrentPath()))
        {
            await Task.Delay(50);
        }

        TestContext.Out.WriteLine($"current path : {player.GetCurrentPath()}");

        var tags = player.GetMediaTags();
        TestContext.Out.WriteLine($"tags         : {tags.Count}");
        foreach (var pair in tags)
        {
            TestContext.Out.WriteLine($"               {pair.Key} = {pair.Value}");
        }

        var hasCover = player.TryGetCoverArt(out var stream);
        TestContext.Out.WriteLine($"TryGetCoverArt: {hasCover}");

        if (hasCover && stream is not null)
        {
            TestContext.Out.WriteLine($"stream size  : {stream.Size}");
            TestContext.Out.WriteLine($"mime         : {player.GetCoverArtMimeType()}");

            var buffer = new byte[8];
            using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(8);
            reader.ReadBytes(buffer);
            TestContext.Out.WriteLine($"first bytes  : {BitConverter.ToString(buffer)}");
        }

        Assert.That(hasCover, Is.True, "cover art was not returned for a file that carries it");
        Assert.That(stream!.Size, Is.GreaterThan(1000), "cover stream is empty or truncated");

        player.Destroy();
    }

    [Test]
    public async Task CoverArtForAPathReadsThatFileRatherThanThePlayingOne()
    {
        var clip = Clip;
        if (string.IsNullOrEmpty(clip) || !File.Exists(clip))
        {
            Assert.Ignore("set MVPW_COVERART_CLIP to a media file with cover art");
            return;
        }

        var player = new MpvPlayer();
        player.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

        var hasCover = player.TryGetCoverArtForPath(clip, out var stream);
        TestContext.Out.WriteLine($"TryGetCoverArtForPath with nothing playing: {hasCover}, size {stream?.Size}");

        Assert.That(hasCover, Is.True,
            "the album page reads a track's cover while a different file may be playing, "
            + "so this must not depend on the loaded file");

        player.Destroy();
        await Task.CompletedTask;
    }
}
