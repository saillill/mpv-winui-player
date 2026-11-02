using mpv_winui.Domains.Playlist;

namespace mpv_conf_test;

/// <summary>
/// The named-playlist store, driven against real files in a temporary directory.
///
/// <para><b>Why real files.</b> Every property worth asserting here is a property
/// of what lands on disk: that one broken file does not hide the rest, that adding
/// a file twice does not duplicate an entry, that a rename refuses to eat the list
/// it would land on. A fake file system would let all three pass while the real
/// one failed - and the defect this class was written against, a data path built
/// by hand one directory short, was invisible to every check except one that
/// looked at where the bytes went.</para>
///
/// <para><b>What is not covered.</b> The parameterless constructor is not
/// exercised: the folder it resolves comes from <c>AppData</c>, and asserting it
/// would be asserting WinUI's behaviour rather than this class's. That the folder
/// is <c>ResolveLocalData("playlists")</c> is one line, and it is the line this
/// fixture's injected directory deliberately steps around.</para>
/// </summary>
[TestFixture]
public class NamedPlaylistStoreTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "mpvw-playlists-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary folder is not worth failing a test over.
        }
    }

    /// <summary>Nothing but the files, ignoring the order the file system lists them in.</summary>
    private string[] FilesInDirectory()
    {
        return Directory.GetFiles(_directory)
            .Select(file => Path.GetFileName(file))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    [Test]
    public void TheConstructorCreatesTheFolder()
    {
        _ = new NamedPlaylistStore(_directory);

        Assert.That(Directory.Exists(_directory), Is.True);
    }

    [Test]
    public void ListOnAnEmptyFolderIsEmpty()
    {
        Assert.That(new NamedPlaylistStore(_directory).List(), Is.Empty);
    }

    [Test]
    public void CreateWritesOneFileNamedAfterTheList()
    {
        new NamedPlaylistStore(_directory).Create("2026 秋", [@"C:\media\a.mp4"]);

        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "2026 秋.m3u" }));
    }

    /// <summary>
    /// The count is parsed entries, not lines. The file carries a header and one
    /// <c>#EXTINF</c> per entry, so a line count would say four for two entries.
    /// </summary>
    [Test]
    public void CountIsEntriesNotLines()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Mine", [@"C:\media\a.mp4", @"C:\media\b.mp4"]);

        var list = store.List().Single();

        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list.Paths, Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\b.mp4" }));
        Assert.That(
            File.ReadAllLines(Path.Combine(_directory, "Mine.m3u")).Length,
            Is.EqualTo(5),
            "header plus two EXTINF/path pairs: the number Count must not report");
    }

    [Test]
    public void CreateOfNothingStillWritesALoadableFile()
    {
        // "New playlist" from the empty state, with nothing ticked. mpv's loadlist
        // must accept the file rather than find a zero-byte one.
        var store = new NamedPlaylistStore(_directory);

        store.Create("Empty", []);

        Assert.That(store.List().Single().Count, Is.Zero);
        Assert.That(File.ReadAllText(Path.Combine(_directory, "Empty.m3u")), Is.EqualTo("#EXTM3U"));
    }

    [Test]
    public void AppendSkipsEntriesTheListAlreadyHolds()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Mine", [@"C:\media\a.mp4"]);
        store.Append("Mine", [@"C:\media\a.mp4", @"C:\media\b.mp4"]);

        Assert.That(
            store.List().Single().Paths,
            Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\b.mp4" }));
    }

    [Test]
    public void AppendComparesPathsIgnoringCase()
    {
        // Two spellings of one Windows path are one file. A list that showed both
        // would play the same file twice.
        var store = new NamedPlaylistStore(_directory);

        store.Create("Mine", [@"C:\Media\A.mp4"]);
        store.Append("Mine", [@"c:\media\a.MP4"]);

        Assert.That(store.List().Single().Count, Is.EqualTo(1));
    }

    [Test]
    public void AppendToANameWithNoListYetCreatesIt()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Append("Fresh", [@"C:\media\a.mp4"]);

        Assert.That(store.List().Single().Paths, Is.EqualTo(new[] { @"C:\media\a.mp4" }));
    }

    [Test]
    public void CreateOnAnExistingNameAddsRatherThanReplaces()
    {
        // The naming dialog treats a typed existing name as "add these to that
        // list". Replacing here would make the dialog a lie.
        var store = new NamedPlaylistStore(_directory);

        store.Create("Mine", [@"C:\media\a.mp4"]);
        store.Create("Mine", [@"C:\media\b.mp4"]);

        Assert.That(
            store.List().Single().Paths,
            Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\b.mp4" }));
    }

    [Test]
    public void ListSortsByNameRegardlessOfFileOrder()
    {
        var store = new NamedPlaylistStore(_directory);

        // Written in the order a folder happens to hand files back, which on a
        // fresh NTFS directory is creation order. The assertion is that the store
        // does not pass that through.
        store.Create("zulu", [@"C:\media\z.mp4"]);
        store.Create("Alpha", [@"C:\media\a.mp4"]);
        store.Create("mike", [@"C:\media\m.mp4"]);

        Assert.That(
            store.List().Select(list => list.Name),
            Is.EqualTo(new[] { "Alpha", "mike", "zulu" }),
            "case-insensitive name order, not creation or enumeration order");
    }

    [Test]
    public void AnUnreadableFileIsSkippedAndTheOthersSurvive()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Good", [@"C:\media\a.mp4"]);

        // A file nothing wrote through the store: locked by another handle, which
        // is the realistic shape of "unreadable" on this platform. One list is not
        // worth losing the others over.
        var locked = Path.Combine(_directory, "Locked.m3u");
        File.WriteAllText(locked, "#EXTM3U");
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var lists = store.List();

            // Materialised before comparing. NUnit compares a lazy IEnumerable
            // against an array by shape, and the failure it prints ("values differ
            // at index [1]") names no list at all - which is how this assertion
            // spent a run failing for a reason it did not describe.
            Assert.That(
                lists.Select(list => list.Name).ToArray(),
                Is.EqualTo(new[] { "Good" }));
        }
    }

    [Test]
    public void GarbageInAFileReadsAsAnEmptyListRatherThanFailing()
    {
        // What a truncated or mis-encoded file looks like. It must appear as a list
        // with no entries, not take the section down and not vanish.
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "Broken.m3u"), [0x00, 0x01, 0x02]);

        var list = new NamedPlaylistStore(_directory).List().Single();

        Assert.That(list.Name, Is.EqualTo("Broken"));
        Assert.That(list.Count, Is.Zero);
    }

    [Test]
    public void OnlyM3uFilesCount()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "#EXTM3U");

        // A near-miss extension, which a "*.<ext>" Win32 pattern can match through
        // an 8.3 short name. It must not appear as a list whose name has no file.
        File.WriteAllText(Path.Combine(_directory, "almost.m3u8"), "#EXTM3U");

        Assert.That(new NamedPlaylistStore(_directory).List(), Is.Empty);
    }

    [Test]
    public void RenameMovesTheFileAndItsEntries()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Old", [@"C:\media\a.mp4"]);
        store.Rename("Old", "New");

        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "New.m3u" }));
        Assert.That(store.List().Single().Paths, Is.EqualTo(new[] { @"C:\media\a.mp4" }));
    }

    [Test]
    public void RenameRefusesToOverwriteAnExistingList()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("One", [@"C:\media\1.mp4"]);
        store.Create("Two", [@"C:\media\2.mp4"]);

        store.Rename("One", "Two");

        // Both untouched: renaming onto an occupied name would have to destroy one
        // of them, and the user asked for neither.
        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "One.m3u", "Two.m3u" }));
        Assert.That(
            store.List().Select(list => list.Paths.Single()),
            Is.EqualTo(new[] { @"C:\media\1.mp4", @"C:\media\2.mp4" }));
    }

    [Test]
    public void RenameThatOnlyChangesCaseIsAllowed()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("autumn", [@"C:\media\a.mp4"]);
        store.Rename("autumn", "Autumn");

        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "Autumn.m3u" }));
    }

    [Test]
    public void RenameToAnInvalidNameChangesNothingAndDoesNotThrow()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Keep", [@"C:\media\a.mp4"]);

        // A separator cannot be in a file name. The store must log and return: the
        // UI that called this has nowhere to put an exception.
        Assert.DoesNotThrow(() => store.Rename("Keep", "a/b"));
        Assert.DoesNotThrow(() => store.Rename("Keep", "  padded  "));

        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "Keep.m3u" }));
    }

    [Test]
    public void RenameOfAMissingListIsANoOp()
    {
        var store = new NamedPlaylistStore(_directory);
        store.Create("Real", [@"C:\media\a.mp4"]);

        Assert.DoesNotThrow(() => store.Rename("NeverExisted", "Other"));

        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "Real.m3u" }));
    }

    [Test]
    public void DeleteRemovesOnlyTheNamedList()
    {
        var store = new NamedPlaylistStore(_directory);

        store.Create("Gone", [@"C:\media\a.mp4"]);
        store.Create("Stays", [@"C:\media\b.mp4"]);

        store.Delete("Gone");

        Assert.That(store.List().Select(list => list.Name), Is.EqualTo(new[] { "Stays" }));
    }

    [Test]
    public void DeleteOfAMissingListIsNotAnError()
    {
        Assert.DoesNotThrow(() => new NamedPlaylistStore(_directory).Delete("NeverExisted"));
    }

    [Test]
    public void AnInvalidNameWritesNothingForAnyOperation()
    {
        var store = new NamedPlaylistStore(_directory);

        Assert.DoesNotThrow(() => store.Create("bad:name", new[] { @"C:\media\a.mp4" }));
        Assert.DoesNotThrow(() => store.Append("bad*name", new[] { @"C:\media\a.mp4" }));
        Assert.DoesNotThrow(() => store.Create("", new[] { @"C:\media\a.mp4" }));
        Assert.DoesNotThrow(() => store.Create(null!, [@"C:\media\a.mp4"]));

        Assert.That(store.List(), Is.Empty);
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
    }

    [Test]
    public void AppendToALockedListDoesNotRewriteIt()
    {
        // The one failure that loses data rather than skipping it: if the existing
        // file cannot be read, merging would start from nothing and the write would
        // replace a real list with only the new entries.
        var store = new NamedPlaylistStore(_directory);
        store.Create("Mine", [@"C:\media\a.mp4"]);

        var file = Path.Combine(_directory, "Mine.m3u");
        var before = File.ReadAllText(file);

        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.DoesNotThrow(() => store.Append("Mine", new[] { @"C:\media\b.mp4" }));
        }

        Assert.That(File.ReadAllText(file), Is.EqualTo(before));
    }

    [Test]
    public void EntriesThatWouldNotReadBackAsThemselvesAreDropped()
    {
        // A path holding a line break would be written as two entries, and one
        // holding a character no path can contain would be dropped on the way back
        // in. Either way the count straight after a write would disagree with the
        // count the next read reports, for a list nobody edited.
        var store = new NamedPlaylistStore(_directory);

        store.Create("Mine", [@"C:\media\a.mp4", "C:\\media\\two\r\nlines.mp4", "C:\\bad\u0000.mp4"]);

        Assert.That(store.List().Single().Paths, Is.EqualTo(new[] { @"C:\media\a.mp4" }));
    }

    /// <summary>
    /// The <c>#EXTINF</c> title is the entry's own file name. It is what every
    /// player shows for an m3u; entries all titled "entry" would make the list
    /// unreadable outside this app.
    /// </summary>
    [Test]
    public void EntriesAreWrittenWithTheirFileNamesAsTitles()
    {
        new NamedPlaylistStore(_directory).Create("Mine", [@"C:\media\holiday.mp4"]);

        Assert.That(
            File.ReadAllText(Path.Combine(_directory, "Mine.m3u")),
            Is.EqualTo("#EXTM3U\r\n#EXTINF:-1,holiday\r\n" + @"C:\media\holiday.mp4"));
    }

    /// <summary>
    /// UTF-8 without a byte-order mark, and CRLF, and no temporary left beside it.
    ///
    /// The BOM case is the one worth a test: <c>ParsePaths</c> strips a BOM, so a
    /// writer that added one would keep round-tripping through these very tests
    /// while every other player saw <c>#EXTM3U</c> as a path.
    /// </summary>
    [Test]
    public void TheFileIsUtf8WithoutABomAndNothingElseIsLeftBehind()
    {
        new NamedPlaylistStore(_directory).Create("Mine", [@"C:\media\a.mp4"]);

        var bytes = File.ReadAllBytes(Path.Combine(_directory, "Mine.m3u"));

        Assert.That(bytes[0], Is.EqualTo((byte)'#'), "must not begin with a UTF-8 BOM");
        Assert.That(FilesInDirectory(), Is.EqualTo(new[] { "Mine.m3u" }), "no temporary may survive");
    }

    /// <summary>
    /// What the store writes, <c>NamedPlaylistFile</c> reads back. The two are
    /// linked here together for exactly this assertion: a writer and a reader that
    /// drift apart still each pass their own tests.
    /// </summary>
    [Test]
    public void WhatCreateWritesReadsBackAsTheSamePaths()
    {
        string[] entries = [@"C:\media\a.mp4", @"C:\media\with, a comma.mp4", @"C:\media\#1 hit.mp4"];

        var store = new NamedPlaylistStore(_directory);
        store.Create("Mine", entries);

        Assert.That(store.List().Single().Paths, Is.EqualTo(entries));
    }
}
