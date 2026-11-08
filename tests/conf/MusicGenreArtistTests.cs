using mpv_winui.Domains.Playback.Cards;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
///
/// <para><b>Task 2's sort guards live here too.</b> The music library's sort is
/// page state, so what can be pinned on this side is that the state and the
/// branches reading it exist: a field of music's own, the tab-dependent collapse
/// the Albums tab needs, and the one predicate that keeps the whole toolbar row
/// off the artists tab.</para>
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
    /// An artist tile's Border radius is half its WIDTH, and the cover's clip is
    /// built from that same width.
    ///
    /// <para><b>The failure this pins is silent.</b> The tile is a circle only while
    /// the radius is half the side the clip is a circle of. The border's radius and
    /// the clip used to be derived from different sides - the border from the
    /// height, the clip from the width - so they agreed only because
    /// <c>IsAlbum</c> made the tile square. That is an invariant of how the PAGE
    /// prepares a card, which this control cannot see and nothing pinned; a card
    /// prepared with <c>IsAlbum</c> false would draw a rounded rectangle over a
    /// circular clip and no test would notice.</para>
    ///
    /// <para><b>Asserted on the source, not on a rendered tile.</b> A rendered
    /// assertion is unreachable from <c>tests/conf</c>: the project targets plain
    /// <c>net10.0</c> and cannot reference <c>Microsoft.UI.Xaml</c>, so it has no
    /// <c>LibraryCard</c>, no <c>Border</c> and no compositor. The established
    /// pattern here is reading the card's sources as text (CardMetricsTests does it
    /// for the markup), so the assertion is that both call sites name the width -
    /// the identifier that is half of every <c>CircleCornerRadius</c> argument -
    /// and that the clip receives its width from the caller rather than reading its
    /// own.</para>
    /// </summary>
    [Test]
    public void TheArtistTileRadiusIsHalfItsWidthOnBothTheBorderAndTheClip()
    {
        var source = StrippedCardCode();

        // The tile's height, the Border's radius and the clip all come from the
        // same method, so "the same width" is a property of this one body.
        var heightBody = MethodBody(source, "private void ApplyCoverHeight()");

        Assert.That(
            heightBody,
            Does.Contain("CardMetrics.CircleCornerRadius(width)"),
            "the Border's radius must be half the tile's WIDTH: height and width are equal only while IsAlbum "
            + "is true, and that is the page's promise, not this control's");

        Assert.That(
            heightBody,
            Does.Not.Contain("CardMetrics.CircleCornerRadius(height)"),
            "the Border's radius is back on the height, so the tile is a rounded rectangle whenever the clip's "
            + "width makes it a circle - two shapes, and nothing renders red");

        // The clip takes the width as an argument instead of reading ActualWidth
        // itself. A signature is the pin: two independent reads are two numbers
        // that can differ, and half of a circle IS the equality of those two.
        Assert.That(
            heightBody,
            Does.Contain("ApplyCoverClip(width)"),
            "ApplyCoverClip must be handed the same width the Border's radius came from, not left to read its "
            + "own");

        var clipBody = MethodBody(source, "private void ApplyCoverClip(double width)");

        Assert.That(
            clipBody,
            Does.Contain("CardMetrics.CircleCornerRadius(width)"),
            "the clip's radius must be half the width its own geometry is sized from");

        Assert.That(
            clipBody,
            Does.Not.Contain("CardMetrics.CircleCornerRadius(height)"),
            "the clip's radius is on the height while its geometry is sized by the width: on a non-square tile "
            + "the clip is a stadium, and the cover's corners show through");
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

        var guard = body.IndexOf("if (_isArtist || _isFolder)", StringComparison.Ordinal);
        Assert.That(
            guard,
            Is.GreaterThanOrEqualTo(0),
            "ShowOverlay must branch on IsArtist (folders join the same guard), or a person's tile shows a play disc and an overflow disc "
            + "for a track nobody chose");

        // The block the guard opens, not the rest of the method: the else branch
        // restores both discs with the very lines asserted below, so a search over
        // the whole body stays green with the two branches swapped - which would
        // show the discs on the artist tile and take them off the file card.
        var artistBlock = body[guard..body.IndexOf('}', guard)];

        Assert.That(
            artistBlock,
            Does.Contain("PlayButton.Visibility = Visibility.Collapsed"),
            "the play disc must be collapsed on an artist tile");

        Assert.That(
            artistBlock,
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

        var guard = body.IndexOf("if (_isArtist || _isFolder)", StringComparison.Ordinal);
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

        // ApplyCaptions keeps its own artist-only branch: a folder card already has its
        // name in Title and its path in Subtitle from FromFolder, so the caption code has
        // nothing to substitute for it.
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
    /// The sort menu on the music tabs reads a field of its own.
    ///
    /// <para><b>This is the smallest guard that covers the defect.</b> Until this
    /// task the music library had no sort at all: <c>ApplyLibraryFilter</c> ordered
    /// the Songs tab with a hardcoded <c>VideoSortMode.NameAsc</c>, so the button's
    /// caption changed when it was clicked and the list never moved. A test on this
    /// side of the window cannot prove that a list moves, but it can prove that the
    /// state a working sort needs exists at all - and that is what was missing.</para>
    ///
    /// <para>Comments are stripped first, for the reason the class summary gives:
    /// the field's own doc comment names <c>_musicSort</c>, so a raw search would
    /// stay green with the field deleted. That is not hypothetical - the injection
    /// that proved this test can fail deleted the three declarations and left the
    /// prose standing.</para>
    /// </summary>
    [Test]
    public void TheSortMenuReadsAMusicFieldOnTheMusicTabs()
    {
        var source = StrippedPageSource(PageVideoCodePath);
        Assert.That(source, Does.Contain("_musicSort"));
        Assert.That(source, Does.Contain("EffectiveMusicSort"));
    }

    /// <summary>
    /// The music library has a sort of its own, and the Albums tab narrows it.
    ///
    /// <para>Three silent failures are pinned here. The field must be declared
    /// SEPARATE from the video library's: sharing one would make reordering music
    /// reorder the video grid. The Albums tab orders groups of files, and a group has
    /// no watch-history time, so both time orders have to collapse to name order
    /// there instead of sitting in the menu doing nothing. And only the Songs tab may
    /// offer those two orders, which is the same condition from the other side.</para>
    /// </summary>
    [Test]
    public void TheAlbumTabNarrowsTheMusicSortToNames()
    {
        var source = StrippedPageSource(PageVideoCodePath);

        Assert.That(
            Squashed(source),
            Does.Contain("private VideoSortMode _musicSort = VideoSortMode.NameAsc;"),
            "the music library needs a field of its own; on the video field, reordering music reorders videos");

        Assert.That(
            Squashed(PropertyExpression(source, "private VideoSortMode EffectiveMusicSort =>")),
            Does.Contain(
                "!MusicSortHasTimeModes && _musicSort is VideoSortMode.Newest or VideoSortMode.Oldest "
                + "? VideoSortMode.NameAsc : _musicSort"),
            "the Albums tab must collapse BOTH time orders to name order, and return every other order as chosen");

        Assert.That(
            Squashed(PropertyExpression(source, "private bool MusicSortHasTimeModes =>")),
            Does.Contain("_librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Songs"),
            "only the Songs tab of the music library offers time orders: the grouped tabs order tags, "
            + "and a tag carries no time");
    }

    /// <summary>
    /// The picker answers two different questions: the whole alphabet for a letter sort,
    /// only the real headings otherwise.
    /// </summary>
    [Test]
    public void ThePickerListsTheWholeAlphabetForALetterSortAndOnlyTheRealHeadingsOtherwise()
    {
        // The two cases answer different questions. A letter sort is "is there anything
        // under Q", which needs the whole closed alphabet - leave Q out and the grid moves
        // as the library changes, and cannot say "no". A year or an artist sort has no
        // closed set, so padding it would invent headings the list does not have.
        var music = Squashed(StrippedPageSource(PageMusicCodePath));

        var entries = Squashed(MethodBody(music, "private List<LibraryIndexBucket> BuildGroupPickerEntries()"));

        Assert.That(
            entries,
            Does.Contain("LibraryIndexLetter.BuildRail"),
            "a letter sort must offer every bucket, marked with whether it leads anywhere");

        Assert.That(
            entries,
            Does.Contain("_groupHeaders.Keys"),
            "every other sort must offer exactly its own headings");
    }

    ///
    /// <para>This is the residue of the reported defect. Before the fix the Songs
    /// branch passed a literal mode, so the menu was decoration: the caption moved and
    /// the cards did not. The counterpart matters just as much - a click outside the
    /// music section must still write the video field, or the video grid silently
    /// stops obeying its own menu.</para>
    /// </summary>

    /// <summary>
    /// The list follows the sort, and each library writes its own field.
    [Test]
    public void TheMusicTabsSortThemselvesAndTheVideoTabKeepsItsOwnField()
    {
        var library = StrippedPageSource(PageLibraryCodePath);
        var video = StrippedPageSource(PageVideoCodePath);

        var filter = Squashed(MethodBody(library, "private void ApplyLibraryFilter()"));

        // The music order no longer runs through SortVideoEntries: three of its modes
        // (artist, year, added) have no meaning for a folder scan, so the two lists got
        // separate switches. The property this test was written for is unchanged - the
        // music list must be ordered by the EFFECTIVE mode, never by a literal.
        Assert.That(
            filter,
            Does.Contain("SortMusicEntries(cards, EffectiveMusicSort)"),
            "the music tabs must order the list they show; a literal mode here is the dead sort this test exists for");

        Assert.That(
            filter,
            Does.Not.Contain("SortVideoEntries(cards, VideoSortMode."),
            "a hardcoded sort mode is back in ApplyLibraryFilter, so one section ignores its menu again");

        Assert.That(
            filter,
            Does.Contain("cards = SortVideoEntries(cards, _videoSort)"),
            "the video library must keep reading the video field");

        var click = Squashed(MethodBody(video, "private void VideoSort_Click(object sender, RoutedEventArgs e)"));

        var guard = click.IndexOf("if (_librarySection == LibrarySectionMusic)", StringComparison.Ordinal);
        Assert.That(guard, Is.GreaterThanOrEqualTo(0), "the click must ask which section it belongs to");

        var otherwise = click.IndexOf("else", guard, StringComparison.Ordinal);
        Assert.That(otherwise, Is.GreaterThan(guard), "the click needs a branch for the other section");

        Assert.That(
            click[guard..otherwise],
            Does.Contain("_musicSort = parsed"),
            "a music click must write the music field; writing the video field is how music used to reorder videos");

        Assert.That(
            click[otherwise..],
            Does.Contain("_videoSort = parsed"),
            "a click outside the music section must still write the video field");
    }

    /// <summary>
    /// The sort button and the menu always describe the order the list is in.
    ///
    /// <para>One value answers the whole strip. The caption, the four radio marks and
    /// the enabled state of the two time items all come from <c>current</c>, which is
    /// the SECTION's field. Reading the video field in any one of those places is
    /// invisible in a screenshot of the video library and a lie on the music one -
    /// which is exactly the state this task was opened on: 「排序方式：最新」 over a list
    /// nothing had ordered by time.</para>
    /// </summary>
    [Test]
    public void TheSortChromeDescribesTheOrderInForce()
    {
        var body = Squashed(MethodBody(StrippedPageSource(PageVideoCodePath), "private void UpdateVideoSortChrome()"));

        Assert.That(
            body,
            Does.Contain("var current = isMusic ? EffectiveMusicSort : _videoSort"),
            "the chrome must read the field the section on screen actually sorts by");

        Assert.That(
            body,
            Does.Contain("var caption = current switch"),
            "the caption must be derived from that same value, or the button narrates an order the list is not in");

        Assert.That(
            body,
            Does.Contain("VideoSortButton.Content = $\"{lang.LibrarySortBy}: {caption}\";"),
            "the button's caption must print the words derived from current");

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("SortByNameAsc.IsChecked = current == VideoSortMode.NameAsc"));
            Assert.That(body, Does.Contain("SortByNameDesc.IsChecked = current == VideoSortMode.NameDesc"));
            Assert.That(body, Does.Contain("SortByNewest.IsChecked = current == VideoSortMode.Newest"));
            Assert.That(body, Does.Contain("SortByOldest.IsChecked = current == VideoSortMode.Oldest"));
        });

        Assert.That(
            body,
            Does.Not.Contain("IsChecked = _videoSort"),
            "a radio mark still reads the video field: on the music tabs it ticks an order the list is not in");

        Assert.Multiple(() =>
        {
            Assert.That(
                body,
                Does.Contain("SortByNewest.IsEnabled = !(isMusic && !MusicSortHasTimeModes)"),
                "Newest must be disabled where the tab has no time dimension");

            Assert.That(
                body,
                Does.Contain("SortByOldest.IsEnabled = !(isMusic && !MusicSortHasTimeModes)"),
                "Oldest must be disabled where the tab has no time dimension");
        });
    }

    /// <summary>
    /// The section header re-points the sort chrome, and that call is not
    /// optional.
    ///
    /// <para>The chrome reads the section and the music tab, and
    /// <c>ApplySectionHeader</c> is the one place either of them changes, so it is
    /// the one place that has to re-point the caption afterwards.</para>
    ///
    /// <para><b>Sliced before searching.</b> A whole-file search passes on the call
    /// in <c>ApplyVideoStrings</c> - a different caller, on a different occasion -
    /// so it would stay green against the deletion that matters. The assertion is
    /// on this method's body alone.</para>
    ///
    /// <para><b>It asks for the call, not for the row.</b> On the artists tab the
    /// whole row is collapsed and this call still runs: the caption must be right
    /// the moment the row comes back. Whether the row shows is
    /// <c>_videoChromeWanted</c>'s single answer, asserted by the test below, and
    /// this guard must not become a second predicate for it.</para>
    /// </summary>
    [Test]
    public void TheSectionHeaderRefreshesTheSortChrome()
    {
        var header = Squashed(MethodBody(StrippedPageSource(PageLibraryCodePath), "private void ApplySectionHeader()"));

        Assert.That(
            header,
            Does.Contain("UpdateVideoSortChrome();"),
            "ApplySectionHeader owns the section and the music tab, so it must re-point the caption and "
            + "the two time items; without the call the chrome keeps describing the previous tab");
    }

    /// <summary>
    /// The artists tab keeps no toolbar, and it says so through ONE predicate.
    ///
    /// <para>The tab condition belongs on <c>_videoChromeWanted</c>, because that
    /// field is the whole row's answer: every control in the strip - Shuffle, the
    /// sort, and the genre button as it arrives - appears and disappears together. A
    /// second tab test inside a button is how the two answers drift apart, which this
    /// repository has spent whole fix rounds on.</para>
    ///
    /// <para>The other half of that ruling is asserted negatively: the method that
    /// applies the row must not mention the music grouping at all. Its own
    /// <c>isMusic</c> local answers a different question - which command stands in the
    /// row - and it stays.</para>
    ///
    /// <para>That negative half is stated once, against <c>MusicGrouping</c>. A
    /// second assertion on the bare word <c>Artists</c> used to sit beside it; it was
    /// strictly broader than the one above, so it could only ever fail alongside it,
    /// while also failing on any unrelated mention of the word. Deleted rather than
    /// narrowed: same coverage, one statement of it.</para>
    /// </summary>
    [Test]
    public void TheArtistsTabHidesTheRowThroughTheOnePredicate()
    {
        var library = StrippedPageSource(PageLibraryCodePath);
        var music = StrippedPageSource(PageMusicCodePath);

        var header = Squashed(MethodBody(library, "private void ApplySectionHeader()"));

        Assert.That(
            header,
            Does.Contain("!(isMusic && _musicGrouping == MusicGrouping.Artists)"),
            "the artists tab must keep no toolbar: it lists people, and every control in the row acts on files");

        Assert.That(
            header,
            Does.Contain("_videoChromeWanted = "),
            "the tab condition belongs on _videoChromeWanted, the single predicate for the whole row");

        var visibility = Squashed(MethodBody(library, "private void ApplyVideoChromeVisibility()"));

        Assert.Multiple(() =>
        {
            Assert.That(visibility, Does.Contain("var show = _videoChromeWanted && !_selectionMode;"));

            Assert.That(
                visibility,
                Does.Not.Contain("MusicGrouping"),
                "ApplyVideoChromeVisibility re-tests the tab: two predicates for one answer, and only one of them "
                + "gets updated the next time the row changes");

            Assert.That(
                visibility,
                Does.Contain("var isMusic = _librarySection == LibrarySectionMusic && !_selectionMode;"),
                "the Play all / Shuffle swap inside the row is the section's business and must stay");
        });

        var grouping = Squashed(MethodBody(music, "private void SetMusicGrouping(MusicGrouping grouping)"));

        Assert.That(
            grouping,
            Does.Contain("ApplySectionHeader();"),
            "_musicGrouping feeds the toolbar's predicate, so switching tabs must re-run the method that computes "
            + "it - otherwise the row lags one tab behind");

        Assert.That(
            grouping.IndexOf("ApplySectionHeader();", StringComparison.Ordinal),
            Is.LessThan(grouping.IndexOf("if (_libraryLoaded)", StringComparison.Ordinal)),
            "the row must be recomputed whether or not the list has loaded: the tabs are clickable before the first "
            + "load, and a stale predicate stays stale until the next section switch");

        Assert.That(
            grouping.IndexOf("ApplySectionHeader();", StringComparison.Ordinal),
            Is.LessThan(grouping.IndexOf("ApplyLibraryFilter();", StringComparison.Ordinal)),
            "the row is recomputed before the list is rebuilt");
    }
    /// <summary>
    /// The album page remembers the artist it was opened from, and the back button
    /// says so.
    ///
    /// <para>Two artists each having a "Greatest Hits" is ordinary, and the album page
    /// used to key on the name alone - so both artists' tracks landed on one page. The
    /// artist component is what stops that, and nothing else in the suite can see it:
    /// the page's members are not reachable from this project.</para>
    /// </summary>
    [Test]
    public void AnAlbumOpenedFromAnArtistIsScopedToThatArtist()
    {
        var music = StrippedPageSource(PageMusicCodePath);

        Assert.Multiple(() =>
        {
            Assert.That(
                music,
                Does.Contain("private (string Album, string? Artist) _openAlbum"),
                "the open album must carry the artist, or two artists' same-named albums merge");

            Assert.That(
                music,
                Does.Contain("private bool _albumOpened"),
                "a tuple is a value type, so it cannot be the 'is the page up' flag");

            var open = Squashed(MethodBody(music, "private void OpenAlbum(string album, string? artist)"));

            Assert.That(
                open,
                Does.Contain(".Where(e => artist is null"),
                "the narrowing must be part of the same filter chain that builds the track list");

            Assert.That(open, Does.Contain("_openAlbum = (album, artist);"));

            // The Albums tab groups by name, so it has no artist to narrow by and must
            // An expression-bodied one-liner, so there is no brace span to slice:
            // the declaration and the pass-through are the same line.
            Assert.That(
                music,
                Does.Contain("private void OpenAlbum(string album) => OpenAlbum(album, artist: null);"),
                "the Albums tab entry point must not narrow by artist");
        });
    }

    /// <summary>
    /// The back button's word follows where the page was opened from.
    ///
    /// <para>A button that says "back to music" and lands on one artist's albums is a
    /// wrong statement, not a shorthand. Its name and tooltip are written in the same
    /// two places as its text, and naming it a second time further down the method would
    /// let the second write undo the first - this file has paid for that before, which is
    /// why the assertion pins BOTH the branch and the absence of a second writer.</para>
    /// </summary>
    [Test]
    public void TheAlbumBackButtonNamesTheArtistItReturnsTo()
    {
        var music = StrippedPageSource(PageMusicCodePath);
        var chrome = Squashed(MethodBody(music, "private async void ApplyAlbumChrome()"));

        Assert.Multiple(() =>
        {
            // All three writers carry the same branch. The text alone is not enough:
            // a name or tooltip left unconditional would tell a screen reader and a
            // hover tooltip a different story from the visible label.
            Assert.That(
                CountOccurrences(chrome, "backToArtist ? lang.LibraryBackToArtist : lang.LibraryAlbumBack"),
                Is.EqualTo(3),
                "the label, the accessible name and the tooltip must all follow where the album was opened from");

            // Squashed collapses the newline between the call and its first argument,
            // so the needle carries that space.
            Assert.That(
                chrome.Split("SetName( AlbumBackButton").Length,
                Is.EqualTo(2),
                "AlbumBackButton is named exactly once: a second unconditional write would undo the branch");
        });
    }

    /// <summary>
    /// The artist page's grid captions albums, and only that grid.
    ///
    /// <para>The card shows the file name everywhere else - a previous round asked for
    /// exactly that. So the album caption has to be something the artist page asks for
    /// per card, not a mode the card decides for itself, and it has to be reapplied on
    /// every prepare or a recycled element keeps the previous album's name.</para>
    /// </summary>
    [Test]
    public void OnlyTheArtistPagesGridCaptionsAlbums()
    {
        var music = StrippedPageSource(PageMusicCodePath);
        var prepare = Squashed(MethodBody(
            music,
            "private void ArtistAlbumGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)"));

        Assert.Multiple(() =>
        {
            Assert.That(
                prepare,
                Does.Contain("card.SetAlbumCaption(album.AlbumTitle, album.AlbumArtistText)"),
                "the artist page album grid must caption the album, not the file name");

            // The presence assertion comes FIRST: IndexOf returns -1 for a deleted call,
            // and -1 is LessThan any positive index, so an ordering test alone would be
            // green with SetEntry gone entirely.
            Assert.That(prepare, Does.Contain("card.SetEntry(album"),
                "the card must still be handed its entry, or nothing renders");

            Assert.That(
                prepare.IndexOf("card.SetEntry(album", StringComparison.Ordinal),
                Is.LessThan(prepare.IndexOf("card.SetAlbumCaption(", StringComparison.Ordinal)),
                "SetEntry draws the file-name caption; the album caption has to come after it or it is overwritten");

            // The caption is now shared by BOTH album grids - the artist page's and the
            // Albums tab's - so the old "no other surface may ask for it" assertion is
            // exactly backwards. What still has to hold is that the card grid asks for it
            // AFTER SetEntry, which is asserted above for the artist page; here the same
            // ordering is pinned for the shared grid.
            Assert.That(
                StrippedPageSource(PageCardsCodePath),
                Does.Contain("card.SetEntry(entry").And.Contain("card.SetAlbumCaption("),
                "the shared grid paints the entry first, then overwrites the caption with the album's");
        });
    }

    /// <summary>
    /// The artist grid can actually render, and the artist layer can actually be seen.
    ///
    /// <para>Both defects these pin shipped green through every logic guard: an
    /// ItemsRepeater with no ItemTemplate realizes no cards and never raises
    /// ElementPrepared with one, and a layer declared before the opaque album-detail
    /// children is painted underneath them. Neither is reachable from a C# assertion
    /// in this project, so these read the markup and the setup method.</para>
    /// </summary>
    [Test]
    public void TheArtistGridHasATemplateAndSitsOverTheAlbumLayer()
    {
        var music = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void ApplyArtistChrome()"));

        var xaml = File.ReadAllText(Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml"));

        Assert.Multiple(() =>
        {
            // The KEY, not just the property write: a different template would satisfy
            // "ItemTemplate is set" while drawing something that is not the library card.
            Assert.That(
                music,
                Does.Contain("ArtistAlbumGrid.ItemTemplate").And.Contain("LibraryCardControlTemplate"),
                "an ItemsRepeater without a template draws nothing; with the wrong one it draws the wrong card");

            // Both operands must resolve before their order means anything: IndexOf
            // returns -1 for a missing name, and -1 on the right of GreaterThan passes
            // vacuously. Same hole as the SetEntry ordering guard.
            var artistAt = xaml.IndexOf("ArtistHost", StringComparison.Ordinal);
            var albumAt = xaml.IndexOf("AlbumBackButton", StringComparison.Ordinal);

            Assert.Multiple(() =>
            {
                Assert.That(artistAt, Is.GreaterThanOrEqualTo(0), "the artist layer is gone from the markup");
                Assert.That(albumAt, Is.GreaterThanOrEqualTo(0), "the album header is gone from the markup");

                Assert.That(
                    artistAt,
                    Is.GreaterThan(albumAt),
                    "the artist layer must come after the album-detail children: later children paint on top, and both carry an opaque background");
            });
        });
    }

    /// <summary>
    /// Back from an album opened on an artist page puts the artist page back on screen.
    ///
    /// <para>The early return rebuilds the artist contents but must also restore the
    /// host, because OpenAlbum collapsed it on the way in. Without this the host is left
    /// blank - and the shared back button keeps pointing at a page nobody can see.</para>
    /// </summary>
    [Test]
    public void BackFromAnArtistsAlbumReshowsTheArtistPage()
    {
        var close = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void CloseAlbum()"));

        Assert.That(
            close,
            Does.Contain("ArtistHost.Visibility = Visibility.Visible"),
            "the artist branch must switch its own host back on, not rely on never having switched it off");
    }

    /// <summary>
    /// The artists tab builds one tile per artist, and each tile carries one entry.
    /// <summary>
    /// The artists tab files artists under a LETTER, one tile each.
    ///
    /// <para>The first version grouped one repeater per ARTIST and looked correct in
    /// source. It laid out as a single column: a repeater draws its items across the
    /// row, so a one-item repeater is one tile on a line of its own, and every heading
    /// repeated the name already printed on the tile below it. The spec asks for a
    /// letter heading and that letter's tiles beneath it, which means the grouping key
    /// is the letter, not the artist.</para>
    ///
    /// <para>These are source assertions because the defect is a shape in the code, not
    /// a value a unit test can hold: the builder needs a live repeater to run. The
    /// library this was verified against held ONE track, so runtime verification could
    /// not have caught it either - which is exactly why the shape is pinned here.</para>
    /// </summary>
    [Test]
    public void ArtistsAreFiledUnderLettersWithOneCarrierEach()
    {
        var body = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void BuildArtistTiles(List<LibraryEntry> entries)"));

        Assert.Multiple(() =>
        {
            // Grouped by the LETTER the name starts with. The rail and the headings have
            // to agree, so both go through LibraryIndexLetter.
            Assert.That(body, Does.Contain("LibraryGroupKey.Letter"), 
                "artists are filed by the shared grouper, whose letter key is what LibraryIndexLetter answers");

            // One carrier per artist survives the regrouping: the artists are collapsed
            // to a single entry each BEFORE the letter grouping runs.
            Assert.That(body, Does.Contain("GroupBy")
                .And.Contain("OrderBy(e => e.Title")
                .And.Contain(".First()"),
                "each artist must contribute exactly one carrier, or the wall of track cards returns");

            Assert.That(body, Does.Not.Contain("group.ToList(),"),
                "handing a whole group to a repeater is the wall this tab replaces");

            // The heading is built as TEXT in code now, not through a DataTemplate.
            //
            // A template could only reach a Button, and a Button draws a box: a Rest fill, a
            // hover fill and a focus ring around a single letter. The heading is a label, so
            // it is a TextBlock in a Border, and what it does under the pointer is grow.
            Assert.That(body, Does.Contain("var headerLabel = new TextBlock"),
                "the heading is text, so no control template can put a box around it");

            Assert.That(body, Does.Contain("MpvAccentHeadingBrush"),
                "the heading is the accent, not secondary ink");

            Assert.That(body, Does.Contain("ScaleHeader"),
                "the only pointer feedback is the text growing: no fill, no ring, no box");
        });
    }

    /// <summary>
    /// The two builders are routed by tab, and each tab gets its own.
    ///
    /// <para>Separate from the shape test because this one fails when the DISPATCH is
    /// wrong while both builders are individually fine - the case where the Albums tab
    /// silently starts showing artist tiles.</para>
    /// </summary>
    [Test]
    public void TheGroupedTabsDispatchToTheirOwnBuilders()
    {
        var filter = Squashed(MethodBody(
            StrippedPageSource(PageLibraryCodePath),
            "private void ApplyLibraryFilter()"));

        var albums = filter.IndexOf("BuildMusicTagGroups(cards)", StringComparison.Ordinal);
        var artists = filter.IndexOf("BuildArtistTiles(cards)", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(albums, Is.GreaterThanOrEqualTo(0), "the album builder is no longer called");
            Assert.That(artists, Is.GreaterThanOrEqualTo(0), "the artist builder is no longer called");

            // Both are called, and from DIFFERENT branches: the Albums test comes first
            // and its else-arm holds the artist builder.
            Assert.That(filter, Does.Contain("if (_musicGrouping == MusicGrouping.Artists)"),
                "the dispatch must test for Artists explicitly: Songs and Albums share one builder, Artists does not");
        });
    }

    /// <summary>
    /// A tile on the artists tab is marked as one, before its entry is handed over.
    ///
    /// <para>Order is the whole point: IsArtist decides the cover branch and the clip,
    /// and SetEntry paints from whatever it says at that moment. Set after, and the
    /// tile keeps the file-name caption and a square clip.</para>
    /// </summary>
    [Test]
    public void TheArtistTileIsMarkedBeforeItsEntryIsSet()
    {
        var prepare = Squashed(MethodBody(
            StrippedPageSource(PageCardsCodePath),
            "private void LibraryGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)"));

        Assert.Multiple(() =>
        {
            Assert.That(prepare, Does.Contain("card.IsArtist = isArtistTile"),
                "the tile identity is a card flag, never an entry flag: the same entries feed the songs grid");

            Assert.That(prepare, Does.Contain("card.SetEntry("),
                "presence first, or -1 makes the ordering assertion vacuous");

            Assert.That(
                prepare.IndexOf("card.IsArtist", StringComparison.Ordinal),
                Is.LessThan(prepare.IndexOf("card.SetEntry(", StringComparison.Ordinal)),
                "IsArtist must be set before SetEntry paints the cover and the caption");
        });
    }

    /// <summary>
    /// Clicking a tile opens the artist page, and does so before the album branch.
    ///
    /// <para>A tile is square, so IsAlbum is true for it too. Reaching the album branch
    /// first would open the carrier track album instead of the person.</para>
    /// </summary>
    [Test]
    public void ClickingATileOpensTheArtistBeforeTheAlbumBranch()
    {
        var activation = Squashed(MethodBody(
            StrippedPageSource(PageCardsCodePath),
            "private void HandleCardActivation(LibraryEntry entry)"));

        Assert.Multiple(() =>
        {
            Assert.That(activation, Does.Contain("OpenArtist(entry.Artist)"),
                "nothing else calls OpenArtist, so without this the artist page stays unreachable");

            Assert.That(activation, Does.Contain("OpenAlbum("),
                "presence first, or -1 makes the ordering assertion vacuous");

            Assert.That(
                activation.IndexOf("OpenArtist(entry.Artist)", StringComparison.Ordinal),
                Is.LessThan(activation.IndexOf("OpenAlbum(", StringComparison.Ordinal)),
                "the artist branch must come before the album branch: a tile is square too");
        });
    }

    /// <summary>
    /// The dropdown vocabulary is taken from before the genre filter runs.
    ///
    /// <para>Read after the filter, the genre the user just picked would vanish from
    /// the menu on the next rebuild: the filter removes every track that is not that
    /// genre, so that genre stops appearing among the tracks, so it stops being offered -
    /// while the button still names it. The menu would show nothing ticked.</para>
    /// </summary>

    /// <summary>
    /// A genre match keeps the whole album, and the album grid follows the sort.
    ///
    /// <para>Two behaviours that only exist together: the trim alone would leave the
    /// album in the sort's own order, and the reverse alone would reorder a list that
    /// still showed only the matching tracks. Both are source assertions because they
    /// live in the page's filter, which needs a live repeater to run.</para>
    /// </summary>
    [Test]
    public void AGenreMatchKeepsTheWholeAlbumAndTheGridFollowsTheSort()
    {
        var filter = Squashed(MethodBody(
            StrippedPageSource(PageLibraryCodePath),
            "private void ApplyLibraryFilter()"));

        var albums = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void BuildMusicTagGroups(List<LibraryEntry> entries)"));

        Assert.Multiple(() =>
        {
            Assert.That(filter, Does.Contain("MusicGenreFilter.KeptAlbumKeys"),
                "a matching track must pull in its album mates, or the heading and the album page disagree");

            // Pins the SOURCE LIST, not merely that the snapshot exists: reading the
            // survivors out of the already-filtered list would keep nothing, and that
            // variant still contains the words "allTracks" elsewhere.
            Assert.That(filter, Does.Contain("cards = allTracks").And.Contain("beforeGenre is { } allTracks"),
                "the survivors come from the pre-genre snapshot; trimming within the filtered list keeps nothing");

            Assert.That(albums, Does.Contain("descending: EffectiveMusicSort == VideoSortMode.NameDesc"),
                "Z-A is decided by the grouper, which reverses the groups and not the items inside them");

            Assert.That(albums, Does.Contain("EffectiveMusicSort == VideoSortMode.NameDesc"),
                "the album grid's order must come from the effective sort, not a literal");

            Assert.That(albums, Does.Not.Contain("ThenByDescending"),
                "ThenByDescending is the wrong shape: the letters would stay A-Z");
        });
    }
    /// <summary>
    /// The vocabulary is the whole music library, taken BEFORE the search.
    ///
    /// <para>Spec 8.3.11 names both halves: before the search, and before the genre
    /// filter. Taking the search RESULTS is the failure the clause exists to prevent -
    /// the menu would shrink to the current query's genres, so the genre being filtered
    /// on could vanish while the button still named it. Taking the post-genre list has
    /// the same effect one layer down. So the source is pinned, not just the order of
    /// two calls: the first version of this test asserted only "before the genre
    /// filter", which the code satisfied while violating the half that matters.</para>
    /// </summary>
    /// <summary>
    /// The menu is rebuilt when it OPENS, not once when the page is built.
    ///
    /// <para>This is the defect that shipped: the vocabulary is filled by the library
    /// load, which finishes long after construction, so a menu built at startup held
    /// only the two fixed entries for the whole session. The genres reached the field
    /// and never the flyout. Nothing in this suite looked at the menu at all, and the
    /// running app looked plausible because "All genres / Unknown genre" is exactly
    /// what a one-track library would show.</para>
    ///
    /// <para>Pinned as a wiring fact - the flyout names an Opening handler, and that
    /// handler rebuilds - because the alternative (asserting the item list) needs a
    /// live MenuFlyout and would test the framework.</para>
    /// </summary>
    [Test]
    public void TheGenreMenuIsRebuiltEveryTimeItOpens()
    {
        var xaml = File.ReadAllText(Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml"));

        var music = Squashed(StrippedPageSource(PageMusicCodePath));

        var opening = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void MusicGenre_Opening(object sender, object e)"));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Contain("Opening=\"MusicGenre_Opening\""),
                "the flyout must name an Opening handler, or the menu is built once and never sees the vocabulary");

            Assert.That(opening, Does.Contain("ApplyGenreStrings()"),
                "the Opening handler must actually rebuild the items");

            // And the builder must add the vocabulary entries, not just the two fixed ones.
            Assert.That(music, Does.Contain("foreach (var name in _musicGenreVocabulary)"),
                "the builder must walk the vocabulary, or the menu can only ever hold All and Unknown");
        });
    }
    [Test]
    public void TheGenreVocabularyIsTakenBeforeTheSearchAndTheGenreFilter()
    {
        var filter = Squashed(MethodBody(
            StrippedPageSource(PageLibraryCodePath),
            "private void ApplyLibraryFilter()"));

        var vocabulary = filter.IndexOf("MusicGenreFilter.Vocabulary", StringComparison.Ordinal);
        var matches = filter.IndexOf("MusicGenreFilter.Matches", StringComparison.Ordinal);
        var search = filter.IndexOf("var needle = _libraryFilter.Trim()", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary, Is.GreaterThanOrEqualTo(0), "the vocabulary is never built");
            Assert.That(matches, Is.GreaterThanOrEqualTo(0), "the genre filter is never applied");
            Assert.That(search, Is.GreaterThanOrEqualTo(0), "the search block is gone");

            // The pre-search snapshot must exist, and the vocabulary must read it.
            Assert.That(filter, Does.Contain("var beforeSearch = "),
                "the pre-search snapshot is gone; without it the vocabulary can only be the search results");

            Assert.That(filter, Does.Contain("MusicGenreFilter.Vocabulary( (beforeSearch ?? beforeGenre).Select"),
                "the vocabulary must read the pre-search set, not the search results and not the genre-filtered list");

            Assert.That(
                vocabulary,
                Is.LessThan(matches),
                "vocabulary first, or the chosen genre removes itself from the menu it was chosen from");
        });
    }

    /// <summary>
    /// The artists tab is exempt, and the button rides the row it already lives in.
    ///
    /// <para>Two separate guards because they fail differently. Without the exemption,
    /// carrying a genre onto the artists tab halves the tiles with nothing on screen to
    /// explain it. With a second tab check on the button, there are two sources of truth
    /// for one answer - the shape this repository has been bitten by three times.</para>
    /// </summary>
    [Test]
    public void GenreSkipsTheArtistsTabAndTheButtonDoesNotCheckTheTabItself()
    {
        var filter = Squashed(MethodBody(
            StrippedPageSource(PageLibraryCodePath),
            "private void ApplyLibraryFilter()"));

        var xaml = File.ReadAllText(Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml"));

        Assert.Multiple(() =>
        {
            Assert.That(filter, Does.Contain("_musicGrouping != MusicGrouping.Artists"),
                "the genre layer must name the artists tab as exempt");

            Assert.That(
                xaml.IndexOf("MusicGenreButton", StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0),
                "the button is gone from the markup");

            // POSITIVE, and over the file that really holds the method. The first
            // version of this assertion was a negative over MpvPlayerPage_Library.cs
            // - a file that cannot contain the symbol at all - so it was true by
            // construction AND it forbade the fix for the button standing on the
            // video library. The button belongs to the same swap as Play-all and
            // Shuffle, because this row is shared by both libraries.
            Assert.That(
                Squashed(MethodBody(StrippedPageSource(PageLibraryCodePath), "private void ApplyVideoChromeVisibility()")),
                Does.Contain("MusicGenreButton.Visibility = isMusic"),
                "the genre button is music-only, so it must follow the same section swap as Play-all and Shuffle");
        });
    }

    /// <summary>
    /// Closing the host drops every piece of the artist state.
    ///
    /// <para>Three pieces, and a stale one is invisible until it matters: the tuple, the
    /// bool the tuple cannot provide, and the artist. Leaving the artist set would make
    /// the next album opened from the Albums tab hand back to an artist page that is not
    /// on screen.</para>
    /// </summary>
    [Test]
    public void TheHostResetsTheAlbumAndArtistStateTogether()
    {
        var library = Squashed(MethodBody(
            StrippedPageSource(PageLibraryCodePath),
            "private void HideSurfacesOverLibrary()"));

        Assert.Multiple(() =>
        {
            Assert.That(library, Does.Contain("_openAlbum = default;"));
            Assert.That(library, Does.Contain("_albumOpened = false;"));
            Assert.That(library, Does.Contain("_openArtist = null;"));
            Assert.That(library, Does.Contain("ArtistHost.Visibility = Visibility.Collapsed;"));
        });
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
    /// A page source file with its comments stripped, plus the proof that stripping
    /// happened.
    ///
    /// <para>Same contract as <see cref="StrippedCardCode"/>, with the file as an
    /// argument because three page sources are read now. The proof is the same length
    /// comparison: if nothing was removed, the assertions built on the result are
    /// searching prose.</para>
    /// </summary>
    private static string StrippedPageSource(string path)
    {
        var raw = File.ReadAllText(path);
        var stripped = WithoutComments(raw);

        Assert.That(
            stripped.Length,
            Is.LessThan(raw.Length),
            $"{Path.GetFileName(path)} carries no comments, so stripping removed nothing and the "
            + "assertions built on this text would be reading prose rather than code");

        return stripped;
    }

    /// <summary>
    /// The expression-bodied member whose declaration is
    /// <paramref name="signature"/>, from the signature through its terminating
    /// semicolon.
    ///
    /// <para><see cref="MethodBody"/> needs braces, and the two music-sort properties
    /// are expression-bodied: searching for a brace after them would walk into the
    /// NEXT member's body and assert against a different piece of code. Strict for
    /// the same reason as MethodBody - a helper that answered "" would fail the
    /// assertion below for a reason that has nothing to do with the code under
    /// test.</para>
    /// </summary>
    private static string PropertyExpression(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);

        Assert.That(
            at,
            Is.GreaterThanOrEqualTo(0),
            $"{signature} is gone from the page - renamed, or the member it names was removed");

        var end = source.IndexOf(';', at + signature.Length);
        Assert.That(end, Is.GreaterThan(at), $"{signature} is never terminated");

        return source[at..(end + 1)];
    }

    /// <summary>
    /// The input with every run of whitespace collapsed to one space.
    ///
    /// <para>For asserting an EXPRESSION whose line breaks belong to its author, not
    /// to the rule: a member is wrapped one way today and another after any reformat,
    /// and a guard that pinned one wrapping would fail on a change that altered
    /// nothing. Names, operators and the order of the operands still have to match
    /// exactly.</para>
    /// </summary>
    private static string Squashed(string text)
        => Regex.Replace(text, @"\s+", " ").Trim();

    /// <summary>
    /// The C# with every <c>//</c> and <c>/* */</c> comment removed.
    ///
    /// <para>Not tidiness. The card's prose names <c>_isArtist</c>,
    /// <c>PlayButton</c> and <c>LibraryUnknownArtist</c> deliberately - the branches
    /// being guarded explain themselves there - so a search over the raw source
    /// passes after the code is deleted, which is the one failure these tests exist
    /// to catch.</para>
    ///
    /// <para>The stripping is checked at its call site rather than here:
    /// <see cref="StrippedCardCode"/> asserts the result is SHORTER than the file it
    /// came from. The check is deliberately not "the result contains no
    /// <c>//</c>" - a <c>//</c> inside a string or char literal is code, this method
    /// copies such literals through untouched on purpose (see below), and the
    /// assertion would fail on correct source the first time the control holds a
    /// URL.</para>
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
    /// The page's video-library half: <c>_videoSort</c>, the music sort beside it, and
    /// the sort menu's own handler and chrome.
    /// </summary>
    private static string PageVideoCodePath
        => Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.Library.Video.cs");

    /// <summary>The page's library half, where the toolbar row's predicate is decided.</summary>
    private static string PageLibraryCodePath
        => Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage_Library.cs");
    /// <summary>The page card half: the grid every library surface shares.</summary>
    private static string PageCardsCodePath
        => Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.Library.Cards.cs");


    /// <summary>The page's music half, where the three music tabs are switched.</summary>
    private static string PageMusicCodePath
        => Locate(@"src\mpvw-app\Domains\Playback\MpvPlayerPage.Library.Music.cs");

    /// <summary>How many times a needle occurs in a haystack, without allocating a split.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;

        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }
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

    [Test]
    public void ThePickerIsSizedBeforeItShowsAndOnlyALetterClosesIt()
    {
        var body = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void GroupHeader_Tapped(object sender, TappedRoutedEventArgs e)"));

        var sheet = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void PlaySheetAnimation(float from, float to, float fromOpacity, float toOpacity)"));

        Assert.Multiple(() =>
        {
            // The sheet must be at its STARTING size before Visibility flips, or it paints one
            // frame at full size and then jumps small - the twitch the user reported as 抽搐.
            var reset = body.IndexOf("ResetSheetTransform", StringComparison.Ordinal);
            var show = body.IndexOf("Visibility.Visible", StringComparison.Ordinal);
            Assert.That(reset, Is.GreaterThanOrEqualTo(0), "the sheet is sized by ResetSheetTransform");
            Assert.That(reset, Is.LessThan(show),
                "sized BEFORE it is shown: the reverse order paints one full-size frame first");

            // The trigger sequence that is now forbidden: a tap anywhere closing the sheet.
            Assert.That(body, Does.Not.Contain("Dismiss_Tapped"),
                "a stray tap must not throw the reader back to the list");

            // A click on a letter is still the real exit, and it navigates first.
            Assert.That(sheet, Does.Contain("LibraryScroll.Opacity"),
                "the list is restored by the same timer that grows the sheet");
        });
    }

    [Test]
    public void EveryLetterLightsUpUnderThePointerUnlessItIsEmpty()
    {
        var body = Squashed(MethodBody(
            StrippedPageSource(PageMusicCodePath),
            "private void FillGroupPicker()"));

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("PointerEntered"),
                "a letter has to render something under the pointer; the bare template has no states");

            Assert.That(body, Does.Contain("hoverFill"),
                "the hover paints a fill the template can show");

            // A dimmed bucket cannot be clicked, so it must not promise a jump it cannot make.
            Assert.That(body, Does.Contain("IsEnabled"),
                "the hover is guarded by IsEnabled so dead letters stay inert");
        });
    }

    /// <summary>
    /// This file's absolute path, as the compiler saw it. Used by
    /// <see cref="Locate"/> to find the checkout when the test host runs outside
    /// it. Never called directly.
    /// </summary>
    private static string ThisFilePath([CallerFilePath] string path = "") => path;
}