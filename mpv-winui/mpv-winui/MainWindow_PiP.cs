using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using mpv_winui.Modules.Player;
using Windows.Graphics;

namespace mpv_winui;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Applies picture-in-picture mode. The video moves to a dedicated PiP
    /// window and the main window is hidden until PiP is left.
    /// </summary>
    public void ApplyPiP()
    {
        if (ShellFrame?.Content is MpvPlayerPage page)
        {
            page.ApplyPiP();
        }
    }

    /// <summary>Hides the main window while the PiP window is shown.</summary>
    public void HideForPiP()
    {
        AppWindow.Hide();
    }

    /// <summary>Shows the main window again after leaving picture-in-picture.</summary>
    public void RestoreFromPiP()
    {
        AppWindow.Show();
    }
}
