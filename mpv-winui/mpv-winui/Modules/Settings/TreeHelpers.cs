using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Binding helpers for the folder tree's item template.
    ///
    /// A TreeView over RootNodes hands its template a TreeViewNode, so every
    /// accessor here takes the node's Content as <see cref="object"/> and
    /// pattern-matches with <c>as</c>/<c>is</c>. That matters: these run from
    /// generated binding code, and a hard cast to the payload type is exactly
    /// what took the settings page down (NavigationView's MenuItems.Clear()
    /// re-enters the binding component with objects that are not the payload).
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

        /// <summary>Tooltip for the drag handle, localized per node kind.</summary>
        public static string DragTip(object? content) =>
            content is TreeViewNodeContent node ? node.Edit.DragHandleTip : string.Empty;

        /// <summary>Tooltip for the inline pencil.</summary>
        public static string EditTip(object? content) =>
            content is TreeViewNodeContent node ? node.Edit.EditTip : string.Empty;

        /// <summary>
        /// Whether the node gets an inline pencil. Only a folder the user made
        /// is renamed from here; a built-in section and a category keep their
        /// edit entry inside the context menu, so the tree does not grow a
        /// button on every row.
        /// </summary>
        public static Visibility EditVisibility(object? content) =>
            content is TreeViewNodeContent { IsSection: true, IsCustom: true }
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>
        /// Dimming for a hidden folder's node. A hidden folder stays listed (so
        /// it can be brought back) and is faded instead of badged, so "set
        /// aside" is legible at a glance next to the folders that are on.
        /// </summary>
        public static double OpacityFor(object? content) =>
            content is TreeViewNodeContent { IsHidden: true } ? 0.45 : 1.0;
    }
}
