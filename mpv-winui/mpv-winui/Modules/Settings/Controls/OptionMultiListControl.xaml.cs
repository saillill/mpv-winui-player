using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// One row per entry for semicolon-separated values (shader lists, search
/// paths): a "+" button appends an input, each row has a remove button, and
/// the entries are re-joined with ";" when committed.
/// </summary>
public sealed partial class OptionMultiListControl : OptionControlBase
{
    private const string AddTag = "multi:add";
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
            if (newValue.Getter is Func<object?> getter && getter() is string value)
            {
                foreach (var part in Split(value))
                {
                    RowsPanel.Children.Add(BuildRow(part));
                }
            }
            RowsPanel.Children.Add(BuildAddButton());
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void OnOptionStateChanged()
    {
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private Grid BuildRow(string text)
    {
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new TextBox
        {
            Text = text,
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
        Grid.SetColumn(box, 0);
        grid.Children.Add(box);

        var remove = new Button
        {
            Content = "\uE711",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            MinWidth = 32,
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, mpv_winui.AppContext.AppLang.Remove);
        remove.Click += (_, _) =>
        {
            RowsPanel.Children.Remove(grid);
            Commit();
        };
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);
        return grid;
    }

    private Button BuildAddButton()
    {
        var add = new Button
        {
            Tag = AddTag,
            Content = "\uE710",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            MinWidth = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(add, mpv_winui.AppContext.AppLang.Add);
        add.Click += (_, _) => InsertRow();
        return add;
    }

    private void InsertRow()
    {
        // Do not stack empty inputs: an empty last row already implies "add".
        var last = RowsPanel.Children.OfType<Grid>().LastOrDefault();
        if (last is not null && last.Children.OfType<TextBox>().FirstOrDefault()?.Text is not { Length: > 0 })
        {
            return;
        }

        var addButton = RowsPanel.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, AddTag));
        if (addButton is not null)
        {
            RowsPanel.Children.Remove(addButton);
        }

        var row = BuildRow(string.Empty);
        RowsPanel.Children.Add(row);
        RowsPanel.Children.Add(BuildAddButton());
        (row.Children[0] as TextBox)?.Focus(FocusState.Programmatic);
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

        Setting?.Setter?.Invoke(string.Join(';', values));
        Setting?.NotifyChanged();
    }
}
