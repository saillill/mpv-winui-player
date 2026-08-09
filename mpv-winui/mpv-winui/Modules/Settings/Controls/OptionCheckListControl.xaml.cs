using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionCheckListControl : OptionControlBase
{
    private bool _expanded;

    public OptionCheckListControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is null)
        {
            return;
        }

        LabelText.Text = newValue.Label;
        UpdateDescription(DescriptionText);
        ApplyButton.Content = newValue.CheckApplyLabel ?? mpv_winui.AppContext.AppLang.Apply;
        ApplyButton.Visibility = newValue.CheckApplyHandler is null ? Visibility.Collapsed : Visibility.Visible;
        BuildCheckList();
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
    }

    /// <summary>The whole card toggles the panel, matching the layout cards.</summary>
    private void OnCardTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (Setting?.IsEnabled == false)
        {
            return;
        }

        _expanded = !_expanded;
        ExpandedPanel.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandIcon.Glyph = _expanded ? "\uE70E" : "\uE70D";
    }

    private void BuildCheckList()
    {
        CheckItemsControl.Items.Clear();
        var items = Setting?.CheckItemsProvider?.Invoke()
            ?? Setting?.CheckItems
            ?? [];
        if (items.Count == 0)
        {
            return;
        }

        var boxes = new List<CheckBox>();
        foreach (var item in items)
        {
            var box = new CheckBox
            {
                IsChecked = item.IsChecked,
                Tag = item,
                MinWidth = 132,
            };
            if (string.IsNullOrEmpty(item.Glyph))
            {
                box.Content = item.Label;
            }
            else
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                panel.Children.Add(new FontIcon { Glyph = item.Glyph, FontSize = 14 });
                panel.Children.Add(new TextBlock { Text = item.Label, VerticalAlignment = VerticalAlignment.Center });
                box.Content = panel;
            }
            box.Checked += OnItemChecked;
            box.Unchecked += OnItemChecked;
            boxes.Add(box);
        }

        foreach (var box in boxes)
        {
            CheckItemsControl.Items.Add(box);
        }
    }

    private void OnItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && Setting is { } option && box.Tag is OptionCheckItem item)
        {
            item.IsChecked = box.IsChecked == true;
            option.CheckChanged?.Invoke(option, item.Value, item.IsChecked);
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Setting?.CheckApplyHandler?.Invoke(Setting);
    }
}
