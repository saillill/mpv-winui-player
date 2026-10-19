using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Settings;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage : Page, IParameterRefreshSupportView, IMpvOptionApplySupport, IMpvCommandApplySupport
    {
        private static readonly Logger _logger = LogManager.GetLogger("MpvPlayer");
        private const string MpvConfigFolderName = "mpv";
        private readonly MpvPlayer _mediaPlayer = new();
        private bool _isPlayerInitialized;
        private static WeakReference<MpvPlayerPage>? _selfWeakReference;

        private readonly AppWindow _appWindow;

        private Task? _task;
        private bool _hdrModeAppliedOnce;

        /// <summary>
        /// The player control bar, which the shell owns so that it is not
        /// unmounted when the content area changes.
        ///
        /// This is a property rather than the XAML-declared field it used to be,
        /// which is why the ~34 call sites in this page's partials still read
        /// <c>PlayerControl.Something</c> unchanged. The return type is spelled
        /// out in full because the member name is the same as its type name -
        /// the "Color Color" case - and a short name there resolves to the type,
        /// not the property.
        ///
        /// Timed so that it is never null: App.OnLaunched assigns Window before
        /// calling Open(), and Open() is what navigates this page into existence.
        /// </summary>
        private mpv_winui.Modules.Player.PlayerControl PlayerControl =>
            (App.Window as MainWindow)?.PlayerBar!;

        public MpvPlayerPage()
        {
            _selfWeakReference = new(this);
            _appWindow = App.Window?.AppWindow!;
            InitializeComponent();
            ApplyLocalizedStrings();

            // Localize the menu bar here, not from CreateAsync's success path.
            //
            // BuildMainMenuBar used to run only after mpv had initialized, so
            // the bar's labels - and therefore the visible language - depended
            // on a network of unrelated steps: deploy the config, drain the
            // queued writers, bring mpv up. Any of those failing, or merely
            // taking its time, left the XAML-declared English items on screen,
            // and the user's report of "the Chinese labels are slow" and "the
            // Chinese labels are gone" are the same defect seen at two speeds.
            //
            // Nothing here needs mpv. MenuBarBuilder reads AppLang (loaded in
            // AppContext.Init, before any page exists) and Menus/menus.json
            // (a plain file). The call in the initialized branch stays as an
            // idempotent rebuild.
            BuildMainMenuBar();
            SetupCustomMenuBarItems();

            SetupLibrary();

            // The three tool keys live on this page (see PlaybackTools in the
            // XAML), so their icons and labels are set here too.
            ApplyPlaybackToolStrings();

            // Pressing any transport button while the library is covering a
            // running video brings the video back first, then does what the
            // button does. One subscription on the bar's own funnel - see the
            // note on TransportInteraction.
            if (App.Window is MainWindow)
            {
                PlayerControl.TransportInteraction += MpvPlayerPage_TransportInteraction;

                PlayerControl.PositionTicked += UpdateLyricsHighlight;
            }

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
                SetupPlayControl();

                // First moment the watch history path exists, so the first
                // moment the library can be filled.
                LoadLibraryFirstTime();

                _mediaPlayer.PlaylistChanged += MpvPlayerPage_PlaylistChanged;
                _mediaPlayer.VolumeChanged += MpvPlayerPage_VolumeChanged;
                _mediaPlayer.WindowChanged += MpvPlayerPage_WindowChanged;
                _mediaPlayer.MediaInfoChanged += MpvPlayerPage_MediaInfoChanged;
                _mediaPlayer.DiscMenuActiveChanged += MpvPlayerPage_DiscMenuActiveChanged;
                // Drives the content area. FileStarted is the transition into
                // the playing page - it is the only event that means "a file is
                // loading", and it fires whether the previous state was the
                // library or the video. PlaybackStateChanged supplies the idle
                // edge that brings the library back.
                _mediaPlayer.FileStarted += MpvPlayerPage_LibraryFileStarted;
                _mediaPlayer.PlaybackStateChanged += MpvPlayerPage_LibraryPlaybackStateChanged;

                AppContext.RunMpvCommand = cmd => _ = _mediaPlayer.RunCommandAsync(cmd);
                AppContext.SetMpvLogLevel = level => _mediaPlayer.SetLogLevel(level);
                AppContext.GetAudioDevices = () => _mediaPlayer.GetAudioDevices();
                AppContext.GetGpuAdapters = () => _mediaPlayer.GetGpuAdapters();
                // LoggerHelper may have run before this hook existed; sync the
                // native mpv log level once the player is available.
                AppContext.SetMpvLogLevel(AppContext.AppSetting.EnableDebugLog ? "info" : "warn");
                var lang = AppContext.AppSetting.CurrentLanguage;
                if (string.IsNullOrWhiteSpace(lang)) lang = "en-US";
                AppContext.SendMpvCommand($"no-osd set user-data/mpvw/language {lang}");
                ApplyDisplayPeak();
                var applyCommands = MpvSettings.BuildApplyAllCommands();
                if (applyCommands.Count > 0)
                {
                    await _mediaPlayer.RunCommandAsync(applyCommands);
                }

                // Playback speed needs no special case here any more. It is a
                // persistent setting, and MpvSettings.ToCommand now has an arm
                // for it, so the startup batch above already carries it; the
                // batch omits it from ApplyAll (SessionStateKeys) so a reset
                // still cannot clobber a live session. The hand-written
                // `set speed` that used to live here was compensating for the
                // missing arm, and it only ever covered startup -- changing the
                // row in the settings window did nothing at all.
                // No drain step: the native player runs each command synchronously
                // inside RunCommandAsync, so by the time the await above returns
                // the whole batch has already been applied.
                // hdr_auto registers its script-message handler after mpv
                // scripts load; the startup batch races that registration, so
                // the saved HDR mode is applied once the first file loads.
                _mediaPlayer.FileLoaded += MpvPlayerPage_ApplyHdrModeOnce;

                SetupKeyboardInput();
                SetupMouseInput();

                if (AppContext.AppSetting.EnableVideoPreview || AppContext.AppSetting.EnableVideoBuiltInPreview)
                {
                    SetupPreview();
                }

                // Config-driven menu bar. Built once already in the constructor
                // so the labels never depend on mpv coming up; rebuilt here so
                // a slow start still converges on the same bar, and so a
                // definition that only became readable in the meantime (a user
                // override written during startup) is picked up.
                BuildMainMenuBar();
                SetupCustomMenuBarItems();

                OpenPendingPath().FireAndForget(OnException);

                // PiP is a transient mode: never auto-enter it on launch. The
                // control-bar PiP button persists WindowPiP while a session is
                // running (so the settings section opens), but a fresh start
                // must always show the main player, never a floating mini
                // window. Clear any state left over from the last session.
                if (AppContext.AppSetting.WindowPiP)
                {
                    AppContext.AppSetting.WindowPiP = false;
                }

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
            AppContext.SettingChanged -= AppContext_SettingChanged;
            AppContext.LanguageChanged -= AppContext_LanguageChanged;
            CleanupDisplayInfo();
            CleanupPlaylistRefresh();
            TeardownLibrary();

            _mediaPlayer.PlaylistChanged -= MpvPlayerPage_PlaylistChanged;
            _mediaPlayer.VolumeChanged -= MpvPlayerPage_VolumeChanged;
            _mediaPlayer.WindowChanged -= MpvPlayerPage_WindowChanged;
            _mediaPlayer.MediaInfoChanged -= MpvPlayerPage_MediaInfoChanged;
            _mediaPlayer.DiscMenuActiveChanged -= MpvPlayerPage_DiscMenuActiveChanged;
            _mediaPlayer.FileStarted -= MpvPlayerPage_LibraryFileStarted;
            _mediaPlayer.PlaybackStateChanged -= MpvPlayerPage_LibraryPlaybackStateChanged;

            TeardownPlayerView();
            CleanupKeyboardInput();
            CleanupMouseInput();
            CleanupPreview();
            ClosePiPWindow();
            CleanupPlayControl();
            _mediaPlayer.Destroy();
        }

        private void MpvPlayerPage_ApplyHdrModeOnce()
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
            if (key == nameof(AppContext.AppSetting.EnableVideoPreview)
                || key == nameof(AppContext.AppSetting.EnableVideoBuiltInPreview))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var enabled = key == nameof(AppContext.AppSetting.EnableVideoBuiltInPreview)
                        ? (value is bool b2 && b2) || AppContext.AppSetting.EnableVideoPreview
                        : (value is bool b && b) || AppContext.AppSetting.EnableVideoBuiltInPreview;

                    PlayerControl.EnablePreviewEvents(enabled);
                    CleanupPreview();
                    if (enabled)
                    {
                        SetupPreview();
                    }
                });
            }
            else if (key == nameof(AppContext.AppSetting.KeepVideoBuiltInPreviewAlive))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (MpvPreview is not null)
                    {
                        MpvPreview.KeepAlive = value is bool b && b;
                    }
                });
            }
            else if (key == nameof(AppContext.AppSetting.BuiltInPreviewAliveTimeout))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (MpvPreview is not null)
                    {
                        MpvPreview.KeepAliveTimeout = Convert.ToInt32(value);
                    }
                });
            }
            else if (key == nameof(AppContext.AppSetting.DisplayPeak))
            {
                DispatcherQueue.TryEnqueue(ApplyDisplayPeak);
            }
            else if (key == nameof(AppContext.AppSetting.ThumbnailUpdateInterval))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_previewThrottleTimer is { } timer)
                    {
                        timer.Interval = TimeSpan.FromMilliseconds(
                            Math.Clamp(Convert.ToInt32(value), 40, 600));
                    }
                });
            }
            else if (key == nameof(AppContext.AppSetting.ThumbnailPreviewWidth))
            {
                // Re-init on the next hover so the new render size applies.
                DispatcherQueue.TryEnqueue(DestroyPreviewer);
            }
            else if (key == nameof(AppContext.AppSetting.WindowPiPOpacity))
            {
                DispatcherQueue.TryEnqueue(() => _pipWindow?.ApplyOpacity());
            }
        }

        private void AppContext_LanguageChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // ApplyLocalizedStrings already re-localizes the control bar;
                // calling it a second time here used to run
                // PlayerControl.ApplyLocalizedStrings unguarded, which dereferences
                // AppLang and would have thrown if it were ever null.
                ApplyLocalizedStrings();
                RebuildMenuBar();
            });
        }

        /// <summary>
        /// Re-applies localized text to this page's own chrome. The menu bar has
        /// its own entry point that the caller invokes separately.
        /// </summary>
        private void ApplyLocalizedStrings()
        {
            if (AppContext.AppLang is null)
            {
                return;
            }

            PlayerControl?.ApplyLocalizedStrings();
            ApplyLibraryStrings();
        }

        private async Task CreateAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
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
            _logger.Debug("CreateAsync: config deployed at {}ms", sw.ElapsedMilliseconds);

            // Ensure settings-managed config files (script-opts/*.conf, the
            // managed mpv.conf block) are written before mpv reads the config
            // dir at Initialize; AppContext.Init enqueues these asynchronously.
            await AppContext.WaitAll();
            _logger.Debug("CreateAsync: pending settings flushed at {}ms", sw.ElapsedMilliseconds);

            _mediaPlayer.VoConfigured += MpvPlayer_SwapChainChanged;
            var refreshRate = AppContext.AppSetting.OverrideDisplayFps > 0
                ? AppContext.AppSetting.OverrideDisplayFps
                : _lastRefreshRate;
            await _mediaPlayer.InitializeAsync(configFolder.Path, AppContext.AppSetting.LastVideoVolume, _lastColorKind, (int)refreshRate);

            _isPlayerInitialized = true;
            _playerReadyTcs.TrySetResult();
            _logger.Debug("CreateAsync: player initialized at {}ms", sw.ElapsedMilliseconds);
        }

        private void MpvPlayerPage_VolumeChanged(VolumeChangedEventArgs args)
        {
            AppContext.AppSetting.LastVideoVolume = (int)args.Volume;
        }

        // A drop (or menu open) landing while CreateAsync is still running
        // used to enqueue loadfile into a native handle that silently no-ops
        // before initialization, losing the request. Callers now await this.
        private TaskCompletionSource _playerReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Task WaitForPlayerReadyAsync()
        {
            return _isPlayerInitialized ? Task.CompletedTask : _playerReadyTcs.Task;
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
            ShowPlayQueue();
        }

        private void AppQuit()
        {
            // Exit() tears the process down without running window teardown,
            // so persist the resumable state explicitly: mpv writes the
            // watch-later file (resume-playback/save-position-on-quit read
            // it back) and the app-side history gets its final entry.
            try
            {
                _mediaPlayer.Command(["quit-watch-later"]);
                _ = _mediaPlayer.SaveWatchHistory();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "pre-exit flush failed");
            }
            AppContext.AppSetting.Flush();
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

        async Task IMpvOptionApplySupport.ApplyMpvOptionAsync(string key, string value)
        {
            if (!_isPlayerInitialized || string.IsNullOrEmpty(key))
            {
                return;
            }

            await Task.Run(() =>
            {
                _mediaPlayer.Command(["set", key, value ?? string.Empty]);
            });
        }

        async Task IMpvCommandApplySupport.ApplyMpvCommandAsync(string command)
        {
            if (!_isPlayerInitialized || string.IsNullOrEmpty(command))
            {
                return;
            }

            await _mediaPlayer.RunCommandAsync(command);
        }
    }
}
