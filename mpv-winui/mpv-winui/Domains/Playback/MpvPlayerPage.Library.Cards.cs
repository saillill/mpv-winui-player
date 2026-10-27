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
        /// The one card overlay currently faded in. A pointer can rest on only
        /// one card, so tracking it lets a scroll or the overflow menu put the
        /// overlay away without walking the repeater's realised children.
        /// </summary>
        private FrameworkElement? _activeCardOverlay;

        /// <summary>
        /// The entry the overflow menu was opened for. Captured because the menu
        /// is a single shared MenuFlyout that outlives the card it points at.
        /// </summary>
        private LibraryEntry? _cardMenuEntry;

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
        /// </summary>
        private void LibraryCard_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not LibraryEntry entry)
            {
                return;
            }

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
        private void PlayLibraryEntry(LibraryEntry entry) =>
            _mediaPlayer.OpenAsync(new FileItem(entry.Path)).FireAndForget(OnException);

        private void LibraryCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement card || FindCardOverlay(card) is not { } overlay)
            {
                return;
            }

            // Filled here, not in the template: the card template is shared, so
            // it cannot bind a page-level string, and doing it at the moment the
            // overlay becomes visible keeps it right after a language change.
            ApplyCardOverlayStrings(card);

            _activeCardOverlay = overlay;
            ShowCardOverlay(overlay);
        }

        private void LibraryCard_PointerExited(object sender, PointerRoutedEventArgs e) => HideCardOf(sender);

        private void LibraryCard_PointerCanceled(object sender, PointerRoutedEventArgs e) => HideCardOf(sender);

        /// <summary>
        /// A scroll moves the cards out from under a resting pointer without
        /// always producing an exit, so the overlay that was up is put away
        /// explicitly. Only the tracked one can be up, so there is nothing to
        /// enumerate.
        /// </summary>
        private void LibraryScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_activeCardOverlay is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void LibraryCardMenu_Closed(object? sender, object e)
        {
            if (_activeCardOverlay is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void HideCardOf(object sender)
        {
            if (sender is FrameworkElement card && FindCardOverlay(card) is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void ShowCardOverlay(FrameworkElement overlay)
        {
            overlay.Visibility = Visibility.Visible;
            overlay.IsHitTestVisible = true;
            MotionBehaviors.FadeIn(overlay);
        }

        /// <summary>
        /// Fades the overlay out and collapses it once the fade has run. It is
        /// made un-hit-testable immediately, so the 83 ms it spends fading out
        /// cannot swallow a click meant for whatever is underneath.
        /// </summary>
        private void HideCardOverlay(FrameworkElement overlay)
        {
            overlay.IsHitTestVisible = false;

            if (ReferenceEquals(_activeCardOverlay, overlay))
            {
                _activeCardOverlay = null;
            }

            MotionBehaviors.FadeOut(overlay, () =>
            {
                // Re-checked in the callback: the pointer can come back inside
                // the 83 ms the fade takes.
                if (!overlay.IsHitTestVisible)
                {
                    overlay.Visibility = Visibility.Collapsed;
                    MotionBehaviors.Reset(overlay);
                }
            });
        }

        /// <summary>Returns an overlay to the state a freshly realised card has.</summary>
        private static void ResetCardOverlay(FrameworkElement overlay)
        {
            MotionBehaviors.Reset(overlay);
            overlay.IsHitTestVisible = false;
            overlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Names the overlay's two corner buttons for screen readers and tooltips.
        ///
        /// The buttons carry glyphs rather than words now - at 32px a label is not
        /// legible and the reference uses bare icons - so the text exists only where it
        /// is needed: the automation name and the tooltip.
        ///
        /// Called as the card is entered rather than from the template, which is shared
        /// and so cannot carry a page-level string.
        /// </summary>
        private static void ApplyCardOverlayStrings(FrameworkElement card)
        {
            var lang = AppContext.AppLang;

            var play = FindDescendant(card, "CardPlayButton")
                ?? FindDescendant(card, "AlbumCardPlayButton");

            if (play is Button playButton)
            {
                AutomationProperties.SetName(playButton, lang.LibraryCardPlay);
                ToolTipService.SetToolTip(playButton, lang.LibraryCardPlay);
            }

            var more = FindDescendant(card, "CardMoreButton")
                ?? FindDescendant(card, "AlbumCardMoreButton");

            if (more is Button moreButton)
            {
                AutomationProperties.SetName(moreButton, lang.LibraryCardMore);
                ToolTipService.SetToolTip(moreButton, lang.LibraryCardMore);
            }
        }

        /// <summary>
        /// The card's overlay, by name, from the card root.
        ///
        /// Two spellings because there are two templates. The album card names its parts
        /// Album* rather than reusing the video card's names - two DataTemplates in one
        /// file sharing an x:Name is a separate problem - so a single-name lookup would
        /// leave album cards with no hover layer: PointerEntered fires, this returns
        /// null, and the card looks inert with nothing to report.
        ///
        /// A candidate list rather than stripping a prefix: these are three fixed names,
        /// and a prefix rule would be a guess about how the next template gets named.
        /// </summary>
        private static FrameworkElement? FindCardOverlay(DependencyObject card) =>
            FindDescendant(card, "CardHoverOverlay") as FrameworkElement
            ?? FindDescendant(card, "AlbumCardHoverOverlay") as FrameworkElement;

        /// <summary>
        /// Depth-first search of a realised card for one named element.
        ///
        /// A name search rather than a template part because the card is a
        /// DataTemplate instance, not a ControlTemplate: there is no
        /// GetTemplateChild, and the overlay sits behind a content presenter
        /// whose shape is the card style's business.
        /// </summary>
        private static DependencyObject? FindDescendant(DependencyObject root, string name)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement { Name: var childName } && childName == name)
                {
                    return child;
                }

                if (FindDescendant(child, name) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        private void CardPlay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryEntry entry)
            {
                PlayLibraryEntry(entry);
            }
        }

        /// <summary>
        /// The overlay's overflow button: enables the items that need the file
        /// to exist, records which entry the menu is for, and shows the shared
        /// menu against this button.
        /// </summary>
        private void CardMore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button || button.DataContext is not LibraryEntry entry)
            {
                return;
            }

            _cardMenuEntry = entry;

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

                cardMenu.ShowAt(button);
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
                    SetSelectionMode(true);

                    // The card that opened the menu starts ticked: choosing "select"
                    // on a specific card means that card, and starting with nothing
                    // ticked makes the click look like it did nothing.
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
            // A recycled card keeps whatever the previous item left on it, and
            // the hover overlay is code-driven state the template cannot reset
            // by itself. Put it back to rest before the card is reused, so no
            // card can appear already dressed for another item's hover.
            if (FindCardOverlay(args.Element) is { } overlay)
            {
                ResetCardOverlay(overlay);
            }

            // The overlay's labels are set here as well as on hover, so a card
            // that is on screen but never hovered still carries its button names
            // for a screen reader walking the grid. Cheap: it reuses the walk
            // above. A language change reaches cards realised after it, and the
            // hover path covers the rest.
            if (args.Element is FrameworkElement card)
            {
                // ItemsRepeater does NOT set DataContext on a realised element. That is the
                // one way it differs from GridView/ListView here, and it silently broke the
                // album card.
                //
                // The album and artist templates bind their two captions with {Binding}, not
                // {x:Bind}, because a single {x:Bind AlbumTitle} makes the WindowsAppSDK
                // 2.3.9 XAML compiler fail with WMC9999 (see the long comment on
                // AlbumCardTemplate). {Binding} resolves against the DataContext, so with no
                // DataContext every path in those templates resolves to null.
                //
                // Measured before this line existed, on the album tab:
                //
                //     stddev 0.0, distinct colours 1, whole card = rgb(32,32,32)
                //
                // a flat grey rectangle with no cover, no caption and not even the fallback
                // glyph -- the reported "长条形的灰色底". The song tab measured stddev 17.1 /
                // 94 colours on the same run, because its template is all {x:Bind} and needs
                // no DataContext.
                //
                // Set here because a DataTemplate has no hook of its own, and ElementPrepared
                // runs for every realised element including recycled ones -- which is also
                // what a recycled card needs.
                if (sender.ItemsSourceView is { } view
                    && args.Index >= 0 && args.Index < view.Count)
                {
                    var item = view.GetAt(args.Index);
                    card.DataContext = item;

                    // Write the two captions directly instead of relying on {Binding}.
                    //
                    // Measured: with DataContext correctly set to the LibraryEntry and the
                    // entry carrying album="i Love" artist="未知艺术家", the card's two
                    // TextBlocks still came out EMPTY -- UIA showed two TextControl children
                    // at the right rectangles with name='', and the whole card rendered as
                    // one flat rgb(32,32,32).
                    //
                    // The template cannot use {x:Bind} here: a single {x:Bind AlbumTitle}
                    // makes the WindowsAppSDK 2.3.9 XAML compiler fail with WMC9999 (see the
                    // comment on AlbumCardTemplate). {Binding} was the workaround, and it is
                    // not resolving inside an ItemsRepeater DataTemplate even with a correct
                    // DataContext. Rather than fight the binding engine, the two strings are
                    // assigned where the data is already in hand -- this runs for every
                    // realised element, including recycled ones, so it also fixes reuse.
                    SetCardCaption(card, "AlbumCardTitleText", (item as LibraryEntry)?.AlbumTitle);
                    SetCardCaption(card, "AlbumCardArtistText", (item as LibraryEntry)?.AlbumArtistText);
                    SetCardCover(card, item as LibraryEntry);
                    SetCardMediaSquare(card);
                }

                ApplyCardOverlayStrings(card);
            }

            var delay = TimeSpan.FromMilliseconds(Math.Min(args.Index, 8) * 20);
            MotionBehaviors.PosterEnter(args.Element, delay: delay);
        }

        /// <summary>
        /// Shows or hides the album card's cover from code, and hides the fallback glyph
        /// while a cover is up.
        ///
        /// Same reason as <see cref="SetCardCaption"/>: the template's bindings do not take
        /// effect here. Measured -- the .flac's thumbnail loaded at 600x600 and
        /// LibraryEntry raised PropertyChanged for Thumbnail, yet the card stayed a flat
        /// rgb(32,32,32) because neither the Image's Source nor its Visibility followed.
        ///
        /// A null thumbnail is not an error: it is the poster-less state, and the media-kind
        /// glyph stays visible for it.
        /// </summary>
        private void SetCardCover(FrameworkElement card, LibraryEntry? entry)
        {
            // FindName does not reach into a DataTemplate's instantiated content: the names
            // belong to the template instance, not to the root the handler was given.
            // Measured -- "no AlbumCardCoverImage on Button" for every card while the element
            // was demonstrably in the tree. Walking the visual tree is what works.
            // TWO templates, two names. The album card's media area and the song card's are
            // different elements with different shapes (square against 16:9), so the cover code
            // has to know which one it was handed. The song card was missing from this method
            // until it was reported: only AlbumCardCoverImage was ever set, so every SONG card
            // stayed a grey tile with a note glyph forever while the album and artist tabs
            // worked. Reported as 音乐库的封面依旧没有加载出来, against exactly that tab.
            var cover = FindDescendant<Image>(card, "AlbumCardCoverImage")
                ?? FindDescendant<Image>(card, "SongCardCoverImage");
            if (cover is null)
            {
                return;
            }

            var glyphElement =
                FindDescendant<FrameworkElement>(card, "AlbumCardFallbackIcon")
                ?? FindDescendant<FrameworkElement>(card, "SongCardFallbackIcon");

            // Cache FIRST, entry second. A recycled card is prepared before the item it is
            // being pointed at has had its Thumbnail filled, so entry.Thumbnail is routinely null
            // for a card whose picture is already decoded and cached -- measured in the log as
            // "found=SongCardCoverImage thumb=null" repeating while a "thumb=present" line sat
            // between them. Reading the cache first is what makes the cover survive recycling.
            var image = entry?.Thumbnail;
            if (image is null && entry is not null
                && _libraryThumbnails.TryGetValue(entry.Path, out var cached))
            {
                image = cached;
            }

            if (image is not null)
            {
                cover.Source = image;
                cover.Visibility = Visibility.Visible;
                if (glyphElement is not null)
                {
                    glyphElement.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                cover.Source = null;
                cover.Visibility = Visibility.Collapsed;
                if (glyphElement is not null)
                {
                    glyphElement.Visibility = Visibility.Visible;
                }

                // The thumbnail arrives LATER: it is fetched asynchronously after the card is
                // already on screen, and the template's {x:Bind Thumbnail} does not deliver
                // the update (same reason as the captions). So the one card that is waiting
                // gets a one-shot listener that repaints when its image lands.
                //
                // Guarded by the handler being removed on the first raise: the entry outlives
                // the card, and a listener kept alive per recycle would repaint elements that
                // are already showing something else.
                if (entry is not null)
                {
                    void OnThumbnailReady(object? s, PropertyChangedEventArgs e)
                    {
                        if (e.PropertyName != nameof(LibraryEntry.Thumbnail))
                        {
                            return;
                        }

                        entry.PropertyChanged -= OnThumbnailReady;
                        SetCardCover(card, entry);
                    }

                    entry.PropertyChanged -= OnThumbnailReady;
                    entry.PropertyChanged += OnThumbnailReady;
                }
            }
        }

        /// <summary>
        /// Gives the album card's media area its square height, in code.
        ///
        /// The template asks for `Height="{Binding ActualWidth, RelativeSource={RelativeSource
        /// Self}}"`, which is a self-referential {Binding}. Every other {Binding} in this
        /// template turned out not to resolve (the captions came out empty), and {x:Bind}
        /// cannot replace it: `{x:Bind ActualWidth}` in this DataTemplate crashes the
        /// WindowsAppSDK 2.3.9 XAML compiler with WMC9999, exactly like {x:Bind AlbumTitle}.
        ///
        /// A zero-height media Border would explain a cover that loads but never shows, so the
        /// height is set here once the real width is known. SizeChanged keeps it square through
        /// a window resize; the handler is removed when the element is recycled, because
        /// ElementPrepared runs again for the next item that lands on this card.
        /// </summary>
        private static void SetCardMediaSquare(FrameworkElement card)
        {
            if (FindDescendant<Border>(card, "AlbumCardMediaBorder") is not Border media)
            {
                return;
            }

            void Fit()
            {
                if (media.ActualWidth > 0)
                {
                    media.Height = media.ActualWidth;
                }
            }

            media.SizeChanged -= OnMediaSizeChanged;
            media.SizeChanged += OnMediaSizeChanged;
            Fit();

            static void OnMediaSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (sender is Border b && b.ActualWidth > 0)
                {
                    b.Height = b.ActualWidth;
                }
            }
        }

        /// <summary>
        /// Writes one caption inside a realised card, by name.
        ///
        /// The album card's two captions cannot use a binding: {x:Bind} to these properties
        /// crashes the WindowsAppSDK 2.3.9 XAML compiler (WMC9999), and {Binding} inside an
        /// ItemsRepeater DataTemplate did not resolve even with a correct DataContext --
        /// measured, both TextBlocks stayed empty while the entry carried album="i Love" and
        /// artist="未知艺术家", and the card rendered as one flat rgb(32,32,32). Setting the
        /// text here is the one mechanism known to work.
        ///
        /// A missing name is ignored rather than thrown: this same handler serves the video
        /// card, which has no album captions at all.
        /// </summary>
        private static void SetCardCaption(FrameworkElement card, string name, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (FindDescendant<TextBlock>(card, name) is { } block)
            {
                block.Text = text;
            }
        }
    }
}
