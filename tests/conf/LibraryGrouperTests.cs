using mpv_winui.Domains.Playback.Cards;
using NUnit.Framework;

namespace mpv_conf_test;

/// <summary>
/// The one grouping rule the three music tabs share.
///
/// <para>These run without a window, which is the point: the tabs used to each build
/// their own groups, and "where does a file with no year go" had three answers. It has
/// one now, and this is where that answer is written down.</para>
/// </summary>
[TestFixture]
public class LibraryGrouperTests
{
    /// <summary>A two-letter pinyin table, enough to prove the band is separate.</summary>
    private static string? FakeHan(char ch) => ch switch
    {
        '周' => "Z",
        '王' => "W",
        _ => null,
    };

    private static readonly string[] Names = ["Apple", "Zebra", "Banana", "周杰伦", "王菲", "24K"];

    [Test]
    public void LettersFileEachNameUnderItsOwnBucket()
    {
        var groups = LibraryGrouper.Group(
            [0, 1, 2, 3, 4, 5],
            LibraryGroupKey.Letter,
            i => Names[i],
            i => null,
            i => string.Empty,
            i => null,
            FakeHan);

        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join("|", groups.Select(g => g.Label)),
                Is.EqualTo("#|A|B|Z|拼音W|拼音Z"),
                "band order is #, Latin A-Z, then the pinyin band - never interleaved");

            Assert.That(groups[0].Items, Is.EqualTo(new[] { 5 }), "24K starts with a digit, so it files under #");
            Assert.That(groups[1].Items, Is.EqualTo(new[] { 0 }));
        });
    }

    [Test]
    public void AChineseNameNeverSharesABucketWithALatinOne()
    {
        var groups = LibraryGrouper.Group(
            [1, 3],
            LibraryGroupKey.Letter,
            i => Names[i],
            i => null,
            i => string.Empty,
            i => null,
            FakeHan);

        var labels = groups.Select(g => g.Label).ToArray();

        Assert.That(string.Join("|", labels), Is.EqualTo("Z|拼音Z"),
            "Zebra and 周杰伦 both start with Z by reading, and they are still two rows; got: " + string.Join("|", labels));
    }

    [Test]
    public void YearsSortChronologicallyAndUntaggedFilesGoLast()
    {
        var years = new int?[] { 2021, null, 999, 1000 };

        var groups = LibraryGrouper.Group(
            [0, 1, 2, 3],
            LibraryGroupKey.Year,
            i => string.Empty,
            i => years[i],
            i => string.Empty,
            i => null);

        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", groups.Select(g => g.Label)), Is.EqualTo("999|1000|2021|"),
                "999 before 1000: the key is numeric, not the text a string compare would see; the unknown bucket is last");

            Assert.That(groups.Last().Items, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public void AnArtistSortGroupsByTheArtistTag()
    {
        var artists = new[] { "Beta", "Alpha", "", "Alpha" };

        var groups = LibraryGrouper.Group(
            [0, 1, 2, 3],
            LibraryGroupKey.Artist,
            i => string.Empty,
            i => null,
            i => artists[i],
            i => null);

        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", groups.Select(g => g.Label)), Is.EqualTo("Alpha|Beta|"));
            Assert.That(groups[0].Items, Is.EqualTo(new[] { 1, 3 }),
                "both of Alpha's tracks are in one group, in the order they came in");
            Assert.That(groups.Last().Items, Is.EqualTo(new[] { 2 }), "an untagged track is reachable, not dropped");
        });
    }

    [Test]
    public void TheAddedMonthGroupsByYearAndMonth()
    {
        var added = new DateTimeOffset?[]
        {
            new(2026, 3, 9, 0, 0, 0, TimeSpan.Zero),
            null,
            new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };

        var groups = LibraryGrouper.Group(
            [0, 1, 2],
            LibraryGroupKey.AddedMonth,
            i => string.Empty,
            i => null,
            i => string.Empty,
            i => added[i]);

        Assert.That(string.Join("|", groups.Select(g => g.Label)), Is.EqualTo("2026-01|2026-03|"));
    }

    [Test]
    public void DescendingReversesTheGroupsNotTheItemsInside()
    {
        var artists = new[] { "Alpha", "Beta", "Alpha" };

        var groups = LibraryGrouper.Group(
            [0, 1, 2],
            LibraryGroupKey.Artist,
            i => string.Empty,
            i => null,
            i => artists[i],
            i => null,
            hanInitial: null,
            descending: true);

        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", groups.Select(g => g.Label)), Is.EqualTo("Beta|Alpha"));

            // The items inside Alpha keep the order the caller supplied. Reversing them
            // too would make Z-A mean "also reverse each album's tracks", which is not
            // what a sort menu says.
            Assert.That(groups.Last().Items, Is.EqualTo(new[] { 0, 2 }));
        });
    }

    [Test]
    public void AnEmptyListProducesNoGroups()
    {
        var groups = LibraryGrouper.Group(
            [],
            LibraryGroupKey.Letter,
            i => string.Empty,
            i => null,
            i => string.Empty,
            i => null);

        Assert.That(groups, Is.Empty);
    }
}
