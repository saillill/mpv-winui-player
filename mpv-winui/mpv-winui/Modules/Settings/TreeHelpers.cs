using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Bind helpers for the customize mode's folder tree.
///
/// A TreeView over RootNodes templates against the <see cref="TreeViewNode"/>,
/// and the payload hangs off <c>Content</c>, which is typed as object. A plain
/// <c>x:Bind Content.SomeProperty</c> would make the compiler emit a hard cast
/// to the payload type; when NavigationView rebuilds its menu items that cast
/// runs against the wrong object and throws. Going through these methods keeps
/// the conversion a checked <c>as</c>, so a mismatch yields empty text instead
/// of an exception.
/// </summary>
public static class TreeHelpers
{
    public static string TextFor(object? content) =>
        content switch
        {
            TreeViewNodeContent node => node.Text,
            string s => s,
            null => string.Empty,
            _ => content.ToString() ?? string.Empty,
        };

    public static string GlyphFor(object? content) =>
        content is TreeViewNodeContent node ? node.Glyph : "\uE8A5";

    public static MenuFlyout? MenuFor(object? content) =>
        content is TreeViewNodeContent node ? node.Edit.Menu : null;
}
