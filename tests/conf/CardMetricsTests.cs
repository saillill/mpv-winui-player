using mpv_winui.Domains.Playback.Cards;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace mpv_winui_test;

/// <summary>
/// The card geometry, in one place.
///
/// These numbers used to be literals repeated in two DataTemplates and in two
/// layout methods: CornerRadius="4" four times, Width/Height="48" twice per
/// template, and a MinItemHeight that had to be recomputed by hand whenever the
/// aspect changed. The tests below pin the arithmetic that ties a card's width
/// to its cover height, because getting it wrong is not a style question - it
/// either crops the picture or opens a gap under it.
/// </summary>
[TestFixture]
public class CardMetricsTests
{
    [Test]
    public void AlbumCoverIsSquare()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: true), Is.EqualTo(1.0));
    }

    [Test]
    public void VideoCoverIsSixteenByNine()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: false), Is.EqualTo(16.0 / 9.0).Within(1e-9));
    }

    [Test]
    public void AlbumCoverHeightEqualsItsWidth()
    {
        Assert.That(CardMetrics.CoverHeightFor(170, isAlbum: true), Is.EqualTo(170).Within(1e-9));
    }

    [Test]
    public void VideoCoverHeightIsWidthOverAspect()
    {
        // 253 through 16:9 is 142.31: the height the markup comment claims.
        Assert.That(CardMetrics.CoverHeightFor(253, isAlbum: false), Is.EqualTo(142.3125).Within(1e-3));
    }

    [Test]
    public void ZeroWidthStaysZero()
    {
        // A recycled card reports 0 before its first layout pass. A NaN or a
        // negative height assigned here throws inside the layout pass instead of
        // drawing nothing.
        Assert.That(CardMetrics.CoverHeightFor(0, isAlbum: true), Is.EqualTo(0));
    }

    [Test]
    public void NegativeWidthStaysZero()
    {
        Assert.That(CardMetrics.CoverHeightFor(-5, isAlbum: false), Is.EqualTo(0));
    }

    [Test]
    public void TokensAreTheDocumentedValues()
    {
        Assert.That(CardMetrics.CornerRadius, Is.EqualTo(4));

        // The caption band follows the cell height, so a change here without a
        // change in CardMetrics.CaptionBandHeight clips the second line. These
        // four numbers are the reference's own, measured on its hovered card.
        Assert.That(CardMetrics.CaptionTopGap, Is.EqualTo(6));
        Assert.That(CardMetrics.CaptionLineGap, Is.EqualTo(2));

        // Both hover buttons are the same size now: the reference draws them as
        // one matched pair, and the old 40/32 split made the second look accidental.
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(36));
        Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(36));
        Assert.That(CardMetrics.CornerInset, Is.EqualTo(10));
        Assert.That(CardMetrics.RingThickness, Is.EqualTo(1.5));
        Assert.That(CardMetrics.NowPlayingBarHeight, Is.EqualTo(2));
    }

    /// <summary>
    /// The ring has to fit inside the disc's corner radius or it renders as a
    /// square with a circle inside it. Half the diameter is the only radius that
    /// makes the two shapes concentric.
    /// </summary>
    [Test]
    public void RingSitsOnTheDiscEdge()
    {
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(CardMetrics.MoreButtonSize));
        Assert.That(CardMetrics.RingThickness, Is.LessThan(CardMetrics.PlayButtonSize / 4));
    }

    /// <summary>
    /// The two caption line heights have to clear the type sizes they stand for.
    ///
    /// BOTH lines are at MpvCardTitleFontSize (15) now - the card no longer has a
    /// smaller second line - so both are checked against that one size. 15 at the
    /// default 1.2 line box is 18. These constants are what the grid cell is built
    /// from before any card is measured, so being merely equal is not enough - a
    /// cell exactly as tall as the glyph box clips the descenders of a wrapped
    /// line.
    /// </summary>
    [Test]
    public void CaptionLineHeightsClearTheirTypeSizes()
    {
        // The subtitle used to be 14 and had its own constant. It is 15 now, and
        // leaving the assertion at 14 would keep passing while the real line ran
        // a DIP taller than the number it was being compared against.
        const double CaptionFontSize = 15;
        const double LineBox = 1.2;

        Assert.That(CardMetrics.TitleLineHeight, Is.GreaterThan(CaptionFontSize * LineBox));
        Assert.That(CardMetrics.SubtitleLineHeight, Is.GreaterThan(CaptionFontSize * LineBox));
    }

    /// <summary>
    /// The card's markup must carry exactly the line heights the cell reserves.
    ///
    /// <para>This is the tie that was missing. CardMetrics has owned these two
    /// numbers and the grid has sized its cells from them since the constants
    /// were introduced, but nothing in LibraryCard.xaml applied them: a 15px
    /// TextBlock draws a ~32 DIP line box by default, against the 22 the cell had
    /// reserved, so the caption band ran 18 DIP past its cell and the second line
    /// ended up outside the card.</para>
    ///
    /// <para>Why a test reads the XAML instead of the markup naming the constant:
    /// CardMetrics is internal and static, so it cannot be reached from a
    /// {StaticResource} or an x:Bind in markup. Assigning the values from the code
    /// behind was tried and reverted - setting LineHeight right after
    /// InitializeComponent left the whole card unlaid-out (UIA reported it at
    /// -31384,-31698 and no caption pixels were drawn, where the same card at HEAD
    /// sat at 804,478). A literal in the markup plus this assertion is the version
    /// that actually renders, and the assertion is what stops the literal from
    /// drifting away from the constant the cell is computed from.</para>
    /// </summary>
    [Test]
    public void CardMarkupUsesTheLineHeightsTheCellReserves()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        Assert.That(
            LineHeightFor(xaml, "TitleText"),
            Is.EqualTo(CardMetrics.TitleLineHeight),
            "LibraryCard.xaml's TitleText LineHeight must equal CardMetrics.TitleLineHeight");

        Assert.That(
            LineHeightFor(xaml, "SubtitleText"),
            Is.EqualTo(CardMetrics.SubtitleLineHeight),
            "LibraryCard.xaml's SubtitleText LineHeight must equal CardMetrics.SubtitleLineHeight");
    }

    /// <summary>
    /// The two caption lines share one type size, and neither is smaller.
    ///
    /// The report was "use the now-playing row's type for every card and drop the
    /// smaller line underneath". The subtitle used to be MpvCardSubtitleFontSize
    /// (14) while the title was MpvCardTitleFontSize (15), and the pair read as a
    /// heading with a caption. Asserted from the markup because that is where the
    /// size is chosen; CardMetrics cannot see a FontSize.
    /// </summary>
    [Test]
    public void BothCaptionLinesShareOneTypeSize()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        Assert.That(FontSizeTokenFor(xaml, "TitleText"), Is.EqualTo("MpvCardTitleFontSize"));
        Assert.That(FontSizeTokenFor(xaml, "SubtitleText"), Is.EqualTo("MpvCardTitleFontSize"));
    }

    /// <summary>
    /// The XAML under test, located without assuming where the binary runs.
    ///
    /// <para>A plain walk up from <c>AppContext.BaseDirectory</c> is not enough:
    /// the test host can run from an artifacts path outside the repository
    /// (<c>dotnet test --artifacts-path</c>), and the source tree is then nowhere
    /// above it. So the search starts from the compiled chain instead - this
    /// file's own source path, recorded by the compiler - and falls back to the
    /// walk. Both are checked, and a miss throws with the paths that were tried
    /// rather than a bare "not found".</para>
    /// </summary>
    private static string CardXamlPath
    {
        get
        {
            const string Relative =
                @"src\mpvw-app\Domains\Playback\Controls\LibraryCard.xaml";

            var tried = new List<string>();

            // 1. This source file's own path. [CallerFilePath] is the compiler's
            //    view, so it points into the checkout no matter where the build
            //    output or the artifacts directory sits. Three levels up from
            //    tests/conf lands on the repository root (tests/conf -> tests ->
            //    root), which is what Relative is written against.
            var fromSource = Path.GetDirectoryName(ThisFilePath());
            if (!string.IsNullOrEmpty(fromSource))
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(fromSource, "..", "..", Relative));
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
                var candidate = Path.Combine(dir.FullName, Relative);
                tried.Add(candidate);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException(
                "LibraryCard.xaml not found. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, tried));
        }
    }

    /// <summary>
    /// This file's absolute path, as the compiler saw it. Used by
    /// <see cref="CardXamlPath"/> to find the checkout when the test host runs
    /// outside it. Never called.
    /// </summary>
    private static string ThisFilePath([CallerFilePath] string path = "") => path;

    /// <summary>
    /// The LineHeight attribute of the TextBlock carrying
    /// <paramref name="elementName"/>, as a double.
    /// </summary>
    private static double LineHeightFor(string xaml, string elementName)
    {
        var element = ElementFor(xaml, elementName);
        var match = Regex.Match(element, @"LineHeight\s*=\s*""([0-9.]+)""");

        Assert.That(
            match.Success,
            $"the {elementName} TextBlock declares no LineHeight, so it draws the " +
            "default line box and overflows the cell reserved for it");

        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>The FontSize token of the TextBlock carrying
    /// <paramref name="elementName"/>.</summary>
    private static string FontSizeTokenFor(string xaml, string elementName)
    {
        var element = ElementFor(xaml, elementName);
        var match = Regex.Match(element, @"FontSize\s*=\s*""\{StaticResource ([A-Za-z0-9_]+)\}""");

        Assert.That(match.Success, $"the {elementName} TextBlock has no FontSize token");

        return match.Groups[1].Value;
    }

    /// <summary>
    /// The markup of one named TextBlock: from its opening tag to the first
    /// self-closing close.
    ///
    /// Scoped to the element rather than searching the whole file, so a match
    /// found in some other element's attributes cannot make an assertion pass.
    /// </summary>
    private static string ElementFor(string xaml, string elementName)
    {
        var start = xaml.IndexOf($"x:Name=\"{elementName}\"", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{elementName} not found in LibraryCard.xaml");

        // Walk back to the opening '<' of this element, then forward to the '>'
        // that closes it. TextBlock here is always self-closing, so the first '>'
        // after its name ends the element.
        var open = xaml.LastIndexOf('<', start);
        Assert.That(open, Is.GreaterThanOrEqualTo(0));

        var close = xaml.IndexOf('>', start);
        Assert.That(close, Is.GreaterThan(start));

        return xaml[open..(close + 1)];
    }
}
