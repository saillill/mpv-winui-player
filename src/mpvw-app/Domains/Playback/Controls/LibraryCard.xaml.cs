using System;
using System.ComponentModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Domains.Playback.Cards;
using mpv_winui.Infrastructure.Motion;

namespace mpv_winui.Domains.Playback.Controls
{
    /// <summary>
    /// One library card. The parts are fields, so a part that stops existing is a
    /// compile error rather than a card that silently never draws its cover.
    ///
    /// This replaces two DataTemplates that were driven from outside by name:
    /// ElementPrepared called FindDescendant(card, "AlbumCardCoverImage") and
    /// friends, because a DataTemplate instance gives its code-behind no handle
    /// on its own children. A name search has no link to the markup, so renaming
    /// an element in XAML broke the cover with no error anywhere.
    ///
    /// Three things stay in code rather than becoming bindings, each measured:
    ///   * The cover - {x:Bind Thumbnail} compiles and paints nothing.
    ///   * The square - a self-referential {Binding ActualWidth} collapses the
    ///     tile to zero height in this layout.
    ///   * The two captions - these COULD bind, but the card is handed its entry
    ///     on every recycle and on every language change, so assigning is one
    ///     notification fewer and one mechanism simpler.
    /// </summary>
    public sealed partial class LibraryCard : UserControl
    {
        /// <summary>
        /// The entry this card is showing. Null only before the first SetEntry.
        /// </summary>
        public LibraryEntry? Entry { get; private set; }

        /// <summary>
        /// Whether this card stands for an album (square) or a video file (16:9).
        ///
        /// Set before <see cref="SetEntry"/>: it changes the tile's aspect, so it
        /// has to be right when the first layout pass computes the height.
        /// </summary>
        public bool IsAlbum
        {
            get => _isAlbum;
            set
            {
                if (_isAlbum == value)
                {
                    return;
                }

                _isAlbum = value;

                ApplyCoverHeight();

                // The tick box no longer depends on this - every card gets one, so a
                // recycled card cannot arrive with the wrong answer either way. Kept
                // as a no-op-free reminder: if the box ever becomes conditional
                // again, this is the line that has to come back.
                UpdateSelectionBoxForState();
            }
        }

        private bool _isAlbum;

        /// <summary>
        /// Whether this card stands for an ARTIST rather than for a file.
        ///
        /// <para>An artist tile is a person, not a track: it draws a circle, shows
        /// a person glyph instead of a cover, and offers nothing to play, overflow
        /// or tick. Set before <see cref="SetEntry"/> for the same reason
        /// <see cref="IsAlbum"/> is - it changes the tile's geometry - and the
        /// page's order is <c>IsArtist</c>, <c>IsAlbum</c>, <c>SetEntry</c>.</para>
        /// </summary>
        public bool IsArtist
        {
            get => _isArtist;
            set
            {
                if (_isArtist == value)
                {
                    return;
                }

                _isArtist = value;

                // 只重算几何与裁剪。封面、人像、标题、副标题都**不在这里重画**：页面按
                // `card.IsArtist = ...; card.IsAlbum = ...; card.SetEntry(...)` 的顺序赋值，
                // SetEntry 会用新身份把这几样全部画一遍；在这里再画一次会用一个卡片自己不可能
                // 知道的 cover 值（传 null）覆盖掉刚解出来的封面。
                //
                // 唯一的另一条路径是"同一张卡不换 entry 只换身份"，它不存在：艺术家瓦片与曲目卡
                // 从不同的 repeater 里来，换 repeater 必然是一次新的 SetEntry。
                ApplyCoverHeight();
            }
        }

        private bool _isArtist;

        /// <summary>
        /// Whether the pointer is on this card right now.
        ///
        /// A field rather than a read of some visual's state, because the frame
        /// has two callers and one of them (IsSelected) cannot see the pointer.
        /// It is also the half of the answer that has to be reset on every
        /// recycle: the pointer lives on the element, the tick on the entry, and
        /// a card prepared while the pointer is elsewhere would otherwise keep
        /// the previous file's "the pointer is here".
        /// </summary>
        private bool _pointerInside;

        /// <summary>Raised when the card body is clicked.</summary>
        public event EventHandler? CardTapped;

        /// <summary>Raised when the hover layer's play button is clicked.</summary>
        public event EventHandler? PlayRequested;

        /// <summary>Raised when the hover layer's overflow button is clicked.</summary>
        public event EventHandler? MoreRequested;

        /// <summary>
        /// Raised when the tick box is clicked. The page owns the mode.
        ///
        /// An event rather than a direct write to <see cref="LibraryEntry.IsSelected"/>:
        /// the tick lives on the entry, but the MODE lives on the page - it is the
        /// count of ticks, pushed back down to every card (UpdateSelectionBar). A card
        /// that flipped its own entry would tick the right file and leave the bar and
        /// every other card's box behind.
        ///
        /// This is the way into selection mode. Before it existed the only entry was
        /// the card's context menu, which is not somewhere a person looks.
        /// </summary>
        public event EventHandler? SelectionRequested;

