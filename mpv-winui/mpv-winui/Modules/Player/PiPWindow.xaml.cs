using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using mpv_winrt;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Dedicated picture-in-picture window: a borderless always-on-top window
/// with rounded corners, native edge resize that is aspect-locked through
/// WM_SIZING (WS_THICKFRAME kept so the OS draws the resize borders and size
/// cursors), drag-anywhere moving, and the video swap chain. It reuses the
/// fullscreen PlayerControl in centered compact mode. Entering PiP always
/// claims the bottom-right corner of the main window's display and the
/// default size is a proportion of that display's work area. The main window
/// is hidden while PiP is active; the top-left button restores it, the
/// top-right button quits the whole player, and Alt+F4 restores the main
/// window.
/// 
/// The official Windows App SDK CompactOverlayPresenter was prototyped as a
/// replacement for the Win32 frame hacks, but rejected: it adds a system
/// title bar over the top overlay buttons (clicks land on the caption), and
/// it swallows the HTCAPTION drag, so the video is no longer draggable.
/// WindowsAppSDK#1593 also tracks that compact-overlay windows cannot be
/// user-resized. AppWindowTitleBar.SetDragRectangles was prototyped as the
/// official drag-move replacement, but the OS ignores the drag regions on a
/// fully frameless window. Drag-anywhere therefore tracks the cursor with
/// GetCursorPos (read-only) and moves with AppWindow.Move; the previous
/// WM_NCLBUTTONDOWN/HTCAPTION modal loop was unreliable in WinUI 3 and made
/// the window stick to the cursor after release. Keep the custom
/// always-on-top frameless window until those are resolved.
/// </summary>
public sealed partial class PiPWindow : Window
{
    private MpvMediaPlayer? _player;
    private bool _closing;
    private bool _tearingDown;
    private bool _topButtonsShow;
    private bool _topButtonsAnimating;
    private bool _topButtonsAnimationShow;
    private Compositor? _topButtonsCompositor;
    private Visual? _topBackButtonVisual;
    private Visual? _topExitButtonVisual;
    private readonly DispatcherTimer _sizeUpdateTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private const double TopMaskHeight = 90;
    private const double BottomMaskHeight = 120;
    private double _videoAspect = 16.0 / 9.0;
    private double _resizeMinW = 320;
    private double _resizeMinH = 180;
    private double _resizeMaxW = 960;
    private double _resizeMaxH = 540;
    private bool _draggingWindow;
    private PointInt32 _dragStartCursor;
    private PointInt32 _dragStartPosition;
    private static WeakReference<PiPWindow>? _selfWeakReference;

    public PiPWindow()
    {
        _selfWeakReference = new(this);
        InitializeComponent();
        RootGrid.RequestedTheme = ElementTheme.Dark;
        ConfigureWindow();
        ApplyLocalizedStrings();
        AppContext.LanguageChanged += PiPWindow_LanguageChanged;

        RootGrid.PointerMoved += RootGrid_PointerMoved;
        RootGrid.PointerExited += RootGrid_PointerExited;

        AppWindow.Closing += AppWindow_Closing;
        AppWindow.Changed += PiPAppWindow_Changed;
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

    public void ShowPiP(int width, int height)
    {
        // Always claim the bottom-right corner of the main window's display
        // on entry; the user can drag the window afterwards.
        PositionAtBottomRight(width, height);
        PiPControls.ApplyControlBarStyle();
        AppWindow.Show();
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
            // Native border resize: the OS provides the resize borders and
            // size cursors on the window edges. The border is painted black
            // (DWM) so the borderless look is kept while WS_THICKFRAME (kept
            // in MakeFrameless) makes the edges resizable.
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        AppWindow.Title = "Picture in picture";
        AppWindow.SetIcon("App.ico");

        ApplyRoundedCorners();
        MakeFrameless();

        // Native resize with aspect lock: WM_SIZING adjusts the proposed drag
        // rectangle so the window always keeps the video aspect while the OS
        // still provides the resize borders and size cursors.
        unsafe
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            PInvoke.SetWindowSubclass(hwnd, &PiPSubclassProc, 52121, 0);
        }
    }

