using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using mpv_winrt;
using System;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Dedicated picture-in-picture window: a fixed-size borderless always-on-top
/// window with rounded corners and the video swap chain. It reuses the
/// fullscreen PlayerControl in centered compact mode. The main window is
/// hidden while PiP is active; PiP can only be left by restoring the main
/// window (top-left back, top-right close, or Alt+F4).
/// </summary>
public sealed partial class PiPWindow : Window
{
    public static PiPWindow? Instance { get; private set; }

    private MpvMediaPlayer? _player;
    private bool _closing;
    private bool _topButtonsShow;
    private bool _topButtonsAnimating;
    private bool _topButtonsAnimationShow;
    private long _topButtonsAnimationStart;
    private readonly DispatcherTimer _topButtonsTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _sizeUpdateTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private const double TopMaskHeight = 90;
    private const double BottomMaskHeight = 120;
    private double _videoAspect = 16.0 / 9.0;
    private bool _resizing;
    private Point _resizeStartPointer;
    private SizeInt32 _resizeStartSize;

    public PiPWindow()
    {
        Instance = this;
        InitializeComponent();
        RootGrid.RequestedTheme = ElementTheme.Dark;
        ConfigureWindow();
        ApplyLocalizedStrings();

        PiPView.PointerPressed += PiPView_PointerPressed;
        RootGrid.PointerMoved += RootGrid_PointerMoved;
        RootGrid.PointerExited += RootGrid_PointerExited;

        AppWindow.Closing += AppWindow_Closing;
        Closed += PiPWindow_Closed;
    }

    /// <summary>The video surface that libmpv renders into.</summary>
    public SwapChainPanel VideoPanel => PiPView;

    public void Attach(MpvMediaPlayer player)
    {
        if (_player == player)
        {
            return;
        }
        if (_player is not null)
        {
            _player.MediaOpened -= PiPPlayer_MediaLoaded;
            _player.MediaInfoChanged -= PiPPlayer_MediaInfoChanged;
        }
        _player = player;
        _player.MediaOpened += PiPPlayer_MediaLoaded;
        _player.MediaInfoChanged += PiPPlayer_MediaInfoChanged;
        PiPControls.MediaPlayer = player;
        PiPControls.IsPiPHost = true;
    }

    public void Detach()
    {
        if (_player is not null)
        {
            _player.MediaOpened -= PiPPlayer_MediaLoaded;
            _player.MediaInfoChanged -= PiPPlayer_MediaInfoChanged;
            PiPControls.MediaPlayer = null;
            _player = null;
        }
    }

    public void ShowPiP(int width, int height, bool reposition = true)
    {
        if (reposition)
        {
            // Only first entry / a freshly created window should claim the
            // bottom-right corner. Re-applying the PiP size setting must not
            // undo the position the user dragged the window to.
            PositionAtBottomRight(width, height);
            PiPControls.ApplyControlBarStyle();
        }
        AppWindow.Show();
        RefreshVideoAspect();
        ApplyPiPSize(width, height);
        ScheduleVideoSizeUpdate();
    }

    private void ScheduleVideoSizeUpdate()
    {
        _sizeUpdateTimer.Tick -= SizeUpdateTimer_Tick;
        _sizeUpdateTimer.Tick += SizeUpdateTimer_Tick;
        _sizeUpdateTimer.Start();
    }

    private void SizeUpdateTimer_Tick(object? sender, object e)
    {
        _sizeUpdateTimer.Stop();
        UpdateVideoSize();
    }

    private void UpdateVideoSize()
    {
        if (_player is null)
        {
            return;
        }

        var width = (uint)Math.Ceiling(PiPView.ActualWidth * PiPView.CompositionScaleX);
        var height = (uint)Math.Ceiling(PiPView.ActualHeight * PiPView.CompositionScaleY);
        if (width > 0 && height > 0)
        {
            _player.UpdateSize(width, height);
        }
    }

    public void HidePiP()
    {
        AppWindow.Hide();
    }

