using Microsoft.UI.Xaml;
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
        var visible = new List<Option>(OptionList.Count);
        foreach (var option in OptionList)
        {
            if (CustomizeMode || option.IsVisible)
            {
                visible.Add(option);
            }
        }

        if (CustomizeMode)
        {
            NewSectionButton.Visibility = Visibility.Visible;
            NewSectionButtonText.Text = AppContext.AppLang.CustomizeNewSection;
            ToolTipService.SetToolTip(NewSectionButton, AppContext.AppLang.CustomizeNewSectionHint);

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
                .Where(o => o.SectionId is not null
                    && SettingsSectionIds.IdFor(o.Section) is null
                    && !string.IsNullOrEmpty(o.Section))
                .Select(o => (o.SectionId!, o.Section))
                .Distinct());

            foreach (var option in visible)
            {
                if (!string.IsNullOrEmpty(option.Section) && option.SectionId != editLastSection)
                {
                    var header = new SectionHeaderItem { Caption = option.Section };
                    header.Edit.Refresh();
                    editable.Add(header);
                    editLastSection = option.SectionId;
                }

                editable.Add(option);
            }

            OptionListView.ItemsSource = editable;
            return;
        }

        NewSectionButton.Visibility = Visibility.Collapsed;

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
        foreach (var option in visible)
        {
            if (showHeaders && !string.IsNullOrEmpty(option.Section) && option.Section != lastSection)
            {
                items.Add(new SectionHeaderItem { Caption = option.Section });
                lastSection = option.Section;
            }

            items.Add(option);
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

    /// <summary>Raised when the user picks "hide" on a row.</summary>
    public event Action<Option>? HideRequested;

    /// <summary>Raised when the user picks "restore default" on a row.</summary>
    public event Action<Option>? ResetRequested;

    /// <summary>Raised after a drag-reorder, with the new key order.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    /// <summary>Raised when the user moves a section; the caption identifies it.</summary>
    public event Action<string, int>? SectionMoveRequested;

    /// <summary>Raised when the user hides a section.</summary>
    public event Action<string>? SectionHideRequested;

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
        if (!CustomizeMode || _draggedRowKey is null)
        {
            // Reordering a section run is the ListView's own business; only a
            // card being carried into a folder needs the "move here" hint.
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.Caption = AppContext.AppLang.CustomizeMoveToSection;
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private void OptionListView_Drop(object sender, DragEventArgs e)
    {
        var optionKey = _draggedRowKey;
        var fromSectionId = _draggedRowSectionId;
        _draggedRowKey = null;
        _draggedRowSectionId = null;

        if (!CustomizeMode || optionKey is null)
        {
            return;
        }

        // The drop lands on whatever row is under the pointer: another card
        // (join its folder) or a folder header (join that folder). Dropping on
        // empty space means "no folder".
        var target = (e.OriginalSource as FrameworkElement)?.DataContext;
        var targetSectionId = target switch
        {
            Option targetOption => targetOption.SectionId,
            SectionHeaderItem header => SettingsSectionIds.IdFor(header.Caption)
                ?? _customSectionIdsByCaption.GetValueOrDefault(header.Caption),
            _ => null,
        };

        if (string.Equals(targetSectionId, fromSectionId, StringComparison.Ordinal))
        {
            return;
        }

        MoveRowRequested?.Invoke(optionKey, targetSectionId);
    }

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

    private void CreateSection_Click(object sender, RoutedEventArgs e) => CreateSectionRequested?.Invoke();

    private void SectionDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SectionCaption(sender) is { } caption)
        {
            SectionDeleteRequested?.Invoke(caption);
        }
    }

    private static string? SectionCaption(object sender) =>
        ((sender as FrameworkElement)?.DataContext as SectionHeaderItem)?.Caption;

    private void SectionMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (SectionCaption(sender) is { } caption)
        {
            SectionMoveRequested?.Invoke(caption, -1);
        }
    }

    private void SectionMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (SectionCaption(sender) is { } caption)
        {
            SectionMoveRequested?.Invoke(caption, 1);
        }
    }

    private void SectionHide_Click(object sender, RoutedEventArgs e)
    {
        if (SectionCaption(sender) is { } caption)
        {
            SectionHideRequested?.Invoke(caption);
        }
    }

    private static Option? RowOption(object sender) =>
        (sender as FrameworkElement)?.DataContext as Option;

    private void HideRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowOption(sender) is { } option)
        {
            HideRequested?.Invoke(option);
        }
    }

    private void ResetRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowOption(sender) is { } option)
        {
            ResetRequested?.Invoke(option);
        }
    }

    private void OptionListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (!CustomizeMode)
        {
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
