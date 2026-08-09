using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace mpv_winui;

public sealed partial class MainWindow : Window
{
    private RectInt32? _prePiPRect;

    /// <summary>Applies the picture-in-picture (mini player) mode from the current settings.</summary>
    public void ApplyPiP()
    {
        if (AppContext.AppSetting.WindowPiP)
        {
            if (_prePiPRect is null)
            {
                _prePiPRect = new RectInt32(_x, _y, _w, _h);
            }

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
            }

            var parts = AppContext.AppSetting.WindowPiPSize.Split('x');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var width)
                && int.TryParse(parts[1], out var height))
            {
                AppWindow.MoveAndResize(new RectInt32(_prePiPRect?.X ?? _x, _prePiPRect?.Y ?? _y, width, height));
            }

            ChangeFullWindow(true);
        }
        else
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = AppContext.AppSetting.AlwaysOnTop;
            }

            if (_prePiPRect is { } rect)
            {
                AppWindow.MoveAndResize(rect);
                _prePiPRect = null;
            }

            ChangeFullWindow(false);
        }
    }
}
