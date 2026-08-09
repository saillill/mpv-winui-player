using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionLayoutControl : OptionControlBase
{
    private string _current = string.Empty;
    private string? _expandedValue;

    public OptionLayoutControl()
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
        if (newValue.Getter is Func<object?> getter && getter() is string value)
        {
            _current = value;
        }
        BuildCards(newValue.LayoutChoices ?? []);
    }

    protected override void OnOptionStateChanged()
    {
    }

    private void BuildCards(IList<OptionLayoutChoice> choices)
    {
        StyleCards.Items.Clear();
        foreach (var choice in choices)
        {
            StyleCards.Items.Add(BuildCard(choice));
        }
    }

    private FrameworkElement BuildCard(OptionLayoutChoice choice)
    {
        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderThickness = new Thickness(2),
            Tag = choice.Value,
        };

        var panel = new StackPanel { Spacing = 8 };
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var radio = new RadioButton
        {
            GroupName = "BarStyle",
            IsChecked = string.Equals(choice.Value, _current, StringComparison.Ordinal),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = choice.Value,
        };
        radio.Checked += (_, _) => Select(choice.Value);
        Grid.SetColumn(radio, 0);
        header.Children.Add(radio);

        var label = new TextBlock
        {
            Text = choice.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        header.Children.Add(label);

        var expandIcon = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(expandIcon, 2);
        header.Children.Add(expandIcon);

        panel.Children.Add(header);
        panel.Children.Add(BuildPreview(choice.Value));

        var expandedPanel = new StackPanel { Visibility = Visibility.Collapsed, Spacing = 6 };
        panel.Children.Add(expandedPanel);

        card.Child = panel;
        card.Tapped += (_, _) => ToggleExpand(card, expandIcon, expandedPanel, choice.Value);
        UpdateCardBorder(card);
        return card;
    }

    private void ToggleExpand(Border card, FontIcon icon, StackPanel panel, string value)
    {
        var expand = !string.Equals(_expandedValue, value, StringComparison.Ordinal);
        _expandedValue = expand ? value : null;
        icon.Glyph = expand ? "\uE70E" : "\uE70D";
        panel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        if (expand)
        {
            BuildIconChecklist(panel);
        }
    }

    /// <summary>Rebuilds the expanded icon checklist for the current layout style.</summary>
    private void BuildIconChecklist(StackPanel panel)
    {
        panel.Children.Clear();
        var items = Setting?.CheckItemsProvider?.Invoke()
            ?? Setting?.CheckItems
            ?? [];
        foreach (var item in items)
        {
            var box = new CheckBox
            {
                IsChecked = item.IsChecked,
                Tag = item,
                MinWidth = 150,
            };
            if (string.IsNullOrEmpty(item.Glyph))
            {
                box.Content = item.Label;
            }
            else
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                row.Children.Add(new FontIcon { Glyph = item.Glyph, FontSize = 14 });
                row.Children.Add(new TextBlock { Text = item.Label, VerticalAlignment = VerticalAlignment.Center });
                box.Content = row;
            }
            box.Checked += OnIconToggled;
            box.Unchecked += OnIconToggled;
            panel.Children.Add(box);
        }
    }

    private void OnIconToggled(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && Setting is { } option && box.Tag is OptionCheckItem item)
        {
            item.IsChecked = box.IsChecked == true;
            option.CheckChanged?.Invoke(option, item.Value, item.IsChecked);
        }
    }

    private void Select(string value)
    {
        if (Setting?.Setter is not { } setter || string.Equals(value, _current, StringComparison.Ordinal))
        {
            return;
        }

        _current = value;
        setter(value);
        Setting?.NotifyChanged();
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card)
            {
                UpdateCardBorder(card);
                if (card.Child is StackPanel panel && panel.Children[0] is Grid header)
                {
                    foreach (var child in header.Children)
                    {
                        if (child is RadioButton radio)
                        {
                            radio.IsChecked = string.Equals(radio.Tag as string, value, StringComparison.Ordinal);
                        }
                    }
                }
            }
        }

        // The expanded checklist belongs to one style, so refresh it when
        // the selected style changes (each style stores its own hidden icons).
        if (!string.IsNullOrEmpty(_expandedValue))
        {
            _expandedValue = value;
            foreach (var item in StyleCards.Items)
            {
                if (item is Border card
                    && string.Equals(card.Tag as string, value, StringComparison.Ordinal)
                    && card.Child is StackPanel panel
                    && panel.Children.Count >= 3
                    && panel.Children[2] is StackPanel expanded)
                {
                    BuildIconChecklist(expanded);
                    break;
                }
            }
        }
    }

    private void UpdateCardBorder(Border card)
    {
        var selected = string.Equals(card.Tag as string, _current, StringComparison.Ordinal);
        card.BorderBrush = selected
            ? new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
            : (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
    }

    private static FrameworkElement BuildPreview(string value)
    {
        var bar = new Border
        {
            Height = 38,
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 0, 6, 0),
        };
        // 原版 follows the upstream control bar order; 居中 follows the
        // ModernX layout (tracks/volume left, transport centered, extras right).
        var icons = value == "modernx"
            ? new[]
            {
                "\uED1F", "\uE995",              // tracks, volume
                "\uF8AC", "\uED3C", "\uE627",    // previous, skip back, rewind
                "\uF5B0",                        // play/pause
                "\uE628", "\uED3D", "\uF8AD",    // fast forward, skip forward, next
                "\uE10C", "\uE8EE", "\uE7C9",    // more, loop, picture-in-picture
                "\uF16B", "\uE740",              // full window, full screen
            }
            : new[]
            {
                "\uF5B0", "\uED3C", "\uED3D",    // play, skip back, skip forward
                "\uF8AC", "\uF8AD",              // previous, next
                "\uE8AC", "\uE8EE",              // shuffle, repeat
                "\uE995", "\uEC57", "\uED1F",    // volume, speed, tracks
                "\uE799", "\uE7C9", "\uF16B",    // zoom, picture-in-picture, full window
                "\uE740", "\uE10C",              // full screen, more
            };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = value == "modernx" ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var glyph in icons)
        {
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        }
        bar.Child = row;
        return bar;
    }
}
