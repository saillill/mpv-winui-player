using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings;
using System;
using System.Collections.Generic;
using Windows.Graphics;

namespace mpv_winui
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SetupStyle();

            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            ShellTitleBar.Title = PackageHelper.AppName;
            SetTitleBar(ShellTitleBar);

            AppWindow.Title = PackageHelper.AppName;
            AppWindow.SetIcon("App.ico");
            var customTitle = AppContext.AppSetting.WindowTitle;
            if (!string.IsNullOrWhiteSpace(customTitle))
            {
                ShellTitleBar.Title = customTitle;
                AppWindow.Title = customTitle;
            }

            SetupWindowSize();

            Closed += Window_Closed;
            AppContext.SettingChanged += MainWindow_SettingChanged;
        }

        public async void Open()
        {
            IReadOnlyList<FileItem>? fileItems = null;
            try
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs);
            }
            catch (System.Exception ex)
            {
                AppContext.AppLogger.Error(ex);
            }

            ShellFrame.Navigate(typeof(MpvPlayerPage), fileItems);
        }

        public async void Refresh(AppActivationArguments activatedArgs)
        {
            try
            {
                var fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs);
                if (fileItems?.Count > 0)
                {
                    DispatcherQueue.RunAsync(() =>
                    {
                        if (ShellFrame?.Content is IParameterRefreshSupportView view)
                        {
                            view.OnRefresh(fileItems);
                            this.ShowWindow();
                        }
                    });
                }
            }
            catch (System.Exception ex)
            {
                AppContext.AppLogger.Error(ex);
            }
        }

        /// <summary>Rebuilds the player page's menu bar (after menu definition or language changes).</summary>
        public void RebuildPlayerMenuBar()
        {
            if (ShellFrame?.Content is MpvPlayerPage page)
            {
                page.RebuildMenuBar();
            }
        }

        public void ChangeFullWindow(bool full)
        {            if (full)
            {
                TitleBarRow.Height = new GridLength(0);
            }
            else
            {
                TitleBarRow.Height = GridLength.Auto;
            }
        }

        private string _lastMediaTitle = PackageHelper.AppName;

        public void UpdateTitle(string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                _lastMediaTitle = title;
            }

            var custom = AppContext.AppSetting.WindowTitle;
            var effective = string.IsNullOrWhiteSpace(custom) ? _lastMediaTitle : custom;
            ShellTitleBar?.Title = effective;
            AppWindow.Title = effective;
        }

        private SettingsWindow? _settingsWindow;
        public void OpenSettingWindow()
        {
            if (null == _settingsWindow)
            {
                _settingsWindow = new();
                _settingsWindow?.Activate();
                _settingsWindow?.Closed += SettingsWindow_Closed;
            }

            var position = AppWindow.Position;
            var size = AppWindow.Size;
            var rect = new RectInt32(
                (int)(position.X + (size.Width * 0.1)),
                (int)(position.Y + (size.Height * 0.1)),
                (int)(size.Width * 0.8),
                (int)(size.Height * 0.8)
                );
            _settingsWindow?.MoveAndResize(rect);

            _settingsWindow?.ShowWindow();
        }

        private void SettingsWindow_Closed(object sender, WindowEventArgs args)
        {
            _settingsWindow = null;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            AppContext.SettingChanged -= MainWindow_SettingChanged;
            _settingsWindow?.Close();
            CleanupStyle();
            AppContext.AppSetting.Flush();
        }
    }
}
