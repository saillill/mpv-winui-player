using Microsoft.UI.Xaml;
using mpv_winui.Modules.Common.View;
using System;
using System.Threading.Tasks;
using Windows.Graphics;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// A separate, freely movable color picker window. It follows the app theme,
/// can be resized, and scrolls so the confirm button stays reachable.
/// </summary>
public sealed partial class ColorPickerWindow : Window
{
    private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ColorPickerWindow? _open;

    public ColorPickerWindow(string? currentColor)
    {
        InitializeComponent();
        Title = AppContext.AppLang.ThemeColorCustomColors;
        TitleText.Text = AppContext.AppLang.ThemeColorCustomColors;
        AppWindow.SetIcon("App.ico");
        RootGrid.RequestedTheme = WindowStyleManager.ResolveTheme();
        Picker.CurrentColor = currentColor ?? string.Empty;
        Picker.Applied += OnPickerApplied;
        Closed += OnWindowClosed;

        const int width = 480;
        const int height = 680;
        var position = CenterPosition(width, height);
        AppWindow.MoveAndResize(new RectInt32(position.X, position.Y, width, height));
        _open?.Close();
        _open = this;
        Activate();
    }

    /// <summary>Completes with the chosen hex color, or null when canceled/closed.</summary>
    public Task<string?> PickAsync() => _completion.Task;

    private static PointInt32 CenterPosition(int width, int height)
    {
        if (App.Window is { } main)
        {
            var size = main.AppWindow.Size;
            var pos = main.AppWindow.Position;
            return new PointInt32(
                Math.Max(0, pos.X + (size.Width - width) / 2),
                Math.Max(0, pos.Y + (size.Height - height) / 2));
        }

        return new PointInt32(120, 120);
    }

    private void OnPickerApplied()
    {
        _completion.TrySetResult(Picker.Result);
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(_open, this))
        {
            _open = null;
        }
        Picker.Applied -= OnPickerApplied;
        Closed -= OnWindowClosed;
        _completion.TrySetResult(Picker.Result);
    }
}
