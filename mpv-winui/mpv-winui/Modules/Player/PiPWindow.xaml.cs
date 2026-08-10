using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Dedicated picture-in-picture window: a borderless always-on-top window with
/// rounded corners, the video swap chain, and a fullscreen-style control bar
/// (volume, seek, play/pause) with a progress line. The main window is hidden
/// while PiP is active; PiP can only be left by restoring the main window.
/// </summary>
public sealed partial class PiPWindow : Window
{
    public static PiPWindow? Instance { get; private set; }

    private MpvMediaPlayer? _player;
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _hideControlsTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _closing;

    public PiPWindow()
    {
        Instance = this;
        InitializeComponent();
        ConfigureWindow();
        ApplyLocalizedStrings();

        PiPView.PointerMoved += (_, _) => ShowControlsTemporarily();
        PiPView.PointerPressed += (_, _) => ShowControlsTemporarily();
        RootGrid.PointerMoved += (_, _) => ShowControlsTemporarily();
        DragArea.PointerPressed += DragArea_PointerPressed;

        _positionTimer.Tick += (_, _) => UpdateProgress();
        _hideControlsTimer.Tick += (_, _) =>
        {
            _hideControlsTimer.Stop();
            ControlsPanel.Visibility = Visibility.Collapsed;
        };

        AppWindow.Closing += AppWindow_Closing;
        Closed += PiPWindow_Closed;
    }

    /// <summary>The video surface that libmpv renders into.</summary>
    public SwapChainPanel VideoPanel => PiPView;

    public void Attach(MpvMediaPlayer player)
    {
        _player = player;
        _player.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.VolumeChangedChanged += OnVolumeChanged;
        UpdatePlayButton();
        UpdateVolumeButton();
    }

    public void Detach()
    {
        if (_player is { } player)
        {
            player.PlaybackStateChanged -= OnPlaybackStateChanged;
            player.VolumeChangedChanged -= OnVolumeChanged;
            _player = null;
        }
    }

    public void ShowPiP(int width, int height)
    {
        AppWindow.MoveAndResize(new RectInt32(
            Math.Max(0, AppWindow.Position.X),
            Math.Max(0, AppWindow.Position.Y),
            Math.Max(160, width),
            Math.Max(90, height)));
        AppWindow.Show();
        ShowControlsTemporarily();
        _positionTimer.Start();
        UpdateProgress();
    }

    public void HidePiP()
    {
        _positionTimer.Stop();
        _hideControlsTimer.Stop();
        AppWindow.Hide();
    }

    private void ConfigureWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        AppWindow.Title = "Picture in picture";
        AppWindow.SetIcon("App.ico");

        ApplyRoundedCorners();
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
            }
        }
        catch (Exception)
        {
            // Rounded corners are cosmetic; older builds keep square corners.
        }
    }

    private void DragArea_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }

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

    private void ApplyLocalizedStrings()
    {
        ToolTipService.SetToolTip(PiPBackButton, AppContext.AppLang.PiPBackToPlayer);
        ToolTipService.SetToolTip(PiPExitButton, AppContext.AppLang.PiPExit);
        ToolTipService.SetToolTip(PiPVolumeButton, AppContext.AppLang.PiPMute);
        ToolTipService.SetToolTip(PiPBackwardButton, AppContext.AppLang.PiPBackward);
        ToolTipService.SetToolTip(PiPPlayPauseButton, AppContext.AppLang.Play);
        ToolTipService.SetToolTip(PiPForwardButton, AppContext.AppLang.PiPForward);
    }

    private void ShowControlsTemporarily()
    {
        ControlsPanel.Visibility = Visibility.Visible;
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void UpdateProgress()
    {
        if (_player is not { } player)
        {
            return;
        }

        var duration = player.Duration;
        if (duration > 0)
        {
            PiPProgressBar.Value = Math.Clamp(player.Position / duration, 0, 1);
        }
        else
        {
            PiPProgressBar.Value = 0;
        }
    }

    private void OnPlaybackStateChanged(MpvMediaPlayer player, bool isPaused)
    {
        DispatcherQueue.TryEnqueue(UpdatePlayButton);
    }

    private void OnVolumeChanged(MpvMediaPlayer player, int volume)
    {
        DispatcherQueue.TryEnqueue(UpdateVolumeButton);
    }

    private void UpdatePlayButton()
    {
        PiPPlayPauseSymbol.Glyph = _player is { Playing: true } ? "\uF8AE" : "\uF5B0";
    }

    private void UpdateVolumeButton()
    {
        PiPVolumeSymbol.Glyph = _player is { Volume: <= 0 } ? "\uE74F" : "\uE995";
    }

    private void PiPView_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void PiPPlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void TogglePlayPause()
    {
        if (_player is not { } player)
        {
            return;
        }

        if (player.Playing)
        {
            player.Pause();
        }
        else
        {
            player.Play();
        }
        ShowControlsTemporarily();
    }

    private void PiPBackwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_player is { } player)
        {
            player.Position = Math.Max(0, player.Position - 10);
        }
        ShowControlsTemporarily();
    }

    private void PiPForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_player is { } player)
        {
            player.Position += 10;
        }
        ShowControlsTemporarily();
    }

    private void PiPVolumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_player is not { } player)
        {
            return;
        }

        var flyout = new Flyout
        {
            Content = new VolumeFlyoutControl(player),
            Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
        };
        flyout.ShowAt(PiPVolumeButton);
        ShowControlsTemporarily();
    }

    private void PiPBackButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
    }

    private void PiPExitButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
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

        _positionTimer.Stop();
        _hideControlsTimer.Stop();
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
}
