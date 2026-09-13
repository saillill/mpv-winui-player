using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.MediaInfo;
using mpv_winui.Modules.Menu.MenuEditor;
using mpv_winui.Modules.MpvConf;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings;
using System;
using System.Collections.Generic;

namespace mpv_winui
{
    public sealed partial class MainWindow : BaseWindow
    {
        private readonly WindowsManager _windowsManager;

        public MainWindow()
        {
            InitializeComponent();

            SetupStyle();

            _windowsManager = new WindowsManager();

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
            AppContext.AppLogger.Debug("MainWindow.Open enter");
            IReadOnlyList<FileItem>? fileItems = null;
            try
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs);
                AppContext.AppLogger.Debug($"MainWindow.Open parsed {fileItems?.Count ?? 0} item(s)");
                // Inside the try: async void — a Navigate throw here would be
                // an unhandled exception and take the app down at launch.
                ShellFrame.Navigate(typeof(MpvPlayerPage), fileItems);
            }
            catch (System.Exception ex)
            {
                AppContext.AppLogger.Error(ex);
            }
        }

        public async void Refresh(AppActivationArguments activatedArgs)
        {
            AppContext.AppLogger.Debug($"MainWindow.Refresh enter, kind={activatedArgs.Kind}");
            try
            {
                var fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs, includeProcessArgv: false);
                if (fileItems?.Count > 0)
                {
                    DispatcherQueue.RunAsync(() =>
                    {
                        if (ShellFrame?.Content is IParameterRefreshSupportView view)
                        {
                            view.OnRefresh(fileItems);
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

        public void OpenSettingWindow()
        {
            _windowsManager.Open("settings", () => new SettingsWindow(), this, 0.8, 320, 200);
        }

        public void OpenMpvConfigWindow()
        {
            _windowsManager.Open("mpvconf", () => new MpvConfEditorWindow(), this, 0.8, 480, 320);
        }

        public void OpenMediaInfoWindow(string? path)
        {
            _windowsManager.Open("mediainfo_" + HashUtil.ComputeMd5(path ?? string.Empty), () => new MediaInfoWindow(path), this, 0.8, 480, 320);
        }

        public void OpenMenuEditorWindow(string filePath, MenuType type)
        {
            _windowsManager.Open("menueditor_" + HashUtil.ComputeMd5(filePath ?? string.Empty), () => new MenuEditorWindow(filePath ?? string.Empty, type), this, 0.8, 720, 480);
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