        public LibraryCard()
        {
            InitializeComponent();

            MediaBorder.SizeChanged += (_, _) => ApplyCoverHeight();

            RootButton.PointerEntered += (_, _) => ShowOverlay();
            RootButton.PointerExited += (_, _) => HideOverlay();
            RootButton.PointerCanceled += (_, _) => HideOverlay();
        }

        /// <summary>
        /// Points this card at an entry and repaints every part from it.
        ///
        /// One entry point rather than a property per part: the card is recycled,
        /// so whatever the previous item left behind has to be replaced wholesale.
        /// Half-updating a recycled card is how a tick box or a now-playing
        /// stripe ends up on the wrong file.
        ///
        /// <paramref name="cover"/> is passed in rather than read from the entry
        /// because the page owns the decode cache, and the cache has to be
        /// consulted BEFORE the entry - a recycled card is prepared before the
        /// item it now points at has had its Thumbnail filled, so reading only
        /// the entry loses a picture that is already decoded.
        /// </summary>
        public void SetEntry(LibraryEntry? entry, ImageSource? cover)
        {
            if (Entry is { } previous)
            {
                previous.PropertyChanged -= OnEntryPropertyChanged;
            }

            Entry = entry;

            // The overlay is code-driven state the template cannot reset by
            // itself: a card prepared while the pointer is elsewhere must not
            // arrive already wearing another item's hover. The frame is part of
            // that reset but is not simply collapsed - ResetOverlay recomputes it
            // from the entry just assigned, so a recycled card arrives framed
            // exactly when the file it now stands for is ticked.
            ResetOverlay();

            if (entry is null)
            {
                TitleText.Text = string.Empty;
                SubtitleText.Text = string.Empty;
                SubtitleText.Visibility = Visibility.Collapsed;
                return;
            }

            // DataContext is what the click handlers read back, and it is also
            // what the shared context menu's enable/disable pass looks at.
            DataContext = entry;

            ApplyCovers(entry, cover);
            ApplyCaptions(entry);
            ApplyNowPlaying(entry);
            ApplySelection(entry);
            ApplyAccessibleNames();

            entry.PropertyChanged += OnEntryPropertyChanged;
        }

        /// <summary>
        /// Refreshes the parts that can change after the card is on screen.
        ///
        /// Only four properties are watched. Everything else on an entry is
        /// immutable once built (it is a record of a file), so watching them
        /// would be a subscription per card per property for no notification.
        /// </summary>
        private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Entry is not { } entry)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(LibraryEntry.Thumbnail):
                    ApplyCovers(entry, entry.Thumbnail);
                    break;
                case nameof(LibraryEntry.IsNowPlaying):
                    ApplyNowPlaying(entry);
                    break;

