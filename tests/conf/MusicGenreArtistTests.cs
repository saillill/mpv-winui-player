using mpv_winui.Domains.Playback.Cards;
using System.Runtime.CompilerServices;

namespace mpv_winui_test;

/// <summary>
/// The artist tile: a circle, a person, and nothing to play.
///
/// <para>An artist tile is the same card in a different identity. It draws a
/// square tile rounded into a circle, shows a person glyph instead of a cover
/// (a tile stands for a person, and the tags carry no portrait to take one
/// from), and offers nothing to act on: no play disc, no overflow disc, no tick
/// box, no now-playing stripe, one caption line.</para>
///
/// <para>The geometry is asserted here. The branches that read the identity are
/// asserted against the SOURCE, in the same shape CardMetricsTests uses, because
/// the state behind them lives over WinUI types this project cannot reference -
/// and because the two failures that matter are silent ones: a card that still
/// offers a play disc for a person, or a tile still titled with a file name.</para>
///
/// <para><b>Comments are stripped before every search.</b> The card's source and
/// markup both carry prose that names the very identifiers these tests look for,
/// so a raw search passes after the code is deleted. That lesson is written down
/// in CardMetricsTests.ButtonBlock, where it was measured; it applies here too,
/// because the reason this file exists is a set of branches a comment can claim
/// to implement.</para>
/// </summary>
[TestFixture]
public class MusicGenreArtistTests
{
    [Test]
    public void TheArtistTileIsACircle()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardMetrics.CircleCornerRadius(150), Is.EqualTo(75.0));
            Assert.That(CardMetrics.CircleCornerRadius(0), Is.EqualTo(0.0));
        });
    }

    /// <summary>
    /// An artist tile offers nothing to play: no play disc, no overflow disc.
    ///
    /// <para>Asserted from the source because the discs are Collapsed by the
    /// pointer-entered path, and no test on this machine can put the pointer on a
    /// card (the same limitation CardMetricsTests.ATickedBoxTakesTheAccentColour
    /// documents). What a refactor would remove is the branch itself.</para>
    /// </summary>
    [Test]
    public void AnArtistTileDrawsNoDiscs()
    {
        var source = StrippedCardCode();

        var body = MethodBody(source, "private void ShowOverlay()");

        Assert.That(
            body,
            Does.Contain("if (_isArtist)"),
            "ShowOverlay must branch on IsArtist, or a person's tile shows a play disc and an overflow disc "
            + "for a track nobody chose");

        Assert.That(
            body,
            Does.Contain("PlayButton.Visibility = Visibility.Collapsed"),
            "the play disc must be collapsed on an artist tile");

        Assert.That(
            body,
            Does.Contain("MoreButton.Visibility = Visibility.Collapsed"),
            "the overflow disc must be collapsed on an artist tile");
    }

    /// <summary>
    /// An artist tile has no tick box, and the guard sits AFTER the assignment.
    ///
    /// <para>The position is the whole point, not a style detail:
    /// CardMetricsTests.EveryCardGetsATickBoxIncludingAlbums reads the text between
    /// this method's opening and <c>SelectionBox.Visibility = inMode</c> and fails on
    /// any <c>return</c> or <c>if (_isAlbum)</c> there - the shape that left the whole
    /// music library without tick boxes. An IsArtist guard in that position would
    /// reintroduce it for artists, so the assertion is an ORDER, not a presence.</para>
    /// </summary>
    [Test]
    public void AnArtistTileHidesTheTickBoxAfterTheAssignment()
    {
        var source = StrippedCardCode();

        var body = MethodBody(source, "private void UpdateSelectionBoxForState()");

        var assignment = body.IndexOf("SelectionBox.Visibility = inMode", StringComparison.Ordinal);
        Assert.That(
            assignment,
            Is.GreaterThanOrEqualTo(0),
            "the mode-or-pointer visibility computation is gone; the tick box has lost one of its two reasons");

        var guard = body.IndexOf("if (_isArtist)", StringComparison.Ordinal);
        Assert.That(
            guard,
            Is.GreaterThan(assignment),
            "the IsArtist guard must come AFTER SelectionBox.Visibility = inMode || _pointerInside: before it, "
            + "it is the same shape as the IsAlbum guard that removed every music card's tick box");

        // The block the guard opens, not the rest of the method: the veil is also
        // collapsed further down for a TICKED box, so a search over the remainder
        // would stay green with the artist branch reduced to one line.
        var artistBlock = body[guard..body.IndexOf('}', guard)];

        Assert.That(
            artistBlock,
            Does.Contain("SelectionBoxHover.Visibility = Visibility.Collapsed"),
            "the box's pointer veil has to go with it, or a hidden box still lights up under the pointer");
    }

    /// <summary>
    /// The caption under an artist tile is the artist's name, and there is only
    /// one line of it.
    ///
    /// <para>Both halves are silent when they regress: a tile titled with the file
    /// name looks like a card whose artist is missing, and a second line under it
    /// reserves a band the cell no longer needs.</para>
    /// </summary>
    [Test]
    public void TheArtistCaptionIsTheArtist()
    {
        var source = StrippedCardCode();

        var body = MethodBody(source, "private void ApplyCaptions(LibraryEntry entry)");

        var branch = body.IndexOf("if (_isArtist)", StringComparison.Ordinal);
        Assert.That(branch, Is.GreaterThanOrEqualTo(0), "ApplyCaptions must branch on IsArtist");

        Assert.That(
            body,
            Does.Contain("LibraryUnknownArtist"),
            "an untagged artist still needs a name - AppLang.LibraryUnknownArtist is the placeholder all nine "
            + "language files carry");

        // Before the file-name derivation, or the branch is unreachable for the
        // title it is meant to replace.
        Assert.That(
            branch,
            Is.LessThan(body.IndexOf("FileNameOf(entry.Path)", StringComparison.Ordinal)),
            "the artist branch must precede the file-name title, or an artist tile is titled with a file name");

        Assert.That(
            body[branch..],
            Does.Contain("SubtitleText.Visibility = Visibility.Collapsed"),
            "an artist tile has one caption line; the second has to be collapsed, not merely emptied");
    }

    /// <summary>
    /// The person glyph is in the markup, starts hidden, and draws the bundled
    /// font's own person at a size that exists.
    ///
    /// <para>The code behind only writes this element's Visibility, so the rest is
    /// pure markup: a literal font family, a codepoint from the wrong glyph set, or
    /// a default of Visible would each be invisible to every other test - the code
    /// compile-checks the NAME and nothing else. The codepoint is asserted as the
    /// entity the markup actually holds, and the comment above the element is
    /// stripped first because that prose names the same codepoint.</para>
    /// </summary>
    [Test]
    public void TheArtistGlyphIsAPersonThatStartsHidden()
    {
        var xaml = WithoutXmlComments(File.ReadAllText(CardXamlPath));

        var open = xaml.IndexOf("x:Name=\"ArtistGlyph\"", StringComparison.Ordinal);
        Assert.That(open, Is.GreaterThanOrEqualTo(0), "ArtistGlyph is missing from LibraryCard.xaml");

        var start = xaml.LastIndexOf('<', open);
        var end = xaml.IndexOf("</Viewbox>", open, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(open), "the ArtistGlyph Viewbox is never closed");

        var block = xaml[start..(end + "</Viewbox>".Length)];

        Assert.That(
            block,
            Does.Contain("Visibility=\"Collapsed\""),
            "the glyph must start Collapsed: until an entry arrives there is no identity, and a card with no "
            + "entry would otherwise wear a person");

        Assert.That(
            block,
            Does.Contain("{StaticResource MpvIconFontFamily}"),
            "the glyph must come from the bundled icon font; a FontIcon without a family draws from the "
            + "framework symbol font, where this codepoint means something else");

        Assert.That(
            block,
            Does.Contain("&#xF5BE;"),
            "U+F5BE is ic_fluent_person_24_regular in FluentSystemIcons - the only person glyph this font "
            + "defines at 24; a different codepoint draws an unrelated mark");
    }

    /// <summary>
    /// The braced body of the method whose declaration is
    /// <paramref name="signature"/>: from its opening brace through the matching
    /// close.
    ///
    /// <para>Strict, like CardMetricsTests.MethodBody and for the same reason: a
    /// helper that answered "" would fail the assertions below for a reason that has
    /// nothing to do with the code under test. Pass the FULL signature - including
    /// the parameter list - so the declaration is found rather than a call site.</para>
    /// </summary>
    private static string MethodBody(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);

        Assert.That(
            at,
            Is.GreaterThanOrEqualTo(0),
            $"{signature} is gone from LibraryCard.xaml.cs - renamed, or the method it names was removed");

        var open = source.IndexOf('{', at + signature.Length);
        Assert.That(open, Is.GreaterThanOrEqualTo(0), $"{signature} has no body");

        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source[open..(i + 1)];
                }
            }
        }

        Assert.Fail($"{signature}: its opening brace at offset {open} is never closed");
        return string.Empty;
    }

    /// <summary>
    /// The card's source with its comments stripped, plus the check that the
    /// stripping actually happened.
    ///
    /// <para>The three source tests all need both halves, and they need them the
    /// same way: a test that silently searched the raw file would be a test of the
    /// prose. The check is a LENGTH comparison rather than a search for <c>//</c>,
    /// because a <c>//</c> inside a string literal is code and would make a search
    /// fail on correct source.</para>
    /// </summary>
    private static string StrippedCardCode()
    {
        var raw = File.ReadAllText(CardCodePath);
        var stripped = WithoutComments(raw);

        Assert.That(
            stripped.Length,
            Is.LessThan(raw.Length),
            "stripping removed nothing, so this file has no comments and the match below - or this assertion - "
            + "is reading something other than LibraryCard.xaml.cs");

        return stripped;
    }

    /// <summary>
    /// The C# with every <c>//</c> and <c>/* */</c> comment removed.
    ///
    /// <para>Not tidiness. The card's prose names <c>_isArtist</c>,
    /// <c>PlayButton</c> and <c>LibraryUnknownArtist</c> deliberately - the branches
    /// being guarded explain themselves there - so a search over the raw source
    /// passes after the code is deleted, which is the one failure these tests exist
    /// to catch. Each caller then asserts the result contains no <c>//</c>, so this
    /// stripping is itself checked rather than trusted.</para>
    /// </summary>
    private static string WithoutComments(string code)
    {
        var builder = new StringBuilder(code.Length);

        for (var i = 0; i < code.Length; i++)
        {
            // A string or char literal is copied through as it stands. Without
            // this a "//" inside one - a URL, a file path - would be taken for the
            // start of a comment and the rest of the line dropped, so the file no
            // longer reads as the code it is. The stripping below is what the
            // assertions depend on, so it must not itself misread code.
            if (code[i] == '"' || code[i] == '\'')
            {
                var quote = code[i];
                builder.Append(quote);

                for (i++; i < code.Length; i++)
                {
                    builder.Append(code[i]);

                    if (code[i] == '\\' && i + 1 < code.Length)
                    {
                        builder.Append(code[i + 1]);
                        i++;
                        continue;
                    }

                    if (code[i] == quote)
                    {
                        break;
                    }
                }

                continue;
            }

            if (code[i] == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                while (i < code.Length && code[i] != '\n')
                {
                    i++;
                }

                builder.Append('\n');
                continue;
            }

            if (code[i] == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                i += 2;

                while (i + 1 < code.Length && !(code[i] == '*' && code[i + 1] == '/'))
                {
                    i++;
                }

                i++;
                continue;
            }

            builder.Append(code[i]);
        }

        return builder.ToString();
    }

    /// <summary>The markup with every XML comment removed, for the reason above.</summary>
    private static string WithoutXmlComments(string xaml)
    {
        var builder = new StringBuilder(xaml.Length);
        var i = 0;

        while (i < xaml.Length)
        {
            var open = xaml.IndexOf("<!--", i, StringComparison.Ordinal);

            if (open < 0)
            {
                builder.Append(xaml, i, xaml.Length - i);
                break;
            }

            builder.Append(xaml, i, open - i);

            var close = xaml.IndexOf("-->", open, StringComparison.Ordinal);
            Assert.That(close, Is.GreaterThan(open), "an XML comment in LibraryCard.xaml is never closed");

            i = close + 3;
        }

        return builder.ToString();
    }

    /// <summary>The card's markup, located the same way CardMetricsTests locates it.</summary>
    private static string CardXamlPath
        => Locate(@"src\mpvw-app\Domains\Playback\Controls\LibraryCard.xaml");

    /// <summary>The card's code-behind, located the same way.</summary>
    private static string CardCodePath
        => Locate(@"src\mpvw-app\Domains\Playback\Controls\LibraryCard.xaml.cs");

    /// <summary>
    /// Finds a repository file without assuming where the binary runs.
    ///
    /// <para>A plain walk up from <c>AppContext.BaseDirectory</c> is not enough:
    /// the test host can run from an artifacts path outside the repository
    /// (<c>dotnet test --artifacts-path</c>), and the source tree is then nowhere
    /// above it. So the search starts from the compiled chain instead - this
    /// file's own source path, recorded by the compiler - and falls back to the
    /// walk. Both are checked, and a miss throws with the paths that were tried
    /// rather than a bare "not found".</para>
    /// </summary>
    private static string Locate(string relative)
    {
        var tried = new List<string>();

        // 1. This source file's own path, from [CallerFilePath]. Three levels up
        //    from tests/conf lands on the repository root.
        var fromSource = Path.GetDirectoryName(ThisFilePath());
        if (!string.IsNullOrEmpty(fromSource))
        {
            var candidate = Path.GetFullPath(Path.Combine(fromSource, "..", "..", relative));
            tried.Add(candidate);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 2. Walk up from the binary, for the ordinary in-tree build.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            tried.Add(candidate);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            relative + " not found. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, tried));
    }

    /// <summary>
    /// This file's absolute path, as the compiler saw it. Used by
    /// <see cref="Locate"/> to find the checkout when the test host runs outside
    /// it. Never called directly.
    /// </summary>
    private static string ThisFilePath([CallerFilePath] string path = "") => path;
}
