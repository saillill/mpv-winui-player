using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace mpv_winui.Modules.Player
{
    public struct PlaylistItem(MpvPlaylistItem item)
    {
        public readonly int Index => item.Index;
        public readonly int Id => item.Id;
        public readonly bool IsCurrent => item.IsCurrent || item.IsPlaying;
        public readonly string Title => item.Title;
        public readonly string Path => item.Filename;
        public readonly string Filename => System.IO.Path.GetFileName(item.Filename);

        /// <summary>Media duration in seconds, or &lt;= 0 when unknown.</summary>
        public readonly double Duration => item.Duration;

        /// <summary>
        /// " · WxH" for the currently playing row - mpv only exposes video
        /// dimensions for the playing entry, so every other row stays empty.
        /// </summary>
        public string ResolutionText { get; set; } = string.Empty;

        /// <summary>Formatted "m:ss" / "h:mm:ss" for the playlist row, empty when unknown.</summary>
        public readonly string DurationText
        {
            get
            {
                if (item.Duration <= 0)
                {
                    return string.Empty;
                }
                var total = (int)Math.Round(item.Duration);
                var hours = total / 3600;
                var minutes = (total % 3600) / 60;
                var seconds = total % 60;
                return hours > 0
                    ? $"{hours}:{minutes:D2}:{seconds:D2}"
                    : $"{minutes}:{seconds:D2}";
            }
        }
    };

    public sealed partial class MpvPlayerPage
    {
        /// <summary>All rows as mpv reports them, in mpv's order.</summary>
        public ObservableCollection<PlaylistItem> PlaylistItems { get; } = [];

        private readonly List<PlaylistItem> _allPlaylistItems = [];
        private DispatcherQueueTimer? _playlistRefreshTimer;
        private bool _playlistRefreshPending;
        private int _lastCurrentPlaylistIndex = -1;

        private void RefreshPlaylistAsync()
        {
            // Coalesce bursty mpv playlist events (a loadlist can emit one
            // PlaylistChanged per entry) into a single refresh.
            _playlistRefreshPending = true;
            _playlistRefreshTimer ??= CreatePlaylistRefreshTimer();
            _playlistRefreshTimer.Start();
        }

        private DispatcherQueueTimer CreatePlaylistRefreshTimer()
        {
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += PlaylistRefreshTimer_Tick;
            return timer;
        }

        private void PlaylistRefreshTimer_Tick(object? sender, object args)
        {
            if (sender is DispatcherQueueTimer timer)
            {
                timer.Stop();
            }

            if (!_playlistRefreshPending)
            {
                return;
            }
            _playlistRefreshPending = false;
            GetPlaylistAsync().FireAndForget(OnException);
        }

        private void CleanupPlaylistRefresh()
        {
            if (_playlistRefreshTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= PlaylistRefreshTimer_Tick;
                _playlistRefreshTimer = null;
            }
            _playlistRefreshPending = false;
        }

        private async Task GetPlaylistAsync()
        {
            // _videoWidth/_videoHeight are plain uints written on the UI
            // thread: reads here stay atomic and at worst one refresh stale.
            var videoWidth = _videoWidth;
            var videoHeight = _videoHeight;
            var items = await Task.Run(() => _mediaPlayer.GetPlaylist().Select(x => new PlaylistItem(x)
            {
                ResolutionText = x.IsCurrent && videoWidth > 0
                    ? $" · {videoWidth}×{videoHeight}"
                    : string.Empty,
            }).ToList());
            ApplyPlaylistDiff(items ?? []);
            SelectCurrentPlayListItem();

            // The queue is another view of this same collection; flips between
            // its rows and its empty message as the collection grows or empties.
            RefreshQueueChrome();
        }

        /// <summary>
        /// Updates the two playlist collections by diff instead of clearing
        /// and re-adding every entry: stale ids are removed, moved ids are
        /// relocated, and only changed entries are replaced in place. Clearing
        /// the collection would reset the scroll position and drop the
        /// selection on every mpv event.
        /// </summary>
        private void ApplyPlaylistDiff(IReadOnlyList<PlaylistItem> newItems)
        {
            var newIds = new HashSet<int>(newItems.Count);
            foreach (var item in newItems)
            {
                newIds.Add(item.Id);
            }

            // Remove entries that no longer exist in mpv's playlist.
            for (int i = _allPlaylistItems.Count - 1; i >= 0; i--)
            {
                if (!newIds.Contains(_allPlaylistItems[i].Id))
                {
                    _allPlaylistItems.RemoveAt(i);
                    PlaylistItems.RemoveAt(i);
                }
            }

            var insertAt = 0;
            foreach (var item in newItems)
            {
                if (insertAt < _allPlaylistItems.Count
                    && _allPlaylistItems[insertAt].Id == item.Id)
                {
                    if (PlaylistItemChanged(_allPlaylistItems[insertAt], item))
                    {
                        _allPlaylistItems[insertAt] = item;
                        PlaylistItems[insertAt] = item;
                    }
                    insertAt++;
                    continue;
                }

                var found = -1;
                for (int j = insertAt + 1; j < _allPlaylistItems.Count; j++)
                {
                    if (_allPlaylistItems[j].Id == item.Id)
                    {
                        found = j;
                        break;
                    }
                }

                if (found >= 0)
                {
                    var existing = _allPlaylistItems[found];
                    _allPlaylistItems.RemoveAt(found);
                    PlaylistItems.RemoveAt(found);
                    _allPlaylistItems.Insert(insertAt, existing);
                    PlaylistItems.Insert(insertAt, existing);
                    if (PlaylistItemChanged(existing, item))
                    {
                        _allPlaylistItems[insertAt] = item;
                        PlaylistItems[insertAt] = item;
                    }
                }
                else
                {
                    _allPlaylistItems.Insert(insertAt, item);
                    PlaylistItems.Insert(insertAt, item);
                }
                insertAt++;
            }

            // Drop any leftovers beyond the new playlist length.
            while (_allPlaylistItems.Count > insertAt)
            {
                _allPlaylistItems.RemoveAt(_allPlaylistItems.Count - 1);
                PlaylistItems.RemoveAt(PlaylistItems.Count - 1);
            }
        }

        private static bool PlaylistItemChanged(PlaylistItem a, PlaylistItem b)
        {
            return a.Id != b.Id
                || a.IsCurrent != b.IsCurrent
                || a.Title != b.Title
                || a.Path != b.Path
                || a.Duration != b.Duration
                || a.ResolutionText != b.ResolutionText;
        }

        /// <summary>
        /// Scrolls the queue to the row that is on air.
        ///
        /// The playlist used to have two renderings - the play queue section and
        /// a right-hand pane - and this method kept the pane in step. The pane
        /// is gone, so there is one target left, and it is only touched while
        /// the queue section is the surface on screen: scrolling a collapsed
        /// list would be work with no observer.
        /// </summary>
        private void SelectCurrentPlayListItem()
        {
            if (_librarySection != LibrarySectionQueue || !_libraryVisible)
            {
                return;
            }

            var currentIndex = -1;
            for (int i = 0; i < PlaylistItems.Count; i++)
            {
                if (PlaylistItems[i].IsCurrent)
                {
                    currentIndex = i;
                    break;
                }
            }

            // Only scroll when the current row actually changed; playlist
            // refresh events otherwise keep re-scrolling the same row and
            // fight the user's own scrolling.
            if (currentIndex == _lastCurrentPlaylistIndex)
            {
                return;
            }
            _lastCurrentPlaylistIndex = currentIndex;
            if (currentIndex < 0)
            {
                return;
            }

            // No SelectedIndex: the queue's SelectionMode is None, and the
            // now-playing row is marked by the template's own IsCurrent
            // highlight rather than by selection.
            QueueView.ScrollIntoView(PlaylistItems[currentIndex], ScrollIntoViewAlignment.Leading);
        }

        private async Task ImportPlaylistAsync()
        {
            var file = await FilePickerHelper.PickSingleFileAsync(picker =>
            {
                picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                picker.FileTypeFilter.Add(".m3u");
                picker.FileTypeFilter.Add(".m3u8");
            });
            if (string.IsNullOrEmpty(file?.Path))
            {
                return;
            }
            // mpv's loadlist replaces the current playlist with the file's
            // entries (relative paths resolve against the m3u's directory).
            _mediaPlayer.Command(["osd-auto", "loadlist", file.Path]);
            RefreshPlaylistAsync();
        }

        private async Task ExportPlaylistAsync()
        {
            if (PlaylistItems.Count == 0)
            {
                return;
            }
            var file = await FilePickerHelper.PickSaveFileAsync(picker =>
            {
                picker.SuggestedFileName = "playlist.m3u";
                picker.FileTypeChoices.Add("M3U Playlist", [".m3u"]);
            });
            if (file is null)
            {
                return;
            }
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("#EXTM3U");
            foreach (var item in PlaylistItems)
            {
                // Line breaks inside a title would split the EXTINF record;
                // normalize them to spaces before writing.
                var title = item.Title.Replace('\r', ' ').Replace('\n', ' ');
                builder.AppendLine($"#EXTINF:-1,{title}");
                builder.AppendLine(item.Path);
            }
            await System.IO.File.WriteAllTextAsync(file.Path, builder.ToString());
            AppContext.AppLogger.Info($"exported playlist ({PlaylistItems.Count} items): {file.Path}");
        }

        // ------------------------------------------------------- play queue

        /// <summary>
        /// The play queue page's row click: start playing the clicked entry.
        /// </summary>
        private void QueueView_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (e.ClickedItem is PlaylistItem playlistItem)
                {
                    _mediaPlayer.PlaylistPlayIndex(playlistItem.Index);
                }
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

        /// <summary>
        /// The queue page's Clear button.
        ///
        /// mpv's own <c>playlist-clear</c> empties the playlist but keeps the
        /// currently playing entry, which is what "clear the queue" should mean
        /// - it drops what is queued next without stopping what is on screen,
        /// the same way the official player's queue behaves. The alternative,
        /// removing every entry by index in reverse, would also stop the
        /// current file, which is not what a Clear button should do.
        /// </summary>
        private void QueueClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaPlayer.Command("playlist-clear");
                RefreshPlaylistAsync();
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

        /// <summary>
        /// The queue page's Add to button.
        ///
        /// A multi-select file picker whose picks join the end of the queue
        /// without interrupting playback, through the same
        /// <c>loadfile &lt;path&gt; append</c> path the rest of the app already
        /// uses (<see cref="MpvPlayerExtensions.Open"/>, OpenMode.Append).
        /// </summary>
        private async Task AddToQueueAsync()
        {
            // The lambda parameter is typed to pick the multi-file overload;
            // the two PickFolderAsync helpers take the same shape otherwise and
            // the call would not resolve.
            var files = await FilePickerHelper.PickFolderAsync((FileOpenPicker picker) =>
            {
                picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            });

            if (files is null || files.Count == 0)
            {
                return;
            }

            var items = new List<FileItem>(files.Count);
            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(file?.Path))
                {
                    items.Add(new FileItem(file.Path));
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            await _mediaPlayer.OpenAsync(items, OpenMode.Append);
            RefreshPlaylistAsync();
        }

        private void QueueAddTo_Click(object sender, RoutedEventArgs e)
        {
            AddToQueueAsync().FireAndForget(OnException);
        }

        private void MpvPlayerPage_PlaylistChanged()
        {
            // Unconditional now. The playlist used to be refreshed only while
            // one of its two old surfaces was on screen; the right-hand pane is
            // gone and the remaining one - the play queue - is not the only
            // reader: the shell's tab strip mirrors the same collection all the
            // time, so a refresh that skipped the hidden case would leave the
            // tabs showing a playlist mpv no longer has. The 100ms coalescing
            // timer is what keeps this cheap.
            DispatcherQueue.RunAsync(RefreshPlaylistAsync);
        }

        /// <summary>
        /// Shows the play queue: the playlist, in the section the navigation
        /// pane calls "queue".
        ///
        /// This is what the old right-hand playlist pane turned into. The pane
        /// was a second rendering of one collection in a resizable column, and
        /// keeping the video in a three-star column beside it is what produced
        /// the split layouts this round removes. The queue section shows the
        /// same rows with the page's own width, so there is one place a
        /// playlist can appear.
        /// </summary>
        internal void ShowPlayQueue()
        {
            SetLibraryVisible(true);
            ShowLibrarySection(LibrarySectionQueue);
        }

        private void QueueView_ContextRequested(UIElement sender, ContextRequestedEventArgs args) =>
            ShowPlaylistContextMenu(QueueView, args);

        /// <summary>
        /// The playlist row menu, shared by the queue section and the side panel.
        ///
        /// One implementation with the view passed in, because the two views are
        /// two windows onto one collection: an action that existed in one and not
        /// the other would be a bug nobody could see from the code that owns it.
        /// The menu is anchored on the view that was right-clicked, which is why
        /// the position is asked of it rather than of QueueView.
        /// </summary>
        private void ShowPlaylistContextMenu(ListView view, ContextRequestedEventArgs args)
        {
            if (FindPlaylistItem(args.OriginalSource as DependencyObject) is not { } item)
            {
                return;
            }

            _contextMenuItem = item;
            var count = PlaylistItems.Count;
            var index = item.Index;

            if (Resources["PlaylistContextMenu"] is MenuFlyout flyout)
            {
                foreach (var flyoutItem in flyout.Items)
                {
                    if (flyoutItem is MenuFlyoutItem menuItem && menuItem.Tag is string tag)
                    {
                        menuItem.IsEnabled = tag switch
                        {
                            "move-up" or "move-top" => index > 0,
                            "move-down" or "move-bottom" => index < count - 1,
                            _ => true,
                        };
                    }
                }

                if (args.TryGetPosition(view, out var point))
                {
                    flyout.ShowAt(view, point);
                }
                else
                {
                    flyout.ShowAt(view);
                }
                args.Handled = true;
            }
        }

        /// <summary>The row the queue's context menu was opened on.</summary>
        private PlaylistItem _contextMenuItem;

        private void PlaylistMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: string action })
            {
                var item = _contextMenuItem;
                try
                {
                    switch (action)
                    {
                        case "play":
                            _mediaPlayer.PlaylistPlayIndex(item.Index);
                            break;
                        case "move-up":
                            MovePlaylistItem(item.Index, item.Index - 1);
                            break;
                        case "move-down":
                            MovePlaylistItem(item.Index, item.Index + 1);
                            break;
                        case "move-top":
                            MovePlaylistItem(item.Index, 0);
                            break;
                        case "move-bottom":
                            MovePlaylistItem(item.Index, PlaylistItems.Count - 1);
                            break;
                        case "remove":
                            _mediaPlayer.PlaylistRemove(item.Index);
                            RefreshPlaylistAsync();
                            break;
                        case "copy-title":
                            ClipboardHelper.SetCopyText(item.Title);
                            break;
                        case "copy-path":
                            ClipboardHelper.SetCopyText(item.Path);
                            break;
                        case "open-location":
                            _ = FileLauncher.ShellLaunchFileAsync(item.Path);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    OnException(ex);
                }
            }
        }

        private void MovePlaylistItem(int from, int visualTo)
        {
            // mpv's playlist-move(i, j) removes entry i first, then inserts it
            // at j-1 when i < j (verified over the IPC pipe). To place the item
            // at a visual row index we must compensate: when moving downward,
            // ask for one position past the visual target.
            int mpvTarget = visualTo + (from < visualTo ? 1 : 0);
            _mediaPlayer.PlaylistMove(from, mpvTarget);
            RefreshPlaylistAsync();
        }

        // Manual drag-reorder, on the queue's rows. WinUI 3 ListView's built-in
        // drag/reorder (CanDragItems/CanReorderItems) is unreliable for mouse in
        // desktop mode, so the drag is driven from the pointer events below: a
        // press on a row followed by a move past the threshold marks the drag
        // active, and release moves the item to the row under the cursor (or to
        // the end when dropped on empty space). A plain press+release stays a
        // click and is handled by ItemClick.
        private const double DragThreshold = 12.0;
        private int _dragSourceIndex = -1;
        private Point _dragStartPoint;
        private bool _dragActive;
        private bool _dragSetup;

        private static PlaylistItem? FindPlaylistItem(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is FrameworkElement { DataContext: PlaylistItem item })
                {
                    return item;
                }
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
        }

        private void QueueView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }
            var item = FindPlaylistItem(e.OriginalSource as DependencyObject);
            if (item is null)
            {
                return;
            }
            _dragSourceIndex = item.Value.Index;
            _dragStartPoint = e.GetCurrentPoint(QueueView).Position;
            _dragActive = false;
        }

        private void QueueView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragSourceIndex < 0)
            {
                return;
            }
            var position = e.GetCurrentPoint(QueueView).Position;
            if (!_dragActive)
            {
                var delta = new Point(position.X - _dragStartPoint.X, position.Y - _dragStartPoint.Y);
                if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < DragThreshold)
                {
                    return;
                }
                _dragActive = true;
                // No CapturePointer here: on WinUI 3 desktop capturing the
                // pointer from an injected mouse stopped subsequent moves from
                // reaching the view (observed via instrumentation). A short
                // within-list drag does not need capture.
            }
        }

        private void QueueView_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragSourceIndex < 0)
            {
                return;
            }
            int from = _dragSourceIndex;
            _dragSourceIndex = -1;
            if (!_dragActive)
            {
                return; // a plain click; ItemClick handles it
            }
            _dragActive = false;
            if (QueueView.PointerCaptures?.Count > 0)
            {
                QueueView.ReleasePointerCaptures();
            }

            var target = FindPlaylistItem(e.OriginalSource as DependencyObject);
            int to = target is { } t ? t.Index : PlaylistItems.Count - 1;
            if (from == to)
            {
                return;
            }

            // MovePlaylistItem applies mpv's downward-shift compensation; the
            // visual row index is passed as-is.
            MovePlaylistItem(from, to);
        }
    }
}
