using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Dragging media onto the window.
    ///
    /// What is accepted, in this order:
    ///
    /// * Storage items - files and folders - that are media this app knows
    ///   (MediaKinds: the same two extension lists the library and the settings
    ///   file-association list use, so "what can be played" has one answer).
    ///   Folders are accepted and played as a folder open.
    /// * An absolute, non-file URI from a text payload, because mpv plays
    ///   network streams and dragging a link out of a browser is a normal thing
    ///   to try.
    ///
    /// Anything else is REFUSED VISIBLY: the drop is not accepted, the overlay
    /// says why, and no file is opened. Silently ignoring a drop is what makes
    /// people try it three more times.
    ///
    /// The overlay is a full-window surface rather than a hint on the library
    /// card grid, because the drop target is now the whole window: the old
    /// handlers were on VideoContainer, which the library covers, so on Home -
    /// the one page where an empty state is asking you to open something - there
    /// was no drop target at all. That is the reported "home should accept a drop
    /// and start playing".
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// The current payload's verdict, decided on DragEnter and repeated on
        /// DragOver: Windows asks again on every DragOver, so the answer has to
        /// be cheap and has to already be known.
        /// </summary>
        private bool _dropPayloadPlayable;

        private async void OnDragEnter(object sender, DragEventArgs e)
        {
            var defer = e.GetDeferral();
            try
            {
                var verdict = await InspectDropAsync(e.DataView);
                _dropPayloadPlayable = verdict.Playable;
                ShowDropOverlay(verdict);
                e.AcceptedOperation = verdict.Playable
                    ? DataPackageOperation.Copy
                    : DataPackageOperation.None;
            }
            catch (Exception ex)
            {
                OnException(ex);
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                defer.Complete();
            }
        }

        /// <summary>
        /// DragOver repeats the verdict rather than re-inspecting: the data view
        /// is already known and Windows calls this many times per second, so
        /// re-reading storage items here would be a per-frame await.
        /// </summary>
        private void OnDragOver(object sender, DragEventArgs e)
        {
            var overlay = DropOverlay;

            e.AcceptedOperation = _dropPayloadPlayable
                ? DataPackageOperation.Copy
                : DataPackageOperation.None;

            // The system cursor caption is suppressed: the overlay already says
            // what will happen, in this app's own language and with this app's
            // own wording, and two captions saying nearly the same thing in
            // different type is noise.
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsContentVisible = false;
            e.DragUIOverride.IsGlyphVisible = _dropPayloadPlayable;

            if (overlay is not null)
            {
                e.Handled = true;
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var defer = e.GetDeferral();
            var items = new List<IStorageItem>();
            var url = string.Empty;
            try
            {
                var dataPackageView = e.DataView;

                if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                {
                    items.AddRange(await dataPackageView.GetStorageItemsAsync());
                }
                else if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    // Dragging a text selection/URL from a browser or editor:
                    // treat it as a media URL when it parses as an absolute,
                    // non-file URI (mpv plays network streams).
                    var text = await dataPackageView.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text)
                        && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
                        && !uri.IsFile)
                    {
                        url = text.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
            finally
            {
                defer.Complete();
            }

            DropOverlay.Visibility = Visibility.Collapsed;

            // Shift means "queue it after what is playing", which is the same
            // modifier the open dialog's multi-select uses.
            var append = InputKeyboardSource
                .GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(CoreVirtualKeyStates.Down);
            var openMode = append ? OpenMode.Append : OpenMode.Replace;

            if (items.Count > 0)
            {
                var playable = items.Where(IsPlayableDropItem).ToList();
                var rejected = items.Count - playable.Count;

                if (playable.Count == 0)
                {
                    // Nothing usable: say so rather than doing nothing. The files
                    // the user dropped are named in the message so it is obvious
                    // which one was the problem.
                    ShowDropError(items);
                    return;
                }

                if (rejected > 0)
                {
                    // A mixed drop plays what it can. The refused files are
                    // reported rather than dropped silently, because "some of my
                    // files vanished" is worse than "three were skipped".
                    ShowDropSkipped(rejected, items.Count);
                }

                // Dropping a file is a request to watch it: jump to the video
                // even if the library is up over a running session.
                ShowVideoForNextOpen();

                PlayStorageItems(playable, openMode).FireAndForget(OnException);
            }
            else if (url.Length > 0)
            {
                ShowVideoForNextOpen();

                PlayUrl(url, openMode).FireAndForget(OnException);
            }
        }

        /// <summary>
        /// What a drop payload is, decided once.
        /// Playable: something here can be played.
        /// Known: how many of the files are media this app opens.
        /// Total: how many files there were.
        /// </summary>
        private readonly record struct DropVerdict(bool Playable, int Known, int Total, bool IsUrl);

        private async Task<DropVerdict> InspectDropAsync(DataPackageView view)
        {
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items;
                try
                {
                    items = await view.GetStorageItemsAsync();
                }
                catch (Exception ex)
                {
                    OnException(ex);
                    return new DropVerdict(false, 0, 0, false);
                }

                if (items.Count == 0)
                {
                    return new DropVerdict(false, 0, 0, false);
                }

                var known = items.Count(IsPlayableDropItem);

                return new DropVerdict(known > 0, known, items.Count, false);
            }

            if (view.Contains(StandardDataFormats.Text))
            {
                try
                {
                    var text = await view.GetTextAsync();
                    var ok = !string.IsNullOrWhiteSpace(text)
                        && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
                        && !uri.IsFile;

                    return new DropVerdict(ok, ok ? 1 : 0, 1, true);
                }
                catch (Exception ex)
                {
                    OnException(ex);
                }
            }

            return new DropVerdict(false, 0, 0, false);
        }

        /// <summary>
        /// A folder counts as playable: mpv opens folders, and the drop itself
        /// says nothing about whether the folder has been indexed yet.
        /// </summary>
        private static bool IsPlayableDropItem(IStorageItem item) =>
            item.IsOfType(StorageItemTypes.Folder)
            || MediaKinds.IsKnownMedia(item.Path);

        private void ShowDropOverlay(DropVerdict verdict)
        {
            var lang = AppContext.AppLang;

            DropOverlayTitle.Text = verdict.Playable
                ? lang.DropPlayTitle
                : lang.DropUnsupportedTitle;

            DropOverlayDescription.Text = verdict.Playable
                ? lang.DropPlayDescription
                : lang.DropUnsupportedDescription;

            DropOverlayIcon.Glyph = verdict.Playable ? "\uE768" : "\uEA39";

            DropOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// The "cannot play this" message after a drop.
        ///
        /// An InfoBar rather than a dialog: it is not a question, it needs no
        /// answer, and the official player reports playback-level problems the
        /// same way - a bar above the transport that you can dismiss. A modal
        /// dialog for "wrong file type" would also block the window the user is
        /// about to drop something else onto.
        /// </summary>
        private void ShowDropError(IReadOnlyList<IStorageItem> items)
        {
            var lang = AppContext.AppLang;
            var names = string.Join(", ", items.Take(3).Select(i => i.Name));
            if (items.Count > 3)
            {
                names += $" (+{items.Count - 3})";
            }

            ShowPlaybackInfoBar(
                InfoBarSeverity.Warning,
                lang.DropUnsupportedTitle,
                string.Format(lang.DropUnsupportedDetail, names));
        }

        private void ShowDropSkipped(int rejected, int total)
        {
            var lang = AppContext.AppLang;
            ShowPlaybackInfoBar(
                InfoBarSeverity.Informational,
                lang.DropPlayTitle,
                string.Format(lang.DropSkippedDetail, rejected, total));
        }
    }
}
