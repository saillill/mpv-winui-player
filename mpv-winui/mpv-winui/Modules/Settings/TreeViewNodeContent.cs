using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Payload of a folder-tree node in the customize mode's bookmark-manager view.
///
/// A node is either a category (1st level) or a folder inside one (2nd level);
/// both carry the stable key they stand for so a rename or a reorder resolves
/// without going through the localized caption.
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

    /// <summary>A category node can create a folder under itself.</summary>
    public bool CanAddFolder { get; set; }

    /// <summary>Localized captions for the node's context menu.</summary>
    public Controls.OptionEditText Edit { get; set; } = new();

    /// <summary>Delete is only meaningful for a folder the user made.</summary>
    public Microsoft.UI.Xaml.Visibility DeleteVisibility =>
        IsSection && IsCustom
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>"New folder" only applies to a category node.</summary>
    public Microsoft.UI.Xaml.Visibility AddFolderVisibility =>
        CanAddFolder
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>A category node cannot be renamed (its name is a localized label).</summary>
    public Microsoft.UI.Xaml.Visibility RenameVisibility =>
        IsSection
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
}
