using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionListControl : UserControl
{
    public OptionListControl()
    {
        InitializeComponent();
    }

    public bool ShowHeaders
    {
        get => (bool)GetValue(ShowHeadersProperty);
        set => SetValue(ShowHeadersProperty, value);
    }

    public static readonly DependencyProperty ShowHeadersProperty = DependencyProperty.Register(
        nameof(ShowHeaders),
        typeof(bool),
        typeof(OptionListControl),
        new PropertyMetadata(true, (d, e) =>
        {
            if (d is OptionListControl self)
            {
                self.ApplyItemsSource();
            }
        }));

    /// <summary>
    /// Sections rendered as a single drill-in card instead of all their rows.
    /// Keyed by stable section id.
    ///
    /// Set by the category overview, which knows which sections a category
    /// keeps folded. Empty everywhere else, where rows are what you want.
    /// </summary>
    public IReadOnlySet<string> CollapsedSectionIds
    {
        get => (IReadOnlySet<string>)GetValue(CollapsedSectionIdsProperty);
        set => SetValue(CollapsedSectionIdsProperty, value);
    }

    public static readonly DependencyProperty CollapsedSectionIdsProperty =
        DependencyProperty.Register(
            nameof(CollapsedSectionIds),
            typeof(IReadOnlySet<string>),
            typeof(OptionListControl),
            new PropertyMetadata(
                (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal),
                (d, e) =>
                {
                    if (d is OptionListControl self)
                    {
                        self.ApplyItemsSource();
                    }
                }));

    /// <summary>Raised when a collapsed section's card is opened, carrying the
    /// stable section id. Navigation belongs to the page, not this control.</summary>
    public event Action<string>? SectionCardClicked;

    /// <summary>Card models by section id, so A+B navigation has something to
    /// open without the control knowing what a "section" means.</summary>
    private readonly Dictionary<string, SectionCardItem> _sectionCards =
        new(StringComparer.Ordinal);

    /// <summary>Registers the card models for the current list. Called by the
    /// page right before assigning <see cref="OptionList"/>.</summary>
    public void SetSectionCards(IEnumerable<SectionCardItem> cards)
    {
        _sectionCards.Clear();
        foreach (var card in cards)
        {
            if (card.SectionId is { } id)
            {
                _sectionCards[id] = card;
            }
        }

        ApplyItemsSource();
    }

    private void SectionCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id } && !string.IsNullOrEmpty(id))
        {
            SectionCardClicked?.Invoke(id);
        }
    }

    public List<Option> OptionList
    {
        get => (List<Option>)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
            nameof(OptionList),
            typeof(List<Option>),
            typeof(OptionListControl),
            new PropertyMetadata((List<Option>)[], OnOptionListChanged)
            );

    private static void OnOptionListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OptionListControl self)
        {
            self.ApplyItemsSource();
        }
    }

    private void ApplyItemsSource()
    {
        // Every option renders through the same common templates (the
        // OptionTemplateSelector below); no tier filtering. Group headers
        // appear only when the page actually spans two or more sections —
        // a single-section page repeating its own name is pure noise.
        //
        // The inline customize mode is the exception: it renders one edit
        // template for every row and keeps hidden rows on screen (flagged), so
        // the user has something to drag, rename and bring back.
        // Inline customize mode is the exception: with its "show hidden" switch
        // on it lists the rows the user hid too, so there is something to bring
        // back. Those rows render faded — see Option.CardOpacity.
        var showHidden = CustomizeMode && ShowHidden;
        var visible = new List<Option>(OptionList.Count);
        foreach (var option in OptionList)
        {
            if (option.IsVisible || showHidden)
            {
                visible.Add(option);
            }
        }

        if (CustomizeMode)
        {
            var lang = AppContext.AppLang;

            // Both branches below run for the same control instance over its
            // lifetime (a mode switch only flips CustomizeMode), and the
            // non-customize branch Collapses these. So the customize branch has
            // to re-assert them: leaving it out is what kept "New folder"
            // permanently invisible, because the pane rendered non-customize
            // once on creation and nothing ever turned it back on.
            CustomizeBar.Visibility = Visibility.Visible;
            NewColumnButton.Visibility = Visibility.Visible;
            NewSectionButton.Visibility = Visibility.Visible;
            ShowHiddenToggle.Visibility = Visibility.Visible;

            NewColumnButtonText.Text = lang.CustomizeNewColumn;
            ToolTipService.SetToolTip(NewColumnButton, lang.CustomizeNewColumnHint);
            NewSectionButtonText.Text = lang.CustomizeNewSubmenu;
            ToolTipService.SetToolTip(NewSectionButton, lang.CustomizeNewSubmenuHint);
            ShowHiddenToggleText.Text = lang.CustomizeShowHidden;
            ToolTipService.SetToolTip(ShowHiddenToggle, lang.CustomizeShowHiddenHint);

            // Each of these three is a glyph plus a TextBlock, and UIA does not
            // compose an accessible name out of a StackPanel's children -- the
            // buttons report as unnamed. Naming them explicitly keeps the
            // toolbar readable to a screen reader without moving the caption
            // out of the button's own content.
            AutomationProperties.SetName(ShowHiddenToggle, lang.CustomizeShowHidden);
            AutomationProperties.SetName(NewColumnButton, lang.CustomizeNewColumn);
            AutomationProperties.SetName(NewSectionButton, lang.CustomizeNewSubmenu);

            // Asserted from the control's own flag, not left as the button was
            // last clicked: the pane is rebuilt on every edit, and a toggle that
            // re-reads its state from its own tick would drift out of sync with
            // the list it is supposed to be filtering.
            ShowHiddenToggle.IsChecked = ShowHidden;

            // Sections stay visible as their own rows so the 2nd level is
            // editable too; each header carries its own move/hide menu.
            var selector = (OptionTemplateSelector)Resources["TemplateSelector"];
            selector.CustomizeMode = true;
            OptionListView.ItemTemplate = null;
            OptionListView.ItemTemplateSelector = selector;

            var editable = new System.Collections.ObjectModel.ObservableCollection<object>();
            string? editLastSection = null;

            // User-created folders have no AppLang caption, so a drop onto one
            // has to resolve by name: record the names currently on screen.
            SetCustomSections(visible
                .Where(o => o.IsCustomSection && !string.IsNullOrEmpty(o.Section))
                .Select(o => (o.SectionId!, o.Section ?? string.Empty))
                .Distinct());

            // The pane stands for one folder: its members come first, then the
            // cards that are in no folder. Without a break between the two runs
            // the user cannot tell filed cards from un-filed ones, so a bare
            // separator row marks the boundary.
            var filed = new List<Option>();
            var unfiled = new List<Option>();
            foreach (var option in visible)
            {
                if (!string.IsNullOrEmpty(option.Section))
                {
                    filed.Add(option);
                }
                else
                {
                    unfiled.Add(option);
                }
            }

            var splitLabel = AppContext.AppLang.CustomizeUnfiledHeader;
            var ordered = new List<Option>(visible.Count);
            ordered.AddRange(filed);
            if (filed.Count > 0 && unfiled.Count > 0)
            {
                editable.Add(new SectionHeaderItem { Caption = splitLabel, IsSplitter = true });
            }
            ordered.AddRange(unfiled);

            foreach (var option in ordered)
            {
                if (!string.IsNullOrEmpty(option.Section) && option.SectionId != editLastSection)
                {
                    var header = new SectionHeaderItem
                    {
                        Caption = option.Section,
                        SectionId = option.SectionId,
                        // A folder with no AppLang caption is one the user made,
                        // and only those may be deleted. Read off the row rather
                        // than re-derived from the caption, which a rename would
                        // no longer match.
                        IsCustom = option.IsCustomSection,
                        // A folder is only listed here while hidden when the
                        // "show hidden" switch is on, and then it has to offer
                        // the way back rather than a second hide.
                        IsHidden = option.IsHiddenSection,
                    };
                    header.Edit.Refresh();
                    editable.Add(header);
                    editLastSection = option.SectionId;
                }

                // The card's context menu lives on the row model, so the row
                // needs a way back into this control for its handlers.
                option.Edit.RowAction = InvokeRowAction;
                editable.Add(option);
            }

            OptionListView.ItemsSource = editable;
            return;
        }

        CustomizeBar.Visibility = Visibility.Collapsed;
        NewColumnButton.Visibility = Visibility.Collapsed;
        NewSectionButton.Visibility = Visibility.Collapsed;
        ShowHiddenToggle.Visibility = Visibility.Collapsed;

        var defaultSelector = (OptionTemplateSelector)Resources["TemplateSelector"];
        defaultSelector.CustomizeMode = false;
        OptionListView.ItemTemplate = null;
        OptionListView.ItemTemplateSelector = defaultSelector;

        var sectionCount = visible
            .Where(o => !string.IsNullOrEmpty(o.Section))
            .Select(o => o.Section)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var showHeaders = sectionCount >= 2;

        var items = new List<object>(visible.Count + 8);
        string? lastSection = null;
        var collapsing = false;
        foreach (var option in visible)
        {
            if (!string.IsNullOrEmpty(option.Section) && option.Section != lastSection)
            {
                lastSection = option.Section;

                // A collapsed section contributes exactly one item: its card,
                // right here, at the position its options would have filled.
                // Sections arrive clustered (the page orders them that way), so
                // one card per run is enough and every later option of the same
                // section is skipped until the next run starts.
                SectionCardItem? card = null;
                collapsing = option.SectionId is { } sectionId
                    && CollapsedSectionIds.Contains(sectionId)
                    && _sectionCards.TryGetValue(sectionId, out card);

                if (collapsing)
                {
                    items.Add(card!);
                }
                else if (showHeaders)
                {
                    items.Add(new SectionHeaderItem { Caption = option.Section, SectionId = option.SectionId });
                }
            }

            if (!collapsing)
            {
                items.Add(option);
            }
        }

        OptionListView.ItemsSource = items;
    }

    /// <summary>
    /// Inline customize mode: drag to reorder, edit text, override the raw mpv
    /// value, hide or restore each row. The page owns the persisted layout and
    /// subscribes to these notifications.
    /// </summary>
    public bool CustomizeMode
    {
        get => (bool)GetValue(CustomizeModeProperty);
        set => SetValue(CustomizeModeProperty, value);
    }

    public static readonly DependencyProperty CustomizeModeProperty = DependencyProperty.Register(
        nameof(CustomizeMode),
        typeof(bool),
        typeof(OptionListControl),
        new PropertyMetadata(false, (d, e) =>
        {
            if (d is OptionListControl self)
            {
                // Reorder only makes sense while the edit template is showing.
                self.OptionListView.CanReorderItems = (bool)e.NewValue;
                self.OptionListView.AllowDrop = (bool)e.NewValue;
                self.ApplyItemsSource();
            }
        }));

    /// <summary>
    /// Caption at the leading edge of the customize toolbar: the category or
    /// folder the pane is showing.
    ///
    /// It lives on the control rather than in a separate row of the host page
    /// so the caption and the toolbar's buttons share one row and one vertical
    /// centre — split across two rows they never line up.
    /// </summary>
    public string HeaderText
    {
        get => PaneTitleText.Text;
        set => PaneTitleText.Text = value;
    }

    /// <summary>Raised when the user picks "hide" on a row.</summary>
    public event Action<Option>? HideRequested;

    /// <summary>
    /// Raised when the user picks "show this row again" on a row that is
    /// currently hidden. A separate event rather than a flag on
    /// <see cref="HideRequested"/> because hiding and restoring are different
    /// intents on the page side, and a bool-carrying event invites callers to
    /// forget which way it points.
    /// </summary>
    public event Action<Option>? UnhideRequested;

    /// <summary>
    /// Whether the pane is listing rows the user hid. Off by default: a hidden
    /// row is meant to be out of the way, and only someone tidying up wants to
    /// see them. While on, hidden rows render faded, so "listed" and "shown"
    /// stay distinguishable.
    /// </summary>
    public bool ShowHidden { get; private set; }

    /// <summary>Raised when the user asks to create a grouping bar (pane-only).</summary>
    public event Action? CreateColumnRequested;

    /// <summary>Raised after a drag-reorder, with the new key order.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    /// <summary>
    /// Raised when the user moves a folder; the stable section id identifies it.
    ///
    /// The id travels, not the caption, because a built-in folder's caption is
    /// renamable: once the user renames it, the caption no longer maps back to
    /// the id, and every command that reported a caption would silently stop
    /// finding its folder.
    /// </summary>
    public event Action<string, int>? SectionMoveRequested;

    /// <summary>
    /// Raised when the user flips a folder between hidden and shown, by stable
    /// section id. Carries the state the folder should end up in, so the one
    /// menu slot serves both directions and the page never has to work out which
    /// way the click pointed.
    /// </summary>
    public event Action<string, bool>? SectionHiddenChanged;

    /// <summary>Raised when the user deletes a folder they created.</summary>
    public event Action<string>? SectionDeleteRequested;

    // ===== drag a card into a 2nd-level folder =====

    private const string RowDragFormat = "mpvwinui.settings.row";

    /// <summary>Row being dragged, so the drop knows what to reassign.</summary>
    private string? _draggedRowKey;

    /// <summary>Section id the dragged row came from, so "back out" is possible.</summary>
    private string? _draggedRowSectionId;

    private void OptionListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!CustomizeMode)
        {
            return;
        }

        _folderJoinHandled = false;

        foreach (var item in e.Items)
        {
            // Only option rows define the payload: a section header can also be
            // dragged (ListView reorder), and that gesture stores the new run
            // order through OrderChanged instead of moving anything.
            if (item is Option option)
            {
                e.Data.SetText(RowDragFormat + "\n" + option.Key);
                _draggedRowKey = option.Key;
                _draggedRowSectionId = option.SectionId;
            }
            else if (item is SectionHeaderItem)
            {
                // A section header is dragged too (to reorder its folder). It
                // carries no payload of its own: the drop handler sees the new
                // run order in the ListView and stores it through OrderChanged.
                _draggedRowKey = null;
                _draggedRowSectionId = null;
            }
        }

        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void OptionListView_DragOver(object sender, DragEventArgs e)
    {
        if (!CustomizeMode)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // Always accept the move. Declining here (the old None branch for a
        // header drag) also vetoes the ListView's own reorder commit, which is
        // why dragging a row or a folder header never changed the order: the
        // drop was rejected before CanReorderItems could act on it.
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

        // The "move into folder" caption only applies while a card is being
        // carried; a plain reorder drag needs no extra chrome.
        if (_draggedRowKey is not null)
        {
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.Caption = AppContext.AppLang.CustomizeMoveToSection;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private void OptionListView_Drop(object sender, DragEventArgs e)
    {
        var optionKey = _draggedRowKey;
        var fromSectionId = _draggedRowSectionId;
        _draggedRowKey = null;
        _draggedRowSectionId = null;

        if (!CustomizeMode || optionKey is null)
        {
            // A header drag (or a drag that carried no card) is a pure reorder:
            // DragItemsCompleted commits it, so there is nothing to do here.
            return;
        }

        // Mark the gesture so DragItemsCompleted knows a folder-join was
        // already handled and must not also write the run order on top of it.
        _folderJoinHandled = true;

        if (_paneDrop)
        {
            // The pane *is* the folder, so the drop position carries no
            // meaning: whatever was dragged in joins the folder on display.
            if (string.Equals(_paneSectionId, fromSectionId, StringComparison.Ordinal))
            {
                _folderJoinHandled = false;
                return;
            }

            PaneJoinRequested?.Invoke(optionKey, _paneSectionId);
            return;
        }

        // The drop lands on whatever row is under the pointer: another card
        // (join its folder) or a folder header (join that folder). Dropping on
        // empty space means "no folder".
        //
        // A header answers with the id it already carries; the caption lookups
        // are the fallback for a header built without one, and cannot resolve a
        // renamed built-in folder — hence the id first.
        var target = (e.OriginalSource as FrameworkElement)?.DataContext;
        var targetSectionId = target switch
        {
            Option targetOption => targetOption.SectionId,
            SectionHeaderItem header => header.SectionId
                ?? SettingsSections.IdFor(header.Caption)
                ?? _customSectionIdsByCaption.GetValueOrDefault(header.Caption),
            _ => null,
        };

        if (string.Equals(targetSectionId, fromSectionId, StringComparison.Ordinal))
        {
            // Dropped back where it started: still a no-op for the folder, but
            // the reorder that came with it must stand.
            _folderJoinHandled = false;
            return;
        }

        MoveRowRequested?.Invoke(optionKey, targetSectionId);
    }

    /// <summary>
    /// Set while a drop is being handled as a folder-join, so the reorder
    /// commit that follows does not overwrite it. Cleared on the next
    /// DragItemsStarting.
    /// </summary>
    private bool _folderJoinHandled;

    /// <summary>
    /// Caption -> id for the folders this list was built from, so a drop onto a
    /// user-created folder resolves without the page having to be consulted.
    /// </summary>
    private readonly Dictionary<string, string> _customSectionIdsByCaption = new(StringComparer.Ordinal);

    /// <summary>Registers the user-created folders currently on screen.</summary>
    public void SetCustomSections(IEnumerable<(string Id, string Name)> sections)
    {
        _customSectionIdsByCaption.Clear();
        foreach (var (id, name) in sections)
        {
            _customSectionIdsByCaption[name] = id;
        }
    }

    /// <summary>
    /// Raised when a row is dropped onto another row or a folder header:
    /// the key of the moved row and the section it should join (null = out of
    /// every folder).
    /// </summary>
    public event Action<string, string?>? MoveRowRequested;

    /// <summary>Raised when the user picks "new folder" in the customize bar.</summary>
    public event Action? CreateSectionRequested;

    /// <summary>
    /// Raised when the user renames a row. The page owns the dialog and the
    /// override store; the control only reports which row and its current
    /// visible label so the dialog can be pre-filled.
    /// </summary>
    public event Action<string, string>? RenameRowRequested;

    /// <summary>Raised when the user renames a folder (option key, current label).</summary>
    public event Action<string, string>? RenameSectionRequested;

    // ===== the bookmark-manager right pane =====

    /// <summary>
    /// In the two-pane view the list no longer owns a drop target of its own:
    /// the pane it sits in is the folder, so a card dropped anywhere in the
    /// list joins the folder currently open instead of whichever row happens
    /// to be under the pointer.
    /// </summary>
    private string? _paneSectionId;

    /// <summary>Whether the list is the bookmark-manager's right pane.</summary>
    private bool _paneDrop;

    /// <summary>
    /// Points the list at a folder: cards dropped in it join that folder.
    /// Pass null for a category page, where dropping re-files nothing.
    /// </summary>
    public void SetPaneDropTarget(string? sectionId)
    {
        _paneSectionId = sectionId;
        _paneDrop = true;
    }

    /// <summary>Raised when a card is dropped into the pane's folder.</summary>
    public event Action<string, string?>? PaneJoinRequested;

    /// <summary>Reverts the list to owning its own per-row drop targets.</summary>
    public void ClearPaneDropTarget() => _paneDrop = false;

    private void CreateSection_Click(object sender, RoutedEventArgs e) => CreateSectionRequested?.Invoke();

    /// <summary>The toolbar's "new column": a grouping bar that stays in this pane.</summary>
    private void CreateColumn_Click(object sender, RoutedEventArgs e) => CreateColumnRequested?.Invoke();

    /// <summary>
    /// Flips the pane between "the rows you kept" and "everything, with the
    /// hidden ones faded". The flag is kept on the control (not read back off
    /// the toggle) so a rebuild re-asserts the button from the state rather
    /// than the state from the button.
    /// </summary>
    private void ShowHiddenToggle_Click(object sender, RoutedEventArgs e)
    {
        ShowHidden = ShowHiddenToggle.IsChecked == true;
        Refresh();
    }

    private void RenameRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowOption(sender) is { } option)
        {
            RenameRowRequested?.Invoke(option.Key, option.Label);
        }
    }

    /// <summary>Renames a folder. The header carries its stable id, not just its caption.</summary>
    private void RenameSection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SectionHeaderItem header
            && header.SectionId is { } id)
        {
            RenameSectionRequested?.Invoke(id, header.Caption);
        }
    }

    private void SectionDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SectionIdOf(sender) is { } id)
        {
            SectionDeleteRequested?.Invoke(id);
        }
    }

    /// <summary>
    /// Stable section id behind a folder-row menu item. Read off the header's
    /// own id rather than resolved from its caption, which a rename breaks.
    /// </summary>
    private static string? SectionIdOf(object sender) =>
        ((sender as FrameworkElement)?.DataContext as SectionHeaderItem)?.SectionId;

    private void SectionMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (SectionIdOf(sender) is { } id)
        {
            SectionMoveRequested?.Invoke(id, -1);
        }
    }

    private void SectionMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (SectionIdOf(sender) is { } id)
        {
            SectionMoveRequested?.Invoke(id, 1);
        }
    }

    private void SectionHide_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SectionHeaderItem { SectionId: { } id } header)
        {
            return;
        }

        // The direction is read off the header rather than off the caption, so
        // the action is right even if the caption lagged a rebuild behind.
        SectionHiddenChanged?.Invoke(id, !header.IsHidden);
    }

    private static Option? RowOption(object sender) =>
        (sender as FrameworkElement)?.DataContext as Option;

    private void ToggleRowHidden_Click(object sender, RoutedEventArgs e)
    {
        if (RowOption(sender) is not { } option)
        {
            return;
        }

        // The menu carries one entry for both directions, so the direction is
        // read off the row rather than off the caption. That keeps the action
        // correct even if the caption lagged a rebuild behind the state.
        if (option.IsHiddenByUser)
        {
            UnhideRequested?.Invoke(option);
        }
        else
        {
            HideRequested?.Invoke(option);
        }
    }

    /// <summary>The card's inline pencil: same destination as "rename".</summary>
    private void EditRowInline_Click(object sender, RoutedEventArgs e) => RenameRow_Click(sender, e);

    /// <summary>
    /// Runs one of the card's own menu actions from a context-menu item.
    ///
    /// The context menu is built on the row's model (a DataTemplate cannot
    /// reach the control), so it calls back in here by name instead of
    /// duplicating the handlers. Invoked on the control instance the row
    /// belongs to, via <see cref="OptionEditText.RowAction"/>.
    /// </summary>
    internal void InvokeRowAction(object sender, string action)
    {
        if (RowOption(sender) is null)
        {
            return;
        }

        // The handler names are the same as the ⋯ menu's click handlers, so
        // there is exactly one implementation per action.
        switch (action)
        {
            case nameof(RenameRow_Click):
                RenameRow_Click(sender, new RoutedEventArgs());
                break;
            case nameof(ToggleRowHidden_Click):
                ToggleRowHidden_Click(sender, new RoutedEventArgs());
                break;
        }
    }

    private void OptionListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (!CustomizeMode)
        {
            return;
        }

        // A folder-join already reassigned the row; writing the run order on
        // top of it would immediately undo the move (the row would be ranked
        // back into the run it just left).
        if (_folderJoinHandled)
        {
            _folderJoinHandled = false;
            return;
        }

        var order = new List<string>(sender.Items.Count);
        foreach (var item in sender.Items)
        {
            if (item is Option option)
            {
                order.Add(option.Key);
            }
        }

        if (order.Count > 0)
        {
            OrderChanged?.Invoke(order);
        }
    }

    /// <summary>Rebuilds the list (e.g. after an option becomes visible/hidden).</summary>
    public void Refresh()
    {
        var offset = GetScrollOffset();
        ApplyItemsSource();
        if (offset > 0)
        {
            DispatcherQueue.TryEnqueue(() => SetScrollOffset(offset));
        }
    }

    /// <summary>Returns the current vertical offset of the options list.</summary>
    public double GetScrollOffset()
    {
        return FindScrollViewer(OptionListView)?.VerticalOffset ?? 0;
    }

    /// <summary>Restores the vertical offset after the list is rebuilt.</summary>
    public void SetScrollOffset(double offset)
    {
        var viewer = FindScrollViewer(OptionListView);
        if (viewer is not null && offset > 0)
        {
            viewer.ChangeView(null, offset, null, disableAnimation: true);
        }
    }

    /// <summary>Scrolls the option with the given key into view (settings search).</summary>
    public void ScrollToOption(string key)
    {
        foreach (var option in OptionList)
        {
            if (option.Key == key)
            {
                OptionListView.ScrollIntoView(option);
                return;
            }
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
            if (FindScrollViewer(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
