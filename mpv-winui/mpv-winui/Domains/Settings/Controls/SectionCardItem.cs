using Microsoft.UI.Xaml;

namespace mpv_winui.Domains.Settings.Controls;

/// <summary>
/// One collapsed section rendered as a drill-in card inside an otherwise flat
/// list.
///
/// This exists because the overview used to render every inline option first
/// and every card second, which meant a category's sections were not in their
/// own order: an option filed under section 8 appeared above a card for
/// section 3. The card is therefore just another list item, emitted at the
/// position its section occupies.
///
/// Everything it displays is resolved once by whoever builds the list, so this
/// is a plain record with no change notification -- and it now really is one.
/// It used to implement INotifyPropertyChanged for a Count that nothing read,
/// while the doc comment above already claimed no field changes on screen.
/// </summary>
public sealed class SectionCardItem
{
    /// <summary>Localized section caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Stable section id, used as the navigation target.</summary>
    public string? SectionId { get; set; }

    /// <summary>Segoe Fluent glyph resolved from the section catalog.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Secondary line under the title: the section's own description. It
    /// replaced a count line ("8 options"), which said nothing about where the
    /// card leads and which no other card in the settings tree carries.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the secondary line has anything to say. Bound directly rather
    /// than through a converter so an empty description collapses the row
    /// instead of leaving a blank gap under the caption.
    /// </summary>
    public Visibility DescriptionVisibility =>
        string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Screen-reader name: caption plus description, never the icon glyph.</summary>
    public string AccessibleName =>
        string.IsNullOrEmpty(Description) ? Caption : $"{Caption}, {Description}";
}
