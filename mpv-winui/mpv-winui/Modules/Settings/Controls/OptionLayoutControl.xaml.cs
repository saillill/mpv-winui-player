using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

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

    /// <summary>Re-renders the card previews when the drag canvas changes the real bar state.</summary>
    private void OnCanvasStateChanged()
    {
        RebuildPreviews();
    }

    private void RebuildPreviews()
    {
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card && card.Child is StackPanel panel)
            {
                // panel[1] is the preview bar.
                if (panel.Children.Count > 1 && panel.Children[1] is FrameworkElement old)
                {
                    panel.Children[1] = BuildPreview(card.Tag as string ?? "classic");
                }
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

    /// <summary>
    /// Renders the card's mini progress bar from the REAL bar state: the
    /// same partitions and glyphs as the actual classic / modernx layouts,
    /// with hidden buttons omitted.
    /// </summary>
    private FrameworkElement BuildPreview(string value)
    {
        var bar = new Border
        {
            Height = 38,
            Background = ThemeResource.Brush(this, "ControlFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 0, 6, 0),
        };

        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        string GlyphOf(string id)
        {
            foreach (var item in ControlBarIconCatalog.MovableButtons)
            {
                if (item.Id == id) return item.Glyph;
            }
            foreach (var item in ControlBarIconCatalog.FixedButtons)
            {
                if (item.Id == id) return item.Glyph;
            }
            return "";
        }

        bool HiddenOf(string id) => _hiddenOf(value).Contains(id, StringComparer.Ordinal);
        var order = _orderOf(value);

        if (value == "modernx")
        {
            // 居中: left cluster (+ fixed tail) | centered transport | right cluster.
            var left = BuildIconCluster(HorizontalAlignment.Left,
                OrderGlyphs(ControlBarIconCatalog.ModernXLeft, order, GlyphOf, HiddenOf)
                    .Concat(OrderGlyphs(ControlBarIconCatalog.FixedTail, order, GlyphOf, HiddenOf))
                    .ToArray());
            var center = BuildIconCluster(HorizontalAlignment.Center,
                OrderGlyphs(ControlBarIconCatalog.TransportModernX, order, GlyphOf, HiddenOf));
            var right = BuildIconCluster(HorizontalAlignment.Right,
                OrderGlyphs(ControlBarIconCatalog.ModernXRight, order, GlyphOf, HiddenOf));
            Grid.SetColumn(left, 0);
            Grid.SetColumn(center, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(center);
            grid.Children.Add(right);
        }
        else
        {
            // 原版: transport + repeat/random on the left, cluster + fixed tail on the right.
            var left = BuildIconCluster(HorizontalAlignment.Left,
                OrderGlyphs(ControlBarIconCatalog.TransportClassic, order, GlyphOf, HiddenOf)
                    .Concat(OrderGlyphs(ControlBarIconCatalog.ClassicLeft, order, GlyphOf, HiddenOf))
                    .ToArray());
            var right = BuildIconCluster(HorizontalAlignment.Right,
                OrderGlyphs(ControlBarIconCatalog.ClassicRight, order, GlyphOf, HiddenOf)
                    .Concat(OrderGlyphs(ControlBarIconCatalog.FixedTail, order, GlyphOf, HiddenOf))
                    .ToArray());
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(right);
        }

        bar.Child = grid;
        return bar;
    }

    private static HashSet<string> _hiddenOf(string value) =>
        (value == "modernx"
            ? AppContext.AppSetting.ControlBarHiddenIconsModernX
            : AppContext.AppSetting.ControlBarHiddenIconsClassic)
        ?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? [];

    private static List<string> _orderOf(string value) =>
        AppContext.AppSetting.ControlBarCustomOrder
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(ControlBarIconCatalog.MovableIds.Contains)
        .ToList();

    /// <summary>Returns the glyphs for the ids in custom order, skipping hidden ones; unreferenced ids keep default order.</summary>
    private static string[] OrderGlyphs(IReadOnlyList<string> ids, List<string> order, Func<string, string> glyphOf, Func<string, bool> hiddenOf)
    {
        var result = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in order)
        {
            if (ids.Contains(id) && !hiddenOf(id) && placed.Add(id))
            {
                result.Add(glyphOf(id));
            }
        }
        foreach (var id in ids)
        {
            if (!hiddenOf(id) && placed.Add(id))
            {
                result.Add(glyphOf(id));
            }
        }
        return result.ToArray();
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

    /// <summary>Renders a FontIcon glyph, or a PathIcon for "F1 M ..." path data. Shuffle/pip glyphs need the Fluent font like the real bar.</summary>
    private UIElement CreateIcon(string value)
    {
        if (value.StartsWith("F1 ", StringComparison.Ordinal))
        {
            return new Viewbox
            {
                Width = 14,
                Height = 14,
                Child = new PathIcon
                {
                    Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), value),
                },
            };
        }

        var icon = new FontIcon { Glyph = value, FontSize = 14 };
        if (value is "\uEF37" or "\uE97E")
        {
            icon.FontFamily = new FontFamily(ControlBarIconCatalog.FluentFont);
        }
        return icon;
    }
}
