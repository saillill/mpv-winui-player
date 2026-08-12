using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Layout card: a radio (原版/居中), its label and the editable control-bar
/// strip rendered directly from the real bar state — there is no separate
/// static preview bar. Collapsed the strip is view-only; tapping an already
/// selected card expands it into edit mode (✕ on movable cells, the hidden
/// drawer, dragging). Selecting a card never expands it.
/// </summary>
public sealed partial class OptionLayoutControl : OptionControlBase
{
    private string _current = string.Empty;
    private string? _expandedValue;

    public OptionLayoutControl()
    {
        InitializeComponent();
        ControlBarCanvasControl.StateChanged += OnCanvasStateChanged;
        Unloaded += (_, _) => ControlBarCanvasControl.StateChanged -= OnCanvasStateChanged;
    }

    /// <summary>Re-reads the real bar state into every card's strip when any canvas changes it.</summary>
    private void OnCanvasStateChanged()
    {
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card
                && card.Child is StackPanel panel
                && panel.Children.Count > 1
                && panel.Children[1] is ControlBarCanvasControl canvas)
            {
                canvas.Reload();
            }
        }
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

        // The editable strip itself is the card body — no separate static
        // preview bar. It renders the real state and only becomes editable
        // (✕, drawer, drag) when the card is expanded.
        var canvas = new ControlBarCanvasControl
        {
            Setting = Setting,
        };
        canvas.Load(choice.Value);
        canvas.SetEditable(false);
        panel.Children.Add(canvas);

        card.Child = panel;
        card.Tapped += (_, e) =>
        {
            // A tap on the radio only changes the selection; the Checked
            // handler already applied it, so never expand here.
            if (e.OriginalSource is DependencyObject tapped && IsDescendantOf(tapped, radio))
            {
                return;
            }

            // Interacting with an expanded strip (✕ click, drag press) must
            // not collapse the card again.
            if (e.OriginalSource is DependencyObject source
                && IsDescendantOf(source, canvas)
                && canvas.IsEditable)
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

            ToggleExpand(card, expandIcon, choice.Value);
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

    private void ToggleExpand(Border card, FontIcon icon, string value)
    {
        var expand = !string.Equals(_expandedValue, value, StringComparison.Ordinal);
        _expandedValue = expand ? value : null;
        icon.Glyph = expand ? "\uE70E" : "\uE70D";
        if (card.Child is StackPanel panel
            && panel.Children.Count > 1
            && panel.Children[1] is ControlBarCanvasControl canvas)
        {
            canvas.SetEditable(expand);
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

        // Selecting a style switches the hidden-icon list to that style, so
        // collapse every card and reset the expand icons (selection never expands).
        _expandedValue = null;
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card && card.Child is StackPanel panel)
            {
                if (panel.Children.Count > 1 && panel.Children[1] is ControlBarCanvasControl canvas)
                {
                    canvas.SetEditable(false);
                }
                if (panel.Children[0] is Grid header
                    && header.Children.Count == 3
                    && header.Children[2] is FontIcon icon)
                {
                    icon.Glyph = "\uE70D";
                }
                UpdateCardBorder(card);
                if (panel.Children[0] is Grid h2)
                {
                    foreach (var child in h2.Children)
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
}