    private DisplayArea GetPiPDisplayArea()
    {
        try
        {
            if (App.Window is MainWindow mainWindow)
            {
                return DisplayArea.GetFromWindowId(mainWindow.AppWindow.Id, DisplayAreaFallback.Nearest);
            }
        }
        catch (Exception)
        {
            // Fall through to the PiP window's own display.
        }
        return DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
    }

    private void PiPAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            // Native edge resize changes the window continuously; re-assert
            // the swap chain size once layout settles (the timer is restarted
            // on every change and fires 300ms after the drag ends).
            ScheduleVideoSizeUpdate();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT PiPSubclassProc(
        HWND hWnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        const int WM_SIZING = 0x0214;
        if (uMsg == WM_SIZING
            && _selfWeakReference?.TryGetTarget(out var self) == true)
        {
            var rect = Marshal.PtrToStructure<RECT>((nint)lParam.Value);
            if (self is not null && self.AdjustSizingRect((int)wParam.Value, ref rect))
            {
                Marshal.StructureToPtr(rect, (nint)lParam.Value, false);
                return (LRESULT)1;
            }
        }
        return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Aspect-locks the native border resize: adjusts the WM_SIZING drag
    /// rectangle so the window always keeps <see cref="_videoAspect"/> while
    /// the OS still runs the resize loop with the standard size cursors.
    /// </summary>
    private bool AdjustSizingRect(int edge, ref RECT rect)
    {
        const int WMSZ_LEFT = 1;
        const int WMSZ_RIGHT = 2;
        const int WMSZ_TOP = 3;
        const int WMSZ_BOTTOM = 6;

        var aspect = _videoAspect > 0 ? _videoAspect : 16.0 / 9.0;
        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;

        double w;
        double h;
        if (edge is WMSZ_TOP or WMSZ_BOTTOM)
        {
            // Height-driven edges (top/bottom): height is the drag axis.
            h = Math.Clamp(height, _resizeMinH, _resizeMaxH);
            w = h * aspect;
            if (w > _resizeMaxW)
            {
                w = _resizeMaxW;
                h = w / aspect;
            }
            if (w < _resizeMinW)
            {
                w = _resizeMinW;
                h = w / aspect;
            }
        }
        else
        {
            // Width-driven edges and all corners: width is the drag axis.
            w = Math.Clamp(width, _resizeMinW, _resizeMaxW);
            h = w / aspect;
            if (h > _resizeMaxH)
            {
                h = _resizeMaxH;
                w = h * aspect;
            }
            if (h < _resizeMinH)
            {
                h = _resizeMinH;
                w = h * aspect;
            }
        }

        var newW = (int)Math.Round(w);
        var newH = (int)Math.Round(h);
        if (newW == width && newH == height)
        {
            return false;
        }

        switch (edge)
        {
            case WMSZ_LEFT:
                rect.left = rect.right - newW;
                rect.bottom = rect.top + newH;
                break;
            case WMSZ_RIGHT:
                rect.right = rect.left + newW;
                rect.bottom = rect.top + newH;
                break;
            case WMSZ_TOP:
                rect.top = rect.bottom - newH;
                rect.right = rect.left + newW;
                break;
            case WMSZ_BOTTOM:
                rect.bottom = rect.top + newH;
                rect.right = rect.left + newW;
                break;
            case 4: // WMSZ_TOPLEFT
                rect.left = rect.right - newW;
                rect.top = rect.bottom - newH;
                break;
            case 5: // WMSZ_TOPRIGHT
                rect.right = rect.left + newW;
                rect.top = rect.bottom - newH;
                break;
            case 7: // WMSZ_BOTTOMLEFT
                rect.left = rect.right - newW;
                rect.bottom = rect.top + newH;
                break;
            case 8: // WMSZ_BOTTOMRIGHT
                rect.right = rect.left + newW;
                rect.bottom = rect.top + newH;
                break;
        }
        return true;
    }

    private void PositionAtBottomRight(int width, int height)
    {
        try
        {
            var area = GetPiPDisplayArea();
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

            var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            // Keep WS_THICKFRAME so the OS still hit-tests the window edges
            // for native resize (size cursors + border drag); only drop the
            // visible frame styles.
            style &= ~(WS_BORDER | WS_DLGFRAME);
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
        AutomationProperties.SetName(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        AutomationProperties.SetName(PiPExitButton, AppContext.AppLang.PiPExit);
    }

    private void PiPWindow_LanguageChanged()
    {
        ApplyLocalizedStrings();
    }

    private void PiPView_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        var point = e.GetCurrentPoint(PiPView);
        if (!point.Properties.IsLeftButtonPressed)
        {
            // Right/middle/X buttons must not start the caption drag; they
            // are reserved for mpv bindings and the context menu.
            return;
        }

        if (!PInvoke.GetCursorPos(out var cursor))
        {
            return;
        }

        // Official drag-move: track the pointer and move with AppWindow.Move.
        // The previous WM_NCLBUTTONDOWN/HTCAPTION caption message was
        // unreliable under WinUI 3: when the modal move loop started outside
        // the physical button press it kept tracking the cursor after
        // release, making the window stick to the mouse.
        _draggingWindow = true;
        _dragStartCursor = new PointInt32(cursor.X, cursor.Y);
        _dragStartPosition = AppWindow.Position;
        PiPView.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PiPView_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!PInvoke.GetCursorPos(out var cursor))
        {
            return;
        }

        if (!_draggingWindow)
        {
            return;
        }

        // Track the cursor in physical screen pixels. XAML pointer
        // coordinates are window-relative, so using them here would feed the
        // window's own movement back into the delta and make the drag jump.
        var deltaX = cursor.X - _dragStartCursor.X;
        var deltaY = cursor.Y - _dragStartCursor.Y;
        AppWindow.Move(new PointInt32(
            _dragStartPosition.X + deltaX,
            _dragStartPosition.Y + deltaY));
        e.Handled = true;
    }

    private void PiPView_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_draggingWindow)
        {
            return;
        }
        _draggingWindow = false;
        PiPView.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void PiPView_PointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _draggingWindow = false;
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
            // Keep the user's current (possibly natively resized) window
            // size; just re-assert the swap chain after the new video is
            // configured.
            ScheduleVideoSizeUpdate();
        });
    }

    private void PiPPlayer_MediaInfoChanged(MpvMediaPlayer player, MediaInfoChangedEventArgs args)
    {
        // Keep the aspect lock in sync with the current video (dwidth/dheight
        // are reported by mpv on VIDEO_RECONFIG).
        if (args.VideoWidth > 0 && args.VideoHeight > 0)
        {
            _videoAspect = args.VideoWidth / args.VideoHeight;
        }
    }

    /// <summary>
    /// Resizes the PiP window to the video aspect ratio, clamped between one
    /// twelfth and one half of the display work area.
    /// </summary>
    private SizeInt32 ApplyPiPSize(double width, double height)
    {
        try
        {
            var area = GetPiPDisplayArea().WorkArea;
            var minW = Math.Max(120, area.Width / 12.0);
            var minH = Math.Max(68, area.Height / 12.0);
            var maxW = area.Width / 2.0;
            var maxH = area.Height / 2.0;
            _resizeMinW = minW;
            _resizeMinH = minH;
            _resizeMaxW = maxW;
            _resizeMaxH = maxH;
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

    private void PiPBackButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
    }

    private void PiPExitButton_Click(object sender, RoutedEventArgs e)
    {
        // The top-right close exits the whole player (mpv + app). Persist the
        // PiP state first so the next start opens the main window normally.
        AppContext.AppSetting.WindowPiP = false;
        Application.Current.Exit();
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
        if (show)
        {
            PiPBackButton.Visibility = Visibility.Visible;
            PiPExitButton.Visibility = Visibility.Visible;
            PiPBackButton.Opacity = 1;
            PiPExitButton.Opacity = 1;
        }

        EnsureTopButtonVisuals();
        if (_topButtonsCompositor is null || _topBackButtonVisual is null || _topExitButtonVisual is null)
        {
            // Composition unavailable: snap to the target state.
            TopButtonsAnimationCompleted(show);
            return;
        }

        _topBackButtonVisual.StopAnimation("Opacity");
        _topExitButtonVisual.StopAnimation("Opacity");

        var ease = _topButtonsCompositor.CreateCubicBezierEasingFunction(
            new Vector2(0.215f, 0.61f),
            new Vector2(0.355f, 1f));
        var opacity = _topButtonsCompositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = TimeSpan.FromMilliseconds(180);
        // Start from the current composition value when hiding so a mid-show
        // reversal fades from where the buttons actually are.
        opacity.InsertKeyFrame(0f, show ? 0f : _topBackButtonVisual.Opacity);
        opacity.InsertKeyFrame(1f, show ? 1f : 0f, ease);

        var batch = _topButtonsCompositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        _topBackButtonVisual.StartAnimation("Opacity", opacity);
        _topExitButtonVisual.StartAnimation("Opacity", opacity);
        batch.Completed += (_, _) => TopButtonsAnimationCompleted(show);
        batch.End();
    }

    private void EnsureTopButtonVisuals()
    {
        if (_topBackButtonVisual is null)
        {
            _topBackButtonVisual = ElementCompositionPreview.GetElementVisual(PiPBackButton);
            _topExitButtonVisual = ElementCompositionPreview.GetElementVisual(PiPExitButton);
            _topButtonsCompositor = _topBackButtonVisual.Compositor;
        }
    }

    private void TopButtonsAnimationCompleted(bool show)
    {
        _topButtonsAnimating = false;
        _topBackButtonVisual?.StopAnimation("Opacity");
        _topExitButtonVisual?.StopAnimation("Opacity");
        if (_topBackButtonVisual is not null)
        {
            _topBackButtonVisual.Opacity = show ? 1f : 0f;
        }
        if (_topExitButtonVisual is not null)
        {
            _topExitButtonVisual.Opacity = show ? 1f : 0f;
        }
        PiPBackButton.Opacity = show ? 1 : 0;
        PiPExitButton.Opacity = show ? 1 : 0;
        if (!show)
        {
            // Fully hidden buttons must not stay hit-testable: an
            // invisible button still shows its tooltip on hover.
            PiPBackButton.Visibility = Visibility.Collapsed;
            PiPExitButton.Visibility = Visibility.Collapsed;
        }
    }

    private void StopTopButtonsAnimation()
    {
        _topBackButtonVisual?.StopAnimation("Opacity");
        _topExitButtonVisual?.StopAnimation("Opacity");
        _sizeUpdateTimer.Stop();
        _sizeUpdateTimer.Tick -= SizeUpdateTimer_Tick;
        _topButtonsAnimating = false;
        PiPBackButton.Visibility = Visibility.Visible;
        PiPExitButton.Visibility = Visibility.Visible;
        PiPBackButton.Opacity = 1;
        PiPExitButton.Opacity = 1;
        if (_topBackButtonVisual is not null)
        {
            _topBackButtonVisual.Opacity = 1f;
        }
        if (_topExitButtonVisual is not null)
        {
            _topExitButtonVisual.Opacity = 1f;
        }
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
        if (!_tearingDown && AppContext.AppSetting.WindowPiP)
        {
            args.Cancel = true;
            RestoreMainWindow();
        }
    }

    /// <summary>
    /// Closes the window for app teardown, bypassing the Alt+F4 restore
    /// behavior so ClosePiPWindow actually closes it.
    /// </summary>
    public void CloseForTeardown()
    {
        _tearingDown = true;
        Close();
    }

    private void PiPWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_closing)
        {
            return;
        }
        _closing = true;

        AppContext.LanguageChanged -= PiPWindow_LanguageChanged;
        AppWindow.Changed -= PiPAppWindow_Changed;
        unsafe
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            PInvoke.RemoveWindowSubclass(hwnd, &PiPSubclassProc, 52121);
        }
        _selfWeakReference = null;
        StopTopButtonsAnimation();
        Detach();
        Closed -= PiPWindow_Closed;
    }

}
