using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
/// Video surface that can switch its cursor from outside the element.
/// UIElement.ProtectedCursor is protected, so a tiny wrapper exposes the
/// resize cursors for the edge zones (WinUI 3 owns cursor input, which is why
/// WM_NCHITTEST/WM_SETCURSOR alone never showed the arrows reliably).
/// </summary>
public sealed partial class PiPCursorSurface : SwapChainPanel
{
    private InputCursor? _sizeWE;
    private InputCursor? _sizeNS;
    private InputCursor? _sizeNWSE;
    private InputCursor? _sizeNESW;

    public void SetResizeCursor(InputSystemCursorShape? shape)
    {
        ProtectedCursor = shape switch
        {
            InputSystemCursorShape.SizeWestEast => _sizeWE ??= InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            InputSystemCursorShape.SizeNorthSouth => _sizeNS ??= InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth),
            InputSystemCursorShape.SizeNorthwestSoutheast => _sizeNWSE ??= InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast),
            InputSystemCursorShape.SizeNortheastSouthwest => _sizeNESW ??= InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest),
            _ => null,
        };
    }
}

/// <summary>
/// Dedicated picture-in-picture window: a borderless always-on-top window
/// with rounded corners. Native edge resize (OS size cursors + border drag)
/// is kept, but WM_NCCALCSIZE hides the frame so no border is drawn, and
/// WM_SIZING locks the video aspect while anchoring the window at its
/// bottom-right corner. Drag-anywhere moving and the video swap chain
/// complete the window. Entering PiP always claims the bottom-right corner
/// of the main window's display and the default size is a proportion of that
/// display's work area. The main window is hidden while PiP is active; the
/// top-left button restores it, the top-right button quits the whole player,
/// and Alt+F4 restores the main window.
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
    private const double ResizeBorderDips = 8;
    private const int ResizeBorder = 8;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private double _videoAspect = 16.0 / 9.0;
    private double _resizeMinW = 320;
    private double _resizeMinH = 180;
    private double _resizeMaxW = 960;
    private double _resizeMaxH = 540;
    [Flags]
    private enum ResizeZone
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
    }
    private bool _resizing;
    private ResizeZone _resizeZone;
    private PointInt32 _resizeStartCursor;
    private RectInt32 _resizeStartRect;
    private bool _draggingWindow;
    private PointInt32 _dragStartCursor;
    private PointInt32 _dragStartPosition;
    private RECT _sizingAnchorRect;
    private static WeakReference<PiPWindow>? _selfWeakReference;
    private static readonly HCURSOR _cursorSizeWE = LoadSizeCursor(32644);    // IDC_SIZEWE
    private static readonly HCURSOR _cursorSizeNS = LoadSizeCursor(32645);    // IDC_SIZENS
    private static readonly HCURSOR _cursorSizeNWSE = LoadSizeCursor(32642);  // IDC_SIZENWSE
    private static readonly HCURSOR _cursorSizeNESW = LoadSizeCursor(32643);  // IDC_SIZENESW

    private static HCURSOR LoadSizeCursor(int id)
    {
        unsafe
        {
            return PInvoke.LoadCursor(HINSTANCE.Null, new PCWSTR((char*)(nint)id));
        }
    }

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
        RootGrid.Opacity = Math.Clamp(AppContext.AppSetting.WindowPiPOpacity, 0.2, 1.0);
        PiPOpacitySlider.Value = RootGrid.Opacity;
        // H2: restore the saved position/size when present, otherwise claim
        // the bottom-right corner of the main window's display on entry; the
        // user can drag the window afterwards.
        if (!RestoreSavedRect(width, height))
        {
            PositionAtBottomRight(width, height);
        }
        PiPControls.ApplyControlBarStyle();
        AppWindow.Show();
        ApplyPiPSize(width, height);
        ScheduleVideoSizeUpdate();
    }

    /// <summary>Restores the persisted PiP rect; clamps it into the work area of its display.</summary>
    private bool RestoreSavedRect(int defaultWidth, int defaultHeight)
    {
        var saved = AppContext.AppSetting.WindowPiPRect;
        if (string.IsNullOrEmpty(saved))
        {
            return false;
        }
        try
        {
            var parts = saved.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }
            var rect = new RectInt32(
                int.Parse(parts[0]), int.Parse(parts[1]),
                int.Parse(parts[2]), int.Parse(parts[3]));
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }
            var work = GetPiPDisplayArea().WorkArea;
            rect.Width = Math.Min(rect.Width, work.Width);
            rect.Height = Math.Min(rect.Height, work.Height);
            rect.X = Math.Clamp(rect.X, work.X, work.X + work.Width - rect.Width);
            rect.Y = Math.Clamp(rect.Y, work.Y, work.Y + work.Height - rect.Height);
            AppWindow.MoveAndResize(rect);
            MakeFrameless();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Persists the current PiP window rect for the next entry.</summary>
    private void SaveCurrentRect()
    {
        try
        {
            var p = AppWindow.Position;
            var size = AppWindow.Size;
            AppContext.AppSetting.WindowPiPRect = $"{p.X},{p.Y},{size.Width},{size.Height}";
        }
        catch (Exception)
        {
        }
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
            // The window must be reported as resizable so the OS shows the
            // size cursors and accepts the native SC_SIZE loop. The frame is
            // then hidden with WM_NCCALCSIZE (client = whole window) and the
            // border drag is anchored bottom-right by WM_SIZING.
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        AppWindow.Title = "Picture in picture";
        AppWindow.SetIcon("App.ico");

        ApplyRoundedCorners();
        MakeFrameless();

        // WM_NCHITTEST only: the OS shows the standard size cursors over the
        // 8px edge zones. The actual resize is handled by the XAML pointer
        // handlers (bottom-right anchored, aspect locked), which keeps the
        // window fully frameless and WinUI input intact.
        unsafe
        {
            var hwnd = new HWND(WindowNative.GetWindowHandle(this));
            PInvoke.SetWindowSubclass(hwnd, &PiPSubclassProc, 52121, 0);
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
        const int WM_NCHITTEST = 0x0084;
        const int WM_SETCURSOR = 0x0020;
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_ENTERSIZEMOVE = 0x0231;
        const int WM_SIZING = 0x0214;

        if (uMsg == WM_SETCURSOR
            && PInvoke.GetCursorPos(out var cursor)
            && PInvoke.GetWindowRect(hWnd, out var cursorRect))
        {
            // WinUI 3 does not go through the legacy WM_NCHITTEST hover path,
            // so the OS never shows the resize cursors by itself. Set them
            // explicitly here from the cursor position over the 8px edge
            // zones; the native SC_SIZE drag still runs on button-down.
            var x = cursor.X;
            var y = cursor.Y;
            var nearLeft = x >= cursorRect.left && x < cursorRect.left + ResizeBorder;
            var nearRight = x >= cursorRect.right - ResizeBorder && x < cursorRect.right;
            var nearTop = y >= cursorRect.top && y < cursorRect.top + ResizeBorder;
            var nearBottom = y >= cursorRect.bottom - ResizeBorder && y < cursorRect.bottom;

            HCURSOR? resizeCursor = null;
            if (nearTop && nearLeft || nearBottom && nearRight)
            {
                resizeCursor = _cursorSizeNWSE;
            }
            else if (nearTop && nearRight || nearBottom && nearLeft)
            {
                resizeCursor = _cursorSizeNESW;
            }
            else if (nearLeft || nearRight)
            {
                resizeCursor = _cursorSizeWE;
            }
            else if (nearTop || nearBottom)
            {
                resizeCursor = _cursorSizeNS;
            }

            if (resizeCursor is { } handle)
            {
                _ = PInvoke.SetCursor(handle);
                return (LRESULT)1;
            }
        }

        if (uMsg == WM_NCCALCSIZE && wParam.Value != 0)
        {
            // Client area = whole window: hides the DWM resize frame that
            // would otherwise draw the black border while keeping the window
            // resizable for the OS.
            return (LRESULT)0;
        }

        if (uMsg == WM_ENTERSIZEMOVE
            && _selfWeakReference?.TryGetTarget(out var sizingSelf) == true)
        {
            PInvoke.GetWindowRect(hWnd, out sizingSelf._sizingAnchorRect);
        }

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

        if (uMsg == WM_NCHITTEST
            && PInvoke.GetWindowRect(hWnd, out var windowRect))
        {
            var x = (short)((int)lParam.Value & 0xFFFF);
            var y = (short)(((int)lParam.Value >> 16) & 0xFFFF);
            var nearLeft = x >= windowRect.left && x < windowRect.left + ResizeBorder;
            var nearRight = x >= windowRect.right - ResizeBorder && x < windowRect.right;
            var nearTop = y >= windowRect.top && y < windowRect.top + ResizeBorder;
            var nearBottom = y >= windowRect.bottom - ResizeBorder && y < windowRect.bottom;

            if (nearTop && nearLeft) return (LRESULT)HTTOPLEFT;
            if (nearTop && nearRight) return (LRESULT)HTTOPRIGHT;
            if (nearBottom && nearLeft) return (LRESULT)HTBOTTOMLEFT;
            if (nearBottom && nearRight) return (LRESULT)HTBOTTOMRIGHT;
            if (nearLeft) return (LRESULT)HTLEFT;
            if (nearRight) return (LRESULT)HTRIGHT;
            if (nearTop) return (LRESULT)HTTOP;
            if (nearBottom) return (LRESULT)HTBOTTOM;
        }

        return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Adjusts the native WM_SIZING drag rectangle: the video aspect is kept
    /// and the window's bottom-right corner (recorded at WM_ENTERSIZEMOVE)
    /// stays fixed, so the PiP scales from the top-left while remaining
    /// docked at its bottom-right position.
    /// </summary>
    private bool AdjustSizingRect(int edge, ref RECT rect)
    {
        const int WMSZ_TOP = 3;
        const int WMSZ_BOTTOM = 6;

        var aspect = _videoAspect > 0 ? _videoAspect : 16.0 / 9.0;
        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;

        double w;
        double h;
        if (edge is WMSZ_TOP or WMSZ_BOTTOM)
        {
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

        rect.right = _sizingAnchorRect.right;
        rect.bottom = _sizingAnchorRect.bottom;
        rect.left = rect.right - newW;
        rect.top = rect.bottom - newH;
        return true;
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
        if (args.DidSizeChange || args.DidPositionChange)
        {
            SaveCurrentRect();
        }
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
            // Keep WS_THICKFRAME (intentionally not cleared) so the OS
            // (0x00040000) accepts the native resize loop and shows the size
            // cursors; drop only the visible frame styles. WM_NCCALCSIZE then
            // hides the frame entirely by making the client cover the window.
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
        ToolTipService.SetToolTip(PiPSettingsButton, AppContext.AppLang.AppSetting);
        AutomationProperties.SetName(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        AutomationProperties.SetName(PiPExitButton, AppContext.AppLang.PiPExit);
        AutomationProperties.SetName(PiPSettingsButton, AppContext.AppLang.AppSetting);
        PiPOpacityLabel.Text = AppContext.AppLang.PiPWindowOpacity;
        AutomationProperties.SetName(PiPOpacitySlider, AppContext.AppLang.PiPWindowOpacity);
        PiPSubtitleToggle.Content = AppContext.AppLang.Subtitles;
        AutomationProperties.SetName(PiPSubtitleToggle, AppContext.AppLang.Subtitles);
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

        var zone = GetResizeZone(point.Position, PiPView.ActualWidth, PiPView.ActualHeight);
        if (zone != ResizeZone.None)
        {
            // Edge/corner press: start the XAML resize loop (bottom-right
            // anchored, aspect locked). WM_NCHITTEST only provides the cursor.
            _resizing = true;
            _resizeZone = zone;
            _resizeStartCursor = new PointInt32(cursor.X, cursor.Y);
            _resizeStartRect = new RectInt32(
                AppWindow.Position.X, AppWindow.Position.Y,
                AppWindow.Size.Width, AppWindow.Size.Height);
            PiPView.CapturePointer(e.Pointer);
            e.Handled = true;
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

        if (_resizing)
        {
            ApplyResize(cursor.X - _resizeStartCursor.X, cursor.Y - _resizeStartCursor.Y);
            e.Handled = true;
            return;
        }

        if (!_draggingWindow)
        {
            UpdateResizeCursor(e.GetCurrentPoint(PiPView).Position);
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
        if (!_resizing && !_draggingWindow)
        {
            return;
        }
        var wasResizing = _resizing;
        _resizing = false;
        _draggingWindow = false;
        PiPView.ReleasePointerCapture(e.Pointer);
        PiPView.SetResizeCursor(null);
        if (wasResizing)
        {
            // Safety net: re-assert the swap chain size once layout settles.
            ScheduleVideoSizeUpdate();
        }
        SaveCurrentRect();
        e.Handled = true;
    }

    private void PiPView_PointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _resizing = false;
        _draggingWindow = false;
        PiPView.SetResizeCursor(null);
    }

    private void UpdateResizeCursor(Point position)
    {
        var zone = GetResizeZone(position, PiPView.ActualWidth, PiPView.ActualHeight);
        PiPView.SetResizeCursor(zone switch
        {
            ResizeZone.Left or ResizeZone.Right => InputSystemCursorShape.SizeWestEast,
            ResizeZone.Top or ResizeZone.Bottom => InputSystemCursorShape.SizeNorthSouth,
            ResizeZone.Top | ResizeZone.Left or ResizeZone.Bottom | ResizeZone.Right => InputSystemCursorShape.SizeNorthwestSoutheast,
            ResizeZone.Top | ResizeZone.Right or ResizeZone.Bottom | ResizeZone.Left => InputSystemCursorShape.SizeNortheastSouthwest,
            _ => (InputSystemCursorShape?)null,
        });
    }

    private ResizeZone GetResizeZone(Point position, double width, double height)
    {
        var zone = ResizeZone.None;
        if (position.X <= ResizeBorderDips)
        {
            zone |= ResizeZone.Left;
        }
        if (position.X >= width - ResizeBorderDips)
        {
            zone |= ResizeZone.Right;
        }
        if (position.Y <= ResizeBorderDips)
        {
            zone |= ResizeZone.Top;
        }
        if (position.Y >= height - ResizeBorderDips)
        {
            zone |= ResizeZone.Bottom;
        }
        return zone;
    }

    /// <summary>
    /// Edge/corner resize anchored at the bottom-right corner: the window
    /// scales from the top-left while its bottom-right corner stays fixed and
    /// the size keeps <see cref="_videoAspect"/>.
    /// </summary>
    private void ApplyResize(double deltaX, double deltaY)
    {
        try
        {
            var start = _resizeStartRect;
            var aspect = _videoAspect > 0 ? _videoAspect : 16.0 / 9.0;

            double w;
            if ((_resizeZone & (ResizeZone.Left | ResizeZone.Right)) != 0)
            {
                w = start.Width + ((_resizeZone & ResizeZone.Right) != 0 ? deltaX : -deltaX);
            }
            else
            {
                var height = start.Height + ((_resizeZone & ResizeZone.Bottom) != 0 ? deltaY : -deltaY);
                w = height * aspect;
            }

            w = Math.Clamp(w, _resizeMinW, _resizeMaxW);
            var h = w / aspect;
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
            w = Math.Clamp(w, _resizeMinW, _resizeMaxW);
            h = Math.Clamp(h, _resizeMinH, _resizeMaxH);

            var newW = (int)Math.Round(w);
            var newH = (int)Math.Round(h);
            var newLeft = start.X + start.Width - newW;
            var newTop = start.Y + start.Height - newH;

            AppWindow.MoveAndResize(new RectInt32(newLeft, newTop, newW, newH));
            _player?.UpdateSize((uint)newW, (uint)newH);
        }
        catch (Exception)
        {
            // Resizing is optional; ignore failures on exotic displays.
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
        PiPView.SetResizeCursor(null);
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

    private void PiPOpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // H4: window opacity. RootGrid.Opacity fades the whole PiP content
        // (video + controls); persisted so the choice survives restarts.
        var value = Math.Round(e.NewValue, 2);
        RootGrid.Opacity = value;
        AppContext.AppSetting.WindowPiPOpacity = value;
    }

    private void PiPSubtitleToggle_Click(object sender, RoutedEventArgs e)
    {
        // H5: quick subtitle toggle on the PiP window.
        _player?.Command(["no-osd", "cycle", "sub-visibility"]);
        PiPSubtitleToggle.IsChecked = !PiPSubtitleToggle.IsChecked;
    }

    private void PiPView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Double-click anywhere on the PiP video restores the main window
        // (equivalent to the top-left back button). Single-click stays a
        // drag/mpv click; the double-tap only fires when no drag happened.
        if (!_draggingWindow && !_resizing)
        {
            RestoreMainWindow();
        }
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
