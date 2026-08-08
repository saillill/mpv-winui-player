using Microsoft.UI.Xaml;
using mpv_winui.Modules.Common.View;

namespace mpv_winui
{
    public sealed partial class MainWindow : Window
    {
        private WindowStyleManager? _styleManager;

        private void SetupStyle()
        {
            _styleManager = new WindowStyleManager(this);
            _styleManager?.Setup();
        }

        private void CleanupStyle()
        {
            _styleManager?.Dispose();
            _styleManager = null;
        }

        public void UpdateCurrentTheme()
        {
            var theme = _styleManager?.GetThemeType();
            if (theme is not null)
            {
                _styleManager?.UpdateTheme(theme.Value);
            }

            _settingsWindow?.UpdateCurrentTheme();
        }

        private void MainWindow_SettingChanged(string key, object? value)
        {
            if (key == nameof(AppContext.AppSetting.BackdropType)
                || key == nameof(AppContext.AppSetting.ThemeAccentColor)
                || key == nameof(AppContext.AppSetting.ThemeOpacity)
                || key == nameof(AppContext.AppSetting.ThemeLuminosity))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateBackdrop();
                    _settingsWindow?.UpdateBackdrop();
                });
            }
            else if (key == nameof(AppContext.AppSetting.UiFont))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateUiFont();
                    _settingsWindow?.UpdateUiFont();
                });
            }
        }
    }
}