    private void ConfigureWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            // Fixed size: a resizable borderless window leaves a transparent
            // top frame through which the desktop shows as a white strip.
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        AppWindow.Title = "Picture in picture";
        AppWindow.SetIcon("App.ico");

        ApplyRoundedCorners();
        MakeFrameless();
    }

    private void PositionAtBottomRight(int width, int height)
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            var x = work.X + work.Width - width - 16;
            var y = work.Y + work.Height - height - 16;
            AppWindow.MoveAndResize(new RectInt32(Math.Max(work.X, x), Math.Max(work.Y, y), width, height));
        }
        catch (Exception)
        {
            AppWindow.MoveAndResize(new RectInt32(
                Math.Max(0, AppWindow.Position.X),
                Math.Max(0, AppWindow.Position.Y),
                width,
                height));
        }
        // The presenter may re-apply a non-client frame when the window is
        // moved/resized; strip it again so the content fills the whole window.
        MakeFrameless();
    }

    private void ApplyRoundedCorners()
    {
        try
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            var preference = 2; // DWMWCP_ROUND
            unsafe
            {
                _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &preference, (uint)sizeof(int));
                var borderColor = 0x000000; // black BGR, hides the light top border
                _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, &borderColor, (uint)sizeof(int));
                _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, &borderColor, (uint)sizeof(int));
                var ncPolicy = 2; // DWMNCRP_DISABLED: content fills the whole window
                _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, &ncPolicy, (uint)sizeof(int));
            }
        }
        catch (Exception)
        {
            // Rounded corners are cosmetic; older builds keep square corners.
        }
    }

    /// <summary>
    /// Removes the DWM non-client frame (resize border) that otherwise leaves
    /// a thin transparent strip around the window, showing the desktop as a
    /// white line on light backgrounds.
    /// </summary>
    private void MakeFrameless()
    {
        try
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            const int WS_BORDER = 0x00800000;
            const int WS_DLGFRAME = 0x00400000;
            const int WS_THICKFRAME = 0x00040000;

            var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            style &= ~(WS_BORDER | WS_DLGFRAME | WS_THICKFRAME);
            _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
            _ = PInvoke.SetWindowPos(
                hwnd,
                HWND.Null,
                0,
                0,
                0,
                0,
                SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE
                | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
        catch (Exception)
        {
            // Frameless styling is cosmetic; keep the default frame on failure.
        }
    }

    private void ApplyLocalizedStrings()
    {
        ToolTipService.SetToolTip(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        ToolTipService.SetToolTip(PiPExitButton, AppContext.AppLang.PiPExit);
    }

    private void PiPView_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        if (!e.GetCurrentPoint(PiPView).Properties.IsLeftButtonPressed)
        {
            // Right/middle/X buttons must not start the caption drag; they
            // are reserved for mpv bindings and the context menu.
            return;
        }

        // Hand the press to the system caption: the modal move loop starts
        // immediately and ends on release, so the window never sticks to the
        // cursor. This makes the whole video area draggable like browser PiP.
        try
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            _ = PInvoke.SendMessage(hwnd, 0x00A1, new WPARAM(2u), default); // WM_NCLBUTTONDOWN, HTCAPTION
        }
        catch (Exception)
        {
            // Drag is optional; ignore failures on exotic input.
        }
    }

    private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(RootGrid).Position;
        var height = RootGrid.ActualHeight;
        var inTopMask = position.Y <= TopMaskHeight;
        var inBottomMask = position.Y >= height - BottomMaskHeight;

        // The top buttons only react to the top mask, the status bar only to
        // the bottom mask; everywhere else both retract.
        SetTopButtonsVisible(inTopMask);
        if (inBottomMask)
        {
            PiPControls.ShowControlPanel();
        }
        else
        {
            PiPControls.HideControlPanel();
        }
    }

    private void RootGrid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Leaving the window must retract everything: the status bar has its
        // idle timer, but the top buttons previously stayed visible until the
        // pointer re-entered and moved to a non-top zone.
        SetTopButtonsVisible(false);
        PiPControls.HideControlPanel();
    }

    private void PiPView_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Wheel over the PiP video is forwarded to mpv (input.conf volume/seek),
        // matching the main window behavior. Left press stays reserved for
        // dragging the PiP window.
        var point = e.GetCurrentPoint(PiPView);
        var props = point.Properties;
        var key = props.IsHorizontalMouseWheel
            ? (props.MouseWheelDelta > 0 ? "WHEEL_LEFT" : "WHEEL_RIGHT")
            : (props.MouseWheelDelta > 0 ? "WHEEL_UP" : "WHEEL_DOWN");

        _player?.Command(["keydown", key]);
        _player?.Command(["keyup", key]);
        e.Handled = true;
    }

    private void PiPPlayer_MediaLoaded(MpvMediaPlayer player, object? args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshVideoAspect();
            ApplyPiPSize(AppWindow.Size.Width, AppWindow.Size.Height);
            ScheduleVideoSizeUpdate();
        });
    }

    private void PiPPlayer_MediaInfoChanged(MpvMediaPlayer player, MediaInfoChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // dwidth/dheight are only current once the video is reconfigured;
            // FILE_LOADED (MediaOpened) can run with the previous file's
            // values. Re-fit only when the aspect actually changed to avoid
            // needless resizes on title/metadata updates.
            if (args.VideoWidth <= 0 || args.VideoHeight <= 0)
            {
                return;
            }
            var aspect = args.VideoWidth / args.VideoHeight;
            if (Math.Abs(aspect - _videoAspect) > 0.001)
            {
                _videoAspect = aspect;
                ApplyPiPSize(AppWindow.Size.Width, AppWindow.Size.Height);
                ScheduleVideoSizeUpdate();
            }
        });
    }

    private void RefreshVideoAspect()
    {
        var width = _player?.VideoWidth ?? 0;
        var height = _player?.VideoHeight ?? 0;
        _videoAspect = width > 0 && height > 0
            ? width / height
            : 16.0 / 9.0;
    }

    /// <summary>
    /// Resizes the PiP window to the video aspect ratio, clamped between one
    /// twelfth and one half of the display work area.
    /// </summary>
    private SizeInt32 ApplyPiPSize(double width, double height)
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var minW = Math.Max(120, area.Width / 12.0);
            var minH = Math.Max(68, area.Height / 12.0);
            var maxW = area.Width / 2.0;
            var maxH = area.Height / 2.0;
            var aspect = _videoAspect > 0 ? _videoAspect : 16.0 / 9.0;

            var w = Math.Clamp(width, minW, maxW);
            var h = w / aspect;
            if (h > maxH)
            {
                h = maxH;
                w = h * aspect;
            }
            if (h < minH)
            {
                h = minH;
                w = h * aspect;
            }
            w = Math.Clamp(w, minW, maxW);
            h = Math.Clamp(h, minH, maxH);

            var size = new SizeInt32((int)Math.Round(w), (int)Math.Round(h));
            AppWindow.Resize(size);
            return size;
        }
        catch (Exception)
        {
            // Resizing is optional; ignore failures on exotic displays.
            return AppWindow.Size;
        }
    }

    private void PiPResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        _resizing = true;
        _resizeStartPointer = point.Position;
        _resizeStartSize = AppWindow.Size;
        PiPResizeGrip.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PiPResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizing)
        {
            return;
        }

        var position = e.GetCurrentPoint(RootGrid).Position;
        // AppWindow.Size is physical pixels while PointerPoint.Position is in
        // DIPs; without the raster scale the drag lags the cursor at
        // fractional DPI.
        var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        var deltaX = (position.X - _resizeStartPointer.X) * scale;
        var deltaY = (position.Y - _resizeStartPointer.Y) * scale;
        var size = ApplyPiPSize(
            _resizeStartSize.Width + deltaX,
            _resizeStartSize.Height + deltaY);
        if (_player is not null && size.Width > 0 && size.Height > 0)
        {
            // Use the computed physical target directly: reading
            // PiPView.ActualWidth right after AppWindow.Resize can return the
            // pre-layout size, and no SizeChanged subscription is safe here.
            _player.UpdateSize((uint)size.Width, (uint)size.Height);
        }
        e.Handled = true;
    }

    private void PiPResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _resizing = false;
        PiPResizeGrip.ReleasePointerCapture(e.Pointer);
        // Safety net: the final layout pass may still differ from the target
        // size used during the drag, so re-assert once after it settles.
        ScheduleVideoSizeUpdate();
        e.Handled = true;
    }

    private void PiPResizeGrip_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _resizing = false;
    }

    private void PiPBackButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
    }

    private void PiPExitButton_Click(object sender, RoutedEventArgs e)
    {
        // The top-right close leaves PiP and restores the main window,
        // matching the back button and Alt+F4 behavior.
        RestoreMainWindow();
    }

    private void SetTopButtonsVisible(bool show)
    {
        if (_topButtonsShow == show)
        {
            return;
        }
        _topButtonsShow = show;
        StartTopButtonsAnimation(show);
    }

    private void StartTopButtonsAnimation(bool show)
    {
        if (_topButtonsAnimating && _topButtonsAnimationShow == show)
        {
            return;
        }

        _topButtonsAnimating = true;
        _topButtonsAnimationShow = show;
        _topButtonsAnimationStart = Environment.TickCount64;
        if (show)
        {
            PiPBackButton.Visibility = Visibility.Visible;
            PiPExitButton.Visibility = Visibility.Visible;
            PiPBackButton.Opacity = 0;
            PiPExitButton.Opacity = 0;
        }
        _topButtonsTimer.Tick -= TopButtonsAnimationTick;
        _topButtonsTimer.Tick += TopButtonsAnimationTick;
        _topButtonsTimer.Start();
    }

    private void TopButtonsAnimationTick(object? sender, object e)
    {
        const double durationMs = 180;
        var elapsed = Environment.TickCount64 - _topButtonsAnimationStart;
        var t = Math.Clamp(elapsed / durationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic

        var opacity = _topButtonsAnimationShow ? eased : 1 - eased;
        PiPBackButton.Opacity = opacity;
        PiPExitButton.Opacity = opacity;

        if (t >= 1)
        {
            _topButtonsTimer.Stop();
            _topButtonsAnimating = false;
            PiPBackButton.Opacity = _topButtonsAnimationShow ? 1 : 0;
            PiPExitButton.Opacity = _topButtonsAnimationShow ? 1 : 0;
            if (!_topButtonsAnimationShow)
            {
                // Fully hidden buttons must not stay hit-testable: an
                // invisible button still shows its tooltip on hover.
                PiPBackButton.Visibility = Visibility.Collapsed;
                PiPExitButton.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void StopTopButtonsAnimation()
    {
        _topButtonsTimer.Stop();
        _topButtonsTimer.Tick -= TopButtonsAnimationTick;
        _sizeUpdateTimer.Stop();
        _sizeUpdateTimer.Tick -= SizeUpdateTimer_Tick;
        _topButtonsAnimating = false;
        PiPBackButton.Visibility = Visibility.Visible;
        PiPExitButton.Visibility = Visibility.Visible;
        PiPBackButton.Opacity = 1;
        PiPExitButton.Opacity = 1;
    }

    /// <summary>Leaves PiP by restoring the hidden main window; PiP never quits the app directly.</summary>
    private void RestoreMainWindow()
    {
        AppContext.AppSetting.WindowPiP = false;
        AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.WindowPiP), false);
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // Alt+F4 on the PiP window restores the main window instead of quitting.
        if (AppContext.AppSetting.WindowPiP)
        {
            args.Cancel = true;
            RestoreMainWindow();
        }
    }

    private void PiPWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_closing)
        {
            return;
        }
        _closing = true;

        StopTopButtonsAnimation();
        Detach();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
        Closed -= PiPWindow_Closed;
    }

}
