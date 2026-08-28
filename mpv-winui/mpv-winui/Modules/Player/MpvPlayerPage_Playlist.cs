using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
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

        /// <summary>Active playlist filter; the row highlight control binds to this.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>Media duration in seconds, or &lt;= 0 when unknown.</summary>
        public readonly double Duration => item.Duration;

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
        public ObservableCollection<PlaylistItem> PlaylistItems { get; } = [];

        /// <summary>Visible playlist rows (PlaylistItems filtered by <see cref="PlaylistFilterBox"/>).</summary>
        public ObservableCollection<PlaylistItem> FilteredPlaylistItems { get; } = [];

        private readonly List<PlaylistItem> _allPlaylistItems = [];
        private string _playlistFilter = "";
        private bool _resizingPlaylist;
        private double _resizeStartWidth;
        private double _resizeStartX;
        private DispatcherQueueTimer? _playlistRefreshTimer;
        private bool _playlistRefreshPending;
        private int _lastCurrentPlaylistIndex = -1;

        private void RefreshPlaylistAsync()
        {
            // Coalesce bursty mpv playlist events into one refresh.
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

        private void PlaylistRefreshTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
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
            var items = await Task.Run(() => _mediaPlayer.Playlist().Select(x => new PlaylistItem(x)).ToList());
            ApplyPlaylistDiff(items ?? []);
            ApplyPlaylistFilter();
            SelectCurrentPlayListItem();
            SetupPlaylistDrag();
        }

        /// <summary>
        /// Updates the two playlist collections by diff instead of clearing
        /// and re-adding every entry: stale ids are removed, moved ids are
        /// relocated, and only changed entries are replaced in place.
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
                || a.Query != b.Query;
        }

        private void ApplyPlaylistFilter()
        {
            var filter = _playlistFilter.Trim();
            var desired = new List<PlaylistItem>();
            if (filter.Length == 0)
            {
                foreach (var item in _allPlaylistItems)
                {
                    desired.Add(item);
                }
            }
            else
            {
                foreach (var item in _allPlaylistItems)
                {
                    if (item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || item.Filename.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || item.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    {
                        var copy = item;
                        copy.Query = filter;
                        desired.Add(copy);
                    }
                }
            }
            SyncFilteredList(desired);
        }

        /// <summary>
        /// Applies the filtered result to the visible collection by diff
        /// (add/remove/update in place) instead of clearing and re-adding
        /// every row on each keystroke.
        /// </summary>
        private void SyncFilteredList(IReadOnlyList<PlaylistItem> desired)
        {
            var insertAt = 0;
            foreach (var item in desired)
            {
                if (insertAt < FilteredPlaylistItems.Count
                    && FilteredPlaylistItems[insertAt].Id == item.Id)
                {
                    if (PlaylistItemChanged(FilteredPlaylistItems[insertAt], item))
                    {
                        FilteredPlaylistItems[insertAt] = item;
                    }
                    insertAt++;
                    continue;
                }

                var found = -1;
                for (int j = insertAt + 1; j < FilteredPlaylistItems.Count; j++)
                {
                    if (FilteredPlaylistItems[j].Id == item.Id)
                    {
                        found = j;
                        break;
                    }
                }

                if (found >= 0)
                {
                    var existing = FilteredPlaylistItems[found];
                    FilteredPlaylistItems.RemoveAt(found);
                    FilteredPlaylistItems.Insert(insertAt, existing);
                    if (PlaylistItemChanged(existing, item))
                    {
                        FilteredPlaylistItems[insertAt] = item;
                    }
                }
                else
                {
                    FilteredPlaylistItems.Insert(insertAt, item);
                }
                insertAt++;
            }

            while (FilteredPlaylistItems.Count > insertAt)
            {
                FilteredPlaylistItems.RemoveAt(FilteredPlaylistItems.Count - 1);
            }
        }

        private void PlaylistFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _playlistFilter = PlaylistFilterBox.Text;
            ApplyPlaylistFilter();
            SelectCurrentPlayListItem(force: true);
        }

        private void SelectCurrentPlayListItem(bool force = false)
        {
            var currentIndex = -1;
            for (int i = 0; i < FilteredPlaylistItems.Count; i++)
            {
                if (FilteredPlaylistItems[i].IsCurrent)
                {
                    currentIndex = i;
                    break;
                }
            }

            // Only scroll when the current row actually changed; playlist
            // refresh events otherwise keep re-scrolling the same row.
            if (!force && currentIndex == _lastCurrentPlaylistIndex)
            {
                return;
            }
            _lastCurrentPlaylistIndex = currentIndex;
            if (currentIndex < 0)
            {
                return;
            }

            PlaylistView.SelectedIndex = currentIndex;
            PlaylistView.ScrollIntoView(FilteredPlaylistItems[currentIndex], ScrollIntoViewAlignment.Leading);
        }

        private void ClosePlaylist_Click(object sender, RoutedEventArgs e)
        {
            TogglePlaylist();
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

        private async void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
        {
            RefreshPlaylistAsync();
        }

        private void PlaylistView_ItemClick(object sender, ItemClickEventArgs e)
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
                // The playlist may have changed between render and click,
                // leaving a stale index; surface instead of crashing.
                OnException(ex);
            }
        }

        private bool NeedUpdatePlaylist()
        {
            return PlaylistContainer.Visibility == Visibility.Visible;
        }

        private void MpvPlayerPage_PlaylistChanged(MpvMediaPlayer player, object? arg2)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (NeedUpdatePlaylist())
                {
                    RefreshPlaylistAsync();
                }
            });
        }

        private void TogglePlaylist(bool refresh = false)
        {
            if (PlaylistContainer.Visibility == Visibility.Collapsed)
            {
                VisualStateManager.GoToState(this, "ShowPlaylist", true);
                PlaylistColumn.Width = new GridLength(GetPlaylistWidth());
                if (refresh)
                {
                    RefreshPlaylistAsync();
                }
            }
            else
            {
                PlaylistColumn.Width = new GridLength(0);
                VisualStateManager.GoToState(this, "HidePlaylist", true);
            }
        }

        private static double GetPlaylistWidth()
        {
            var saved = AppContext.AppSetting.PlaylistWidth;
            return saved is >= 280 and <= 420 ? saved : 320;
        }

        private void PlaylistResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            _resizingPlaylist = true;
            _resizeStartWidth = PlaylistColumn.ActualWidth;
            _resizeStartX = e.GetCurrentPoint(PageRoot).Position.X;
            PlaylistResizeGrip.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void PlaylistResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_resizingPlaylist)
            {
                return;
            }

            var x = e.GetCurrentPoint(PageRoot).Position.X;
            PlaylistColumn.Width = new GridLength(Math.Clamp(_resizeStartWidth + (_resizeStartX - x), 280, 420));
            e.Handled = true;
        }

        private void PlaylistResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_resizingPlaylist)
            {
                return;
            }

            _resizingPlaylist = false;
            if (PlaylistResizeGrip.PointerCaptures?.Count > 0)
            {
                PlaylistResizeGrip.ReleasePointerCaptures();
            }
            AppContext.AppSetting.PlaylistWidth = (int)PlaylistColumn.ActualWidth;
            e.Handled = true;
        }

        private void PlaylistView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.OriginalSource is FrameworkElement fe && fe.DataContext is PlaylistItem item)
            {
                PlaylistView.SelectedItem = item;
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

                    if (args.TryGetPosition(PlaylistView, out var point))
                    {
                        flyout.ShowAt(PlaylistView, point);
                    }
                    else
                    {
                        flyout.ShowAt(PlaylistView);
                    }
                    args.Handled = true;
                }
            }
        }

        private void PlaylistMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: string action } && PlaylistView.SelectedItem is PlaylistItem item)
            {
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

        // Manual drag-reorder. WinUI 3 ListView's built-in drag/reorder
        // (CanDragItems/CanReorderItems) is unreliable for mouse in desktop
        // mode, so the drag is driven from the pointer events below: a press
        // on a row followed by a move past the threshold captures the pointer,
        // and release moves the item to the row under the cursor (or to the
        // end when dropped on empty space). A plain press+release stays a
        // click and is handled by ItemClick.
        private const double DRAG_THRESHOLD = 12.0;
        private int _dragSourceIndex = -1;
        private Point _dragStartPoint;
        private bool _dragActive;
        private bool _dragSetup;

        private void SetupPlaylistDrag()
        {
            if (_dragSetup)
            {
                return;
            }
            _dragSetup = true;
            // Handlers are attached to the container, not the ListView:
            // ListViewItem captures the pointer on press, and captured
            // pointer events are routed to the capturer's ancestor chain —
            // they never reach a handler on the ListView itself. The
            // container is on that chain, so move/release still arrive.
            PlaylistContainer.AddHandler(PointerPressedEvent, new PointerEventHandler(PlaylistView_PointerPressed), true);
            PlaylistContainer.AddHandler(PointerMovedEvent, new PointerEventHandler(PlaylistView_PointerMoved), true);
            PlaylistContainer.AddHandler(PointerReleasedEvent, new PointerEventHandler(PlaylistView_PointerReleased), true);
            PlaylistContainer.AddHandler(PointerCanceledEvent, new PointerEventHandler(PlaylistView_PointerReleased), true);
            PlaylistContainer.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(PlaylistView_PointerReleased), true);
        }

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

        private void PlaylistView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                return;
            }
            var item = FindPlaylistItem(e.OriginalSource as DependencyObject);
            if (item is null)
            {
                return;
            }
            _dragSourceIndex = item.Value.Index;
            _dragStartPoint = e.GetCurrentPoint(PlaylistView).Position;
            _dragActive = false;
            PlaylistView.SelectedItem = item.Value;
        }

        private void PlaylistView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragSourceIndex < 0)
            {
                return;
            }
            var position = e.GetCurrentPoint(PlaylistView).Position;
            if (!_dragActive)
            {
                var delta = new Point(position.X - _dragStartPoint.X, position.Y - _dragStartPoint.Y);
                if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < DRAG_THRESHOLD)
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

        private void PlaylistView_PointerReleased(object sender, PointerRoutedEventArgs e)
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
            if (PlaylistView.PointerCaptures?.Count > 0)
            {
                PlaylistView.ReleasePointerCaptures();
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
