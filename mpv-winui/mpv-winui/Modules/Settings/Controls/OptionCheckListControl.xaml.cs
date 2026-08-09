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
        ExpandButton.Content = newValue.CheckExpandLabel ?? mpv_winui.AppContext.AppLang.Expand;
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
        CheckItemsControl.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = _expanded
            ? (Setting?.CheckCollapseLabel ?? mpv_winui.AppContext.AppLang.Collapse)
            : (Setting?.CheckExpandLabel ?? mpv_winui.AppContext.AppLang.Expand);
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
                Content = item.Label,
                IsChecked = item.IsChecked,
                Tag = item.Value,
                MinWidth = 132,
            };
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
}
