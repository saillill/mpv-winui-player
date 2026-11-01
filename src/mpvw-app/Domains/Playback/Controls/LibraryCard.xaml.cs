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
            }
        }

        private bool _isAlbum;

        /// <summary>Raised when the card body is clicked.</summary>
        public event EventHandler? CardTapped;

        /// <summary>Raised when the hover layer's play button is clicked.</summary>
        public event EventHandler? PlayRequested;

        /// <summary>Raised when the hover layer's overflow button is clicked.</summary>
        public event EventHandler? MoreRequested;

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
            // arrive already wearing another item's hover.
            ResetOverlay();

            if (entry is null)
            {
                TitleText.Text = string.Empty;
                SubtitleText.Text = string.Empty;
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
                // that decide the caption colour. Without this, ticking a card in
                // selection mode showed the tick box but left the text white,
                // because the caption repaint only ran on IsNowPlaying.
                case nameof(LibraryEntry.IsSelected):
                    ApplyCaptionEmphasis(entry);
                    break;
                case nameof(LibraryEntry.SelectionVisibility):
                case nameof(LibraryEntry.SelectionGlyph):
                    ApplySelection(entry);
                    break;
            }
        }

        private void ApplyCovers(LibraryEntry entry, ImageSource? cover)
        {
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

            // Left assigned (not cleared) so nothing that reads it sees a stale
            // value from a card that has been recycled onto a different file.
            // It is Collapsed in the markup; this only keeps it coherent.
            SubtitleText.Text = string.Empty;
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

            NowPlayingBar.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;

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
            SelectionBox.Visibility = entry.SelectionVisibility;
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
            var height = CardMetrics.CoverHeightFor(MediaBorder.ActualWidth, _isAlbum);

            if (height > 0)
            {
                MediaBorder.Height = height;

                // The overlay gets the tile's height, not the card's: the scrim
                // should stop where the picture does, and the buttons ride that
                // edge. Leaving it unset let the Grid size itself to its tallest
                // child - a 32px button - so the scrim was a 32px strip and both
                // buttons sat in it, which is why MoreButton never appeared at the
                // card's corner.
                HoverOverlay.Height = height;
            }
        }

        private void ShowOverlay()
        {
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
        }

        private void RootButton_Click(object sender, RoutedEventArgs e) => CardTapped?.Invoke(this, EventArgs.Empty);

        private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayRequested?.Invoke(this, EventArgs.Empty);

        private void MoreButton_Click(object sender, RoutedEventArgs e) => MoreRequested?.Invoke(this, EventArgs.Empty);
    }
}
