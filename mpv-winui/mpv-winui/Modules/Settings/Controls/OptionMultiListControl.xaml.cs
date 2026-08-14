using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// One row per entry for multi-value settings (shader lists, search paths):
/// each row is [ + | input | x ]; "+" inserts a row below, "x" removes the
/// row, and entries are re-joined with the option's separator when committed.
/// </summary>
public sealed partial class OptionMultiListControl : OptionControlBase
{
    private const double ButtonSize = 32;
    private bool _loading;

    public OptionMultiListControl()
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

        _loading = true;
        try
        {
            RowsPanel.Children.Clear();
            var parts = Split(newValue.Getter is Func<object?> getter && getter() is string value
                ? value
                : string.Empty).ToList();
            if (parts.Count == 0)
            {
                parts.Add(string.Empty);
            }
            foreach (var part in parts)
            {
                RowsPanel.Children.Add(BuildRow(part));
            }
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void OnOptionStateChanged()
    {
    }

    private IEnumerable<string> Split(string? value)
    {
        var separator = Setting?.ListSeparator ?? ';';
        return (value ?? string.Empty).Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private Grid BuildRow(string text)
    {
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var add = BuildSquareButton("\uE710", mpv_winui.AppContext.AppLang.Add);
        add.Click += (_, _) => InsertRowAfter(grid);
        Grid.SetColumn(add, 0);
        grid.Children.Add(add);

        var box = new TextBox
        {
            Text = text,
            MinHeight = ButtonSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Commit();
            }
        };
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);

        var remove = BuildSquareButton("\uE711", mpv_winui.AppContext.AppLang.Remove);
        remove.Click += (_, _) =>
        {
            RowsPanel.Children.Remove(grid);
            Commit();
        };
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);
        return grid;
    }

    private static Button BuildSquareButton(string glyph, string name)
    {
        var button = new Button
        {
            Width = ButtonSize,
            Height = ButtonSize,
            MinWidth = ButtonSize,
            MinHeight = ButtonSize,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
            },
        };
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void InsertRowAfter(Grid anchor)
    {
        var index = RowsPanel.Children.IndexOf(anchor);
        if (index < 0)
        {
            return;
        }
        var row = BuildRow(string.Empty);
        RowsPanel.Children.Insert(index + 1, row);
        (row.Children[1] as TextBox)?.Focus(FocusState.Programmatic);
    }

    private void Commit()
    {
        if (_loading)
        {
            return;
        }

        var values = RowsPanel.Children
            .OfType<Grid>()
            .Select(grid => (grid.Children.OfType<TextBox>().FirstOrDefault()?.Text ?? string.Empty).Trim())
            .Where(text => text.Length > 0)
            .ToList();

        var separator = Setting?.ListSeparator ?? ';';
        Setting?.Setter?.Invoke(string.Join(separator, values));
        Setting?.NotifyChanged();
    }
}
