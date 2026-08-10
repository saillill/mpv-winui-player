using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
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
    private readonly DispatcherTimer _topButtonsTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private long _topButtonsStart;
    private bool _topButtonsShow;

    public PiPWindow()
    {
        Instance = this;
        InitializeComponent();
        RootGrid.RequestedTheme = ElementTheme.Dark;
        ConfigureWindow();
        ApplyLocalizedStrings();

        PiPView.PointerPressed += PiPView_PointerPressed;
        RootGrid.PointerMoved += RootGrid_PointerMoved;

        AppWindow.Closing += AppWindow_Closing;
        Closed += PiPWindow_Closed;
    }

    /// <summary>The video surface that libmpv renders into.</summary>
    public SwapChainPanel VideoPanel => PiPView;

    public void Attach(MpvMediaPlayer player)
    {
        _player = player;
        PiPControls.MediaPlayer = player;
        PiPControls.IsPiPHost = true;
        PiPControls.OnPanelVisibleChanged += PiPControls_OnPanelVisibleChanged;
    }

    public void Detach()
    {
        if (_player is not null)
        {
            PiPControls.OnPanelVisibleChanged -= PiPControls_OnPanelVisibleChanged;
            PiPControls.MediaPlayer = null;
            _player = null;
        }
    }

    public void ShowPiP(int width, int height)
    {
        PositionAtBottomRight(width, height);
        AppWindow.Show();
        PiPControls.ShowControlPanel();
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
            var hwnd = WindowNative.GetWindowHandle(this);
            var preference = 2; // DWMWCP_ROUND
            unsafe
            {
                _ = DwmSetWindowAttribute(hwnd, 33, &preference, sizeof(int));
                var borderColor = 0x000000; // black BGR, hides the light top border
                _ = DwmSetWindowAttribute(hwnd, 34, &borderColor, sizeof(int)); // DWMWA_BORDER_COLOR
                _ = DwmSetWindowAttribute(hwnd, 35, &borderColor, sizeof(int)); // DWMWA_CAPTION_COLOR
                var ncPolicy = 2; // DWMNCRP_DISABLED: content fills the whole window
                _ = DwmSetWindowAttribute(hwnd, 2, &ncPolicy, sizeof(int));    // DWMWA_NCRENDERING_POLICY
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
            var hwnd = WindowNative.GetWindowHandle(this);
            const int GWL_STYLE = -16;
            const int WS_BORDER = 0x00800000;
            const int WS_DLGFRAME = 0x00400000;
            const int WS_THICKFRAME = 0x00040000;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_FRAMECHANGED = 0x0020;

            var style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_BORDER | WS_DLGFRAME | WS_THICKFRAME);
            _ = SetWindowLong(hwnd, GWL_STYLE, style);
            _ = SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
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

        // Hand the press to the system caption: the modal move loop starts
        // immediately and ends on release, so the window never sticks to the
        // cursor. This makes the whole video area draggable like browser PiP.
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _ = SendMessage(hwnd, 0x00A1, new IntPtr(2), IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
        }
        catch (Exception)
        {
            // Drag is optional; ignore failures on exotic input.
        }
    }

    private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(RootGrid).Position;
        var inMask = position.Y >= RootGrid.ActualHeight - 120;
        if (inMask)
        {
            PiPControls.ShowControlPanel();
        }
        else
        {
            PiPControls.HideControlPanel();
        }
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

    private void PiPBackButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
    }

    private void PiPExitButton_Click(object sender, RoutedEventArgs e)
    {
        // The top-right close ends the application directly.
        Application.Current.Exit();
    }

    /// <summary>Fades the top-left back and top-right close buttons with the control bar.</summary>
    private void PiPControls_OnPanelVisibleChanged(bool hide)
    {
        DispatcherQueue.TryEnqueue(() => StartTopButtonsAnimation(!hide));
    }

    private void StartTopButtonsAnimation(bool show)
    {
        _topButtonsShow = show;
        _topButtonsStart = Environment.TickCount64;
        _topButtonsTimer.Tick -= TopButtonsTick;
        _topButtonsTimer.Tick += TopButtonsTick;
        _topButtonsTimer.Start();
    }

    private void TopButtonsTick(object? sender, object e)
    {
        const double durationMs = 180;
        var t = Math.Clamp((Environment.TickCount64 - _topButtonsStart) / durationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
        var opacity = _topButtonsShow ? eased : 1 - eased;
        PiPBackButton.Opacity = opacity;
        PiPExitButton.Opacity = opacity;
        if (t >= 1)
        {
            _topButtonsTimer.Stop();
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

        _topButtonsTimer.Stop();
        Detach();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
        Closed -= PiPWindow_Closed;
    }

    [DllImport("dwmapi.dll")]
    private static extern unsafe int DwmSetWindowAttribute(IntPtr hwnd, int attribute, int* value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
