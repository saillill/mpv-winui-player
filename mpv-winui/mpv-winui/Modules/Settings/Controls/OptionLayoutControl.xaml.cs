using Microsoft.UI;
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
        BuildLayouts(newValue.LayoutChoices ?? []);
    }

    protected override void OnOptionStateChanged()
    {
        // Nothing to disable; previews stay visible.
    }

    private void BuildLayouts(IList<OptionLayoutChoice> choices)
    {
        LayoutItems.Items.Clear();
        foreach (var choice in choices)
        {
            var card = new Border
            {
                Width = 120,
                Height = 84,
                Margin = new Thickness(0, 0, 12, 0),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                Tag = choice.Value,
            };

            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = choice.Label,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
            });
            panel.Children.Add(BuildPreview(choice.Value));
            card.Child = panel;

            card.PointerPressed += (_, _) => Select(choice.Value);
            LayoutItems.Items.Add(card);
            UpdateCardSelection(card);
        }
    }

    private static FrameworkElement BuildPreview(string value)
    {
        var bar = new Border
        {
            Height = 30,
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4),
        };

        var icons = value switch
        {
            "modernx" => new[] { "\uE76B", "\uE8BB", "\uE768", "\uE8BB", "\uE76C" },
            "compact" => new[] { "\uE768", "\uE7C9", "\uE740" },
            _ => new[] { "\uE76B", "\uE8BB", "\uE768", "\uE76C", "\uE8D6", "\uE740" },
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var glyph in icons)
        {
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 10 });
        }

        if (value == "compact")
        {
            row.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else if (value == "classic")
        {
            row.HorizontalAlignment = HorizontalAlignment.Left;
        }

        bar.Child = row;
        return bar;
    }

    private void Select(string value)
    {
        if (value == _current || Setting?.Setter is not { } setter)
        {
            return;
        }

        _current = value;
        setter(value);
        Setting?.NotifyChanged();
        foreach (var item in LayoutItems.Items)
        {
            if (item is Border card)
            {
                UpdateCardSelection(card);
            }
        }
    }

    private void UpdateCardSelection(Border card)
    {
        var selected = string.Equals(card.Tag as string, _current, StringComparison.Ordinal);
        card.BorderThickness = new Thickness(selected ? 2 : 1);
        card.BorderBrush = selected
            ? new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
            : (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
    }
}
