using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Whether this section's rows sit behind the card rather than on the page.
    ///
    /// The single source of truth for that question. It used to be a
    /// CollapsedSectionIds dependency property on <c>OptionListControl</c>,
    /// pushed down from the page at four call sites and cleared at a fifth, so
    /// whether a section folded depended on which branch of the page's builder
    /// had run last. It is a property of the section, so it lives on the
    /// section's model.
    /// </summary>
    public bool IsFolded { get; set; }

    /// <summary>Segoe Fluent glyph resolved from the section catalog.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Secondary line under the title: the section's own description. It
    /// replaced a count line ("8 options"), which said nothing about where the
    /// card leads and which no other card in the settings tree carries.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this section opens in place instead of navigating to a page.
    ///
    /// The distinction is semantic, not visual. A section that is a
    /// *subdivision* of what its category is about -- the several sides of
    /// subtitles, the stages of the video pipeline -- expands where it sits,
    /// because opening a separate page for it would put the thing you came for
    /// one navigation away from the thing you were reading. A section that is a
    /// *parallel feature block* -- screenshots, window frame, network transport
    /// -- keeps its card and navigates, because those are separate jobs and a
    /// page is the right size for them.
    ///
    /// See SectionShapes for the per-section answer and the reasoning.
    /// </summary>
    public bool ExpandsInline { get; set; }

    /// <summary>
    /// The rows this section holds, in tree order, when it expands in place.
    ///
    /// Empty for a section that navigates: those rows are never handed to the
    /// card, they live on the page the card opens.
    /// </summary>
    public IReadOnlyList<Option> Rows { get; set; } = Array.Empty<Option>();

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

    /// <summary>
    /// The caption, matching what the row shows.
    ///
    /// UIA falls back to ToString() for a list row that sets no accessible name,
    /// and the default is the type name -- these rows were announced as
    /// "mpv_winui.Domains.Settings.Controls.SectionCardItem".
    /// </summary>
    public override string ToString() => Caption;
}
