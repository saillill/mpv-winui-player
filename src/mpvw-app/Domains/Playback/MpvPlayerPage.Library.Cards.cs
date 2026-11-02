using mpv_winui.Infrastructure.Common.Media;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Infrastructure.Motion;
using mpv_winui.Domains.Playback.Controls;
using mpv_winui.Domains.Playlist;
using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

// Part of MpvPlayerPage. The partial keyword means the compiler sees one class;
// this file exists so a reader looking for one responsibility opens one file
// instead of scrolling through three thousand lines.
namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// the cards: the grid, the hover overlay, the overflow menu, and the A-Z index rail.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// The entry the overflow menu was opened for. Captured because the menu
        /// is a single shared MenuFlyout that outlives the card it points at.
        /// </summary>
        private LibraryEntry? _cardMenuEntry;

        /// <summary>
        /// The card whose overflow button opened the shared menu.
        ///
        /// Held so the menu's Closed event can put that card's overlay away. The
        /// card cannot hear Closed itself - the flyout is shared and lives on the
        /// page - and it will not receive PointerExited either, because the flyout
        /// takes the pointer with it.
        /// </summary>
        private LibraryCard? _cardMenuCard;

        private void ApplyCardStrings()
        {
            var lang = AppContext.AppLang;

            if (Resources["LibraryCardContextMenu"] is not MenuFlyout cardMenu)
            {
                return;
            }

            foreach (var item in cardMenu.Items)
            {
                if (item is not MenuFlyoutItem { Tag: string tag } menuItem)
                {
                    continue;
                }

                menuItem.Text = tag switch
                {
                    "play" => lang.LibraryCardPlay,
                    "add-queue" => lang.LibraryCardAddToQueue,
                    "open-location" => lang.LibraryCardOpenLocation,
                    "copy-path" => lang.LibraryCardCopyPath,
                    "copy-title" => lang.LibraryCardCopyTitle,
                    "select" => lang.LibraryCardSelect,
                    _ => menuItem.Text,
                };
            }
        }

        /// <summary>
        /// Builds the A-Z rail for a grouped view.
        ///
        /// Every letter is shown, dimmed when empty. The reference does the same, and
        /// hiding them would make the rail move as the library changes - a person could
        /// not learn where a letter sits, or see that Q is empty without trying Q.
        ///
        /// Both grouped views have one: the music library's album and artist tabs,
        /// and the video library's folder tab. The video folders were the gap - the
        /// rail was gated on music, and the folder groups carried no Letter at all,
        /// so a library of forty folders had no index and no way to ask for one.
        /// The queue still has none: it is mpv's own playlist.
        /// </summary>
        private void BuildLibraryIndexRail(IEnumerable<VideoFolderGroup> groups)
        {
            // The rail belongs to any grouped view that assigns letters, not
            // only the music ones. The video folder view files its groups under
            // letters too (see BuildVideoFolderGroups, which passes
            // LibraryIndexLetter.For), and gating on music meant a library with
            // forty folders had a working index that was never shown -- the
            // reported "the letter buttons do nothing" was really "there are no
            // letter buttons".
            // Only the music library's grouped tabs have an alphabet rail.
            //
            // The video library used to qualify whenever the folder view was up, and measured
            // on the deployed build it did: `VideoTabFolders` exposed a rail of 27 letter
            // buttons while `VideoTabAll` exposed none. Reported as "视频库不需要右侧的字母排序栏",
            // and the report is right -- the video library groups by DIRECTORY, and a directory
            // has no initial to jump to. The letters were keying off the first character of a
            // folder name, which is an ordering nobody asked for and does not help anyone find
            // a folder whose name they already know.
            var isGrouped = _librarySection == LibrarySectionMusic
                && _musicGrouping != MusicGrouping.Songs;

            if (!isGrouped)
            {
                LibraryIndexRail.ItemsSource = null;

                LibraryIndexRail.Visibility = Visibility.Collapsed;

                return;
            }

            var inUse = groups
                .Select(group => group.Letter)
                .Where(letter => !string.IsNullOrEmpty(letter))
                .Distinct(StringComparer.Ordinal);

            // One table decides the letters and their order for both the rail and the
            // grouping, so the two cannot disagree about which bucket exists.
            LibraryIndexRail.ItemsSource = LibraryIndexLetter.BuildRail(inUse);

            LibraryIndexRail.ItemTemplate = (DataTemplate)Resources["LibraryIndexLetterTemplate"];

            LibraryIndexRail.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Jumps to the first group filed under the clicked letter.
        ///
        /// StartBringIntoView rather than a hand-computed offset: the groups live in a
        /// StackPanel inside a ScrollViewer, so the framework already knows how to scroll
        /// a descendant to the top. Doing the arithmetic by hand is how the lyrics scroll
        /// ended up needing its own centring maths for no benefit.
        /// </summary>
        private void IndexRailButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string letter)
            {
                return;
            }

            if (!_groupHeaders.TryGetValue(letter, out var header))
            {
                return;
            }

            header.StartBringIntoView(new BringIntoViewOptions
            {
                // 0.0 pins the heading to the top of the viewport, which is what a jump
                // list means - a ratio in the middle would leave the previous group
                // showing above the one that was clicked.
                VerticalAlignmentRatio = 0.0,
                AnimationDesired = true,
            });
        }

        /// <summary>
        /// The hover wave: the letter under the pointer grows, its neighbours grow less, and the
        /// effect falls off with distance.
        ///
        /// Reported as 字母排序不应出现诡异重叠的按钮，应只显示字母文字，悬浮放大并带波浪式渐变.
        /// The overlap half was measured and did not reproduce -- 27 single-letter buttons at an
        /// even 32 px pitch, no two overlapping -- so this is the missing half.
        ///
        /// The arithmetic lives in <see cref="IndexRailHover"/>, which is pure and unit-tested.
        /// That split is not decoration: no measurement in this repo can see the effect (the
        /// harness never holds the foreground, so hover input never arrives and the handler never
        /// runs), so the shape it produces is the only part that can be asserted at all.
        ///
        /// **Driven from each letter, not from the repeater.** The first version listened on
        /// LibraryIndexRail's own PointerMoved and never fired: an ItemsRepeater with no
        /// Background is not hit testable over its own area, only its children are.
        ///
        /// Layout is untouched on purpose -- RenderTransform and Opacity do not participate in
        /// layout. Animating Width or FontSize would re-measure the rail on every hover and push
        /// the neighbouring letters around, which is precisely the "buttons jumping about" the
        /// report complained about.
        /// </summary>
        private void IndexRailLetter_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement hovered)
            {
                return;
            }

            var rail = FindAncestorRepeater(hovered);
            if (rail is null)
            {
                return;
            }

            var count = RailEntryCount(rail);
            var peak = -1;

            for (var i = 0; i < count; i++)
            {
                if (ReferenceEquals(rail.TryGetElement(i), hovered))
                {
                    peak = i;
                    break;
                }
            }

            if (peak < 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (rail.TryGetElement(i) is FrameworkElement element)
                {
                    ApplyRailHover(element, i - peak);
                }
            }
        }

        private static ItemsRepeater? FindAncestorRepeater(DependencyObject node)
        {
            var current = node;
            while (current is not null)
            {
                if (current is ItemsRepeater repeater)
                {
                    return repeater;
                }

                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static int RailEntryCount(ItemsRepeater rail) =>
            rail.ItemsSource is System.Collections.IEnumerable source
                ? source.Cast<object>().Count()
                : 0;

        /// <summary>
        /// One letter's share of the wave. <paramref name="strength"/> is 0 at rest and 1 under
        /// the pointer.
        ///
        /// Both properties are written every time rather than only when non-zero, so a letter that
        /// was at the peak returns to rest as the pointer moves away. A one-way setter leaves a
        /// trail of enlarged letters behind the cursor, which is the bug shape this always grows.
        /// </summary>
        private static void ApplyRailHover(FrameworkElement element, double slotsFromPointer)
        {
            var strength = IndexRailHover.StrengthAt(slotsFromPointer);
            var opacity = IndexRailHover.OpacityAt(slotsFromPointer);

            if (element.RenderTransform is not ScaleTransform transform)
            {
                transform = new ScaleTransform();
                element.RenderTransform = transform;
                // About the letter's own centre, not its top-left: scaling from the corner would
                // drag the glyph sideways as it grows.
                element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }

            var scale = 1.0 + (IndexRailHover.PeakScale - 1.0) * strength;
            transform.ScaleX = scale;
            transform.ScaleY = scale;

            element.Opacity = opacity;
        }

        /// <summary>
        /// Fills in thumbnails one card at a time, after the grid is already up.
        ///
        /// Serial rather than parallel on purpose: every card is the same disk
        /// and the same shell thumbnail service, and a burst of fifty decodes
        /// at once would make the grid appear instantly and then stutter while
        /// it filled. One at a time, the cards simply arrive.
        /// </summary>
        private async Task LoadThumbnailsAsync(int generation)
        {
            // Walked from the field rather than out of the grid: the video
            // library can be showing the grouped view, whose cards live in
            // per-folder repeaters and not in LibraryGrid.ItemsSource. Both
            // views hold the same LibraryEntry objects, so filling this list
            // fills whichever one is on screen.
            foreach (var entry in _visibleEntries)
            {
                if (generation != _libraryGeneration)
                {
                    return;
                }

                if (entry.Thumbnail is not null)
                {
                    continue;
                }

                if (_libraryThumbnails.TryGetValue(entry.Path, out var cached))
                {
                    entry.SetThumbnail(cached);
                    continue;
                }

                if (_libraryWithoutThumbnail.Contains(entry.Path))
                {
                    continue;
                }

                var image = await TryLoadThumbnailAsync(entry);

                if (image is null)
                {
                    _libraryWithoutThumbnail.Add(entry.Path);
                    PlayChain.Page("thumb", "none for " + System.IO.Path.GetFileName(entry.Path));
                    continue;
                }

                PlayChain.Page("thumb", "got " + image.PixelWidth + "x" + image.PixelHeight
                    + " for " + System.IO.Path.GetFileName(entry.Path));

                _libraryThumbnails[entry.Path] = image;

                if (generation == _libraryGeneration)
                {
                    entry.SetThumbnail(image);

                    // A group's header shows its first track's cover, and that
                    // group was built before this decode finished. Telling every
                    // realised group that its cover may have moved is cheaper than
                    // rebuilding the tree - which is what the tag pass does, and
                    // what loses the scroll position.
                    NotifyGroupCoversFor(entry.Path);
                }
            }
        }

        /// <summary>
        /// Tells the groups whose first track is this path that their cover arrived.
        ///
        /// Only the first track of a group supplies its cover (see
        /// VideoFolderGroup.Cover), so only that one needs to notify. Walking the
        /// groups is cheaper than keeping an index for a handful of them.
        /// </summary>
        private void NotifyGroupCoversFor(string path)
        {
            foreach (var header in _groupHeaders.Values)
            {
                // The headers are ContentPresenters (see BuildMusicTagGroups and
                // BuildVideoFolderGroups), which is what holds the group object.
                if (header is ContentPresenter presenter
                    && presenter.Content is VideoFolderGroup group
                    && group.Items.Count > 0
                    && string.Equals(group.Items[0].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    group.RaiseCoverChanged();
                }
            }
        }

        /// <summary>
        /// One card's thumbnail.
        ///
        /// Audio first asks the player for the file's embedded picture, because
        /// the shell does not reliably have one. Measured: a music library of
        /// tagged files rendered as a wall of note glyphs, the video grid beside
        /// it full of frames. The shell's provider answers for what Explorer can
        /// preview, and for `.flac` / `.m4a` / `.ogg` it commonly answers with a
        /// type icon rather than the cover -- which the check below then rejects,
        /// correctly, leaving nothing.
        ///
        /// TryGetCoverArtForPath exists for exactly this and was already used by
        /// the album detail page; only the grid was not asking it. It reads the
        /// tags, so it works for a file that is not the one playing.
        ///
        /// Video still goes to the shell: a frame from the middle of the file is
        /// the right thumbnail for a video, and mpv would have to decode one.
        ///
        /// Returns null - never throws - when there is no thumbnail to be had.
        /// Null is a designed outcome: the card falls back to the media-kind
        /// icon, which is what a poster-less row should look like anyway.
        /// </summary>
        private async Task<BitmapImage?> TryLoadThumbnailAsync(LibraryEntry entry)
        {
            if (!entry.IsVideo && await TryLoadEmbeddedCoverAsync(entry) is { } cover)
            {
                return cover;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.Path);

                var mode = entry.IsVideo ? ThumbnailMode.VideosView : ThumbnailMode.MusicView;
                using var thumbnail = await file.GetThumbnailAsync(
                    mode, LibraryThumbnailSize, ThumbnailOptions.UseCurrentScale);

                // The shell answers with a generic type icon when it has no real
                // frame for the file. That icon is another app's idea of what a
                // video looks like, drawn at a size picked for Explorer, and
                // taking it would replace a correct Fluent icon with a wrong
                // one.
                if (thumbnail is null || thumbnail.Type != ThumbnailType.Image)
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                return bitmap;
            }
            catch (Exception)
            {
                // Missing file, no provider, unsupported container, a drive that
                // went away mid-scan. All of them mean the same thing here.
                return null;
            }
        }

        /// <summary>
        /// An audio file's embedded cover art, or null when it has none.
        ///
        /// The native getter does the tag parsing; this only turns the stream
        /// into something a card can show. Decoding is capped by the card size,
        /// so a 3000px sleeve does not cost 36 MB of bitmap per row.
        /// </summary>
        private async Task<BitmapImage?> TryLoadEmbeddedCoverAsync(LibraryEntry entry)
        {
            if (_mediaPlayer is null)
            {
                return null;
            }

            try
            {
                if (!_mediaPlayer.TryGetCoverArtForPath(entry.Path, out var stream) || stream is null)
                {
                    return null;
                }

                using (stream)
                {
                    // Physical pixels, matching what the shell path above asks
                    // for (LibraryThumbnailSize is the same 256 the shell call
                    // uses). Logical would shrink the decode at high DPI and then
                    // have the Image scale it back up, which is the softer of the
                    // two results for the same memory.
                    var bitmap = new BitmapImage
                    {
                        DecodePixelWidth = (int)LibraryThumbnailSize,
                        DecodePixelType = DecodePixelType.Physical,
                    };

                    await bitmap.SetSourceAsync(stream);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                // A corrupt tag, an unsupported picture format, a file that
                // vanished. All of them mean "no cover", never "fail the grid".
                _logger.Debug(ex, "cover art unreadable: {0}", entry.Path);
                return null;
            }
        }

        /// <summary>
        /// Opens the file a card stands for.
        ///
        /// Kept as the click handler's entry point so the card control can stay
        /// ignorant of what a click means: on the Albums tab it opens the album,
        /// in selection mode it ticks, otherwise it plays.
        /// </summary>
        private void HandleCardActivation(LibraryEntry entry)
        {
            // In selection mode a click ticks rather than plays: the card's tick box is
            // an indicator, so the whole card is the hit target. Checked first, so a
            // tick cannot be mistaken for a play request on the Albums tab.
            if (_selectionMode)
            {
                entry.ToggleSelection();

                UpdateSelectionBar();

                return;
            }

            // On the Albums tab a card stands for its album, not for one track:
            // clicking it opens the album, which is what the reference player does
            // and what makes the tab more than a differently-sorted list.
            if (_librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Albums)
            {
                OpenAlbum(string.IsNullOrWhiteSpace(entry.Album)
                    ? AppContext.AppLang.LibraryUnknownAlbum
                    : entry.Album);

                return;
            }

            PlayLibraryEntry(entry);
        }

        /// <summary>
        /// Opens a card's file. The card click and the overlay's Play button are
        /// the same action, so they share this one path rather than drifting.
        /// </summary>
        private void PlayLibraryEntry(LibraryEntry entry)
        {
            MarkNowPlaying(entry);

            // Ask for the video surface BEFORE the load, every time.
            //
            // This is the fix for "play a video, press back, click another video,
            // and the playing page does not come back". FileStarted keeps the
            // library up when <see cref="_withdrewToLibrary"/> is set, which is
            // right for an auto-advance during browsing - but a card click is not
            // an advance, it is the user saying "show me this file". Without this
            // flag the second click was treated as a track change inside the
            // withdrawn session, so the video loaded behind a library that never
            // went away.
            //
            // Every other way of opening a file already sets it (the open menu,
            // a drop, a URL, a folder); the card was the one path that did not,
            // which is why the defect only ever appeared after pressing back.
            ShowVideoForNextOpen();

            _mediaPlayer.OpenAsync(new FileItem(entry.Path)).FireAndForget(OnException);
        }

        /// <summary>
        /// Moves the playing marker to one entry, clearing it from the last.
        ///
        /// Exactly one card may carry it, so this is a sweep rather than a single
        /// assignment: a previous entry is not remembered on purpose, because the
        /// list is the authority on what is in it and an index would go stale on
        /// the first filter change. The lists are small - a library section, not
        /// the whole disk - so the sweep is cheaper than tracking is correct.
        ///
        /// The card watches IsNowPlaying, so the stripe appears without a rebuild;
        /// LoadMusicTagsAsync carries the flag across when it DOES rebuild.
        /// </summary>
        private void MarkNowPlaying(LibraryEntry playing)
        {
            foreach (var candidate in _libraryAll)
            {
                if (!ReferenceEquals(candidate, playing) && candidate.IsNowPlaying)
                {
                    candidate.SetNowPlaying(false);
                }
            }

            playing.SetNowPlaying(true);
        }













        /// <summary>The card body was clicked.</summary>
        private void Card_Tapped(object? sender, EventArgs e)
        {
            if (sender is not LibraryCard { Entry: { } entry })
            {
                return;
            }

            HandleCardActivation(entry);
        }

        /// <summary>The hover layer's play button was clicked.</summary>
        private void Card_PlayRequested(object? sender, EventArgs e)
        {
            if (sender is LibraryCard { Entry: { } entry })
            {
                PlayLibraryEntry(entry);
            }
        }

        /// <summary>The hover layer's overflow button was clicked.</summary>
        private void Card_MoreRequested(object? sender, EventArgs e)
        {
            if (sender is not LibraryCard { Entry: { } entry } card)
            {
                return;
            }

            _cardMenuEntry = entry;

            ShowCardContextMenu(card, entry);
            _cardMenuCard = card;
        }

        /// <summary>
        /// The overlay's overflow button: enables the items that need the file
        /// to exist, records which entry the menu is for, and shows the shared
        /// menu against this button.
        /// </summary>
        /// <summary>
        /// The shared menu closed: the card that opened it puts its overlay away.
        ///
        /// A flyout takes the pointer with it, so the card never receives its own
        /// PointerExited and the overlay would otherwise stay up behind the menu.
        /// </summary>
        private void CardMenu_Closed(object? sender, object e)
        {
            _cardMenuCard?.HideOverlayForMenu();
            _cardMenuCard = null;
        }

        private void ShowCardContextMenu(FrameworkElement anchor, LibraryEntry entry)
        {
            if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
            {
                foreach (var item in cardMenu.Items)
                {
                    if (item is MenuFlyoutItem { Tag: string tag } menuItem)
                    {
                        // The three that open the file need it to be there; the
                        // two that copy a path or a title do not.
                        menuItem.IsEnabled = tag is "play" or "add-queue" or "open-location"
                            ? !entry.IsMissing
                            : true;
                    }
                }

                cardMenu.ShowAt(anchor);
            }
        }

        /// <summary>The card menu's five commands. All pre-existing app paths.</summary>
        private void CardMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { Tag: string action } || _cardMenuEntry is not { } entry)
            {
                return;
            }

            switch (action)
            {
                case "play":
                    PlayLibraryEntry(entry);
                    break;
                case "add-queue":
                    // The queue's own "Add to" opens a picker, so what is reused
                    // here is its mechanism - a single loadfile append - not that
                    // method.
                    _mediaPlayer.OpenAsync((FileItem[])[new FileItem(entry.Path)], OpenMode.Append)
                        .FireAndForget(OnException);
                    break;
                case "select":
                    // The card that opened the menu starts ticked: choosing "select"
                    // on a specific card means that card, and starting with nothing
                    // ticked makes the click look like it did nothing.
                    //
                    // No mode switch beside this: the tick itself is what puts the
                    // page in the mode, and UpdateSelectionBar derives it from the
                    // count.
                    entry.SetSelected(true);

                    UpdateSelectionBar();
                    break;
                case "open-location":
                    // Explorer with the file selected, which is what "open the
                    // file's location" means and what this helper already does.
                    FileLauncher.ShellLaunchFileAsync(entry.Path).FireAndForget(OnException);
                    break;
                case "copy-path":
                    ClipboardHelper.SetCopyText(entry.Path);
                    break;
                case "copy-title":
                    ClipboardHelper.SetCopyText(entry.Title);
                    break;
            }
        }

        /// <summary>
        /// The poster cards arriving, staggered so the grid reads as filling in
        /// rather than blinking on.
        ///
        /// This is the same behaviour the settings list uses, with the same
        /// distance, curve, duration and stagger - the two surfaces are meant to
        /// look like one product, and that only holds while both call this one
        /// name.
        /// </summary>
        private void LibraryGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            // One assignment, no name search. The card is a typed control now, so
            // a part that stops existing fails the build instead of quietly
            // never painting: FindDescendant(card, "AlbumCardCoverImage") had no
            // link to the markup, so a rename broke only the picture.
            //
            // SetEntry is also where the overlay is reset and the entry's own
            // subscription is moved, because both are per-entry state on a
            // recycled element - the card cannot know when it is being reused.
            if (args.Element is LibraryCard card
                && sender.ItemsSourceView is { } view
                && args.Index >= 0 && args.Index < view.Count
                && view.GetAt(args.Index) is LibraryEntry entry)
            {
                // From the ENTRY, not the section. The home page now carries both
                // kinds side by side, so a section-level answer is wrong there: it
                // said "not the music section" for a track and gave the card a 16:9
                // tile. Measured as a 298x168 cover where a square was expected.
                card.IsAlbum = !entry.IsVideo && MediaKinds.IsAudio(entry.Path);
                card.SetEntry(entry, ResolveCover(entry));

                card.CardTapped -= Card_Tapped;
                card.CardTapped += Card_Tapped;
                card.PlayRequested -= Card_PlayRequested;
                card.PlayRequested += Card_PlayRequested;
                card.MoreRequested -= Card_MoreRequested;
                card.MoreRequested += Card_MoreRequested;
            }

            var delay = TimeSpan.FromMilliseconds(Math.Min(args.Index, 8) * 20);
            MotionBehaviors.PosterEnter(args.Element, delay: delay);
        }

        /// <summary>
        /// The cover for an entry, cache first.
        ///
        /// Cache FIRST, entry second. A recycled card is prepared before the item it
        /// is being pointed at has had its Thumbnail filled, so entry.Thumbnail is
        /// routinely null for a card whose picture is already decoded and cached --
        /// measured in the log as "found=SongCardCoverImage thumb=null" repeating while
        /// a "thumb=present" line sat between them. Reading the cache first is what
        /// makes the cover survive recycling.
        /// </summary>
        private ImageSource? ResolveCover(LibraryEntry entry)
        {
            if (entry.Thumbnail is { } own)
            {
                return own;
            }

            return _libraryThumbnails.TryGetValue(entry.Path, out var cached) ? cached : null;
        }



    }
}