                // IsSelected is watched because it is now one of the two things
                // that decide the caption colour, and one of the two things that
                // decide the frame. Without this, ticking a card in selection
                // mode showed the tick box but left the text white, because the
                // caption repaint only ran on IsNowPlaying.
                case nameof(LibraryEntry.IsSelected):
                    ApplyCaptionEmphasis(entry);
                    UpdateFrameForState();
                    break;
                case nameof(LibraryEntry.SelectionVisibility):
                case nameof(LibraryEntry.SelectionGlyph):
                    ApplySelection(entry);
                    break;
            }
        }

        private void ApplyCovers(LibraryEntry entry, ImageSource? cover)
        {
            // An artist tile draws no cover at all. This is a complete path of its
            // own rather than an early return bolted onto the file path: an artist
            // tile has exactly one thing to show, so there is nothing here to fall
            // through to and nothing below to keep in sync with it.
            //
            // The glyph is unconditional, and deliberately not conditional on
            // entry.IsMissing the way the media-kind fallback is: a tile stands for
            // a PERSON, not for the song that happened to be tagged with them, so
            // it is not the file that decides whether there is anything to draw.
            if (_isArtist)
            {
                CoverImage.Source = null;
                CoverImage.Visibility = Visibility.Collapsed;
                FallbackIcon.Visibility = Visibility.Collapsed;
                ArtistGlyph.Visibility = Visibility.Visible;

                return;
            }

            // The other identity path. Written here rather than only in the branch
            // above, because a card RECYCLED from an artist tile back onto a file
            // would otherwise still be wearing the person.
            ArtistGlyph.Visibility = Visibility.Collapsed;

            // The passed-in cover wins, then the entry's own. Both are checked
            // because a late thumbnail arrives on the entry while the page's
            // cache can already hold one for the same path.
            var image = cover ?? entry.Thumbnail;

            if (image is not null)
            {
                CoverImage.Source = image;
                CoverImage.Visibility = Visibility.Visible;
                FallbackIcon.Visibility = Visibility.Collapsed;

                return;
            }

            CoverImage.Source = null;
            CoverImage.Visibility = Visibility.Collapsed;

            // A missing file must not look playable, so its glyph is hidden even
            // though there is no cover to put in its place.
            FallbackIcon.Visibility = entry.IsMissing ? Visibility.Collapsed : Visibility.Visible;

            // A fresh Geometry per assignment: a Geometry belongs to a single
            // PathIcon for its lifetime, and a recycled card would throw on the
            // second one. See LibraryEntry.MediaIconPath.
            FallbackGlyph.Data = entry.MediaIcon;
        }

        private void ApplyCaptions(LibraryEntry entry)
        {
            // An artist tile's title is the PERSON, not a file name: the tile stands
            // for a person, while the name below a file card is the name of one song.
            //
            // LibraryUnknownArtist is the placeholder AppLang already carries in all
            // nine language files - an empty tag is not an error here, it just means
            // there is no name to print.
            if (_isArtist)
            {
                TitleText.Text = string.IsNullOrWhiteSpace(entry.Artist)
                    ? AppContext.AppLang.LibraryUnknownArtist
                    : entry.Artist;

                // One line, no second one: under a person's tile there is only a
                // name.
                SubtitleText.Text = string.Empty;
                SubtitleText.Visibility = Visibility.Collapsed;

                return;
            }

            // Both kinds of card show one line, and that line is the plain FILE
            // NAME - extension included.
            //
            // Reported as "drop the date string and the repeated title, keep the
            // top line, and make it the normal file name; audio cards the same,
            // no '未知专辑' / '未知艺术家' placeholders". Three things were wrong
            // before and each came from a different place:
            //
            //   * The video line was media-title, which for a library scan is
            //     whatever mpv read out of the file - hence the
            //     "Crossfire HD 2025.09.17 - 16.09.03.01" date string sitting
            //     above a second line repeating the folder.
            //   * The album card titled itself AlbumTitle, which falls back to
            //     the LibraryUnknownAlbum placeholder for an untagged file, and
            //     its second line then said LibraryUnknownArtist.
            //   * The second line existed at all.
            //
            // Path.GetFileName is used rather than the entry's Title because the
            // entry's Title is exactly the value that was wrong: it is a record of
            // what the player displayed, not of what the file is called. The
            // extension stays on - the report asked for the file name, and for a
            // folder of camera exports the extension is half of what tells two
            // clips apart.
            var fileName = FileNameOf(entry.Path);

            TitleText.Text = string.IsNullOrEmpty(fileName) ? entry.Title : fileName;

            // The length, on the second line. Assigned from the entry's own
            // DurationText rather than formatted here: that property already owns
            // the "1:23:45 / 4:05 / empty when unknown" rule, and a second copy of
            // it is how a card and the queue's row come to print the same duration
            // two ways.
            //
            // Collapsed when there is no length - the watch history records none,
            // so most video cards have nothing to say on this line. A visible empty
            // TextBlock would reserve the line's height and leave the hole the
            // report complained about in the other direction.
            SubtitleText.Text = entry.DurationText;
            SubtitleText.Visibility = entry.DurationVisibility;
        }

        /// <summary>
        /// Replaces the caption with an album's own name and artist.
        ///
        /// <para><b>Why this is a method rather than a card mode.</b> Only one grid wants
        /// it - the artist page's, which lists albums. The Songs and Albums tabs show the
        /// file name, and a previous round asked for exactly that (「就是普通文件名，不要
        /// 未知专辑/未知艺术家这种占位」), so this must not become something the card
        /// decides for itself. The page that wants the other caption says so, per card,
        /// after <see cref="SetEntry"/> has drawn the normal one.</para>
        ///
        /// <para>Called on every prepare, which is what keeps a recycled element from
        /// wearing the previous album's name: preparation always runs SetEntry first and
        /// this second, so the pair is rewritten together.</para>
        /// </summary>
        public void SetAlbumCaption(string album, string artist)
        {
            TitleText.Text = album;

            SubtitleText.Text = artist;

            // The second line is normally the length, and normally hidden when there is
            // none. An album always has an artist line to show, even when that line is
            // the localized "unknown artist", so it is forced visible here.
            SubtitleText.Visibility = Visibility.Visible;

            // Derived from the text just written rather than re-stating `album`, so the
            // button's name and the caption cannot disagree. SetEntry names it the same
            // way from the same source - one rule, two callers.
            AutomationProperties.SetName(RootButton, TitleText.Text);
        }

        /// <summary>
        /// The file name of a path, or empty when the path is unusable.
        ///
        /// Swallows the ArgumentException Path.GetFileName throws on a string
        /// with illegal characters: a malformed history entry must cost a caption,
        /// not the whole library pass.
        /// </summary>
        private static string FileNameOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The caption colour and the now-playing stripe.
        ///
        /// <para><b>Colour is the only thing that varies, and only by state.</b>
        /// The two caption lines carry one typographic style - same size, same
        /// weight, same line box - and the card switches between exactly two
        /// colours: white for an ordinary card, the accent for one that is
        /// playing or ticked in selection mode. Nothing about the type changes
        /// between the two, so a row does not re-flow when it becomes the playing
        /// one, and there is no second, smaller line under it to read as a
        /// caption.</para>
        ///
        /// <para>The tint is cleared by assigning null rather than by restoring
        /// the theme brush explicitly: null is what the element had before, and it
        /// keeps following the theme instead of pinning whatever brush was current
        /// when the card happened to be recycled.</para>
        /// </summary>
        private void ApplyNowPlaying(LibraryEntry entry)
        {
            ApplyCaptionEmphasis(entry);
        }

        /// <summary>
        /// Picks the one colour both caption lines wear, from the entry's state.
        ///
        /// Selected and playing share a colour on purpose: the report asked for
        /// exactly one emphasised state ("when the card is selected the text turns
        /// orange"), and a playing card is the other case where the row is meant
        /// to stand out. Giving them two different colours would mean inventing a
        /// third meaning nobody asked for.
        /// </summary>
        private void ApplyCaptionEmphasis(LibraryEntry entry)
        {
            var playing = entry.IsNowPlaying;

            // An artist tile never wears the stripe, whatever the entry says: the
            // stripe marks the FILE that is sounding, and a tile is not that file -
            // it is the person the entry happens to be filed under. Written as part
            // of the visibility decision rather than as a separate collapse, so
            // there is one writer of this property and not two.
            NowPlayingBar.Visibility = playing && !_isArtist
                ? Visibility.Visible
                : Visibility.Collapsed;

            var emphasised = playing || entry.IsSelected;

            var brush = (Brush)Application.Current.Resources[
                emphasised
                    ? "AccentTextFillColorPrimaryBrush"
                    : "TextFillColorPrimaryBrush"];

            TitleText.Foreground = brush;
            SubtitleText.Foreground = brush;
        }

        private void ApplySelection(LibraryEntry entry)
        {
            SelectionGlyphIcon.Glyph = entry.SelectionGlyph;

            // The box's Visibility is NOT written here any more. It has two reasons
            // to be on - the card is in selection mode, or the pointer is on it -
            // and ApplySelection only knows the first. UpdateSelectionBoxForState
            // owns both, the same way UpdateFrameForState owns the frame's two.
            UpdateSelectionBoxForState();

            // A ticked card keeps its frame after the pointer leaves: the frame IS
            // the "this one" mark, and a tick box alone disappears under the
            // cursor. Reached from SetEntry and from a SelectionVisibility/Glyph
            // change, so it is a second chance for the frame to agree with the
            // entry rather than the only one - ResetOverlay already computes it,
            // and UpdateFrameForState is idempotent by construction.
            UpdateFrameForState();
        }

        private void ApplyAccessibleNames()
        {
            // The Button's name is the caption: UIA does not fold child TextBlock
            // text into a parent's name, so without this a card announces nothing.
            AutomationProperties.SetName(RootButton, TitleText.Text);

            var lang = AppContext.AppLang;

            AutomationProperties.SetName(PlayButton, lang.LibraryCardPlay);
            ToolTipService.SetToolTip(PlayButton, lang.LibraryCardPlay);

            AutomationProperties.SetName(MoreButton, lang.LibraryCardMore);
            ToolTipService.SetToolTip(MoreButton, lang.LibraryCardMore);

            // The tick box is a Button and a Button needs both, exactly like the two
            // discs above: UIA does not fold the glyph into a name, so without this it
            // announces "button" and nothing else. Named here rather than in markup
            // because the string follows the language, and this is the same pass that
            // names the rest of the card.
            AutomationProperties.SetName(SelectionBox, lang.LibraryCardSelect);
            ToolTipService.SetToolTip(SelectionBox, lang.LibraryCardSelect);

            // A stable automation id, because the accessible name is the same on
            // every card: a UIA query for 「选择」 matches all of them and cannot
            // choose one. The discs already took ids on the same reasoning
            // (PlaylistPlay_/PlaylistMore_ in the playlists section); this is the
            // third control that needed to be addressable by a test.
            AutomationProperties.SetAutomationId(SelectionBox, "LibraryCardSelect");

            // The two discs' pointer-over state: a darkening veil laid over the
            // whole disc, as a SIBLING of the Button rather than inside it.
            //
            // The previous attempt put a second Border in the Button's content, and
            // that is why 「描边会缩成一个小圈」: the Button sets Padding="0" and
            // ContentAlignment=Center, so its content box is the glyph, and the
            // "highlight" drew a 17px ring around the glyph instead of covering the
            // 36px disc. These Borders live in the overlay Grid at the same size and
            // place as the discs, so the ring and the glyph both survive untouched
            // and only the surface's luminance changes.
            //
            // Wired here rather than as VisualStates because a Button's own
            // PointerOver state is part of its template and would fight these, and
            // ShowHover is the whole of the behaviour.
            PlayButton.PointerEntered += (_, _) => ShowHover(PlayButtonHover, true);
            PlayButton.PointerExited += (_, _) => ShowHover(PlayButtonHover, false);
            PlayButton.PointerCanceled += (_, _) => ShowHover(PlayButtonHover, false);

            MoreButton.PointerEntered += (_, _) => ShowHover(MoreButtonHover, true);
            MoreButton.PointerExited += (_, _) => ShowHover(MoreButtonHover, false);
            MoreButton.PointerCanceled += (_, _) => ShowHover(MoreButtonHover, false);

            // The tick box's veil follows the same rule, with one extra condition:
            // it is only ever shown while the card is UNTICKED. 「选中后在悬浮时不要有
            // 变化」 - a ticked box states that the file is in the selection, and
            // brightening it under the pointer would make the tick look like a hover
            // effect rather than a decision.
            //
            // Checked at event time rather than cached, so a tick that happens while
            // the pointer is already on the box takes effect on the next move without
            // needing another handler. UpdateSelectionBoxForState also hides it when
            // the entry becomes ticked.
            SelectionBox.PointerEntered += (_, _) => ShowHover(SelectionBoxHover, Entry?.IsSelected != true);
            SelectionBox.PointerExited += (_, _) => ShowHover(SelectionBoxHover, false);
            SelectionBox.PointerCanceled += (_, _) => ShowHover(SelectionBoxHover, false);
        }

        /// <summary>
        /// Shows or hides one disc's hover veil.
        ///
        /// Visibility rather than Opacity: <c>MotionBehaviors.FadeIn</c> on an
        /// ancestor multiplies with a XAML <c>Opacity</c> value rather than
        /// replacing it, which is a trap this control already documents. A
        /// collapsed Border animates nothing and composes with nothing.
        ///
        /// It is also hidden when the pointer leaves by ANY route - including the
        /// button collapsing under it, which is what PointerCanceled covers when the
        /// two discs vanish as the pointer leaves the card - so a veil cannot be left
        /// lit on a hidden button.
        /// </summary>
        private static void ShowHover(Border veil, bool show)
        {
            veil.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Gives the media tile its height for the width it just got.
        ///
        /// Called from SizeChanged as well as from SetEntry: the grid re-flows on
        /// every window resize, and a height computed once would be right at one
        /// window width and wrong at every other.
        /// </summary>
        private void ApplyCoverHeight()
        {
            // One side of the tile, read once. The height is derived from it, and
            // so is the shape: the Border's radius and the cover's clip are both
            // computed from this number and from nothing else, so they cannot
            // describe two different tiles.
            var width = MediaBorder.ActualWidth;
            var height = CardMetrics.CoverHeightFor(width, _isAlbum);

            if (height > 0)
            {
                MediaBorder.Height = height;

                // The buttons' layer gets the tile's height, not the card's: the
                // two discs ride the picture's lower edge, not the caption's.
                // Leaving it unset let the Grid size itself to its tallest child -
                // a 36px button - so both buttons sat in a 36px strip and
                // MoreButton never reached the tile's corner.
                //
                // The hover FRAME is deliberately not sized here. It is a sibling
                // stretched over the whole card, which is the entire point: it
                // encloses the picture and the caption together, so any height
                // assignment would put it back to wrapping the picture alone.
                HoverOverlay.Height = height;

                // The tile's corner radius, from the identity: a circle for an
                // artist, CardMetrics.CornerRadius for a file.
                //
                // From the WIDTH, and the width is passed to the clip below for
                // exactly that reason. A circle is a square's rounded corner, and
                // an artist tile only LOOKS square because IsAlbum makes it one -
                // an invariant that belongs to how the page prepares cards, not to
                // this control. Deriving the radius from the height here would
                // make the Border a rounded rectangle (0.28 x width) on any artist
                // card prepared with IsAlbum false, while the clip below stayed a
                // circle, and nothing would fail. One side, read once, used twice.
                var radius = _isArtist
                    ? new CornerRadius(CardMetrics.CircleCornerRadius(width))
                    : new CornerRadius(CardMetrics.CornerRadius);

                MediaBorder.CornerRadius = radius;

                ApplyCoverClip(width);
            }
        }

        /// <summary>
        /// Rounds the cover image's corners to the tile's own radius.
        ///
        /// <para><b>A Border does not clip its children.</b> MediaBorder carries
        /// CornerRadius for its background and its hairline, but the Image inside is
        /// a plain rectangle drawn over them: at the old radius of 6 the four corners
        /// of the bitmap poked into the rounded corner by under a pixel and nobody
        /// noticed. At 12 they would be four visible square bites out of a rounded
        /// tile - which is what 「圆角还要再圆滑一点」 would produce if the radius were
        /// raised without this.</para>
        ///
        /// <para><b>A composition clip, not <c>FrameworkElement.Clip</c>.</b> Clip
        /// takes a Geometry, and WinUI's <c>RectangleGeometry</c> has no radii at all
        /// (unlike WPF's) - the first attempt set <c>RadiusX</c> on it and did not
        /// compile. The rounded shapes live in the composition layer, so the clip is
        /// built there with <c>CompositionRoundedRectangleGeometry</c>. That is the
        /// documented way to clip to a rounded rectangle in WinUI 3.</para>
        ///
        /// <para><paramref name="width"/> is passed in rather than re-read so that the
        /// radius here is the SAME number the Border's radius was built from: one
        /// width, two shapes. A circle depends on that - a clip and a Border that
        /// each pick their own side are a circle and a stadium.</para>
        /// </summary>
        private void ApplyCoverClip(double width)
        {
            var height = MediaBorder.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                // A recycled card reports 0 before its first layout pass; a clip of
                // zero size would hide the cover rather than merely round it.
                return;
            }

            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview
                .GetElementVisual(CoverImage);

            var rounded = visual.Compositor.CreateRoundedRectangleGeometry();

            rounded.Size = new System.Numerics.Vector2((float)width, (float)height);

            // Half the WIDTH for an artist tile, so the clip is the same circle the
            // Border draws.
            var r = (float)(_isArtist ? CardMetrics.CircleCornerRadius(width) : CardMetrics.CornerRadius);

            rounded.CornerRadius = new System.Numerics.Vector2(r, r);

            var clip = visual.Compositor.CreateGeometricClip();
            clip.Geometry = rounded;

            visual.Clip = clip;
        }

        private void ShowOverlay()
        {
            // Two layers, two jobs, shown together.
            //
            // HoverFrame wraps the whole card - picture and caption - and is what
            // the report called "the outermost hover frame". HoverOverlay holds
            // the two corner buttons and is pinned to the tile, because the buttons
            // belong on the picture's lower corners rather than on the card's.
            //
            // The frame is toggled by Visibility alone, with no MotionBehaviors
            // fade, and that is measured rather than tidy: FadeIn animates the
            // compositor visual's opacity, and a Border with no Background and no
            // visual content does not get one composed for it, so the animation ran
            // against a visual that never drew. The frame reported Visible and put
            // no white pixels on screen - sampled across the card's edge, the only
            // stroke present was the card background's own colour. A hard on/off is
            // also correct here: the frame is a boundary, and fading a boundary in
            // reads as the card resizing.
            //
            // The pointer is one of TWO reasons the frame can be on, so it is
            // recorded and the decision is left to UpdateFrameForState: writing
            // Visibility here as well is how two writers of one property start
            // disagreeing, which for a recycled card means another item's frame.
            _pointerInside = true;

            UpdateFrameForState();

            // The tick box is the way into selection mode, so a card with nothing
            // selected has to show it while the pointer is on it - otherwise the
            // affordance exists only for people who already found the context
            // menu. Same pointer flag as the frame, so the two cannot disagree
            // about whether the pointer is here.
            UpdateSelectionBoxForState();

            // The overlay itself still shows on an artist tile - it is the layer
            // that carries the discs, and the hover WASH behind them is the
            // feedback for the pointer being here. What it must not draw is
            // anything to act on: there is no track to play and no track to open a
            // menu for, so all four elements go.
            //
            // Collapsed rather than unwired. ApplyAccessibleNames attaches the
            // pointer handlers to both discs on every pass it runs, so unwiring
            // here would fight it; a collapsed Button cannot be reached by the
            // pointer in the first place.
            if (_isArtist)
            {
                PlayButton.Visibility = Visibility.Collapsed;
                MoreButton.Visibility = Visibility.Collapsed;
                PlayButtonHover.Visibility = Visibility.Collapsed;
                MoreButtonHover.Visibility = Visibility.Collapsed;
            }
            else
            {
                // The other identity path: a card recycled from an artist tile back
                // onto a file has to arrive with its discs.
                PlayButton.Visibility = Visibility.Visible;
                MoreButton.Visibility = Visibility.Visible;
            }

            HoverOverlay.Visibility = Visibility.Visible;
            HoverOverlay.IsHitTestVisible = true;
            MotionBehaviors.FadeIn(HoverOverlay);
        }

        /// <summary>
        /// Fades the overlay out and collapses it once the fade has run. It goes
        /// un-hit-testable immediately, so the fade cannot swallow a click meant
        /// for the card underneath.
        /// </summary>
        private void HideOverlay()
        {
            HoverOverlay.IsHitTestVisible = false;

            // The frame goes off with the pointer, unless the card is ticked: a
            // ticked card keeps its frame after the pointer leaves, because the
            // frame is the "this one" mark and the tick box alone disappears under
            // the cursor. UpdateFrameForState holds that rule; this only says the
            // pointer is gone.
            //
            // No fade and no callback for the frame, so nothing here to re-check:
            // HideOverlayForMenu and the exit can both land mid-fade, and the frame
            // is already in whatever state the ticks say it should be.
            _pointerInside = false;

            UpdateFrameForState();

            // The box follows the pointer out too - unless the card is in
            // selection mode, in which case every card shows it regardless. That
            // rule lives in UpdateSelectionBoxForState, not here.
            UpdateSelectionBoxForState();

            MotionBehaviors.FadeOut(HoverOverlay, () =>
            {
                // Re-checked inside the callback: the pointer can come back
                // within the fade, and collapsing here would hide the overlay
                // that ShowOverlay has just revealed again.
                if (!HoverOverlay.IsHitTestVisible)
                {
                    HoverOverlay.Visibility = Visibility.Collapsed;
                    MotionBehaviors.Reset(HoverOverlay);
                }
            });
        }

        /// <summary>
        /// Puts the overlay away because a menu opened over it.
        ///
        /// The page calls this from the shared MenuFlyout's Closed event: the
        /// flyout takes the pointer away, so this card never gets its own exit and
        /// would otherwise be left wearing its hover layer behind the menu.
        /// </summary>
        public void HideOverlayForMenu() => HideOverlay();

        private void ResetOverlay()
        {
            MotionBehaviors.Reset(HoverOverlay);
            HoverOverlay.IsHitTestVisible = false;
            HoverOverlay.Visibility = Visibility.Collapsed;

            // The frame is reset with the buttons, never left behind: a card
            // recycled onto a different file while the pointer is elsewhere must
            // not arrive already wearing another item's highlight.
            //
            // Both halves of the answer are rebuilt, not collapsed outright. The
            // tick lives on the entry and is what a recycled card has to be
            // judged against, so "reset" means: declare the pointer absent, then
            // let the one rule decide from the entry this card now points at.
            // Collapsing unconditionally would be a second, independent writer of
            // HoverFrame.Visibility - the shape this file already lost a card to -
            // and it would be wrong for a card prepared onto a ticked entry, which
            // must arrive framed.
            //
            // SetEntry assigns Entry before calling this, so the recompute sees
            // the entry being recycled TO, not the one being released. Nothing
            // later is relied on to correct it: ResetOverlay is a complete answer
            // on its own, including on the null-entry path, which returns from
            // SetEntry before ApplySelection ever runs.
            //
            // Visibility only, no MotionBehaviors.Reset: the frame has no opacity
            // animation to reset (see ShowOverlay), and Reset on a composited
            // visual is what previously left it at one opacity while the element
            // said Visible.
            _pointerInside = false;

            UpdateFrameForState();

            // The box is recomputed here for the same reason the frame is: it is a
            // sibling of the overlay now, so collapsing HoverOverlay no longer takes
            // it with it. A recycled card would otherwise arrive showing (or hiding)
            // the previous file's box until something else happened to repaint it.
            UpdateSelectionBoxForState();
        }

        /// <summary>
        /// The frame is on when the pointer is on the card OR the card is ticked.
        ///
        /// Both callers - the pointer and the entry's IsSelected - go through here,
        /// so there is one place that decides. Two independent writers of the same
        /// Visibility is how a card ends up wearing another card's highlight after a
        /// recycle, which is the defect ResetOverlay already exists to prevent.
        ///
        /// Read as "pointer or tick" rather than as a second, separate highlight for
        /// the ticked state: the frame IS the mark that says which card is chosen,
        /// and the tick box alone vanishes under the pointer.
        ///
        /// Nothing here moves. The gutter around the content is permanent (the
        /// content sits its inset inside the cell whether or not the frame shows),
        /// so turning the frame on and off changes no element's position - measured
        /// as the content's UIA rect being identical in both states.
        /// </summary>
        private void UpdateFrameForState()
        {
            var selected = Entry?.IsSelected == true;

            HoverFrame.Visibility = _pointerInside || selected
                ? Visibility.Visible
                : Visibility.Collapsed;

            // The wash follows the same input but marks a different thing: it is the
            // POINTER lifting the card, not the selection. A ticked card keeps its
            // frame after the pointer leaves - 「已选中常驻」 - but it is no longer
            // under the cursor, so the wash goes out with the pointer.
            HoverWash.Visibility = _pointerInside
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// The tick box is on when the card is in selection mode OR the pointer is on it.
        ///
        /// Two reasons, one writer. The box had no such rule before because it was
        /// inside HoverOverlay: the overlay's own collapse was the second half, so
        /// "hover-only" fell out of the tree rather than out of a decision. It is a
        /// sibling now - it has to be, or it cannot be clicked and cannot outlive the
        /// pointer - so the second half has to be written down.
        ///
        /// Read off entry.SelectionVisibility for the mode half, not off a field of
        /// its own: that property is what the page pushes down through
        /// SetSelectionMode, and a second copy of it here is a second answer to the
        /// same question. Null entry means no mode (a card prepared with nothing),
        /// and the pointer half still applies.
        ///
        /// Called from the three places that can change either input: the pointer
        /// entering and leaving, and ApplySelection (which runs on SetEntry and on
        /// every SelectionVisibility/Glyph change). ResetOverlay reaches it through
        /// ApplySelection on the entry path and directly on the null path.
        /// </summary>
        private void UpdateSelectionBoxForState()
        {
            // Every card gets a tick box now, albums included.
            //
            // <para>The guard that used to live here collapsed the box whenever
            // IsAlbum was true, on the argument that a card standing for a whole
            // album would mean two things at once. Two things were wrong with
            // that:</para>
            //
            // <para>First, <b>IsAlbum is not "this card is an album"</b> - it is
            // "draw a square tile instead of 16:9", and it is set from the media
            // type (MpvPlayerPage.Library.Cards). So the guard was really saying
            // "no tick box on any audio card", which is why the music library had
            // none at all, including on its Songs tab where a card is one track.
            // The report is 「音乐卡片也需要勾选框」.</para>
            //
            // <para>Second, the album/second meaning is decided in one place
            // already - HandleCardActivation, by section and grouping - not here.
            // A tick and a click never collide: the box is a Button, so it takes
            // the pointer, and the page's activation path checks the selection mode
            // before anything else. Confirmed by the owner: 「专辑卡也能勾选，和点击
            // 不冲突」.</para>
            var inMode = Entry?.SelectionVisibility == Visibility.Visible;

            SelectionBox.Visibility = inMode || _pointerInside
                ? Visibility.Visible
                : Visibility.Collapsed;

            // 艺术家瓦片没有勾选框：勾一个人没有可操作的对象。
            //
            // Written AFTER the assignment above, not folded into it, because
            // CardMetricsTests.EveryCardGetsATickBoxIncludingAlbums reads the text
            // between this method's opening and that assignment: a guard placed
            // before it - or an early return - is the shape it exists to forbid.
            // The box is the way into selection mode for a card that stands for a
            // file; a person is not something to tick.
            if (_isArtist)
            {
                SelectionBox.Visibility = Visibility.Collapsed;
                SelectionBoxHover.Visibility = Visibility.Collapsed;
            }

            // A ticked box is the ACCENT colour, an unticked one the neutral
            // surface - 「选择到的按钮应该是强调色」. Set here rather than in markup
            // because it depends on the entry's state, and this is the one place
            // that already re-runs for every input that can change it: the mode,
            // the pointer, and SetEntry on a recycled card.
            //
            // The glyph follows the same rule: on the accent fill it has to be the
            // contrast ink. An unticked box draws no glyph at all now (E739 was
            // Fluent's own checkbox symbol, which put a square outline inside this
            // round border), so its unticked colour rarely shows.
            var ticked = Entry?.IsSelected == true;

            SelectionBox.Background = ResourceBrush(
                ticked ? "AccentFillColorDefaultBrush" : "MpvCardTickBoxFillBrush");

            SelectionBox.BorderBrush = ResourceBrush(
                ticked ? "AccentFillColorDefaultBrush" : "MpvCardTickBoxRingBrush");

            SelectionGlyphIcon.Foreground = ResourceBrush(
                ticked ? "TextOnAccentFillColorPrimaryBrush" : "MpvCardTickGlyphBrush");

            // A ticked box never wears the pointer veil. Cleared here as well as at
            // the pointer handlers because the pointer can already be resting on the
            // box when it gets ticked - the scroll wheel and the keyboard can both
            // reach a tick without moving the mouse, and then no PointerExited will
            // fire to take the veil off.
            if (ticked)
            {
                SelectionBoxHover.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// A brush from the merged resource dictionaries, by key.
        ///
        /// A lookup rather than a typed field because the three tick-box colours are
        /// resolved per theme at runtime, and a direct cast of a missing key would
        /// throw inside a layout pass - the failure mode this repo has already paid
        /// for once with <c>MpvSpaceXS</c>.
        /// </summary>
        private static Microsoft.UI.Xaml.Media.Brush ResourceBrush(string key) =>
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];

        /// <summary>
        /// The tick box was clicked: ask the page to flip this card's tick.
        ///
        /// <b>Nothing is decided here.</b> The tick lives on the entry and the mode
        /// lives on the page, so this only says "the person asked"; the page's handler
        /// calls ToggleSelection and then UpdateSelectionBar, which is what brings the
        /// other cards' boxes in line.
        ///
        /// <b>Why there is no e.Handled here.</b> The card's ROOT is also a Button
        /// (it opens the file), so the obvious worry is that a tick click also fires
        /// RootButton_Click. WinUI's Click is raised with a plain
        /// <see cref="RoutedEventArgs"/>, which has no Handled property at all - that
        /// flag exists on the pointer and Tapped args, and on WPF's RoutedEventArgs,
        /// not on this one. There is nothing to set.
        ///
        /// What actually stops the card's click is ButtonBase itself: the inner button
        /// marks the pointer press and release handled while it presses, so they never
        /// reach the outer button and it raises no Click of its own. That is a routing
        /// behaviour, not a convention, so it was measured rather than assumed -
        /// clicking the box on a video card with nothing selected ticks the card and
        /// leaves playback alone (evidence/task8-c-box-click-did-not-start-playback.png:
        /// the library is still up and mpv's time-pos is still unavailable afterwards;
        /// evidence/task8-e-control-card-body-click-plays.png is the positive control,
        /// where a body click does start playing).
        /// </summary>
        private void SelectionBox_Click(object sender, RoutedEventArgs e) =>
            SelectionRequested?.Invoke(this, EventArgs.Empty);

        private void RootButton_Click(object sender, RoutedEventArgs e) => CardTapped?.Invoke(this, EventArgs.Empty);

        private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayRequested?.Invoke(this, EventArgs.Empty);

        private void MoreButton_Click(object sender, RoutedEventArgs e) => MoreRequested?.Invoke(this, EventArgs.Empty);
    }
}
