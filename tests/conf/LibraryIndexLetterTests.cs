using System;
using System.Linq;
using mpv_winui.Domains.Playback;

namespace mpv_winui_test;

/// <summary>
/// The index letter rules.
///
/// These are pure: <see cref="LibraryIndexLetter.For"/> takes the Han-initial lookup as
/// a delegate, so the classification rules can be asserted without a collation call and
/// the platform call is tested separately. That separation is the point - a wrong rule
/// and a wrong API both look like "the index is wrong" from outside.
/// </summary>
[TestFixture]
public class LibraryIndexLetterTests
{
    /// <summary>A stand-in for the pinyin lookup: maps the characters used in these tests.</summary>
    private static readonly System.Collections.Generic.Dictionary<char, string> FakePinyin = new()
    {
        ['周'] = "Z", ['王'] = "W", ['爱'] = "A", ['李'] = "L",
        ['苹'] = "P", ['音'] = "Y", ['爵'] = "J",
    };

    private static string? FakeHan(char ch) => FakePinyin.TryGetValue(ch, out var letter) ? letter : null;

    [TestCase("Abbey Road", "A")]
    [TestCase("abbey road", "A")]
    [TestCase("Ziggy Stardust", "Z")]
    [TestCase("24.0", LibraryIndexLetter.Other)]
    [TestCase("1989", LibraryIndexLetter.Other)]
    [TestCase("!!!", LibraryIndexLetter.Other)]
    [TestCase("", LibraryIndexLetter.Other)]
    [TestCase("   ", LibraryIndexLetter.Other)]
    [TestCase(null, LibraryIndexLetter.Other)]
    public void LatinAndNumericTitlesFileByTheirFirstCharacter(string? title, string expected)
    {
        Assert.That(LibraryIndexLetter.For(title, FakeHan), Is.EqualTo(expected));
    }

    [Test]
    public void LeadingBracketsAndQuotesAreSkipped()
    {
        // "[Deluxe] Album" files under D: the bracket is typography, not content, and
        // every entry filed under "#" because it started with a bracket would make the
        // # bucket useless.
        Assert.That(LibraryIndexLetter.For("[Deluxe] Album", FakeHan), Is.EqualTo("D"));
        Assert.That(LibraryIndexLetter.For("\"Heroes\"", FakeHan), Is.EqualTo("H"));
        Assert.That(LibraryIndexLetter.For("《音乐》", FakeHan), Is.EqualTo("拼音Y"));
        Assert.That(LibraryIndexLetter.For("「周」", FakeHan), Is.EqualTo("拼音Z"));
    }

    [Test]
    public void HanTitlesFileByPinyin()
    {
        // 拼音X, not X: a Chinese title gets its own sequence so it never shares a
        // bucket with a Latin title that happens to start with the same letter.
        Assert.That(LibraryIndexLetter.For("周杰伦", FakeHan), Is.EqualTo("拼音Z"));
        Assert.That(LibraryIndexLetter.For("王菲", FakeHan), Is.EqualTo("拼音W"));
        Assert.That(LibraryIndexLetter.For("苹果", FakeHan), Is.EqualTo("拼音P"));
        Assert.That(LibraryIndexLetter.For("音乐", FakeHan), Is.EqualTo("拼音Y"));
    }

    [Test]
    public void HanWithoutAReadingFallsBackToOther()
    {
        // 未 in the fake table: the platform could not answer, so the honest answer is
        // "#" rather than a guess.
        Assert.That(LibraryIndexLetter.For("未知艺术家", FakeHan), Is.EqualTo(LibraryIndexLetter.Other));
    }

    [Test]
    public void HanIsOtherWhenNoLookupIsSupplied()
    {
        Assert.That(LibraryIndexLetter.For("周杰伦"), Is.EqualTo(LibraryIndexLetter.Other));
    }

    [TestCase("Édith Piaf", "E")]
    [TestCase("Ärzte", "A")]
    [TestCase("Øya", "O")]
    [TestCase("Ångström", "A")]
    [TestCase("Ça", "C")]
    [TestCase("Ñandú", "N")]
    public void AccentedLatinFoldsToItsBaseLetter(string title, string expected)
    {
        Assert.That(LibraryIndexLetter.For(title, FakeHan), Is.EqualTo(expected));
    }

    [Test]
    public void OnlyTheFirstCharacterDecides()
    {
        // "A1" is A and "1A" is #. A title that starts with a symbol belongs with the
        // symbols even when a letter follows it.
        Assert.That(LibraryIndexLetter.For("A1", FakeHan), Is.EqualTo("A"));
        Assert.That(LibraryIndexLetter.For("1A", FakeHan), Is.EqualTo(LibraryIndexLetter.Other));
    }

    [Test]
    public void KanaAndHangulFallToOther()
    {
        // Not Han, so no pinyin reading; not Latin, so no fold. The reference puts them
        // with the symbols too.
        Assert.That(LibraryIndexLetter.For("あいう", FakeHan), Is.EqualTo(LibraryIndexLetter.Other));
        Assert.That(LibraryIndexLetter.For("한글", FakeHan), Is.EqualTo(LibraryIndexLetter.Other));
    }

    [Test]
    public void TheRailIsTheWholeAlphabetInReferenceOrder()
    {
        Assert.That(LibraryIndexLetter.All[0], Is.EqualTo(LibraryIndexLetter.Other));

        // Three bands now: #, the twenty-six Latin letters, then the twenty-six pinyin
        // buckets. Fifty-three, not twenty-seven.
        Assert.That(LibraryIndexLetter.All.Length, Is.EqualTo(53));

        Assert.That(
            LibraryIndexLetter.All.Skip(1).Take(26),
            Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => c.ToString())));

        Assert.That(
            LibraryIndexLetter.All.Skip(27),
            Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => LibraryIndexLetter.PinyinPrefix + c)),
            "the pinyin band follows the Latin one, in the same order");
    }

    [Test]
    public void TheRailKeepsEmptyLettersAndDimsThem()
    {
        // The reference shows the whole alphabet with the empty letters greyed. Removing
        // them would make the rail move as the library changes, and a person could not
        // see that there is nothing under Q without clicking Q.
        var rail = LibraryIndexLetter.BuildRail(["B", "Z"]);

        Assert.That(rail, Has.Count.EqualTo(LibraryIndexLetter.All.Length));

        Assert.That(rail.Single(b => b.Letter == "B").HasContent, Is.True);
        Assert.That(rail.Single(b => b.Letter == "M").HasContent, Is.False);
        Assert.That(rail.Single(b => b.Letter == LibraryIndexLetter.Other).HasContent, Is.False);
    }

    [Test]
    public void RailOrderMatchesTheLettersArray()
    {
        var rail = LibraryIndexLetter.BuildRail([]);

        Assert.That(rail.Select(b => b.Letter), Is.EqualTo(LibraryIndexLetter.All));
    }
}
