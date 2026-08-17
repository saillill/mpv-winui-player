using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Layout card: a radio (原版/居中), its label and the control-bar strip
/// rendered directly from the real bar state (zone frames + icons + per-frame
/// "+" placeholders). Collapsed the strip is view-only; the chevron in the
/// header is a customize button that toggles edit mode on the selected card
/// (✕ on movable cells, dragging). Selecting a card never enters edit mode.
/// </summary>
public sealed partial class OptionLayoutControl : OptionControlBase
{
    private string _current = string.Empty;
    private string? _expandedValue;
    private bool _suppressRadio;

    public OptionLayoutControl()
    {
        InitializeComponent();
        ControlBarCanvasControl.StateChanged += OnCanvasStateChanged;
        Unloaded += (_, _) => ControlBarCanvasControl.StateChanged -= OnCanvasStateChanged;
    }

    /// <summary>Re-reads the real bar state into every card's strip when any canvas changes it.</summary>
    private void OnCanvasStateChanged(ControlBarCanvasControl sender)
    {
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card
                && card.Child is StackPanel panel
                && panel.Children.Count > 1
                && panel.Children[1] is ControlBarCanvasControl canvas
                && !ReferenceEquals(canvas, sender))
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
            MinWidth = 0, // WinUI defaults to 120px, which shoves the title far from the dot
            Tag = choice.Value,
        };
        radio.Checked += (_, _) =>
        {
            if (_suppressRadio)
            {
                return;
            }
            Select(choice.Value);
        };
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

        // A "Customize" button that reuses the exact same style as the other
        // setting action buttons (导出设置/导入设置/取消文件关联 render with
        // MinWidth=120, right-aligned, default button template): nothing custom.
        // It toggles edit mode on the selected card — ✕/lock badges, the
        // per-frame "+" popups and dragging only appear when editing. The
        // collapsed strip shows just the zone frames and the plain icons.
        var customize = new Button
        {
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = choice.Value,
            Content = AppContext.AppLang.SettingsControlBarCustomize,
        };
        customize.Click += (_, _) => ToggleEdit(choice.Value);
        Grid.SetColumn(customize, 2);
        header.Children.Add(customize);

        panel.Children.Add(header);

        var canvas = new ControlBarCanvasControl
        {
            Setting = Setting,
        };
        canvas.Load(choice.Value);
        canvas.SetEditable(false);
        panel.Children.Add(canvas);

        card.Child = panel;
        UpdateCardBorder(card);
        return card;
    }

    /// <summary>
    /// The customize button: on a non-selected card it selects that style and
    /// enters edit mode; on the selected card it toggles edit mode.
    /// </summary>
    private void ToggleEdit(string value)
    {
        try
        {
            if (!string.Equals(value, _current, StringComparison.Ordinal))
            {
                _current = value;
                if (Setting?.Setter is { } setter)
                {
                    setter(value);
                }
                Setting?.NotifyChanged();
            }

            var expand = !string.Equals(_expandedValue, value, StringComparison.Ordinal);
            _expandedValue = expand ? value : null;
            _suppressRadio = true;
            try
            {
                foreach (var item in StyleCards.Items)
                {
                    if (item is Border card
                        && card.Tag is string tag
                        && card.Child is StackPanel panel)
                    {
                        if (panel.Children.Count > 1 && panel.Children[1] is ControlBarCanvasControl canvas)
                        {
                            canvas.SetEditable(string.Equals(tag, value, StringComparison.Ordinal) && expand);
                        }
                        UpdateCardBorder(card);
                        if (panel.Children[0] is Grid header)
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
            finally
            {
                _suppressRadio = false;
            }
        }
        catch (Exception ex)
        {
            // Never let a customize click tear down the settings window; log
            // the failure so the next report carries the real stack.
            AppContext.AppLogger.Error(ex, "control-bar layout customize toggle failed");
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
        // collapse every card (selection never edits).
        _expandedValue = null;
        foreach (var item in StyleCards.Items)
        {
            if (item is Border card && card.Child is StackPanel panel)
            {
                if (panel.Children.Count > 1 && panel.Children[1] is ControlBarCanvasControl canvas)
                {
                    canvas.SetEditable(false);
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
