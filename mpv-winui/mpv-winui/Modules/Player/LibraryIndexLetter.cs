using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Which letter a title files under, for the library's A-Z index.
///
/// The rule the reference player follows, stated once here so the index rail and the
/// grouping cannot disagree:
///
///   * Latin letters fold to their upper-case initial, ignoring diacritics
///     ("Édith" -> E, not É)
///   * a Han character takes the initial of its pinyin reading, which is what
///     "拼音A..拼音Z" in the reference's rail means - Chinese users index by reading,
///     not by stroke or by codepoint
///   * anything else - digits, punctuation, kana, Hangul, symbols - goes to "#"
///
/// The pinyin half is deliberately NOT a lookup table of characters. A table would
/// cover the few thousand characters someone thought to type and silently misfile the
/// rest to "#"; `PinyinInitial` asks Windows instead, through the same collation
/// service that already sorts this machine's Chinese text correctly.
///
/// This type is pure: it takes a string and a delegate that answers "what letter does
/// this Han character start with". That keeps the classification rules testable without
/// a Windows collation call, and keeps the platform call in one place.
/// </summary>
internal static class LibraryIndexLetter
{
    /// <summary>The bucket for entries that have no letter.</summary>
    internal const string Other = "#";

    /// <summary>
    /// Every bucket the rail offers, in the reference player's order:
    /// <c>#</c>, then A-Z.
    /// </summary>
    internal static readonly string[] All =
    [
        Other,
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    ];

    /// <summary>
    /// The bucket a title belongs to.
    /// </summary>
    /// <param name="title">The album or artist name. Leading whitespace and quotes are ignored.</param>
    /// <param name="hanInitial">
    /// Answers the pinyin initial for one Han character, as an upper-case A-Z string, or
    /// null when the platform cannot answer. Supplied by the caller so this method stays
    /// pure; see <see cref="Win32HanInitial"/> for the real implementation.
    /// </param>
    internal static string For(string? title, Func<char, string?>? hanInitial = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Other;
        }

        foreach (var raw in title)
        {
            var ch = raw;

            // Sort keys ignore these: leading brackets and quotes are typography, not
            // content. "[Deluxe] Album" files under D, which is where a person looks.
            if (char.IsWhiteSpace(ch) || IsIgnoredPrefix(ch))
            {
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                return char.ToUpperInvariant(ch).ToString();
            }

            // Latin letters with diacritics fold to their base letter. The range covers
            // Latin-1 Supplement and Latin Extended-A/B, which is what album art from
            // most of Europe produces.
            var folded = FoldLatin(ch);

            if (folded is not null)
            {
                return folded;
            }

            if (IsHan(ch) && hanInitial is not null)
            {
                // A Han initial is a real answer, not a fallback: stop here.
                if (hanInitial(ch) is { Length: > 0 } initial)
                {
                    return initial.ToUpperInvariant();
                }
            }

            // First character decides. A title starting with a digit or a symbol is "#"
            // even if a letter follows - "24.0" is not filed under T, and the reference
            // player puts it with the symbols too.
            return Other;
        }

        return Other;
    }

    /// <summary>
    /// Punctuation that a sort key skips rather than files under.
    ///
    /// Deliberately short. An apostrophe or a bracket before the first letter is
    /// noise; a leading "#" or a digit is not, and those reach the Other bucket through
    /// the ordinary path.
    /// </summary>
    private static bool IsIgnoredPrefix(char ch) => ch switch
    {
        '\'' or '"' or '\u2018' or '\u2019' or '\u201C' or '\u201D' => true,
        '(' or ')' or '[' or ']' or '{' or '}' => true,
        '\u300A' or '\u300B' or '\u3008' or '\u3009' => true, // 《》〈〉
        '\u300C' or '\u300D' or '\u300E' or '\u300F' => true, // 「」『』
        _ => false,
    };

    private static bool IsAsciiLetter(char ch) => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    /// <summary>
    /// The ASCII letter a Latin character with diacritics folds to, or null.
    ///
    /// A hand-written range rather than CultureInfo or Normalize: this runs once per
    /// group heading while a library is being rebuilt, and both of those allocate.
    /// </summary>
    private static string? FoldLatin(char ch)
    {
        // Latin-1 Supplement letters, upper and lower.
        if (ch is >= '\u00C0' and <= '\u00C5') return "A";
        if (ch is '\u00C6') return "A";
        if (ch is '\u00C7') return "C";
        if (ch is >= '\u00C8' and <= '\u00CB') return "E";
        if (ch is >= '\u00CC' and <= '\u00CF') return "I";
        if (ch is '\u00D0') return "D";
        if (ch is '\u00D1') return "N";
        if (ch is >= '\u00D2' and <= '\u00D6' or '\u00D8') return "O";
        if (ch is >= '\u00D9' and <= '\u00DC') return "U";
        if (ch is '\u00DD') return "Y";
        if (ch is '\u00DE') return "T";
        if (ch is '\u00DF') return "S";

        if (ch is >= '\u00E0' and <= '\u00E5') return "A";
        if (ch is '\u00E6') return "A";
        if (ch is '\u00E7') return "C";
        if (ch is >= '\u00E8' and <= '\u00EB') return "E";
        if (ch is >= '\u00EC' and <= '\u00EF') return "I";
        if (ch is '\u00F0') return "D";
        if (ch is '\u00F1') return "N";
        if (ch is >= '\u00F2' and <= '\u00F6' or '\u00F8') return "O";
        if (ch is >= '\u00F9' and <= '\u00FC') return "U";
        if (ch is '\u00FD' or '\u00FF') return "Y";
        if (ch is '\u00FE') return "T";

        return null;
    }

    /// <summary>
    /// True for the CJK ideographs whose reading a pinyin initial can answer.
    ///
    /// The unified block plus Extension A. Extensions B and beyond are rare in media
    /// tags and outside the BMP, so a char-based test cannot see them anyway - they
    /// fall through to Other, which is the honest answer rather than a guess.
    /// </summary>
    private static bool IsHan(char ch) =>
        ch is >= '\u4E00' and <= '\u9FFF' or >= '\u3400' and <= '\u4DBF';

    /// <summary>
    /// Builds the rail: every bucket in order, each marked with whether it has content.
    ///
    /// Empty buckets are KEPT and dimmed rather than removed - the reference's rail
    /// shows the whole alphabet with the empty letters greyed, which is what lets a
    /// person see at a glance that there is nothing under Q without clicking Q.
    /// </summary>
    internal static List<LibraryIndexBucket> BuildRail(IEnumerable<string> lettersInUse)
    {
        var used = new HashSet<string>(lettersInUse, StringComparer.Ordinal);
        var rail = new List<LibraryIndexBucket>(All.Length);

        foreach (var letter in All)
        {
            rail.Add(new LibraryIndexBucket(letter, used.Contains(letter)));
        }

        return rail;
    }
}

/// <summary>
/// One letter in the index rail, and whether the library has anything under it.
///
/// Deliberately free of any UI type: the rail's appearance (bold for a letter with
/// content, dim for one without) is the control's decision, so this record can be linked
/// into the test project and asserted without a WinUI runtime. The same rule keeps
/// SpectrumAnalyzer and AlbumColorExtractor testable.
/// </summary>
internal sealed record LibraryIndexBucket(string Letter, bool HasContent);
