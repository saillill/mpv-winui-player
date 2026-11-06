using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Playback.Cards;

/// <summary>What a grouped library list is grouped BY.</summary>
internal enum LibraryGroupKey
{
    /// <summary>The first character of the displayed name, through <see cref="LibraryIndexLetter"/>.</summary>
    Letter,

    /// <summary>The release year from the tag.</summary>
    Year,

    /// <summary>The artist tag.</summary>
    Artist,

    /// <summary>The month the file was added, as yyyy-MM.</summary>
    AddedMonth,
}

/// <summary>One heading and the items filed under it.</summary>
/// <param name="Label">What the heading prints.</param>
/// <param name="SortKey">
/// What the group is ordered by. For <see cref="LibraryGroupKey.Letter"/> this is the
/// bucket from <see cref="LibraryIndexLetter.All"/>, so ordering is by the band's own
/// order rather than by the label's culture order - otherwise 拼音A would sort between
/// Latin P and Q.
/// </param>
/// <param name="Items">The items under the heading, already in the caller's order.</param>
internal readonly record struct LibraryGrouping(
    string Label,
    string SortKey,
    IReadOnlyList<int> Items);

/// <summary>
/// Files a flat list under headings, where the heading is decided by the sort.
///
/// <para><b>Why this exists.</b> The three music tabs used to each build their own
/// groups, and two of them grouped by a tag while the third grouped by nothing. Once
/// the sort decides what the headings ARE - letters for A-Z, years for a year sort,
/// names for an artist sort - three copies of that logic would be three chances to
/// disagree about where an untagged file goes. There is one copy instead.</para>
///
/// <para><b>The "unknown" bucket.</b> A file with no year, no artist or no name still
/// needs a home, and dropping it would make the list silently shorter than the library.
/// Every key type gets exactly one bucket for its missing values, and it is ordered
/// last: it is the fallback, not the most important group.</para>
///
/// <para>No UI type and no <c>AppContext</c>: the labels come back as empty strings for
/// the unknown bucket and the page supplies the localized wording. Same rule the genre
/// filter follows, and for the same reason - the test project links this file and cannot
/// reference <c>Microsoft.UI.Xaml</c>.</para>
/// </summary>
internal static class LibraryGrouper
{
    /// <summary>
    /// The heading label for a group the caller must name itself.
    ///
    /// <para>Returned rather than a null: the page's switch over labels is easier to read
    /// with one explicit token in it than with a null check around every arm.</para>
    /// </summary>
    internal const string UnknownLabel = "";

    /// <summary>
    /// Groups <paramref name="items"/> under headings chosen by <paramref name="key"/>.
    /// </summary>
    /// <param name="items">The items, already ordered the way the page wants them read.</param>
    /// <param name="key">What decides a heading.</param>
    /// <param name="nameOf">The text a Letter grouping files by. Unused for other keys.</param>
    /// <param name="yearOf">The tag year, or null.</param>
    /// <param name="artistOf">The tag artist, or empty.</param>
    /// <param name="addedOf">When the file entered the library, or null.</param>
    /// <param name="hanInitial">Answers a Han character's pinyin initial; see LibraryIndexLetter.</param>
    /// <param name="descending">Reverses the GROUPS, not the items inside them.</param>
    internal static IReadOnlyList<LibraryGrouping> Group(
        IReadOnlyList<int> items,
        LibraryGroupKey key,
        Func<int, string> nameOf,
        Func<int, int?> yearOf,
        Func<int, string> artistOf,
        Func<int, DateTimeOffset?> addedOf,
        Func<char, string?>? hanInitial = null,
        bool descending = false)
    {
        var order = new List<string>();
        var buckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var (label, sortKey) = Describe(item, key, nameOf, yearOf, artistOf, addedOf, hanInitial);

            if (!buckets.TryGetValue(sortKey, out var list))
            {
                list = [];
                buckets[sortKey] = list;
                order.Add(sortKey);
            }

            list.Add(item);
        }

        // Ordering by the KEY, not the label: for letters the key is the bucket, whose
        // position in LibraryIndexLetter.All is the order the bands are meant to read
        // in; the label's own culture order would interleave 拼音A among the Latin
        // letters.
        order.Sort((a, b) => CompareKeys(a, b, key));

