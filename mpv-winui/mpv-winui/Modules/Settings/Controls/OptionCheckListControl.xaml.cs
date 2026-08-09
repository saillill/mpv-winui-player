using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
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

        // Clicks inside the expanded panel (checkboxes, Apply) must not
        // collapse the card again.
        if (e.OriginalSource is Microsoft.UI.Xaml.DependencyObject source
            && IsDescendantOf(source, ExpandedPanel))
        {
            return;
        }

        _expanded = !_expanded;
        ExpandedPanel.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandIcon.Glyph = _expanded ? "\uE70E" : "\uE70D";
    }

    private static bool IsDescendantOf(Microsoft.UI.Xaml.DependencyObject? node, Microsoft.UI.Xaml.DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void BuildCheckList()
    {
        CheckItemsControl.Children.Clear();
        var items = Setting?.CheckItemsProvider?.Invoke()
            ?? Setting?.CheckItems
            ?? [];
        if (items.Count == 0)
        {
            return;
        }

        string? lastGroup = null;
        ItemsControl? groupWrap = null;
        foreach (var item in items)
        {
            if (!string.Equals(item.Group, lastGroup, StringComparison.Ordinal))
            {
                lastGroup = item.Group;
                if (!string.IsNullOrEmpty(item.Group))
                {
                    CheckItemsControl.Children.Add(new TextBlock
                    {
                        Text = item.Group,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        Margin = new Thickness(0, 6, 0, 2),
                    });
                }
                groupWrap = new ItemsControl
                {
                    ItemsPanel = (ItemsPanelTemplate)Resources["CheckWrapPanel"],
                };
                CheckItemsControl.Children.Add(groupWrap);
            }

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
            groupWrap?.Items.Add(box);
        }
    }

    private void OnItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && Setting is { } option && box.Tag is OptionCheckItem item)
        {
            item.IsChecked = box.IsChecked == true;
            option.CheckChanged?.Invoke(option, item.Value, item.IsChecked, item.Target);
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Setting?.CheckApplyHandler?.Invoke(Setting);
    }
}
