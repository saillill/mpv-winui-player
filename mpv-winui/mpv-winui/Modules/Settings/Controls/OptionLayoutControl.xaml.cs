using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Player;
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
            Background = ThemeResource.Brush(this, "CardBackgroundFillColorSecondaryBrush"),
            BorderThickness = new Thickness(2),
            Tag = choice.Value,
        };

        var panel = new StackPanel { Spacing = 8 };
        var header = new Grid { ColumnSpacing = 2 };
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
        card.Tapped += (_, e) =>
        {
            // Toggling an icon checkbox must not collapse the card again.
            if (e.OriginalSource is DependencyObject source && IsDescendantOf(source, expandedPanel))
            {
                return;
            }

            // A tap on the radio itself only changes the selection; the
            // Checked handler already applied it, so never expand here.
            if (e.OriginalSource is DependencyObject tapped && IsDescendantOf(tapped, radio))
            {
                return;
            }

            // Selecting a card must not expand it; expand only happens when
            // tapping an already selected card.
            if (!string.Equals(choice.Value, _current, StringComparison.Ordinal))
            {
                Select(choice.Value);
                return;
            }

            ToggleExpand(card, expandIcon, expandedPanel, choice.Value);
        };
        UpdateCardBorder(card);
        return card;
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void ToggleExpand(Border card, FontIcon icon, StackPanel panel, string value)
    {
        var expand = !string.Equals(_expandedValue, value, StringComparison.Ordinal);
        _expandedValue = expand ? value : null;
        icon.Glyph = expand ? "\uE70E" : "\uE70D";
        panel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        if (expand)
        {
            BuildIconChecklist(panel, value);
        }
    }

    /// <summary>Rebuilds the expanded drag canvas for the current layout style.</summary>
    private void BuildIconChecklist(StackPanel panel, string style)
    {
        panel.Children.Clear();
        var canvas = new ControlBarCanvasControl
        {
            Setting = Setting,
            VerticalAlignment = VerticalAlignment.Center,
        };
        canvas.Load(style);
        panel.Children.Add(canvas);
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

        // Selecting a style switches the hidden-icon list to that style, so
        // collapse any card that was expanded before (selection never expands).
        _expandedValue = null;
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card
                && card.Child is StackPanel panel
                && panel.Children.Count >= 3)
            {
                if (panel.Children[2] is StackPanel expanded)
                {
                    expanded.Visibility = Visibility.Collapsed;
                }
                if (panel.Children[0] is Grid header
                    && header.Children.Count == 3
                    && header.Children[2] is FontIcon icon)
                {
                    icon.Glyph = "\uE70D";
                }
            }
        }

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
    }

    private void UpdateCardBorder(Border card)
    {
        var selected = string.Equals(card.Tag as string, _current, StringComparison.Ordinal);
        card.BorderBrush = selected
            ? new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
            : ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush");
    }

    private FrameworkElement BuildPreview(string value)
    {
        var bar = new Border
        {
            Height = 38,
            Background = ThemeResource.Brush(this, "ControlFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 0, 6, 0),
        };

        // 原版 keeps the upstream two-sided layout (transport on the left,
        // volume/rate/tracks/zoom/pip/window controls on the right). 居中
        // follows ModernX: tracks and volume on the left, the transport
        // cluster centered, window controls on the right.
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (value == "modernx")
        {
            var left = BuildIconCluster(
                HorizontalAlignment.Left,
                "\uED1F", ControlBarIcons.Shuffle, "\uE8EE", "\uEC57", "\uE995"); // tracks, random, repeat, speed, volume
            var center = BuildIconCluster(
                HorizontalAlignment.Center,
                "\uF8AC", "\uE627",                                // previous, skip back
                "\uF5B0",                                          // play/pause
                "\uE628", "\uF8AD");                               // skip forward, next
            var right = BuildIconCluster(
                HorizontalAlignment.Right,
                "\uE799", ControlBarIcons.PictureInPicture,        // aspect, picture-in-picture
                "\uF16B", "\uE740");                               // full window, full screen
            Grid.SetColumn(left, 0);
            Grid.SetColumn(center, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(center);
            grid.Children.Add(right);
        }
        else
        {
            var left = BuildIconCluster(
                HorizontalAlignment.Left,
                "\uF5B0", "\uE627", "\uE628",                      // play, skip back, skip forward
                "\uF8AC", "\uF8AD",                                // previous, next
                ControlBarIcons.Shuffle, "\uE8EE");                // shuffle, repeat
            var right = BuildIconCluster(
                HorizontalAlignment.Right,
                "\uE995", "\uEC57", "\uED1F",                      // volume, speed, tracks
                "\uE799", ControlBarIcons.PictureInPicture, "\uF16B", // zoom, picture-in-picture, full window
                "\uE740");                                         // full screen
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(right);
        }

        bar.Child = grid;
        return bar;
    }

    private StackPanel BuildIconCluster(HorizontalAlignment alignment, params string[] glyphs)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var glyph in glyphs)
        {
            row.Children.Add(CreateIcon(glyph));
        }
        return row;
    }

    /// <summary>Renders a FontIcon glyph, or a PathIcon for "F1 M ..." path data.</summary>
    private UIElement CreateIcon(string value)
    {
        return value.StartsWith("F1 ", StringComparison.Ordinal)
            ? new Viewbox
            {
                Width = 14,
                Height = 14,
                Child = new PathIcon
                {
                    Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), value),
                },
            }
            : new FontIcon { Glyph = value, FontSize = 14 };
    }
}
