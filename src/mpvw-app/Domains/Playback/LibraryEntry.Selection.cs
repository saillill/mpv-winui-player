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
    /// The tick box's visibility IN SELECTION MODE: present in the mode, gone otherwise.
    ///
    /// The reference hides it entirely outside the mode - a box on every card in the
    /// library would read as a bulk-edit screen.
    ///
    /// <para>This is the MODE half only, not the whole answer. The card shows the box
    /// when this says Visible OR the pointer is on it, because the box is also the way
    /// into the mode: a control nobody can see before using it is not an entry point.
    /// The other half is the card's own _pointerInside and is deliberately not stored
    /// here - the pointer belongs to the element, not to the file.</para>
    /// </summary>
    public Visibility SelectionVisibility =>
        _selectionMode ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Flips this card's tick. Unconditional, and that is the point.
    ///
    /// <para>This used to bail out unless <see cref="_selectionMode"/> was already
    /// true: "ticking is only possible in selection mode", from when the mode was a
    /// switch the page turned on before any card could be ticked.</para>
    ///
    /// <para><b>The guard had to go, because the mode stopped being a switch.</b>
    /// The mode is now the COUNT of ticks - the page derives it in
    /// UpdateSelectionBar and pushes it back down - so it is true only once
    /// something is already ticked. With the guard in place, the first tick could
    /// never happen: the card was asked not to enter the very state that would
    /// have made true the condition it was waiting for. The tick box is the way
    /// into the mode, and the guard made that entry point a no-op.</para>
    ///
    /// <para>There is no "not in the mode" state a card can be in any more, so
    /// there is nothing here to refuse. The card that owns this entry draws its box
    /// while the pointer is on it precisely so the first click is reachable, and
    /// the page's handler recomputes the mode after every flip.</para>
    /// </summary>
    internal void ToggleSelection() => IsSelected = !IsSelected;
}