        if (descending)
        {
            order.Reverse();
        }

        var result = new List<LibraryGrouping>(order.Count);

        foreach (var sortKey in order)
        {
            result.Add(new LibraryGrouping(LabelFor(sortKey, key), sortKey, buckets[sortKey]));
        }

        return result;
    }

    /// <summary>The heading and the ordering key for one item.</summary>
    private static (string Label, string SortKey) Describe(
        int item,
        LibraryGroupKey key,
        Func<int, string> nameOf,
        Func<int, int?> yearOf,
        Func<int, string> artistOf,
        Func<int, DateTimeOffset?> addedOf,
        Func<char, string?>? hanInitial) => key switch
    {
        LibraryGroupKey.Letter => DescribeLetter(nameOf(item), hanInitial),

        LibraryGroupKey.Year => yearOf(item) is { } year
            ? (year.ToString(System.Globalization.CultureInfo.InvariantCulture), YearKey(year))
            : (UnknownLabel, UnknownSortKey),

        LibraryGroupKey.Artist => ArtistOf(artistOf, item),

        LibraryGroupKey.AddedMonth => addedOf(item) is { } added
            ? (added.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), added.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
            : (UnknownLabel, UnknownSortKey),

        _ => (UnknownLabel, UnknownSortKey),
    };

    /// <summary>
    /// The letter bucket, with the same "nothing to file under" answer the rail uses.
    ///
    /// <para>A name that is empty files under <c>#</c>, which
    /// <see cref="LibraryIndexLetter.For"/> already returns - so the unknown bucket for
    /// letters is that same mark rather than a second one beside it.</para>
    /// </summary>
    private static (string Label, string SortKey) DescribeLetter(string? name, Func<char, string?>? hanInitial)
    {
        var bucket = LibraryIndexLetter.For(name, hanInitial);

        return (bucket, bucket);
    }

    /// <summary>The sort key that puts a group last, whatever the key type needs.</summary>
    private const string UnknownSortKey = "\uFFFF";

    /// <summary>
    /// Year keys sort chronologically, not as text: "999" would otherwise sort after
    /// "1000" because '9' > '1'.
    /// </summary>
    private static string YearKey(int year)
    {
        // Zero-padded to a fixed width so a plain string compare is chronological. Four
        // digits covers every year a music tag can carry; a wider value still compares
        // correctly because the padding is uniform.
        return year.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Ordering for one key type.</summary>
    private static int CompareKeys(string a, string b, LibraryGroupKey key)
    {
        // The unknown bucket is last for every key type, before any culture comparison.
        var aUnknown = a == UnknownSortKey;
        var bUnknown = b == UnknownSortKey;

        if (aUnknown || bUnknown)
        {
            return aUnknown == bUnknown ? 0 : aUnknown ? 1 : -1;
        }

        if (key == LibraryGroupKey.Letter)
        {
            // Band order from the one list that defines it. A key the list does not know
            // is not a bucket this rail can show, so it goes last rather than first.
            var ai = Array.IndexOf(LibraryIndexLetter.All, a);
            var bi = Array.IndexOf(LibraryIndexLetter.All, b);

            if (ai < 0)
            {
                ai = int.MaxValue;
            }

            if (bi < 0)
            {
                bi = int.MaxValue;
            }

            return ai.CompareTo(bi);
        }

        return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// What the heading prints.
    ///
    /// <para>Every key type already spells its own label except Year, whose sort key is
    /// zero-padded so a string compare is chronological - the padding has to come off
    /// before anyone reads it.</para>
    /// </summary>
    private static string LabelFor(string sortKey, LibraryGroupKey key)
    {
        if (sortKey == UnknownSortKey)
        {
            return UnknownLabel;
        }

        return key == LibraryGroupKey.Year
            ? int.Parse(sortKey, System.Globalization.CultureInfo.InvariantCulture)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : sortKey;
    }

    /// <summary>The artist bucket, read once.</summary>
    private static (string Label, string SortKey) ArtistOf(Func<int, string> artistOf, int item)
    {
        var artist = artistOf(item);

        return string.IsNullOrWhiteSpace(artist)
            ? (UnknownLabel, UnknownSortKey)
            : (artist, artist);
    }
}
