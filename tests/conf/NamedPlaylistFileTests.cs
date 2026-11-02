using mpv_winui.Domains.Playlist;

namespace mpv_conf_test;

/// <summary>
/// The named-playlist m3u, asserted against the source the app ships.
///
/// <para>The app already writes this format in
/// <c>MpvPlayerPage_Playlist.ExportPlaylistAsync</c> and reads it by handing the
/// file to mpv's <c>loadlist</c>. The two halves are about to stop being a
/// picker callback and become a store, so what has to hold is narrower and
/// checkable on its own: which lines become paths, and that a title can never
/// split a record in two.</para>
/// </summary>
[TestFixture]
public class NamedPlaylistFileTests
{
    [Test]
    public void ParseReadsBarePathsAndExtinf()
    {
        var content = string.Join("\n",
            "#EXTM3U",
            "#EXTINF:-1,First",
            @"C:\media\a.mp4",
            @"C:\media\b.mp4");

        Assert.That(
            NamedPlaylistFile.ParsePaths(content),
            Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\b.mp4" }));
    }

    [Test]
    public void ParseSkipsCommentsAndBlankLines()
    {
        var content = "#EXTM3U\n\n# a comment\nC:\\a.mp4\n\n";

        Assert.That(NamedPlaylistFile.ParsePaths(content), Is.EqualTo(new[] { @"C:\a.mp4" }));
    }

    [Test]
    public void ParseOfGarbageYieldsNothingRatherThanThrowing()
    {
        Assert.That(NamedPlaylistFile.ParsePaths("\u0000\u0001 not a playlist"), Is.Empty);
    }

    [Test]
    public void FormatSurvivesATitleWithLineBreaks()
    {
        var text = NamedPlaylistFile.Format([("A\r\nB", @"C:\x.mp4")]);

        Assert.That(text, Does.Contain("A B"));
        Assert.That(text.Split('\n').Length, Is.EqualTo(3));
    }

    [Test]
    public void NamesWithPathCharactersAreRejected()
    {
        foreach (var bad in new[] { "a/b", "a\\b", "a:b", "a*b", "a?b", "a\"b", "a<b", "a>b", "a|b" })
        {
            Assert.That(
                NamedPlaylistFile.IsValidName(bad, out var reason), Is.False,
                $"{bad} must be refused - it is a file name");
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void EmptyOrWhitespaceNamesAreRejected()
    {
        Assert.That(NamedPlaylistFile.IsValidName("", out _), Is.False);
        Assert.That(NamedPlaylistFile.IsValidName("   ", out _), Is.False);
        Assert.That(NamedPlaylistFile.IsValidName("  padded  ", out _), Is.False);
    }

    [Test]
    public void AnOrdinaryNameIsAccepted()
    {
        Assert.That(NamedPlaylistFile.IsValidName("2026 秋", out var reason), Is.True);
        Assert.That(reason, Is.Null);
    }

    /// <summary>
    /// The exact bytes: header, one EXTINF per item, the path under it. This is
    /// what <c>ExportPlaylistAsync</c> writes (minus its trailing newline, which
    /// a joined string cannot have), so replacing that StringBuilder loop with
    /// this call cannot change a file a user exported and re-imported.
    /// </summary>
    [Test]
    public void FormatWritesTheHeaderAndExtinfTheExporterWrites()
    {
        var text = NamedPlaylistFile.Format(
            [("First", @"C:\media\a.mp4"), ("Second", @"C:\media\b.mp4")]);

        Assert.That(
            text,
            Is.EqualTo(
                "#EXTM3U\r\n" +
                "#EXTINF:-1,First\r\n" +
                @"C:\media\a.mp4" + "\r\n" +
                "#EXTINF:-1,Second\r\n" +
                @"C:\media\b.mp4"));
    }

    [Test]
    public void FormatOfNothingIsStillAHeader()
    {
        // A playlist can exist and be empty. Writing it must produce something
        // mpv's loadlist accepts rather than a zero-byte file.
        Assert.That(NamedPlaylistFile.Format([]), Is.EqualTo("#EXTM3U"));
    }

    [Test]
    public void ParseOfNothingIsEmpty()
    {
        Assert.That(NamedPlaylistFile.ParsePaths(""), Is.Empty);
        Assert.That(NamedPlaylistFile.ParsePaths("#EXTM3U"), Is.Empty);
    }

    [Test]
    public void ParseTrimsSurroundingWhitespace()
    {
        Assert.That(
            NamedPlaylistFile.ParsePaths("  C:\\a.mp4  \r\n\tC:\\b.mp4\t\r\n"),
            Is.EqualTo(new[] { @"C:\a.mp4", @"C:\b.mp4" }));
    }

    [Test]
    public void ABomInFrontOfTheHeaderIsNotAPath()
    {
        // ReadAllText strips a BOM, so this only bites a caller that decoded the
        // bytes itself. Cheap to survive, and the failure would be a phantom
        // first entry that mpv cannot open.
        var content = "\uFEFF#EXTM3U\nC:\\a.mp4";

        Assert.That(NamedPlaylistFile.ParsePaths(content), Is.EqualTo(new[] { @"C:\a.mp4" }));
    }

    [Test]
    public void AHashInsideAPathDoesNotMakeItAComment()
    {
        // Only a line that BEGINS with '#' is a comment. '#' is legal in a
        // Windows file name, and a title like "#1 hit" is not a rare shape.
        Assert.That(
            NamedPlaylistFile.ParsePaths(@"C:\media\#1 hit.mp4"),
            Is.EqualTo(new[] { @"C:\media\#1 hit.mp4" }));
    }

    [Test]
    public void WhatFormatWritesIsWhatParseReads()
    {
        (string Title, string Path)[] items =
        [
            ("First", @"C:\media\a.mp4"),
            ("A\r\nB", @"C:\media\with, a comma.mp4"),
        ];

        Assert.That(
            NamedPlaylistFile.ParsePaths(NamedPlaylistFile.Format(items)),
            Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\with, a comma.mp4" }));
    }
}
