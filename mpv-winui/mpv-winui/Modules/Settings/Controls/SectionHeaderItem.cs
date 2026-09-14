namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// A settings category section caption rendered as its own list row,
/// separate from the option cards below it.
/// </summary>
public sealed class SectionHeaderItem
{
    public string Caption { get; set; } = string.Empty;

    /// <summary>
    /// Stable id of the folder this header opens (built-in section id, or the
    /// id of a folder the user created). Carried on the header so a rename or
    /// delete resolves to a stored id instead of the localized caption, which
    /// would orphan the change on the next language switch.
    /// </summary>
    public string? SectionId { get; set; }

    /// <summary>
    /// True for a folder the user created (and can therefore delete). Built-in
    /// sections are only movable, renamable and hideable.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// True for the bare separator that marks where the folder's own cards end
    /// and the cards outside every folder begin. A splitter carries no folder,
    /// so it gets no edit affordances.
    /// </summary>
    public bool IsSplitter { get; set; }

    /// <summary>
    /// <see cref="IsCustom"/> as a Visibility, so the XAML can bind the delete
    /// entry directly. WinUI has no built-in bool-to-Visibility converter here
    /// and x:Bind cannot apply one implicitly.
    /// </summary>
    public Microsoft.UI.Xaml.Visibility DeleteVisibility =>
        IsCustom ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>
    /// Localized captions for the customize-mode section row. Sections reuse the
    /// same edit-card strings as option rows, so they share this holder.
    /// </summary>
    public OptionEditText Edit { get; } = new();
}
