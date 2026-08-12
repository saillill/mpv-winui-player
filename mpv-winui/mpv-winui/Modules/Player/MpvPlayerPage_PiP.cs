using Microsoft.UI.Windowing;
using System;

namespace mpv_winui.Modules.Player;

public sealed partial class MpvPlayerPage
{
    private PiPWindow? _pipWindow;

    /// <summary>Enters or exits the dedicated picture-in-picture window.</summary>
    public void ApplyPiP()
    {
        if (AppContext.AppSetting.WindowPiP)
        {
            EnterPiP();
        }
        else
        {
            ExitPiP();
        }
    }

    private void EnterPiP()
    {
        // Startup ApplyPiP() can now only run after initialization, but keep
        // the guard so any future early call path cannot race AttachSwapChain.
        if (!_isPlayerInitialized)
        {
            return;
        }

        if (_pipWindow is null)
        {
            _pipWindow = new PiPWindow();
            _pipWindow!.Attach(_mediaPlayer);
            _pipWindow.VideoPanel.CompositionScaleChanged += PiPView_CompositionScaleChanged;
        }
        else
        {
            // Re-attach after an exit detached the window. Attach is idempotent
            // when the same player is already attached (e.g. a WindowPiPSize
            // change while PiP is active), so the event cannot stack.
            _pipWindow!.Attach(_mediaPlayer);
        }

        var (width, height) = ComputeDefaultPiPSize();
        var pipWindow = _pipWindow!;

        // Move the existing composition swap chain into the PiP window; libmpv
        // keeps rendering to it, so no second render context is needed.
        _mediaPlayer.UpdatePanel(pipWindow.VideoPanel);
        _mediaPlayer.UpdateSize((uint)width, (uint)height);
        pipWindow.ShowPiP(width, height);
        UpdatePiPPanelScale();

        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.HideForPiP();
        }

        PlayerControl.UpdatePiPBar();
    }

    private void ExitPiP()
    {
        // ApplyPiP() is also called on startup when WindowPiP is false; if we
        // never entered PiP there is nothing to tear down, and re-attaching
        // the swap chain here races mpv initialization (crash in
        // AttachSwapChain when mpv is not created yet).
        if (_pipWindow is null)
        {
            return;
        }

        if (_pipWindow is { } pipWindow)
        {
            _pipWindow.VideoPanel.CompositionScaleChanged -= PiPView_CompositionScaleChanged;
            _pipWindow.HidePiP();
            _pipWindow.Detach();
        }

        // Re-attach the swap chain to the main window's video surface.
        _mediaPlayer.UpdatePanel(PlayerView);
        UpdateMainViewSize();
        _mediaPlayer.UpdatePanelScale(
            (float)PlayerView.CompositionScaleX,
            (float)PlayerView.CompositionScaleY);

        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.RestoreFromPiP();
        }

        // WinUI 3: hiding and re-showing a window that is in the FullScreen
        // presenter leaves the XAML content island stale - the video swap
        // chain keeps rendering, but the overlay (control bar) stops painting
        // and stops receiving pointer events. Cycling the presenter forces
        // the content island to rebuild, which restores the overlay.
        if (_isFullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        PlayerControl.SetOverlayMode(_isFullWindow || _isFullScreen);
        PlayerControl.ShowControlPanel();
        PlayerControl.UpdatePiPBar();
    }

    private void PiPView_CompositionScaleChanged(Microsoft.UI.Xaml.Controls.SwapChainPanel sender, object args)
    {
        UpdatePiPPanelScale();
    }

    private void UpdatePiPPanelScale()
    {
        if (_pipWindow is { } pipWindow)
        {
            _mediaPlayer.UpdatePanelScale(
                (float)pipWindow.VideoPanel.CompositionScaleX,
                (float)pipWindow.VideoPanel.CompositionScaleY);
        }
    }

    private void UpdateMainViewSize()
    {
        UpdatePlayerViewSize(new ViewSize(
            PlayerView.ActualWidth,
            PlayerView.ActualHeight,
            PlayerView.CompositionScaleX,
            PlayerView.CompositionScaleY));
    }

    /// <summary>
    /// Default PiP size as a proportion of the main window's display work
    /// area so it scales across different resolutions. The three presets map
    /// to 15% / 25% / 35% of the work-area width; height follows 16:9 and the
    /// window re-fits to the current video aspect on entry.
    /// </summary>
    private (int Width, int Height) ComputeDefaultPiPSize()
    {
        var fraction = ParsePiPSizeFraction(AppContext.AppSetting.WindowPiPSize);
        DisplayArea area;
        try
        {
            area = DisplayArea.GetFromWindowId(
                App.Window is MainWindow mainWindow ? mainWindow.AppWindow.Id : _appWindow.Id,
                DisplayAreaFallback.Nearest);
        }
        catch (Exception)
        {
            area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        }

        var work = area.WorkArea;
        var maxWidth = Math.Max(320, (int)(work.Width / 2.0));
        var width = Math.Clamp((int)(work.Width * fraction), 320, maxWidth);
        var height = Math.Max(180, (int)(width / (16.0 / 9.0)));
        return (width, height);
    }

    private static double ParsePiPSizeFraction(string value)
    {
        return value switch
        {
            "320x180" => 0.15,
            "640x360" => 0.35,
            _ => 0.25,
        };
    }

    private void ClosePiPWindow()
    {
        if (_pipWindow is { } pipWindow)
        {
            pipWindow.VideoPanel.CompositionScaleChanged -= PiPView_CompositionScaleChanged;
            pipWindow.Detach();
            pipWindow.CloseForTeardown();
            _pipWindow = null;
        }
    }
}
