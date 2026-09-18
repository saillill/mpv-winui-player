using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// One collapsed section rendered as a drill-in card inside otherwise flat list.
///
/// This exists because the overview used to render every inline option first
/// and every card second, which meant a category's sections were not in their
/// own order: an option filed under section 8 appeared above a card for
/// section 3. The card is therefore just another list item, emitted at the
/// position its section occupies.
///
/// Everything it displays is resolved once by whoever builds the list, so this
/// stays a plain record: unlike <see cref="Option"/> it needs no change
/// notification, because none of its fields change while it is on screen.
/// </summary>
public sealed class SectionCardItem : INotifyPropertyChanged
{
    private int _count;

    /// <summary>Localized section caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Stable section id, used as the navigation target.</summary>
    public string? SectionId { get; set; }

    /// <summary>Segoe Fluent glyph resolved from the section catalog.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Secondary line under the title; empty when there is none.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Localized count line, e.g. "8 options".</summary>
    public string CountText { get; set; } = string.Empty;

    /// <summary>Screen-reader name: caption plus count, never the icon glyph.</summary>
    public string AccessibleName =>
        string.IsNullOrEmpty(CountText) ? Caption : $"{Caption}, {CountText}";

    /// <summary>How many options the card opens onto.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
