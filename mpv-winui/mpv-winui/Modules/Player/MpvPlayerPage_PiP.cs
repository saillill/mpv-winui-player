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
        if (_pipWindow is null)
        {
            _pipWindow = new PiPWindow();
            _pipWindow.Attach(_mediaPlayer);
            _pipWindow.VideoPanel.CompositionScaleChanged += PiPView_CompositionScaleChanged;
            _pipWindow.VideoPanel.SizeChanged += PiPView_SizeChanged;
        }
        else
        {
            _pipWindow.Attach(_mediaPlayer);
        }

        var (width, height) = ParsePiPSize(AppContext.AppSetting.WindowPiPSize);

        // Move the existing composition swap chain into the PiP window; libmpv
        // keeps rendering to it, so no second render context is needed.
        _mediaPlayer.UpdatePanel(_pipWindow.VideoPanel);
        _mediaPlayer.UpdateSize((uint)width, (uint)height);
        _pipWindow.ShowPiP(width, height);
        UpdatePiPPanelScale();
        // Re-assert the swap chain size against the laid-out panel: before
        // ShowPiP the panel has no real size, so the first UpdateSize above
        // may leave the video rendered at the previous surface size.
        var panelScaleX = _pipWindow.VideoPanel.CompositionScaleX;
        var panelScaleY = _pipWindow.VideoPanel.CompositionScaleY;
        _mediaPlayer.UpdateSize(
            (uint)Math.Ceiling(_pipWindow.VideoPanel.ActualWidth * panelScaleX),
            (uint)Math.Ceiling(_pipWindow.VideoPanel.ActualHeight * panelScaleY));

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
            _pipWindow.VideoPanel.SizeChanged -= PiPView_SizeChanged;
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
        UpdatePiPPanelSize();
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

    private void PiPView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdatePiPPanelSize();
    }

    /// <summary>
    /// Keeps the libmpv swap chain at the PiP panel's physical size. Without
    /// this the video keeps the main window's surface size after repeated
    /// PiP toggles and only the top-left corner is visible.
    /// </summary>
    private void UpdatePiPPanelSize()
    {
        if (_pipWindow is { } pipWindow)
        {
            _mediaPlayer.UpdateSize(
                (uint)Math.Ceiling(pipWindow.VideoPanel.ActualWidth * pipWindow.VideoPanel.CompositionScaleX),
                (uint)Math.Ceiling(pipWindow.VideoPanel.ActualHeight * pipWindow.VideoPanel.CompositionScaleY));
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

    private static (int Width, int Height) ParsePiPSize(string value)
    {
        var parts = value.Split('x');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var width)
            && int.TryParse(parts[1], out var height)
            && width >= 160
            && height >= 90)
        {
            return (width, height);
        }
        return (480, 270);
    }

    private void ClosePiPWindow()
    {
        if (_pipWindow is { } pipWindow)
        {
            pipWindow.VideoPanel.CompositionScaleChanged -= PiPView_CompositionScaleChanged;
            pipWindow.VideoPanel.SizeChanged -= PiPView_SizeChanged;
            pipWindow.Detach();
            pipWindow.Close();
            _pipWindow = null;
        }
    }
}
