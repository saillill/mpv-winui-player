namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// A settings category section caption rendered as its own list row,
/// separate from the option cards below it.
/// </summary>
public sealed class SectionHeaderItem
{
    public string Caption { get; set; } = string.Empty;

    /// <summary>
    /// Localized captions for the customize-mode section row. Sections reuse the
    /// same edit-card strings as option rows, so they share this holder.
    /// </summary>
    public OptionEditText Edit { get; } = new();
}
