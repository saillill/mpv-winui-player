using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Transparent hit-test surface with a west-east resize cursor, used as the
/// playlist width grip. (WinUI 3 <c>Border</c> is sealed, so the cursor is
/// set through a small UserControl subclass.)
/// </summary>
public sealed partial class ResizeGrip : UserControl
{
    public ResizeGrip()
    {
        InitializeComponent();
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
