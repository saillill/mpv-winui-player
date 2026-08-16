using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Settings;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage : Page, IParameterRefreshSupportView
    {
        private static readonly Logger _logger = LogManager.GetLogger("MpvPlayer");
        private const string MpvConfigFolderName = "mpv";
        private readonly MpvMediaPlayer _mediaPlayer = new();
        private bool _isPlayerInitialized;
        private static WeakReference<MpvPlayerPage>? _selfWeakReference;

        private readonly AppWindow _appWindow;

        private Task? _task;
        private bool _hdrModeAppliedOnce;

        public MpvPlayerPage()
        {
            _selfWeakReference = new(this);
            _appWindow = App.Window?.AppWindow!;
            InitializeComponent();
            ApplyLocalizedStrings();

            _task = CreateAsync();

            Loaded += MpvPlayerPage_Loaded;
            Unloaded += MpvPlayerPage_Unloaded;
        }

        private async void MpvPlayerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_task is { } task)
            {
                try
                {
                    await task;
                    _task = null;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }

            if (_isPlayerInitialized)
            {
                SetupPlayerView();

                PlayerControl.MediaPlayer = _mediaPlayer;

                _mediaPlayer.PlaylistChanged += MpvPlayerPage_PlaylistChanged;
                _mediaPlayer.VolumeChangedChanged += VolumeChangedChanged;
                _mediaPlayer.WindowChanged += MpvPlayerPage_WindowChanged;
                _mediaPlayer.MediaInfoChanged += MpvPlayerPage_MediaInfoChanged;
                _mediaPlayer.StartListen();

                AppContext.RunMpvCommand = cmd => _ = _mediaPlayer.EnqueueCommand(cmd);
                AppContext.SetMpvLogLevel = level => _mediaPlayer.SetLogLevel(level);
                AppContext.GetAudioDevices = () => _mediaPlayer.AudioDevices();
                AppContext.GetGpuAdapters = () => _mediaPlayer.GpuAdapters();
                AppContext.GetMpvProfiles = () => _mediaPlayer.Profiles();
                // LoggerHelper may have run before this hook existed; sync the
                // native mpv log level once the player is available.
                AppContext.SetMpvLogLevel(AppContext.AppSetting.EnableDebugLog ? "info" : "warn");
                var lang = AppContext.AppSetting.CurrentLanguage;
                if (string.IsNullOrWhiteSpace(lang)) lang = "en-US";
                AppContext.SendMpvCommand($"no-osd set user-data/mpvw/language {lang}");
                var mpvCli = Path.Combine(System.AppContext.BaseDirectory, "mpv.exe");
                if (File.Exists(mpvCli))
                {
                    // mpv_command_string parses C-style escapes: Windows paths
                    // need doubled backslashes inside the quotes.
                    var escapedCli = mpvCli.Replace("\\", "\\\\");
                    AppContext.SendMpvCommand($"no-osd set user-data/mpvw/mpv-exe \"{escapedCli}\"");
                }
                var applyCommands = MpvSettings.BuildApplyAllCommands();
                if (applyCommands.Count > 0)
                {
                    await _mediaPlayer.EnqueueCommands(applyCommands);
                }

                // Playback speed is a persistent setting but is intentionally
                // not part of ApplyAll (it would clobber the live session on a
                // settings reset); apply it here once at startup so the saved
                // value takes effect on launch.
                if (AppContext.AppSetting.Speed is not 1.0)
                {
                    AppContext.SendMpvCommand($"no-osd set speed {AppContext.AppSetting.Speed}");
                }
                await _mediaPlayer.DrainCommandsAsync();
                // hdr_auto registers its script-message handler after mpv
                // scripts load; the startup batch races that registration, so
                // the saved HDR mode is applied once the first file loads.
                _mediaPlayer.MediaOpened += MpvPlayerPage_ApplyHdrModeOnce;

                SetupKeyboardInput();
                AppContext.SettingChanged += AppContext_SettingChanged;
                AppContext.LanguageChanged += AppContext_LanguageChanged;
                BuildMainMenuBar();
                SetupPreview();

                OpenPendingPath().FireAndForget(OnException);

                // Apply the persisted PiP setting only after mpv and the main
                // swap chain are ready. Running this from the window's
                // Body.Loaded raced CreateAsync: AttachSwapChain could run
                // before mpv initialization (native crash), or SetupPlayerView
                // would later move the swap chain back to the hidden main
                // window, leaving the PiP window without video.
                ApplyPiP();

                // Silent background update check (GitHub Releases); never
                // blocks startup and never surfaces network errors.
                _ = UpdateChecker.CheckForUpdatesAsync(XamlRoot);
            }
            else
            {
                // Initialization failed or is still in flight; the page stays
                // inert rather than showing a half-wired player.
            }
        }

        private void MpvPlayerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            AppContext.RunMpvCommand = null;
            AppContext.SetMpvLogLevel = null;
            AppContext.GetAudioDevices = null;
            AppContext.GetGpuAdapters = null;
            AppContext.GetMpvProfiles = null;
            AppContext.SettingChanged -= AppContext_SettingChanged;
            AppContext.LanguageChanged -= AppContext_LanguageChanged;
            CleanupDisplayInfo();
            CleanupPlaylistRefresh();

            _mediaPlayer.PlaylistChanged -= MpvPlayerPage_PlaylistChanged;
            _mediaPlayer.VolumeChangedChanged -= VolumeChangedChanged;
            _mediaPlayer.WindowChanged -= MpvPlayerPage_WindowChanged;
            _mediaPlayer.MediaInfoChanged -= MpvPlayerPage_MediaInfoChanged;
            _mediaPlayer.StopListen();
            TeardownPlayerView();
            CleanupKeyboardInput();
            CleanupPreview();
            ClosePiPWindow();
            _mediaPlayer.MediaOpened -= MpvPlayerPage_ApplyHdrModeOnce;
            _mediaPlayer.Close();
        }

        private void MpvPlayerPage_ApplyHdrModeOnce(MpvMediaPlayer player, object? args)
        {
            if (_hdrModeAppliedOnce)
            {
                return;
            }
            _hdrModeAppliedOnce = true;
            AppContext.SendMpvCommand($"script-message-to hdr_auto mode {AppContext.AppSetting.HdrAutoMode}");
        }

        private void AppContext_SettingChanged(string key, object? value)
        {
            if (key == nameof(AppContext.AppSetting.EnableVideoPreview))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var enabled = value is bool b && b;
                    PlayerControl.EnablePreviewEvents(enabled);
                    if (enabled)
                    {
                        SetupPreview();
                    }
                    else
                    {
                        CleanupPreview();
                    }
                });
            }
        }

        private void AppContext_LanguageChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyLocalizedStrings();
                PlayerControl.ApplyLocalizedStrings();
                BuildMainMenuBar();
            });
        }

        private async Task CreateAsync()
        {
            InitDisplayInfo();

            var configFolder = await AppData.Current.OpenOrCreateLocalDataFolderAsync(MpvConfigFolderName);
            if (_logger.IsDebugEnabled)
            {
                _logger.Debug("mpv config folder, path={}", configFolder.Path);
            }

            // First run: copy the bundled config layer into the mpv config dir
            // so the app works without a manual deploy-config.ps1 step. No-op
            // once mpv.conf exists there (never overwrites user changes).
            // This must run BEFORE WaitAll: the queued config writers (plugin
            // script-opts, the managed mpv.conf block) expect mpv.conf to
            // already exist and merge into it.
            await ConfigDeployer.EnsureDeployedAsync(configFolder.Path);

            // Ensure settings-managed config files (script-opts/*.conf, the
            // managed mpv.conf block) are written before mpv reads the config
            // dir at Initialize; AppContext.Init enqueues these asynchronously.
            await AppContext.WaitAll();

            _mediaPlayer.SwapChainChanged += MpvPlayer_SwapChainChanged;
            var refreshRate = AppContext.AppSetting.OverrideDisplayFps > 0
                ? AppContext.AppSetting.OverrideDisplayFps
                : _lastRefreshRate;
            await _mediaPlayer.InitializeAsync(configFolder.Path, AppContext.AppSetting.LastVideoVolume, _lastColorKind, (int)refreshRate);

            _isPlayerInitialized = true;
        }

        private void VolumeChangedChanged(MpvMediaPlayer player, int volume)
        {
            AppContext.AppSetting.LastVideoVolume = volume;
        }

        private void PlayerView_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // input.conf: MBTN_LEFT cycle pause
            SendMouseButton("MBTN_LEFT");
        }

        private void TopBarOntop_Click(object sender, RoutedEventArgs e)
        {
            AppContext.SendMpvCommand("cycle ontop");
        }

        private void TopBarScreenshot_Click(object sender, RoutedEventArgs e)
        {
            AppContext.SendMpvCommand("screenshot");
        }

        private void TopBarPlaylist_Click(object sender, RoutedEventArgs e)
        {
            TogglePlaylist(true);
        }

        private void AppQuit()
        {
            Application.Current.Exit();
        }

        private void OnException(Exception ex)
        {
            _logger.Error(ex);
            if (ex is OperationCanceledException)
            {
                return;
            }

            // Surface unexpected failures once per 5s; log-only would leave
            // users with no feedback when a menu/playlist action fails.
            if (Environment.TickCount64 - _lastExceptionNotifyTicks < 5000)
            {
                return;
            }
            _lastExceptionNotifyTicks = Environment.TickCount64;

            DispatcherQueue.RunAsync(async () =>
            {
                try
                {
                    if (XamlRoot is null)
                    {
                        return;
                    }
                    var dialog = new ContentDialog
                    {
                        Title = "mpv-winui",
                        Content = ex.Message,
                        CloseButtonText = AppContext.AppLang.Ok,
                        XamlRoot = XamlRoot,
                    };
                    await dialog.ShowAsync();
                }
                catch
                {
                    // Dialog failures are non-fatal (e.g. app closing).
                }
            });
        }

        private long _lastExceptionNotifyTicks;

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _pendingPaths = null;
            if (e.NavigationMode == NavigationMode.New)
            {
                _pendingPaths = e.Parameter as IReadOnlyList<FileItem>;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
        }

        void IParameterRefreshSupportView.OnRefresh(object? parameter)
        {
            var paths = parameter as IReadOnlyList<FileItem>;
            if (paths?.Count > 0)
            {
                _pendingPaths = paths;
                OpenPendingPath().FireAndForget(OnException);
            }
        }
    }
}
