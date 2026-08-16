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
                    return kind;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            return mpv_winrt.DisplayColorKind.SDR;
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
                var lines = new[]
                {
                    $"Detected kind     : {kind}",
                    $"Advanced color   : {advanced}",
                    $"SDR white level  : {sdrWhite} nits",
                    $"Max luminance    : {maxLuma} nits",
                    $"Min luminance    : {minLuma} nits",
                    $"Refresh rate     : {rate} Hz",
                    "",
                    "kind drives profiles.conf [mpvw-sdr|mpvw-wcg|mpvw-hdr].",
                };
                var dialog = new ContentDialog
                {
                    Title = "Display info",
                    Content = new TextBlock { Text = string.Join("\n", lines), TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "OK",
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
