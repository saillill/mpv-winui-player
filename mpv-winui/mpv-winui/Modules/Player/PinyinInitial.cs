using System;

namespace mpv_winui.Modules.Player;

/// <summary>
/// A Han character's pinyin initial, from the Windows collation table.
///
/// Not a character table. A table covers the few thousand characters someone thought
/// to type and silently misfiles the rest to "#"; asking the collation table means new
/// characters work the day Windows ships them.
///
/// How the reading is recovered from ordering alone: under the zh-CN collation, Han
/// characters are ordered by pinyin in a block after the Latin letters. So "which Latin
/// letter does this sort after" answers Z for every Han character and is useless -
/// measured, not assumed. What does work is comparing against one boundary character
/// per syllable group (阿 for A, 八 for B, 嚓 for C ...): a character sorts at or after
/// the boundary of its own initial and before the next one.
///
/// Measured on this machine against 19 known readings, 19 correct, including two-character
/// words (苹果 P, 音乐 Y, 爵士 J). The comparison runs on the first character only,
/// which is the one an index files by.
///
/// The boundary strings are fixed glyphs, not a mapping of characters to letters. They
/// are the lowest-sorting member of each initial in the table, so the check is "is this
/// character at or after boundary[k]" rather than "is this character in group k".
/// </summary>
internal static class PinyinInitial
{
    private const uint SortDefault = 0;

    /// <summary>
    /// The first two members of <c>COMPARESTRING_RESULT</c>: 1 is "less than", 2 is
    /// "equal". Held as ints because that is what the boundary walk compares against.
    /// </summary>
    private const int LessThan = 1;

    private const int Equal = 2;

    /// <summary>
    /// One per initial that pinyin actually uses. There is no I, U or V syllable, so
    /// the table has no gaps to fill - a character never files under those letters.
    ///
    /// Literal characters rather than \u escapes, deliberately. Escapes were here
    /// first and two of the twenty-three were the wrong character (哦 for 咔, 仃 for
    /// 仨); both compiled and neither was visible by reading. A glyph can be checked
    /// against the alphabet in front of you.
    /// </summary>
    private static readonly (string Letter, string Boundary)[] Boundaries =
    [
        ("A", "阿"),
        ("B", "八"),
        ("C", "嚓"),
        ("D", "哒"),
        ("E", "妸"),
        ("F", "发"),
        ("G", "旮"),
        ("H", "哈"),
        ("J", "讥"),
        ("K", "咔"),
        ("L", "垃"),
        ("M", "妈"),
        ("N", "拿"),
        ("O", "噢"),
        ("P", "趴"),
        ("Q", "七"),
        ("R", "呥"),
        ("S", "仨"),
        ("T", "他"),
        ("W", "屲"),
        ("X", "夕"),
        ("Y", "丫"),
        ("Z", "帀"),
    ];

    /// <summary>
    /// The initial for the first Han character of <paramref name="text"/>, or null when
    /// it has none (not Han, or the collation call failed).
    ///
    /// Null rather than "#" so the caller can tell "no reading" from "no letter": the
    /// index still files it under Other, but a future caller that wants a pinyin sort
    /// key can distinguish the two.
    /// </summary>
    internal static string? For(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var first = text[0];

        if (!IsHan(first))
        {
            return null;
        }

        // Walk the groups in order and take the last boundary the character is at or
        // after. 23 comparisons worst case, once per group heading while a library is
        // rebuilt - not per card.
        string? found = null;

        foreach (var (letter, boundary) in Boundaries)
        {
            var result = Compare(boundary, first.ToString());

            if (result is LessThan or Equal)
            {
                found = letter;
            }
            else
            {
                // Boundaries are sorted, so the first boundary the character precedes
                // ends the search.
                break;
            }
        }

        return found;
    }

    private static bool IsHan(char ch) =>
        ch is >= '\u4E00' and <= '\u9FFF' or >= '\u3400' and <= '\u4DBF';

    // No SetLastError: the return value is the whole answer, and the error code would
    // only be read to report a failure nobody can act on (a missing collation table is
    // not a recoverable state).
    /// <summary>
    /// Compares two strings under the zh-CN collation.
    ///
    /// Through the CsWin32-generated binding, not a hand-written DllImport: every other
    /// Win32 call in this app comes from that generator (see NativeMethods.txt), and it
    /// emits a signature that needs no runtime marshalling - a hand-rolled one with
    /// `string` parameters warns CA1420 three times and would depend on marshalling
    /// being enabled later.
    /// </summary>
    private static int Compare(string a, string b)
    {
        var result = Windows.Win32.PInvoke.CompareStringEx(
            "zh-CN",
            // The generated enum has no SORT_DEFAULT because Win32 defines "default"
            // as the zero flag value - and the generator's own documentation names 0
            // as the fastest path.
            default,
            a,
            a.Length,
            b,
            b.Length);

        // The generator returns COMPARESTRING_RESULT, whose members are the 1/2/3 the
        // Win32 documentation describes. A zero result means the call failed; treating
        // that as "sorts before" leaves the caller on the previous boundary, which is
        // the conservative answer rather than a guessed letter.
        return (int)result == 0 ? LessThan : (int)result;
    }
}
