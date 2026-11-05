using mpv_winui.Domains.Playback.Cards;
using NUnit.Framework;

namespace mpv_conf_test;

/// <summary>
/// The genre vocabulary rules, which the page cannot express.
///
/// <para>A genre tag is written by whoever ripped the file, so the same idea arrives
/// spelled a dozen ways: "Rock", "rock", "Rock; Pop", "Rock, Pop", trailing spaces,
/// a stray third separator with nothing after it. Every one of those has to reduce to
/// the same set, or the dropdown grows duplicate entries that filter differently from
/// each other.</para>
///
/// <para>These live here rather than beside the page because the rules are pure: a
/// string in, a decision out. That also means they run without a window, which is the
/// only way they get run on every build.</para>
/// </summary>
[TestFixture]
public class MusicGenreFilterTests
{
    [Test]
    public void OneValueStaysOneValue()
    {
        Assert.That(MusicGenreFilter.Split("Rock"), Is.EqualTo(new[] { "Rock" }));
    }

    [Test]
    public void CommasAndSemicolonsBothSplit()
    {
        // Both separators, because both are in the wild in equal numbers and a file
        // tagged with only one of them would otherwise arrive as a single genre whose
        // name contains the other separator.
        Assert.That(
            MusicGenreFilter.Split("Rock, Pop; Jazz"),
            Is.EqualTo(new[] { "Rock", "Pop", "Jazz" }));
    }

    [Test]
    public void SurroundingSpaceIsDropped()
    {
        Assert.That(
            MusicGenreFilter.Split("  Rock , Pop  "),
            Is.EqualTo(new[] { "Rock", "Pop" }));
    }

    [Test]
    public void CarelessCasingCollapsesToOneEntry()
    {
        // First spelling wins, so the dropdown shows what the first file wrote rather
        // than a canonical form nobody typed.
        var split = MusicGenreFilter.Split("rock, POP, Rock");

        Assert.That(split, Has.Count.EqualTo(2));
        Assert.That(split, Is.EqualTo(new[] { "rock", "POP" }));
    }

