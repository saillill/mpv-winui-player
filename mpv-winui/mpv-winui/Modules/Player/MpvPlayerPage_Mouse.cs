using Microsoft.UI.Xaml.Input;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Forwards mouse input from the WinUI video surface to mpv so that the
/// mouse bindings in input.conf (wheel volume/seek, mouse buttons) work in
/// the embedded composition mode, where libmpv never sees Win32 mouse input.
/// </summary>
public sealed partial class MpvPlayerPage
{
    private void VideoArea_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PlayerView);
        var props = point.Properties;

        var key = props.IsHorizontalMouseWheel
            ? (props.MouseWheelDelta > 0 ? "WHEEL_LEFT" : "WHEEL_RIGHT")
            : (props.MouseWheelDelta > 0 ? "WHEEL_UP" : "WHEEL_DOWN");

        SendMouseButton(key);
        e.Handled = true;
    }

    private void PlayerView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // input.conf: MBTN_LEFT_DBL cycle fullscreen
        SendMouseButton("MBTN_LEFT_DBL");
        e.Handled = true;
    }

    private static void SendMouseButton(string keyName)
    {
        if (_selfWeakReference?.TryGetTarget(out var self) == true)
        {
            self?._mediaPlayer?.Command(["keydown", keyName]);
            self?._mediaPlayer?.Command(["keyup", keyName]);
        }
    }
}
