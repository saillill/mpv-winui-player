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
    private double _resizeMinW = 400;
    private double _resizeMinH = 225;
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
        // Hand the subtitle toggle to the compact status bar (far right).
        // PlayerControl builds a fresh status-bar toggle from this source on
        // attach (the XAML element itself cannot be reparented), so this one
        // stays collapsed as the state/icon authority.
        PiPControls.PiPRightToggle = PiPSubtitleToggle;
        PiPControls.PiPRightToggleAction = visible => SetSubtitleVisibility(visible);
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
        ApplyOpacity();
        // Always claim the bottom-right corner of the main window's display
        // on entry (the user can drag the window around afterwards).
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

    /// <summary>Re-applies the configured opacity to an open mini player.
    /// Below full opacity the black background is replaced with transparent
    /// so the XAML content becomes see-through against the desktop.</summary>
    public void ApplyOpacity()
    {
        // Whole-window alpha via a layered window: XAML Opacity on the root
        // only fades the content toward the compositor's black backdrop
        // (darkening the video instead of showing the desktop through).
        var opacity = Math.Clamp(AppContext.AppSetting.WindowPiPOpacity, 0.2, 1.0);
        var hwnd = new HWND(WindowNative.GetWindowHandle(this));
        const int WS_EX_LAYERED = 0x00080000;
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | WS_EX_LAYERED);
        PInvoke.SetLayeredWindowAttributes(hwnd, default(COLORREF), (byte)Math.Round(opacity * 255), SET_LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
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








    private void ApplyLocalizedStrings()
    {
        ToolTipService.SetToolTip(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        ToolTipService.SetToolTip(PiPExitButton, AppContext.AppLang.PiPExit);
        AutomationProperties.SetName(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        AutomationProperties.SetName(PiPExitButton, AppContext.AppLang.PiPExit);
        ToolTipService.SetToolTip(PiPSubtitleToggle, AppContext.AppLang.Subtitles);
        AutomationProperties.SetName(PiPSubtitleToggle, AppContext.AppLang.Subtitles);
    }

    private void PiPWindow_LanguageChanged()
    {
        ApplyLocalizedStrings();
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
