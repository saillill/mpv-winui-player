using Microsoft.Graphics.Display;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private DisplayInformation? _displayInfo;
        private mpv_winrt.DisplayColorKind _lastColorKind = mpv_winrt.DisplayColorKind.SDR;
        private const uint DefaultRefreshRate = 60;
        private uint _lastRefreshRate = DefaultRefreshRate;
        private HMONITOR? _lastMonitor;
        private DispatcherTimerDebouncer<int>? _displayInfoDebouncer;
        private DispatcherQueueTimer? _displayInfoTimer;
        // Last advanced-color read failure written to display-info.log, so a
        // persistently broken detection logs its reason once instead of on
        // every 15s polling tick.
        private string? _lastDisplayFailureReason;

        private void InitDisplayInfo()
        {
            //TODO use player view rect
            _displayInfo = DisplayInformation.CreateForWindowId(_appWindow.Id);
            _lastColorKind = ReadColorKind();
            _displayInfo.AdvancedColorInfoChanged += OnAdvancedColorInfoChanged;

            _displayInfoDebouncer = new(DispatcherQueue, TimeSpan.FromSeconds(1), CheckAndUpdateDisplayInfo);
            _displayInfoTimer = DispatcherQueue.CreateTimer();
            // Events (AdvancedColorInfoChanged, AppWindow.Changed,
            // WM_DISPLAYCHANGE, WM_EXITSIZEMOVE) cover nearly all display
            // changes; this timer is only a slow fallback.
            _displayInfoTimer.Interval = TimeSpan.FromSeconds(15);
            _displayInfoTimer.Tick += OnDisplayInfoTimerTick;
            _displayInfoTimer.Start();
            _lastMonitor = Win32WindowHelper.GetMonitor(App.Window!);
            _lastRefreshRate = ReadRefreshRate();
            _appWindow.Changed += OnDisplayAppWindowChanged;

            unsafe
            {
                //TODO move to Window
                var hwnd = Win32WindowHelper.GetHwnd(App.Window!);
                PInvoke.SetWindowSubclass(new HWND(hwnd), &SubclassWindowProc, 52120, 0);
            }
        }

        private void CleanupDisplayInfo()
        {
            _appWindow.Changed -= OnDisplayAppWindowChanged;

            if (_displayInfoTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= OnDisplayInfoTimerTick;
                _displayInfoTimer = null;
            }

            if (_displayInfo is { } displayInfo)
            {
                try
                {
                    _displayInfo = null;
                    displayInfo?.AdvancedColorInfoChanged -= OnAdvancedColorInfoChanged;
                    displayInfo?.Dispose();
                }
                catch (Exception)
                {
                    //
                }
            }

            if (_displayInfoDebouncer is { } debouncer)
            {
                try
                {
                    _displayInfoDebouncer = null;
                    debouncer?.Dispose();
                }
                catch (Exception)
                {
                    //
                }
            }

            unsafe
            {
                var hwnd = Win32WindowHelper.GetHwnd(App.Window!);
                PInvoke.RemoveWindowSubclass(new HWND(hwnd), &SubclassWindowProc, 52120);
            }
        }

        private mpv_winrt.DisplayColorKind ReadColorKind(bool log = true)
        {
            try
            {
                var colorInfo = _displayInfo?.GetAdvancedColorInfo();
                if (colorInfo != null)
                {
                    var kind = colorInfo.CurrentAdvancedColorKind switch
                    {
                        DisplayAdvancedColorKind.HighDynamicRange => mpv_winrt.DisplayColorKind.HDR,
                        DisplayAdvancedColorKind.WideColorGamut => mpv_winrt.DisplayColorKind.WCG,
                        _ => mpv_winrt.DisplayColorKind.SDR
                    };
                    if (log)
                    {
                        TryLogDisplayInfo(kind, colorInfo);
                    }
                    _lastDisplayFailureReason = null;
                    return kind;
                }

                if (log)
                {
                    TryLogDisplayInfoFailure(
                        "GetAdvancedColorInfo returned null - DisplayInformation is null or its WindowId is stale");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                if (log)
                {
                    TryLogDisplayInfoFailure("GetAdvancedColorInfo threw: " + ex.Message);
                }
            }

            // Keep the last known kind instead of hard-falling back to SDR.
            // A transient read failure used to pin the player to SDR, which
            // activates the [mpvw-sdr] profile (d3d11-output-csp=srgb) and
            // silently disables RTX Video HDR - hdr_auto.lua only engages when
            // the output colorspace is PQ. A genuine "display is SDR" reading
            // is a successful read and still applies normally.
            return _lastColorKind;
        }

        // Writes a diagnostic line when the advanced-color state could not be
        // read at all. display-info.log used to contain successful reads only,
        // so "the display really is SDR" and "detection is broken" looked
        // identical - which is what made the RTX Video HDR chain so hard to
        // diagnose.
        private void TryLogDisplayInfoFailure(string reason)
        {
            try
            {
                // Log each distinct reason once: the 15s fallback timer would
                // otherwise append the same line thousands of times a day.
                if (string.Equals(_lastDisplayFailureReason, reason, System.StringComparison.Ordinal))
                {
                    return;
                }
                _lastDisplayFailureReason = reason;

                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mpv-winui", "logs");
                var logPath = System.IO.Path.Combine(logDir, "display-info.log");
                var monitorText = _lastMonitor?.ToString();
                var monitor = string.IsNullOrEmpty(monitorText) ? "<unresolved>" : monitorText!;
                var line = $"{DateTime.Now:HH:mm:ss.fff} kind=FAIL advanced=<unavailable> " +
                           $"reason={reason} " +
                           $"monitor={monitor} windowId={_appWindow.Id.Value}\n";
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(logDir);
                        System.IO.File.AppendAllText(logPath, line);
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        /// <summary>Opens a dialog with the current display/HDR detection results.</summary>
        public async System.Threading.Tasks.Task ShowDisplayInfoDialogAsync()
        {
            try
            {
                var kind = _lastColorKind.ToString();
                var colorInfo = _displayInfo?.GetAdvancedColorInfo();
                var advanced = colorInfo?.CurrentAdvancedColorKind.ToString() ?? "n/a";
                var sdrWhite = colorInfo?.SdrWhiteLevelInNits.ToString("0.0") ?? "n/a";
                var maxLuma = colorInfo?.MaxLuminanceInNits.ToString("0.0") ?? "n/a";
                var minLuma = colorInfo?.MinLuminanceInNits.ToString("0.0") ?? "n/a";
                var rate = _lastRefreshRate;
                var lang = AppContext.AppLang;
                var lines = new[]
                {
                    $"{lang.DisplayInfoDetectedKind}: {kind}",
                    $"{lang.DisplayInfoAdvancedColor}: {advanced}",
                    $"{lang.DisplayInfoSdrWhiteLevel}: {sdrWhite} nits",
                    $"{lang.DisplayInfoMaxLuminance}: {maxLuma} nits",
                    $"{lang.DisplayInfoMinLuminance}: {minLuma} nits",
                    $"{lang.DisplayInfoRefreshRate}: {rate} Hz",
                    "",
                    lang.DisplayInfoProfilesNote,
                };
                var dialog = new ContentDialog
                {
                    Title = lang.DisplayInfoTitle,
                    Content = new TextBlock { Text = string.Join("\n", lines), TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = lang.Ok,
                    XamlRoot = XamlRoot,
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

        // 定时兜底：WinRT 事件在跨显示器/系统 HDR 切换时可能不触发，
        // 定期重读一次，确保 color-kind 最终与当前显示器一致。
        private void OnDisplayInfoTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_displayInfo is null || IsMainWindowMinimized())
            {
                return;
            }

            var monitor = Win32WindowHelper.GetMonitor(App.Window!);
            if (_lastMonitor != monitor)
            {
                // 换显示器：重建 DisplayInformation 并完整读取一次
                CheckAndUpdateDisplayInfo(2);
                return;
            }

            // 同显示器：轻量轮询一次即可（不再重复调用 CheckAndUpdateDisplayInfo）
            var newKind = ReadColorKind(false);
            if (newKind != _lastColorKind)
            {
                _lastColorKind = newKind;
                _mediaPlayer?.UpdateDisplayColorInfo(newKind);
            }

            var rate = ReadRefreshRate();
            if (rate != _lastRefreshRate)
            {
                _lastRefreshRate = rate;
                if (ShouldAutoApplyRefreshRate())
                {
                    _mediaPlayer?.UpdateDisplayRefreshRate(rate);
                }
            }
            ApplyDisplayPeak();
        }

        private bool IsMainWindowMinimized()
        {
            return _appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };
        }

        private static bool ShouldAutoApplyRefreshRate()
        {
            // A non-zero user override wins; only the auto-detected rate is
            // written back when the user has not configured one.
            return AppContext.AppSetting.OverrideDisplayFps <= 0;
        }

        private void TryLogDisplayInfo(mpv_winrt.DisplayColorKind kind, DisplayAdvancedColorInfo? colorInfo)
        {
            try
            {
                if (colorInfo is null) return;
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mpv-winui", "logs");
                var logPath = System.IO.Path.Combine(logDir, "display-info.log");
                var line = $"{DateTime.Now:HH:mm:ss.fff} kind={kind} " +
                           $"advanced={colorInfo.CurrentAdvancedColorKind} " +
                           $"sdrWhite={colorInfo.SdrWhiteLevelInNits:0.0} " +
                           $"maxLuma={colorInfo.MaxLuminanceInNits:0.0} " +
                           $"minLuma={colorInfo.MinLuminanceInNits:0.0} " +
                           $"monitor={_lastMonitor} windowId={_appWindow.Id.Value}\n";
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(logDir);
                        // Cap display-info.log at ~1 MiB: rotate to .old so a
                        // long-lived HDR/display session cannot grow unbounded.
                        var fileInfo = new System.IO.FileInfo(logPath);
                        if (fileInfo.Exists && fileInfo.Length > 1024 * 1024)
                        {
                            var oldPath = logPath + ".old";
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                            System.IO.File.Move(logPath, oldPath);
                        }
                        System.IO.File.AppendAllText(logPath, line);
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        private uint ReadRefreshRate()
        {
            if (_lastMonitor is { IsNull: false } monitor)
            {
                try
                {
                    return Win32WindowHelper.GetDisplayFrequency(monitor, DefaultRefreshRate);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
            return DefaultRefreshRate;
        }

        private const int DefaultDisplayPeakNits = 1000;

        // Last pair pushed to mpv. The 15s fallback timer calls ApplyDisplayPeak
        // unconditionally, so without this every tick rewrote identical
        // properties - four log lines a minute for the whole session, and each
        // write re-runs mpv's auto-profile condition evaluation.
        // int.MinValue forces the first call through.
        private int _lastAppliedDisplayPeak = int.MinValue;
        private int _lastAppliedTargetPeak = int.MinValue;

        /// <summary>
        /// Applies the effective display peak: user <see cref="AppSettings.DisplayPeak"/>
        /// wins over detection; the legacy advanced TargetPeak, when set, wins over
        /// both. Written to user-data/mpvw/display-peak (drives the [mpvw-hdr]
        /// profile-cond) and to target-peak for tone mapping.
        /// </summary>
        private void ApplyDisplayPeak()
        {
            try
            {
                var detected = ReadDisplayPeakNits();
                var userPeak = AppContext.AppSetting.DisplayPeak;
                var userTarget = AppContext.AppSetting.TargetPeak;

                int displayPeak;
                int targetPeak;
                if (detected <= 0 && userPeak <= 0)
                {
                    // SDR display (or Windows HDR off): mpv's auto handling is
                    // correct here. Publishing 0 also keeps the [mpvw-hdr]
                    // profile from triggering on an SDR display — the previous
                    // code published the panel's 1000-nit max regardless of the
                    // HDR toggle, which made mpv render SDR content for a
                    // 1000-nit display (dark picture).
                    displayPeak = 0;
                    targetPeak = userTarget > 0 ? userTarget : 0;
                }
                else
                {
                    displayPeak = userPeak > 0 ? userPeak : detected;
                    if (displayPeak <= 0)
                    {
                        displayPeak = DefaultDisplayPeakNits;
                    }

                    targetPeak = userTarget > 0 ? userTarget : displayPeak;
                }

                if (displayPeak == _lastAppliedDisplayPeak && targetPeak == _lastAppliedTargetPeak)
                {
                    return;
                }

                AppContext.SendMpvCommand($"no-osd set user-data/mpvw/display-peak {displayPeak}");
                AppContext.SendMpvCommand($"no-osd set target-peak {targetPeak}");

                _lastAppliedDisplayPeak = displayPeak;
                _lastAppliedTargetPeak = targetPeak;

                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("display peak applied: detected={}, displayPeak={}, targetPeak={}",
                        detected, displayPeak, targetPeak);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "apply display peak failed");
            }
        }

        private int ReadDisplayPeakNits()
        {
            try
            {
                var colorInfo = _displayInfo?.GetAdvancedColorInfo();
                if (colorInfo is null || colorInfo.MaxLuminanceInNits <= 0)
                {
                    return 0;
                }

                // CRITICAL: MaxLuminanceInNits reports the PANEL's capability and
                // is non-zero even while Windows HDR is turned off. Using it in
                // SDR mode made mpv render for a 1000-nit display, which is why
                // SDR videos looked dark (the previewer instance never sets
                // target-peak and rendered correctly). Only trust the peak while
                // advanced color is actually on.
                if (colorInfo.CurrentAdvancedColorKind
                    != Microsoft.Graphics.Display.DisplayAdvancedColorKind.HighDynamicRange)
                {
                    return 0;
                }

                return Math.Clamp((int)Math.Round(colorInfo.MaxLuminanceInNits), 1, 10000);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "read display peak failed");
                return 0;
            }
        }

        private void OnAdvancedColorInfoChanged(DisplayInformation sender, object args)
        {
            var newKind = ReadColorKind();
            if (newKind != _lastColorKind)
            {
                _lastColorKind = newKind;
                _mediaPlayer?.UpdateDisplayColorInfo(newKind);
            }
            else
            {
                TryLogDisplayInfo(newKind, _displayInfo?.GetAdvancedColorInfo());
            }
            ApplyDisplayPeak();
        }

        private void OnDisplayAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidPositionChange)
            {
                return;
            }

            _displayInfoDebouncer?.OnEvent(0);
        }

        private void CheckAndUpdateDisplayInfo(int type)
        {
            var monitor = Win32WindowHelper.GetMonitor(App.Window!);
            if (_logger.IsTraceEnabled)
            {
                _logger.Trace("display check, monitor={}", monitor.ToString());
            }

            if (type > 1 || _lastMonitor != monitor)
            {
                var monitorChanged = _lastMonitor != monitor;
                _lastMonitor = monitor;

                // 窗口换了显示器后，DisplayInformation 需要重建才能跟随新显示器
                if (monitorChanged)
                {
                    RecreateDisplayInfo();
                }

                // 修复：跨显示器/显示器状态变化时同步更新 color-kind，驱动自动配置切换
                var newKind = ReadColorKind(type > 1 || monitorChanged);
                if (newKind != _lastColorKind)
                {
                    _lastColorKind = newKind;
                    _mediaPlayer?.UpdateDisplayColorInfo(newKind);
                }

                var rate = ReadRefreshRate();
                _logger.Debug("display update, last monitor={}, lastRefreshRate={}, new monitor={}, newRefreshRate={}", monitor.ToString(), _lastRefreshRate, monitor.ToString(), rate);
                if (rate != _lastRefreshRate)
                {
                    _lastRefreshRate = rate;
                    if (ShouldAutoApplyRefreshRate())
                    {
                        _mediaPlayer?.UpdateDisplayRefreshRate(rate);
                    }
                }
                ApplyDisplayPeak();
            }
        }

        private void RecreateDisplayInfo()
        {
            try
            {
                if (_displayInfo is { } old)
                {
                    old.AdvancedColorInfoChanged -= OnAdvancedColorInfoChanged;
                    old.Dispose();
                }
            }
            catch
            {
            }

            try
            {
                _displayInfo = DisplayInformation.CreateForWindowId(_appWindow.Id);
                _displayInfo.AdvancedColorInfoChanged += OnAdvancedColorInfoChanged;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                _displayInfo = null;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static LRESULT SubclassWindowProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
        {
            switch (uMsg)
            {
                case PInvoke.WM_DISPLAYCHANGE:
                {
                    if (_selfWeakReference?.TryGetTarget(out var self) == true)
                    {
                        self?._displayInfoDebouncer?.OnEvent(2);
                    }
                    break;
                }

                case PInvoke.WM_EXITSIZEMOVE:
                {
                    if (_selfWeakReference?.TryGetTarget(out var self) == true)
                    {
                        self?._displayInfoDebouncer?.OnEvent(1);
                    }
                    break;
                }

                case 0x020B: // WM_XBUTTONDOWN
                {
                    // input.conf: MBTN_BACK / MBTN_FORWARD -> playlist prev/next
                    if (_selfWeakReference?.TryGetTarget(out var self) == true)
                    {
                        var xButton = (int)(((ulong)wParam.Value >> 16) & 0xFFFF);
                        var keyName = xButton switch
                        {
                            0x0001 => "MBTN_BACK",
                            0x0002 => "MBTN_FORWARD",
                            _ => null,
                        };
                        if (keyName is not null)
                        {
                            self?._mediaPlayer?.Command(["keydown", keyName]);
                            self?._mediaPlayer?.Command(["keyup", keyName]);
                        }
                    }
                    break;
                }
            }
            return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
}
