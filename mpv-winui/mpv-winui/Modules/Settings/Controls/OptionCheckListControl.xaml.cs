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
        ToolTipService.SetToolTip(ExpandButton, newValue.CheckExpandLabel ?? mpv_winui.AppContext.AppLang.Expand);
        ApplyButton.Content = newValue.CheckApplyLabel ?? mpv_winui.AppContext.AppLang.Apply;
        ApplyButton.Visibility = newValue.CheckApplyHandler is null ? Visibility.Collapsed : Visibility.Visible;
        BuildCheckList();
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        ExpandButton.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        _expanded = !_expanded;
        ExpandedPanel.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandIcon.Glyph = _expanded ? "\uE70E" : "\uE70D";
    }

    private void BuildCheckList()
    {
        CheckItemsControl.Items.Clear();
        if (Setting?.CheckItems is not { } items)
        {
            return;
        }

        var boxes = new List<CheckBox>();
        foreach (var item in items)
        {
            var box = new CheckBox
            {
                IsChecked = item.IsChecked,
                Tag = item.Value,
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
        if (sender is CheckBox box && Setting is { } option && box.Tag is string value)
        {
            option.CheckChanged?.Invoke(option, value, box.IsChecked == true);
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Setting?.CheckApplyHandler?.Invoke(Setting);
    }
}