    [Test]
    public void AnEmptyValueYieldsNoGenres()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MusicGenreFilter.Split(null), Is.Empty, "a file with no tag has no genres");
            Assert.That(MusicGenreFilter.Split(""), Is.Empty);
            Assert.That(MusicGenreFilter.Split(" , ; "), Is.Empty, "separators with nothing between them are still nothing");
        });
    }

    [Test]
    public void AllMatchesEveryFile()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MusicGenreFilter.Matches("", MusicGenreSelection.All), Is.True);
            Assert.That(MusicGenreFilter.Matches("Rock", MusicGenreSelection.All), Is.True);
            Assert.That(MusicGenreFilter.Matches(null, MusicGenreSelection.All), Is.True);
        });
    }

    [Test]
    public void UnknownMatchesOnlyFilesWithNoGenre()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MusicGenreFilter.Matches("", MusicGenreSelection.Unknown), Is.True);
            Assert.That(MusicGenreFilter.Matches(" , ", MusicGenreSelection.Unknown), Is.True,
                "separators with nothing between them are an untagged file, not a genre named empty");
            Assert.That(MusicGenreFilter.Matches("Rock", MusicGenreSelection.Unknown), Is.False);
        });
    }

    [Test]
    public void ANamedGenreMatchesAnyOfTheFilesGenres()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MusicGenreFilter.Matches("Rock, Pop", MusicGenreSelection.Named("Pop")), Is.True,
                "a file can be several genres and matching any one of them is enough");
            Assert.That(MusicGenreFilter.Matches("Rock, Pop", MusicGenreSelection.Named("Jazz")), Is.False);
        });
    }

    [Test]
    public void MatchingIgnoresCase()
    {
        Assert.That(MusicGenreFilter.Matches("rock", MusicGenreSelection.Named("ROCK")), Is.True);
    }

    [Test]
    public void TheVocabularyIsSortedAndDeduped()
    {
        // Untagged files contribute nothing here: an empty name would become a
        // dropdown entry that reads as a blank line. Those files are reached through
        // the Unknown entry instead, which is why it exists.
        var vocabulary = MusicGenreFilter.Vocabulary(new[]
        {
            ("A", "rock"),
            ("B", "Pop"),
            ("C", "Rock"),
            ("D", ""),
        });

        Assert.That(vocabulary, Is.EqualTo(new[] { "Pop", "rock" }),
            "sorted by the culture's own order, deduped case-insensitively, first spelling kept, empties excluded");
    }

    [Test]
    public void KeptAlbumsIncludeEveryTrackOfAMatchingAlbum()
    {
        var tracks = new[]
        {
            ("X", "Rock"),
            ("X", "Pop"),
            ("Y", "Jazz"),
            ("Z", ""),
        };

        Assert.Multiple(() =>
        {
            // The album X has one Rock track and one Pop track. Filtering to Rock must
            // keep X WHOLE - both its tracks - or the album page opens showing half an
            // album, which is what "整张留下" is about.
            Assert.That(
                MusicGenreFilter.KeptAlbumKeys(tracks, MusicGenreSelection.Named("Rock")),
                Is.EqualTo(new[] { "X" }));

            Assert.That(
                MusicGenreFilter.KeptAlbumKeys(tracks, MusicGenreSelection.Unknown),
                Is.EqualTo(new[] { "Z" }));

            Assert.That(
                MusicGenreFilter.KeptAlbumKeys(tracks, MusicGenreSelection.All),
                Is.EquivalentTo(new[] { "X", "Y", "Z" }));
        });
    }

    [Test]
    public void AnAlbumIsKeptWholeWhenOneTrackMatches()
    {
        // The page-side step, not just the key list: an album enters the filter through
        // ANY one of its tracks, and what survives is every track of that album. Pinned
        // as a pair because a key list alone does not prove the page filters on it.
        var tracks = new[]
        {
            ("X", "Rock"),
            ("X", "Pop"),
            ("X", ""),
            ("Y", "Jazz"),
        };

        var kept = MusicGenreFilter.KeptAlbumKeys(tracks, MusicGenreSelection.Named("Rock"));

        var keptSet = new HashSet<string>(kept, StringComparer.CurrentCultureIgnoreCase);

        var surviving = tracks
            .Where(t => keptSet.Contains(string.IsNullOrWhiteSpace(t.Item1) ? string.Empty : t.Item1))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.EqualTo(new[] { "X" }));

            Assert.That(
                surviving.Select(t => t.Item1 + "|" + t.Item2),
                Is.EquivalentTo(new[] { "X|Rock", "X|Pop", "X|" }),
                "the matching track keeps its album mates even when they are another genre or untagged");

            Assert.That(surviving.Any(t => t.Item1 == "Y"), Is.False,
                "an album nothing in matched must not ride along");
        });
    }

    [Test]
    public void AWhitespaceOnlyAlbumKeepsItsKey()
    {
        // The page looks the key up with IsNullOrWhiteSpace, so a key of "   " would
        // match the genre and then miss its own lookup - that album would disappear
        // from the Albums tab while still counting toward the filter.
        Assert.That(
            MusicGenreFilter.KeptAlbumKeys(new[] { ("   ", "Rock") }, MusicGenreSelection.Named("Rock")),
            Is.EqualTo(new[] { string.Empty }),
            "a blank album tag files under the same empty key as a missing one");
    }
    [Test]
    public void AFilesWithNoAlbumUsesTheEmptyKey()
    {
        // The empty key is returned as-is rather than mapped to a label: this class
        // cannot reach AppContext, and the page owns the wording for "unknown album".
        Assert.That(
            MusicGenreFilter.KeptAlbumKeys(new[] { (string.Empty, "Rock") }, MusicGenreSelection.Named("Rock")),
            Is.EqualTo(new[] { string.Empty }));
    }
}
