using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Row shown in the customize mode's folder tree.
///
/// Deliberately a plain payload with no DataTemplate behind it. A TreeView
/// built from <c>RootNodes</c> hands its template a <see cref="TreeViewNode"/>,
/// not this object, so an ItemTemplate that binds to this type has to be
/// declared against the wrong data type and the compiler-generated binding
/// component then hard-casts a TreeViewNode to it. NavigationView's
/// <c>MenuItems.Clear()</c> re-enters that component and the bad cast throws an
/// InvalidCastException, which takes the whole settings page down whenever
/// customize mode rebuilds the sidebar.
///
/// Without a template the TreeView falls back to ToString(), so that is what
/// produces the label.
/// </summary>
public sealed class TreeViewNodeContent
{
    /// <summary>Label shown in the tree.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Icon shown before the label: a folder for the 2nd level, a stack for a
    /// category, so the two tiers read differently at a glance.
    /// </summary>
    public string Glyph => IsSection ? "\uE8B7" : "\uE8A5";

    /// <summary>True for a 2nd-level folder; false for a 1st-level category.</summary>
    public bool IsSection { get; set; }

    /// <summary>Stable category key (1st-level nodes).</summary>
    public string? CategoryKey { get; set; }

    /// <summary>Stable section id (2nd-level nodes).</summary>
    public string? SectionId { get; set; }

    /// <summary>True when the folder was created by the user and can be deleted.</summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// True when the folder is hidden: its cards stay in the page model but are
    /// set aside. The node is dimmed here rather than dropped, so the structure
    /// reads as "still there, switched off" — and so there is something left on
    /// screen offering to switch it back on.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>A category node can create a folder under itself.</summary>
    public bool CanAddFolder { get; set; }

    /// <summary>Localized captions for the node's context menu.</summary>
    public Controls.OptionEditText Edit { get; set; } = new();

    /// <summary>Delete is only meaningful for a folder the user made.</summary>
    public Visibility DeleteVisibility =>
        IsSection && IsCustom ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>"New folder" only applies to a category node.</summary>
    public Visibility AddFolderVisibility =>
        CanAddFolder ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>A category node cannot be renamed (its name is a localized label).</summary>
    public Visibility RenameVisibility =>
        IsSection ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// What the tree shows. Also what the UIA name ends up being, so the row
    /// reads as its label to assistive tech instead of as a type name.
    /// </summary>
    public override string ToString() => Text;
}
