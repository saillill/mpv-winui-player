using mpv_winui.Infrastructure.Common.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using mpv_winrt;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Domains.MpvConf;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mpv_winui.Domains.Playback
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
        private mpv_winui.Domains.Playback.PlayerControl PlayerControl =>
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
            // A new arrival means a new departure to guard against. Without this the flag stays
            // latched after the first teardown and the second Unloaded returns early - while the
            // subscriptions below are re-established, so they would never be released again.
            _tornDown = false;

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
                // FileStarted / FileLoaded are attached earlier, in CreateAsync right after
                // InitializeAsync returns. Subscribing them here was the defect documented in
                // SubscribePerLoadEvents: the first file is already loaded by the time Loaded
                // runs, so the handler was never called for it.
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
                // Send these as LINES, one call each.
                //
                // BuildApplyAllCommands returns whole command lines
                // ("no-osd set alang auto"). Handing that List to the
                // IList<string> overload of RunCommandAsync sends each line as
                // ONE argv element, and MpvPlayer::Command maps argv directly to
                // mpv_command -- so mpv saw a command literally named
                // "no-osd set alang auto" and rejected the whole batch. Proven
                // against the live player: ["no-osd","set","alang","jpn"] applies
                // and ["no-osd set alang jpn"] returns invalid parameter.
                //
                // The settings window never had this bug because it goes through
                // MpvSettings.ApplyAll -> SendMpvCommand -> CommandString, which
                // parses a line. This is the same path.
                foreach (var command in MpvSettings.BuildApplyAllCommands())
                {
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        await _mediaPlayer.RunCommandAsync(command);
                    }
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
                // That handler and the play-chain one are attached in
                // SubscribePerLoadEvents, right after InitializeAsync -- see the
                // comment there for why they cannot wait for Loaded.

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

        /// <summary>
        /// True once the page has torn itself down. Guards <see cref="TeardownPage"/>.
        /// </summary>
        private bool _tornDown;

        /// <summary>
        /// Tears the page down exactly once, whichever event gets here first.
        ///
        /// NavigationCacheMode is Disabled, so a navigation destroys this page and BOTH
        /// OnNavigatedFrom and Unloaded fire. The body ends with `_mediaPlayer.Destroy()`, and
        /// running that twice is a crash rather than a leak - which is why the migration to
        /// OnNavigatedFrom could not be a copy.
        ///
        /// OnNavigatedFrom is the primary because it is the navigation-correct event and because
        /// it runs while the visual tree is still attached: TeardownPlayerView and
        /// CleanupPlayControl reach for named elements, and Unloaded runs after detach, which is
        /// where those calls are most fragile.
        ///
        /// Unloaded is kept as the fallback for a page that is unloaded without a navigation -
        /// the window closing, or the element being reparented - where OnNavigatedFrom never runs.
        /// </summary>
        private void TeardownPage()
        {
            if (_tornDown)
            {
                return;
            }

            _tornDown = true;

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
            _mediaPlayer.LogMessage -= MpvPlayer_LogMessage;
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

        private void MpvPlayerPage_Unloaded(object sender, RoutedEventArgs e) => TeardownPage();

        private void MpvPlayerPage_ApplyHdrModeOnce()
        {
            LogColorDecision();

            if (_hdrModeAppliedOnce)
            {
                return;
            }
            _hdrModeAppliedOnce = true;
            AppContext.SendMpvCommand($"script-message-to hdr_auto mode {AppContext.AppSetting.HdrAutoMode}");
        }

        /// <summary>
        /// The third link of the play chain: mpv says it has read the file.
        ///
        /// <para>Separate from <see cref="MpvPlayerPage_ApplyHdrModeOnce"/>, which
        /// shares the event but returns early after the first file, so its line
        /// cannot stand for "every file loaded". This one fires per load, which is
        /// what a reader needs to match against the open request.</para>
        ///
        /// <para>Runs on mpv's event thread; PlayChain is thread-safe and must stay
        /// that way — a DispatcherQueue hop here would queue the log behind the UI
        /// thread, which is the opposite of what a log is for when the UI is the
        /// thing that is wrong.</para>
        /// </summary>
        private void MpvPlayerPage_PlayChainFileLoaded()
        {
            // Deliberately logs even when the path read fails: the point of this
            // line is to record that the EVENT arrived, which is a different fact
            // from which file it was for. The first version returned "<no path>"
            // and that was indistinguishable from the handler never running, which
            // is exactly the ambiguity the play chain exists to remove.
            string path;
            try
            {
                path = _mediaPlayer?.GetCurrentPath() ?? "<player null>";
            }
            catch (Exception ex)
            {
                path = "<GetCurrentPath threw " + ex.GetType().Name + ">";
            }

            PlayChain.Loaded(string.IsNullOrEmpty(path) ? "<empty path>" : path);
        }

        /// <summary>
        /// Records what mpv decided about colour for this file.
        ///
        /// The reported symptoms - HDR clips not tone-mapped, HDR clips washed out
        /// on an HDR display - are both invisible in the current logs, because
        /// nothing writes the properties the decision is made from. This is the
        /// only place that can: the values only exist once a file is loaded.
        ///
        /// Debug level, one line per file, and every read is the generic property
        /// getter, so a property this mpv build does not have simply logs empty
        /// rather than failing the load.
        /// </summary>
        private void LogColorDecision()
        {
            if (!_logger.IsDebugEnabled || _mediaPlayer is null)
            {
                return;
            }

            try
            {
                static string Read(mpv_winrt.MpvPlayer player, string name)
                {
                    var value = player.GetPropertyString(name);

                    return string.IsNullOrEmpty(value) ? "-" : value;
                }

                _logger.Debug(
                    "colour decision: vo={} csp={} gamma={} primaries={} sig-peak={} "
                    + "tone-mapping={} current-tone-mapping={} target-peak={} target-trc={} "
                    + "target-prim={} ref-white={} peak-percentile={} compute-peak={}",
                    Read(_mediaPlayer, "current-vo"),
                    Read(_mediaPlayer, "d3d11-output-csp"),
                    Read(_mediaPlayer, "video-params/gamma"),
                    Read(_mediaPlayer, "video-params/primaries"),
                    Read(_mediaPlayer, "video-params/sig-peak"),
                    Read(_mediaPlayer, "tone-mapping"),
                    Read(_mediaPlayer, "current-tone-mapping"),
                    Read(_mediaPlayer, "target-peak"),
                    Read(_mediaPlayer, "target-trc"),
                    Read(_mediaPlayer, "target-prim"),
                    Read(_mediaPlayer, "hdr-reference-white"),
                    Read(_mediaPlayer, "hdr-peak-percentile"),
                    Read(_mediaPlayer, "hdr-compute-peak"));
            }
            catch (Exception ex)
            {
                // Diagnostics must never be able to break a load.
                _logger.Warn(ex, "colour decision log failed");
            }
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
            else if (key == nameof(AppContext.AppSetting.VideoLibraryFolders)
                || key == nameof(AppContext.AppSetting.MusicLibraryFolders))
            {
                // A folder was bound or unbound from the settings window, so what the
                // library contains has changed. A full reload rather than an incremental
                // removal: the list is history plus every bound folder, so this changes
                // the grouping, the album cards, the rail's letter set and the empty
                // state - and unloading one folder means getting all four right.
                //
                // Dispatched because the event comes from the settings window's thread.
                DispatcherQueue.TryEnqueue(ReloadLibraryForFolderChange);
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

            // mpv's own log lines. Nothing subscribed to this event before, so
            // every line mpv forwarded -- warn and above by default, everything
            // while debug logging is on -- built an MpvLogEventArgs and raised
            // it into an empty handler set. The cost is the three to_hstring
            // conversions per message, and msg-level=all=info is what the
            // bundled mpv.conf asks for, so it was paying that for lines nobody
            // could see.
            //
            // Mirrored into the app log rather than a second file: the log the
            // user is told to look at is logs\mpv-winui.<date>.log.txt, and one
            // file with mpv's lines interleaved is what a bug report needs.
            // mpv's level names map onto NLog's one-for-one (no/error/warn/info/
            // verbose/debug/trace); anything else falls back to Info rather than
            // being dropped.
            _mediaPlayer.LogMessage += MpvPlayer_LogMessage;
            var refreshRate = AppContext.AppSetting.OverrideDisplayFps > 0
                ? AppContext.AppSetting.OverrideDisplayFps
                : _lastRefreshRate;
            await _mediaPlayer.InitializeAsync(configFolder.Path, AppContext.AppSetting.LastVideoVolume, _lastColorKind, (int)refreshRate);

            _isPlayerInitialized = true;
            _playerReadyTcs.TrySetResult();

            // Subscribe the per-load events HERE, not in Loaded.
            //
            // **The defect this fixes.** Every subscription used to happen in
            // MpvPlayerPage_Loaded, which runs after the page's first layout - and mpv is
            // already initialised by then, because CreateAsync started in the constructor.
            // A file handed to the app at launch is therefore loaded before anyone is
            // listening. Measured:
            //
            //     mpv raised file-loaded  at 02:15:15.0375
            //     FileLoaded handlers attached at 02:15:16.7265   <- 1.7 s too late
            //
            // FileStarted survived by luck (it fires twice, once before the load), so the
            // page switch worked while FileLoaded never arrived - which is indistinguishable
            // from "mpv never finished loading" and sent the search into the native
            // component for hours.
            //
            // Both events are routed through one handler each, and the handlers are
            // idempotent with respect to re-subscription, so the later Loaded pass below
            // no longer re-adds them.
            SubscribePerLoadEvents();
            _logger.Debug("CreateAsync: player initialized at {}ms", sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// Attaches the handlers that must be listening before the first file is loaded.
        ///
        /// Safe to call twice: <see cref="_perLoadEventsSubscribed"/> makes the second call
        /// a no-op, so the constructor path (via <see cref="CreateAsync"/>) and the
        /// Loaded path cannot double-subscribe and fire every handler twice per file.
        /// </summary>
        private void SubscribePerLoadEvents()
        {
            if (_perLoadEventsSubscribed || _mediaPlayer is null)
            {
                return;
            }

            _perLoadEventsSubscribed = true;

            // Drives the content area. FileStarted is the transition into the playing
            // page - it fires whether the previous state was the library or the video.
            _mediaPlayer.FileStarted += MpvPlayerPage_LibraryFileStarted;

            // Third link of the play chain. Subscribed before the HDR handler
            // deliberately: both ride FileLoaded, and the HDR handler does real work. If
            // that work throws, the multicast invocation stops and every later handler is
            // skipped - which is how a per-load diagnostic can appear never to fire while
            // its event is demonstrably raised.
            _mediaPlayer.FileLoaded += MpvPlayerPage_PlayChainFileLoaded;
            _mediaPlayer.FileLoaded += MpvPlayerPage_ApplyHdrModeOnce;

            PlayChain.Page("subscribed", "FileLoaded handlers attached");
        }

        /// <summary>
        /// True once <see cref="SubscribePerLoadEvents"/> has run, so the Loaded pass does
        /// not subscribe a second time.
        /// </summary>
        private bool _perLoadEventsSubscribed;

        private void MpvPlayerPage_VolumeChanged(VolumeChangedEventArgs args)
        {
            AppContext.AppSetting.LastVideoVolume = (int)args.Volume;
        }

        /// <summary>
        /// Forwards one mpv log line into the app log.
        /// </summary>
        /// <remarks>
        /// Raised on the native event thread, not the UI thread. NLog's Logger
        /// is thread-safe and the file target opens per write, so no dispatch is
        /// needed here -- and adding one would queue mpv's output behind the UI,
        /// which is the opposite of what a log is for when the UI is what hung.
        /// </remarks>
        private void MpvPlayer_LogMessage(MpvLogEventArgs args)
        {
            var level = args.Level switch
            {
                "no" => LogLevel.Off,
                "fatal" => LogLevel.Fatal,
                "error" => LogLevel.Error,
                "warn" => LogLevel.Warn,
                "info" => LogLevel.Info,
                "v" => LogLevel.Trace,
                "debug" => LogLevel.Debug,
                "trace" => LogLevel.Trace,
                // mpv occasionally adds a level before this app knows about it.
                // Logging it at Info keeps the line instead of dropping it.
                _ => LogLevel.Info,
            };

            if (level == LogLevel.Off)
            {
                return;
            }

            _logger.Log(level, "[mpv/{0}] {1}", args.Prefix, args.Text);
        }

        // A drop (or menu open) landing while CreateAsync is still running
        // used to enqueue loadfile into a native handle that silently no-ops
        // before initialization, losing the request. Callers now await this.
        private TaskCompletionSource _playerReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <summary>
        /// Teardown on the navigation-correct event; see <see cref="TeardownPage"/>.
        ///
        /// The body used to live in Unloaded with this override empty. Unloaded happens to fire
        /// on navigation today only because the page is not cached, so the teardown was tied to a
        /// coincidence rather than to the intent.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            TeardownPage();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _pendingPaths = null;
            if (e.NavigationMode == NavigationMode.New)
            {
                _pendingPaths = e.Parameter as IReadOnlyList<FileItem>;
            }
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
