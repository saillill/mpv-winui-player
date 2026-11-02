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
        Assert.That(CardMetrics.CornerRadius, Is.EqualTo(6));

        // The caption band follows the cell height, so a change here without a
        // change in CardMetrics.CaptionBandHeight clips the line it reserves.
        // These numbers are the reference's own, measured on its hovered card.
        Assert.That(CardMetrics.CaptionTopGap, Is.EqualTo(6));

        // Both hover buttons are the same size now: the reference draws them as
        // one matched pair, and the old 40/32 split made the second look accidental.
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(30));
        Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(30));
        Assert.That(CardMetrics.CornerInset, Is.EqualTo(8));
        Assert.That(CardMetrics.RingThickness, Is.EqualTo(1.5));
        Assert.That(CardMetrics.NowPlayingBarHeight, Is.EqualTo(2));

        // The hover frame's geometry. It wraps the whole card, so it is stated in
        // terms of the gutter it opens around the content rather than an inset into
        // the picture - the previous constants (HoverInset 6, HoverRingThickness 2)
        // described the black inner wash the report rejected.
        Assert.That(CardMetrics.HoverFrameInset, Is.EqualTo(6));
        Assert.That(CardMetrics.HoverFrameThickness, Is.EqualTo(1.5));
        Assert.That(CardMetrics.HoverFrameCornerRadius, Is.EqualTo(12));

        // The frame is the same weight as the corner discs it encloses: heavier
        // reads as a selection border rather than a hover affordance.
        Assert.That(CardMetrics.HoverFrameThickness, Is.EqualTo(CardMetrics.RingThickness));
    }

    /// <summary>
    /// The frame's radius is derived from the content's, not sampled from the
    /// reference.
    /// </summary>
    [Test]
    public void HoverFrameRadiusIsConcentricWithTheContent()
    {
        // The frame sits HoverFrameInset OUTSIDE the content, so its arc must be
        // that many DIP larger or the two edges are not parallel. This is the
        // relation the comparison page got wrong (it offered 11 against an
        // outward push of 6) and the reason the number is derived, not sampled.
        Assert.That(
            CardMetrics.HoverFrameCornerRadius,
            Is.EqualTo(CardMetrics.CornerRadius + CardMetrics.HoverFrameInset));
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
    /// The card draws exactly one caption line, and it is the title.
    ///
    /// Reported as "drop the date string and the repeated title, keep only the top
    /// line". The second line is still in the tree - the code behind assigns it on
    /// every recycle - so nothing but Collapsed keeps it off the screen, and
    /// nothing but this assertion keeps it Collapsed. If it comes back visible
    /// while CaptionBandHeight reserves one line, it is drawn outside its cell.
    /// </summary>
    [Test]
    public void TheSubtitleLineIsCollapsed()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var subtitle = ElementFor(xaml, "SubtitleText");

        Assert.That(
            subtitle.Contains("Visibility=\"Collapsed\"", StringComparison.Ordinal),
            "SubtitleText must stay Collapsed: the card shows one line (the file name) " +
            "and the cell reserves room for one");
    }

    /// <summary>
    /// The title line is a single line, never two.
    ///
    /// MaxLines="2" was there while the card had a wrapped title over a subtitle.
    /// With one line, a title allowed to wrap pushes the second line into whatever
    /// the cell reserved for the thing that no longer exists.
    /// </summary>
    [Test]
    public void TheTitleLineDoesNotWrapToTwoLines()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var title = ElementFor(xaml, "TitleText");

        Assert.That(
            title.Contains("MaxLines=\"1\"", StringComparison.Ordinal),
            "TitleText must cap at one line: the cell reserves one");
    }

    /// <summary>
    /// The root button stretches its content, so the hover frame has a card to
    /// trace.
    ///
    /// <para><b>This is the bug that made the frame invisible, and it took three
    /// wrong guesses to find.</b> <c>VerticalContentAlignment="Top"</c> pinned the
    /// content to the top of the button, so the card's root Grid collapsed to its
    /// content height and the frame - a Border with <c>Margin="4"</c> and no size
    /// of its own - had nothing left to contract into. It was in the visual tree,
    /// reported Visible, and drew five pixels: measured by giving it a red 4 DIP
    /// border and counting them, which is what finally distinguished "not drawn"
    /// from "not shown".</para>
    ///
    /// <para>The earlier guesses are worth recording so they are not repeated:
    /// an Opacity="0" left in the markup (it multiplies with the compositor fade),
    /// drawing order (moved to Canvas.ZIndex=10, which did not help on its own),
    /// and the border brush (swapped for red, which proved the brush was fine).</para>
    /// </summary>
    [Test]
    public void TheRootButtonStretchesItsContent()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var root = SelfClosingElement(xaml, "RootButton");

        Assert.That(
            root.Contains("VerticalContentAlignment=\"Stretch\"", StringComparison.Ordinal),
            "RootButton must stretch its content: with \"Top\" the card's Grid collapses " +
            "to its content height, and the hover frame has nothing to stretch into");
    }

    /// <summary>
    /// The hover frame sits in a permanent gutter around the card's content.
    ///
    /// <para>Reported after the previous attempt: "you did not understand the
    /// second image - the black frame is inside the card; the outermost hover frame
    /// should be the outlined one, and what you framed is the video thumbnail".
    /// The previous design was a wash with a black ring inset into the picture,
    /// which is the opposite of what was asked for on both counts: wrong colour,
    /// and wrapping the thumbnail instead of the card.</para>
    ///
    /// <para>Three properties make it the requested shape, and each is silent when
    /// it regresses:</para>
    ///
    /// <list type="bullet">
    /// <item>Themed - <c>MpvCardFrameBrush</c>, one value per theme. The near
    /// opaque <c>MpvCardRingBrush</c> it used to wear is shared with the corner
    /// buttons and the OSD capsule, so a single value could not suit all three.</item>
    /// <item>Concentric - its radius is the content's plus the gutter, so the two
    /// arcs stay parallel.</item>
    /// <item>Separated and permanent - the content sits <c>HoverFrameInset</c>
    /// inside the card whether or not the frame is showing, so hovering moves no
    /// pixels. The cell carries that gutter (<c>CardMetrics.CellWidthFor</c>), the
    /// frame stretches the cell, and its own tag carries no Margin: two independent
    /// Margins is how a stroke and the content it traces drift apart.</item>
    /// </list>
    /// </summary>
    [Test]
    public void TheHoverFrameSitsInAGutterOutsideTheContent()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var frame = SelfClosingElement(xaml, "HoverFrame");

        Assert.That(
            frame.Contains("{ThemeResource MpvCardFrameBrush}", StringComparison.Ordinal),
            "the frame must use the themed MpvCardFrameBrush, not the near-opaque " +
            "MpvCardRingBrush the buttons and the OSD capsule share");

        Assert.That(
            frame.Contains($"CornerRadius=\"{CardMetrics.HoverFrameCornerRadius}\"",
                StringComparison.Ordinal),
            "the frame's radius must be the content's plus the gutter, or the two arcs " +
            "are not concentric");

        // The gutter is permanent: the OUTER Grid carries it whether or not the
        // frame is showing, so the content never moves on hover. Measured: the grid
        // cell clips anything drawn outside it, so the frame lives in this gutter
        // rather than beyond the cell.
        var content = ContentGridFor(xaml);

        Assert.That(
            content.Contains($"Margin=\"{CardMetrics.HoverFrameInset}\"", StringComparison.Ordinal),
            $"the content must sit CardMetrics.HoverFrameInset ({CardMetrics.HoverFrameInset}) " +
            "inside the card at all times - a gutter that appears on hover would move the " +
            "picture, which is the one thing this card must not do");

        // And the frame must NOT be a sibling that stretches over the content: it is
        // the outermost layer, offset by the same gutter.
        Assert.That(
            frame.Contains("Margin=", StringComparison.Ordinal),
            Is.False,
            "the frame takes the gutter from its container, not from its own Margin - " +
            "two independent Margs are how the stroke and the content drift apart");
    }

    /// <summary>
    /// The cover wears a hairline in its resting state, and the caption does not.
    ///
    /// <para>The report asked for "a faint grey-white shadow stroke around the
    /// thumbnail" while the card is not hovered. It is a hairline on the MEDIA
    /// BORDER, not a halo around the whole card: the second would fight the hover
    /// frame, which is the one that means "this card".</para>
    ///
    /// <para><b>Both properties, because either one alone is silent.</b> A
    /// BorderBrush with no BorderThickness paints nothing, so asserting the brush
    /// alone would stay green through exactly the edit that removes the hairline.
    /// The brush is checked with its markup form, not just its key, so the token is
    /// a {ThemeResource} and follows the theme rather than resolving once.</para>
    ///
    /// <para>Asserted from the markup because it is pure XAML: nothing in the code
    /// behind can observe "there is a one-pixel edge around the picture".</para>
    /// </summary>
    [Test]
    public void TheCoverWearsAHairlineAndTheCaptionDoesNot()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var media = ElementFor(xaml, "MediaBorder");

        Assert.That(
            media.Contains("{ThemeResource MpvCardCoverStrokeBrush}", StringComparison.Ordinal),
            "MediaBorder must paint the resting-state hairline; found: " + media);

        Assert.That(
            media.Contains("BorderThickness=\"1\"", StringComparison.Ordinal),
            "the hairline must be 1 DIP - a brush with no thickness paints nothing; found: " + media);

        Assert.That(
            ElementFor(xaml, "TitleText").Contains("Stroke", StringComparison.Ordinal),
            Is.False,
            "the caption must not wear it - only the picture does");
    }

    /// <summary>
    /// The card's content layer: the named Grid that carries the frame's gutter.
    ///
    /// <para>Found by <c>x:Name</c> rather than by a Margin attribute: the search
    /// string is the anchor, and a grid found by a property it may stop having is
    /// the search that silently starts matching some other Grid.</para>
    /// </summary>
    private static string ContentGridFor(string xaml)
    {
        var marker = "x:Name=\"HoverFrame\"";
        var at = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), "no HoverFrame in LibraryCard.xaml");

        // The content Grid follows the frame element.
        var after = xaml.IndexOf("<Grid x:Name=\"CardContent\"", at, StringComparison.Ordinal);
        Assert.That(after, Is.GreaterThan(at), "no content Grid after HoverFrame");

        var close = xaml.IndexOf('>', after);
        Assert.That(close, Is.GreaterThan(after));

        return xaml[after..(close + 1)];
    }

    /// <summary>
    /// Both hover buttons centre their glyph rather than inheriting a default.
    ///
    /// <para>Reported with a screenshot circling the two discs: "the icons are not
    /// centred; the play glyph sits off-centre and its corner got clipped". Two
    /// causes, and both are silent when they come back:</para>
    ///
    /// <list type="bullet">
    /// <item><c>Padding</c>. A Button's default padding is not symmetric about its
    /// content, so a 36x36 button pushed its glyph up and right. Every other
    /// fixed-size icon button in this repository already sets
    /// <c>Padding="0"</c>; these two were the only ones that did not.</item>
    /// <item>The content being a bare <c>FontIcon</c>. It lays its glyph out on a
    /// text baseline, so the ink sits above the centre line even at zero padding.
    /// A Viewbox is measured and centred as a shape.</item>
    /// </list>
    ///
    /// <para>Asserted from the markup because both are pure XAML: nothing in the
    /// code behind can observe "the triangle is 2 DIP high of centre".</para>
    /// </summary>
    [Test]
    public void HoverButtonsCentreTheirGlyph()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        foreach (var name in new[] { "PlayButton", "MoreButton" })
        {
            var button = ButtonBlock(xaml, name);

            Assert.That(
                button.Contains("Padding=\"0\"", StringComparison.Ordinal),
                $"{name} must set Padding=\"0\": the inherited padding is not symmetric " +
                "and pushes the glyph up and right");

            Assert.That(
                button.Contains("HorizontalContentAlignment=\"Center\"", StringComparison.Ordinal),
                $"{name} must centre its content horizontally");

            Assert.That(
                button.Contains("VerticalContentAlignment=\"Center\"", StringComparison.Ordinal),
                $"{name} must centre its content vertically");

            // The glyph goes through a Viewbox, not straight into the Button: a
            // FontIcon's baseline offset survives Padding="0".
            Assert.That(
                button.Contains("<Viewbox", StringComparison.Ordinal),
                $"{name} must wrap its glyph in a Viewbox; a bare FontIcon sits above " +
                "the centre line because it is laid out on a text baseline");
        }
    }

    /// <summary>
    /// A self-closing element's full tag, comments stripped.
    ///
    /// <para>The same comment-stripping as <see cref="ButtonBlock"/> and for the
    /// same measured reason: this file's prose quotes the very attribute values
    /// the assertions look for, so a search over raw markup can pass after the
    /// attribute is deleted.</para>
    /// </summary>
    private static string SelfClosingElement(string xaml, string name)
    {
        var at = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), $"{name} not found in LibraryCard.xaml");

        var open = xaml.LastIndexOf('<', at);
        Assert.That(open, Is.GreaterThanOrEqualTo(0));

        var close = xaml.IndexOf('>', at);
        Assert.That(close, Is.GreaterThan(at), $"{name} is not closed");

        return Regex.Replace(xaml[open..(close + 1)], "<!--[\\s\\S]*?-->", string.Empty);
    }

    /// <summary>
    /// Everything from a button's opening tag through its closing tag, with XML
    /// comments removed.
    ///
    /// <para><see cref="ElementFor"/> stops at the first '>', which is right for
    /// the self-closing TextBlocks it was written for and wrong for these buttons:
    /// their glyph lives between the two tags, which is what the centring
    /// assertions need to see.</para>
    ///
    /// <para><b>Comments are stripped first, and that is not tidiness.</b> The
    /// markup beside these two buttons quotes the very attributes the assertions
    /// look for - "every other icon button already sets Padding=\"0\"" - so a
    /// search over the raw block found the phrase in the prose. It passed with
    /// the attribute deleted, which was measured: removing Padding from
    /// PlayButton left this test green. Matching only real attributes is the
    /// difference between a regression guard and a comment reader.</para>
    /// </summary>
    private static string ButtonBlock(string xaml, string name)
    {
        var open = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.That(open, Is.GreaterThanOrEqualTo(0), $"{name} not found in LibraryCard.xaml");

        var start = xaml.LastIndexOf('<', open);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));

        var end = xaml.IndexOf("</Button>", open, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(open), $"{name} has no closing </Button>");

        var block = xaml[start..(end + "</Button>".Length)];

        return Regex.Replace(block, "<!--[\\s\\S]*?-->", string.Empty);
    }

    /// <summary>
    /// The caption reserves a line and a half below the title.
    ///
    /// <para>Reported as "the title should leave a line and a half of empty space
    /// below it". Without it the last line sits hard against the card's lower edge
    /// and the hover frame encloses nothing underneath.</para>
    ///
    /// <para><b>Two places, one number.</b> The reserve lives in the caption's
    /// Bottom margin AND in <c>CardMetrics.CaptionBandHeight</c>, because the grid
    /// sizes its cell before any card exists. Dropping it from the markup leaves
    /// the cell padded but the card empty; dropping it from the band clips it. This
    /// asserts the markup side; CardCellHeightTests asserts the band.</para>
    /// </summary>
    [Test]
    public void TheCaptionReservesALineAndAHalfBelowTheTitle()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        // The caption StackPanel is the one between the tile and the title.
        var marker = "Spacing=\"{StaticResource MpvSpaceXxs}\"";
        var at = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), "no caption StackPanel");

        var open = xaml.LastIndexOf('<', at);
        var tag = xaml[open..(at + marker.Length)];

        var expected = $"0,{CardMetrics.CaptionTopGap},0,{CardMetrics.TrailingCaptionSpace}";
        Assert.That(
            tag.Contains($"Margin=\"{expected}\"", StringComparison.Ordinal),
            $"the caption must reserve {CardMetrics.TrailingCaptionSpace} DIP below the " +
            $"title (Margin=\"{expected}\"); found: {tag}");
    }

    /// <summary>
    /// Both caption lines share one type size, and neither is smaller.
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
    /// All three card grids must leave the frame a home.
    ///
    /// The frame lives in a 6 DIP gutter INSIDE the cell (the grid clips anything
    /// drawn outside it - measured, see Task 0), so the cell is wider than the
    /// content, and the spacing is what keeps two selected frames apart. The grids
    /// shipped 12, which put a selected card's frame 2 DIP from its neighbour's.
    ///
    /// There are THREE layouts - two built in code, one in markup as the
    /// first-paint default - and missing any of them leaves one screen tight.
    ///
    /// The markup cannot name CardMetrics (it is internal static), so its numbers
    /// are literals pinned by this test, the same arrangement LineHeight uses.
    ///
    /// <para><b>Comments are stripped before the search, and that is not
    /// tidiness.</b> The comment above the layout quotes the very literals this
    /// test looks for ("MinItemWidth 265 is CellWidthFor(253)"), so a raw search
    /// over the file stays green with the attributes reverted to 253 and 12 while
    /// only the prose moved. That is the trap <see cref="ButtonBlock"/> documents,
    /// and it was measured there.</para>
    /// </summary>
    [Test]
    public void EveryCardGridLeavesRoomForTheFrame()
    {
        Assert.That(CardMetrics.GridColumnSpacing, Is.EqualTo(20));
        Assert.That(CardMetrics.GridRowSpacing, Is.EqualTo(20));

        // Two selected frames, back to back, must not read as one line.
        Assert.That(
            CardMetrics.GridColumnSpacing,
            Is.GreaterThan(CardMetrics.HoverFrameInset * 2));

        var page = Regex.Replace(File.ReadAllText(PageXamlPath), "<!--[\\s\\S]*?-->", string.Empty);

        var columnSpacing = AttributeFor(page, "MinColumnSpacing");

        Assert.That(
            page.Contains($"MinColumnSpacing=\"{CardMetrics.GridColumnSpacing}\"", StringComparison.Ordinal),
            "the markup's default grid must use the same column spacing. It says " +
            $"{columnSpacing}: MpvSpaceM, which is 12, put a selected card's frame 2 DIP " +
            "from its neighbour's frame");

        var rowSpacing = AttributeFor(page, "MinRowSpacing");

        Assert.That(
            page.Contains($"MinRowSpacing=\"{CardMetrics.GridRowSpacing}\"", StringComparison.Ordinal),
            "the markup's default grid must use the same row spacing. It says " +
            $"{rowSpacing}: MpvSpaceL is 16, and the cell is 265 wide now, so the rows " +
            "need the same air the columns get");

        // The cell is the content plus the gutter on both sides.
        const double ContentWidth = 253;

        var itemWidth = AttributeFor(page, "MinItemWidth");

        Assert.That(
            page.Contains($"MinItemWidth=\"{CardMetrics.CellWidthFor(ContentWidth)}\"", StringComparison.Ordinal),
            "the markup's cell must be wide enough for the content AND the frame's gutter. " +
            $"It says {itemWidth}, while CellWidthFor({ContentWidth}) is " +
            $"{CardMetrics.CellWidthFor(ContentWidth)} - at 253 the picture is squeezed to " +
            "241 by its own gutter");

        var minItemHeight = Regex.Match(page, @"MinItemHeight=""([\d.]+)""");

        Assert.That(minItemHeight.Success, "no MinItemHeight in the markup grid");

        Assert.That(
            double.Parse(minItemHeight.Groups[1].Value, CultureInfo.InvariantCulture),
            Is.GreaterThanOrEqualTo(CardMetrics.CellHeightFor(ContentWidth, isAlbum: false)),
            "the first-paint cell is shorter than CardMetrics says the card needs, so the " +
            "caption band is clipped until ApplyLibraryGridLayout replaces it");
    }

    /// <summary>
    /// The selection mode is the selection, not a switch beside it.
    ///
    /// Reported twice, in this order: "0 selected means the bar is gone and the
    /// cards go back to their normal hover", then "with one selected the others
    /// show their boxes too". Both hold only if unticking the last card leaves the
    /// mode - otherwise the mode is on with nothing on screen to say so, and the
    /// only way out is a menu item the user has no reason to look for.
    ///
    /// Asserted from the source because the state lives behind WinUI types the
    /// test project cannot reference, and a comment saying "mode follows the count"
    /// is exactly the kind of thing that stops being true.
    /// </summary>
    [Test]
    public void SelectionModeFollowsTheCountRatherThanASwitch()
    {
        var source = File.ReadAllText(PageLibrarySourcePath);

        var body = MethodBody(source, "UpdateSelectionBar");

        Assert.That(
            body.Contains("_selectionMode =", StringComparison.Ordinal),
            "UpdateSelectionBar must be the place the mode is decided");

        // No other method may assign the flag: SetSelectionMode is gone.
        Assert.That(
            source.Contains("private void SetSelectionMode", StringComparison.Ordinal),
            Is.False,
            "there must be no SetSelectionMode(bool): a public on/off is what let the mode " +
            "outlive the ticks");
    }
    /// <summary>
    /// The tick box is an affordance, not a readout, so it takes clicks.
    ///
    /// It used to live inside HoverOverlay with IsHitTestVisible="False" and no
    /// handler: it showed a state and could not change one, which is why the only
    /// way into selection mode was a context menu item nobody would find. It also
    /// sat on the tile's top-left and vanished with the overlay, so a ticked card
    /// lost its mark as soon as the pointer left.
    /// </summary>
    [Test]
    public void TheTickBoxIsClickableAndOutlivesTheOverlay()
    {
        var xaml = File.ReadAllText(CardXamlPath);

        var box = ElementFor(xaml, "SelectionBox");

        Assert.That(
            box.Contains("Tapped=", StringComparison.Ordinal)
                || box.Contains("Click=", StringComparison.Ordinal),
            "the tick box must have a handler: it is the way into selection mode");

        Assert.That(
            box.Contains("IsHitTestVisible=\"False\"", StringComparison.Ordinal),
            Is.False,
            "a hit-test-invisible tick box is a readout, not an entry point");

        // It is a sibling of the overlay now, not a child: the overlay collapses
        // on pointer exit, and a tick mark that disappears with it is not a mark.
        var overlayEnd = xaml.IndexOf("</Grid>", xaml.IndexOf("x:Name=\"HoverOverlay\"", StringComparison.Ordinal), StringComparison.Ordinal);
        var boxAt = xaml.IndexOf("x:Name=\"SelectionBox\"", StringComparison.Ordinal);

        Assert.That(
            boxAt, Is.GreaterThan(overlayEnd),
            "SelectionBox must sit outside HoverOverlay; inside it, it collapses with the hover");
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
    /// The page markup under test, located the same way as
    /// <see cref="CardXamlPath"/>: from this file's compiled source path first,
    /// then a walk up from the binary.
    ///
    /// <para>A second helper rather than a parameter on the first: two files, two
    /// relative paths, and the error has to name the one that was missing. The
    /// search order is deliberately identical - a different way of finding the
    /// checkout is a different set of conditions for the test to pass under.</para>
    /// </summary>
    private static string PageXamlPath
    {
        get
        {
            const string Relative =
                @"src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml";

            var tried = new List<string>();

            // 1. This source file's own path, from [CallerFilePath]: the compiler's
            //    view of the checkout, which does not move with --artifacts-path.
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
                "MpvPlayerPage.xaml not found. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, tried));
        }
    }

    /// <summary>
    /// The page's library half, where the selection mode is decided. Located the
    /// same way as <see cref="PageXamlPath"/>: from this file's compiled source
    /// path first, then a walk up from the binary.
    ///
    /// <para>This is source, not markup, and it is read for the same reason the
    /// markup is: the state behind it is a page field over WinUI controls, and the
    /// test project targets <c>net10.0</c>, so it cannot reference the assembly the
    /// field lives in.</para>
    /// </summary>
    private static string PageLibrarySourcePath
    {
        get
        {
            const string Relative =
                @"src\mpvw-app\Domains\Playback\MpvPlayerPage_Library.cs";

            var tried = new List<string>();

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
                "MpvPlayerPage_Library.cs not found. Tried:" + Environment.NewLine +
                string.Join(Environment.NewLine, tried));
        }
    }

    /// <summary>
    /// The braced body of the method named <paramref name="name"/>: from its
    /// opening brace through the matching close.
    ///
    /// <para><b>Strict on purpose.</b> It throws rather than returning an empty
    /// string: a helper that quietly answers "" would make the assertion on the
    /// body fail for a reason that has nothing to do with the code under test, and
    /// a body-only search that never finds its method is indistinguishable from a
    /// method that no longer assigns anything.</para>
    ///
    /// <para>The name is searched by occurrence rather than by its first one,
    /// because the first occurrences in this file are CALL sites: the page calls
    /// <c>UpdateSelectionBar()</c> from other methods before the declaration itself
    /// appears. An occurrence is the declaration only when its parameter list is
    /// followed directly by <c>{</c>; a call is followed by <c>;</c>, which is what
    /// rules it out.</para>
    /// </summary>
    private static string MethodBody(string source, string name)
    {
        var at = source.IndexOf(name, StringComparison.Ordinal);

        while (at >= 0)
        {
            var open = BraceAfterParameterList(source, at + name.Length);

            if (open >= 0)
            {
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

                Assert.Fail($"{name}: its opening brace at offset {open} is never closed");
            }

            at = source.IndexOf(name, at + name.Length, StringComparison.Ordinal);
        }

        Assert.Fail(
            $"no declaration of {name} found in the page source - either the method was " +
            "renamed or it is gone, and neither is something this test can read past");
        return string.Empty;
    }

    /// <summary>
    /// The offset of the <c>{</c> that opens a declaration whose name ends at
    /// <paramref name="afterName"/>, or -1 when what follows is a call rather than
    /// a declaration.
    /// </summary>
    private static int BraceAfterParameterList(string source, int afterName)
    {
        var i = afterName;
        while (i < source.Length && char.IsWhiteSpace(source[i]))
        {
            i++;
        }

        if (i >= source.Length || source[i] != '(')
        {
            return -1;
        }

        var depth = 0;

        for (; i < source.Length; i++)
        {
            if (source[i] == '(')
            {
                depth++;
            }
            else if (source[i] == ')')
            {
                depth--;

                if (depth == 0)
                {
                    break;
                }
            }
        }

        if (i >= source.Length)
        {
            return -1;
        }

        i++;
        while (i < source.Length && char.IsWhiteSpace(source[i]))
        {
            i++;
        }

        return i < source.Length && source[i] == '{' ? i : -1;
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

    /// <summary>
    /// The first `name="value"` in <paramref name="text"/>, for a failure message
    /// that shows what the markup actually says instead of only what was wanted.
    /// </summary>
    private static string AttributeFor(string text, string name)
    {
        var match = Regex.Match(text, "\\b" + name + "=\"([^\"]*)\"");

        return match.Success ? match.Value : $"{name} (absent)";
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
