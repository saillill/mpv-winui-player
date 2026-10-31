using mpv_winui.Infrastructure.Common.Media;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Domains.Playlist;
using System;
using System.Threading.Tasks;

namespace mpv_winui.Domains.Playback
{
    public sealed partial class MpvPlayerPage
    {
        private WeakReference<ContentDialog>? _watchHistoryDialog;
        private WeakReference<ContentDialog>? _watchLaterDialog;

        private async Task ShowWatchHistoryDialogAsync()
        {
            var control = new WatchHistoryControl();
            control.Initialize(_mediaPlayer.GetWatchHistoryPath(), _mediaPlayer.SaveWatchHistory(), OnException, _logger);
            control.ItemClick += WatchHistoryControl_ItemClick;

            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.WatchHistoryDialogTitle,
                Content = control,
                CloseButtonText = AppContext.AppLang.Close,
                XamlRoot = XamlRoot
            };
            _watchHistoryDialog = new WeakReference<ContentDialog>(dialog);
            await dialog.ShowAsync();

            control.ItemClick -= WatchHistoryControl_ItemClick;
        }

        private async Task ShowWatchLaterDialogAsync()
        {
            var control = new WatchLaterControl();
            control.Initialize(_mediaPlayer.GetWatchLaterFolderPath(), OnException, _logger);
            control.ItemClick += WatchLaterControl_ItemClick;
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.WatchLaterDialogTitle,
                Content = control,
                CloseButtonText = AppContext.AppLang.Close,
                XamlRoot = XamlRoot
            };
            _watchLaterDialog = new WeakReference<ContentDialog>(dialog);
            await dialog.ShowAsync();

            control.ItemClick -= WatchLaterControl_ItemClick;
        }

        private void WatchHistoryControl_ItemClick(object? sender, string path)
        {
            if (_watchHistoryDialog?.TryGetTarget(out var dialog) == true)
            {
                dialog.Hide();
                _watchHistoryDialog = null;
            }

            // A row is an explicit "play this", the same as a card click: without
            // this the file loads behind a library that is still up, because the
            // session counts as withdrawn once the user has pressed back once.
            ShowVideoForNextOpen();

            _mediaPlayer.OpenAsync(new FileItem(path)).FireAndForget(OnException);
        }

        private void WatchLaterControl_ItemClick(object? sender, string path)
        {
            if (_watchLaterDialog?.TryGetTarget(out var dialog) == true)
            {
                dialog.Hide();
                _watchLaterDialog = null;
            }

            ShowVideoForNextOpen();

            _mediaPlayer.OpenAsync(new FileItem(path)).FireAndForget(OnException);
        }
    }
}
