using mpv_winui.Modules.Common.Utils;
using System.Text;

namespace mpv_conf_test;

/// <summary>
/// The atomic-replace writer, checked against a real file system.
///
/// <para>What is actually being asserted is not "a file was written" - the
/// <c>File.WriteAllText</c> this replaced did that too. It is the properties that
/// made the replacement worth making: the destination is never observed
/// truncated, an overwrite does not leave a tail of the previous contents, no
/// temporary sibling survives a successful write, and the bytes on disk are the
/// ones asked for (UTF-8 without a preamble, matching what the settings layer
/// wrote before).</para>
/// </summary>
[TestFixture]
public class AtomicFileTests
{
    private string _dir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "atomic-file-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    /// <summary>Anything the writer leaves next to the target, other than the target.</summary>
    private string[] Siblings(string name)
    {
        return Directory.GetFiles(_dir)
            .Where(f => !string.Equals(Path.GetFileName(f), name, StringComparison.Ordinal))
            .ToArray();
    }

    [Test]
    public void CreatesTheFileWithTheRequestedContent()
    {
        var path = Path_("mpv.conf");

        AtomicFile.WriteAllText(path, "volume=100\n");

        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo("volume=100\n"));
        Assert.That(Siblings("mpv.conf"), Is.Empty, "no temporary should remain");
    }

    [Test]
    public void OverwriteDoesNotLeaveATailOfThePreviousContents()
    {
        var path = Path_("mpv.conf");
        File.WriteAllText(path, new string('x', 4096));

        AtomicFile.WriteAllText(path, "short");

        Assert.That(File.ReadAllText(path), Is.EqualTo("short"));
        Assert.That(Siblings("mpv.conf"), Is.Empty);
    }

    [Test]
    public void WritesUtf8WithoutAPreamble()
    {
        var path = Path_("mpv.conf");

        AtomicFile.WriteAllText(path, "osd-back-color=\"#AF000000\"", new UTF8Encoding(false));

        var bytes = File.ReadAllBytes(path);
        Assert.That(bytes[0], Is.Not.EqualTo(0xEF), "must not gain a UTF-8 BOM");
        Assert.That(File.ReadAllText(path), Is.EqualTo("osd-back-color=\"#AF000000\""));
    }

    [Test]
    public void CreatesMissingDirectories()
    {
        var path = Path.Combine(_dir, "a", "b", "mpv.conf");

        AtomicFile.WriteAllText(path, "volume=50\n");

        Assert.That(File.ReadAllText(path), Is.EqualTo("volume=50\n"));
    }

    [Test]
    public async Task AsyncOverwriteReplacesTheContents()
    {
        var path = Path_("script-opts.conf");
        await AtomicFile.WriteAllTextAsync(path, "first\n");
        await AtomicFile.WriteAllTextAsync(path, "second\n");

        Assert.That(File.ReadAllText(path), Is.EqualTo("second\n"));
        Assert.That(Siblings("script-opts.conf"), Is.Empty);
    }

    [Test]
    public async Task WriteAllLinesMatchesWriteAllLinesSemantics()
    {
        var path = Path_("menu.conf");

        await AtomicFile.WriteAllLinesAsync(path, new[] { "a=1", "b=2" });

        Assert.That(File.ReadAllText(path),
            Is.EqualTo("a=1" + Environment.NewLine + "b=2" + Environment.NewLine));
    }

    [Test]
    public async Task WriteAllLinesOfNothingWritesAnEmptyFile()
    {
        var path = Path_("empty.conf");

        await AtomicFile.WriteAllLinesAsync(path, Array.Empty<string>());

        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.ReadAllText(path), Is.Empty);
    }

    [Test]
    public void ManyRewritesLeaveExactlyOneFile()
    {
        var path = Path_("mpv.conf");

        for (var i = 0; i < 50; i++)
        {
            AtomicFile.WriteAllText(path, "run=" + i);
        }

        Assert.That(File.ReadAllText(path), Is.EqualTo("run=49"));
        Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1),
            "repeated replacement must not accumulate temporaries");
    }

    [Test]
    public void TheTargetIsNeverObservedEmptyWhileWriting()
    {
        // A reader that sees an empty file is the failure mode being removed: the
        // old implementation truncated the destination before filling it. Here the
        // destination must always hold either the old or the new contents.
        var path = Path_("mpv.conf");
        AtomicFile.WriteAllText(path, "old");

        var observed = new List<string>();
        using (var reader = new FileSystemWatcher(_dir, "mpv.conf"))
        {
            reader.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName;
            reader.EnableRaisingEvents = true;
            reader.Changed += (_, _) =>
            {
                try
                {
                    observed.Add(File.ReadAllText(path));
                }
                catch (IOException)
                {
                }
            };
            reader.Created += (_, _) => { };

            AtomicFile.WriteAllText(path, "new");

            Thread.Sleep(200);
        }

        Assert.That(observed, Has.None.Empty,
            "the destination was seen empty, which is what non-atomic replacement looks like");
        Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
    }
}
