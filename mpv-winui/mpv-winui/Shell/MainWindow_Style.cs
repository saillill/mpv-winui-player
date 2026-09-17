using mpv_winui.Modules.Common.View;

namespace mpv_winui
{
    public sealed partial class MainWindow
    {
        public override void UpdateTheme()
        {
            base.UpdateTheme();
            _windowsManager.UpdateTheme();
        }

        public override void UpdateBackdrop()
        {
            base.UpdateBackdrop();
            _windowsManager.UpdateBackdrop();
        }

        private void MainWindow_SettingChanged(string key, object? value)
        {
            if (key == nameof(AppContext.AppSetting.BackdropType))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateBackdrop();
                    _settingsWindow?.UpdateBackdrop();
                });
            }
            else if (key == nameof(AppContext.AppSetting.WindowPiP)
                || key == nameof(AppContext.AppSetting.WindowPiPSize))
            {
                DispatcherQueue.TryEnqueue(() => ApplyPiP());
            }
            else if (key == nameof(AppContext.AppSetting.UiFont))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateUiFont();
                    _settingsWindow?.UpdateUiFont();
                });
            }
            else if (key == nameof(AppContext.AppSetting.ControlBarLayout))
            {
                // The centered layout needs a wider minimum; re-apply so the
                // window cannot be squeezed below the bar's comfortable width.
                DispatcherQueue.TryEnqueue(() =>
                {
                    this.SetWindowMinSize(GetMinLogicalWidth(), MIN_LOGICAL_HEIGHT);
                });
            }
        }
    }
}
