using System;
using Microsoft.UI.Xaml;

namespace mpv_winui.Domains.Playback;

/// <summary>
/// Whether a library card is ticked, for the selection bar.
///
/// On the ENTRY rather than on the card, because the card is a recycled template: a bit
/// stored on the element follows the container to a different entry, which is the classic
/// "the tick moves to the wrong file after scrolling" defect. The entry is the thing the
/// tick is about.
///
/// In its own partial rather than in LibraryEntry.cs because that file is about what a
/// file IS (path, tags, thumbnail) and this is about a transient UI state.
/// </summary>
public sealed partial class LibraryEntry
{
    private bool _selectionMode;

    private bool _isSelected;

    /// <summary>
    /// True while the page is in selection mode.
    ///
    /// Per entry rather than one page-level flag the template reads: a DataTemplate
    /// cannot bind to the page, and the reference draws a tick box on every card in the
    /// mode, so each card needs the answer.
    /// </summary>
    public bool SelectionMode
    {
        get => _selectionMode;
        internal set
        {
            if (_selectionMode == value)
            {
                return;
            }

            _selectionMode = value;

            RaiseSelectionChanged(nameof(SelectionMode));
            RaiseSelectionChanged(nameof(SelectionVisibility));
        }
    }

    /// <summary>True when this card is ticked.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;

            RaiseSelectionChanged(nameof(IsSelected));
            RaiseSelectionChanged(nameof(SelectionGlyph));
        }
    }

    /// <summary>
    /// The tick box's glyph: E73E checked, E739 unchecked, from Segoe Fluent Icons.
    ///
    /// A glyph rather than two PathIcons swapping Visibility: the generated FluentIcons
    /// set has no checkbox in it, the settings page already draws its own check marks
    /// with these two codepoints, and a FontIcon carries no geometry lifetime rule.
    /// </summary>
    public string SelectionGlyph => _isSelected ? "\uE73E" : "\uE739";

    /// <summary>
    /// The tick box's visibility: present in selection mode, gone otherwise.
    ///
    /// The reference hides it entirely outside the mode - a box on every card in the
    /// library would read as a bulk-edit screen.
    /// </summary>
    public Visibility SelectionVisibility =>
        _selectionMode ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Ticking is only possible in selection mode.</summary>
    internal void ToggleSelection()
    {
        if (!_selectionMode)
        {
            return;
        }

        IsSelected = !IsSelected;
    }
}
