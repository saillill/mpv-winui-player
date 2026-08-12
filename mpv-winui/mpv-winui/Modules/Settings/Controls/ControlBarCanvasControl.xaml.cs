using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Drag canvas for the control-bar buttons, rendered from the real state
/// (hidden set + custom order of the selected layout style), like an Android
/// launcher: the shown bar holds the visible buttons (each with a ✕ in the
/// top-right corner to hide it), the drawer below holds the hidden ones and
/// dragging a drawer icon onto the bar shows it. The transport buttons are a
/// locked fixed group. Removing a button makes the neighbours close up
/// automatically (the bar re-renders from the ordered list).
/// </summary>
public sealed partial class ControlBarCanvasControl : OptionControlBase
{
    private string _style = "classic";
    private readonly List<string> _shown = [];   // movable ids in display order
    private readonly List<string> _hidden = [];  // movable ids hidden

    private string? _dragSourceId;
    private bool _dragFromAvailable;
    private bool _dragActive;
    private Point _dragStart;

    public ControlBarCanvasControl()
    {
        InitializeComponent();
        RootPanel.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        RootPanel.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        RootPanel.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        RootPanel.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnPointerReleased), true);
        AvailableBar.PointerPressed += OnAvailablePressed;
    }

    /// <summary>Loads the state for the given layout style and re-renders.</summary>
    public void Load(string style)
    {
        _style = style;
        var hiddenSetting = style == "modernx"
            ? AppContext.AppSetting.ControlBarHiddenIconsModernX
            : AppContext.AppSetting.ControlBarHiddenIconsClassic;
        _hidden.Clear();
        _hidden.AddRange(ParseTokens(hiddenSetting));

        _shown.Clear();
        var custom = AppContext.AppSetting.ControlBarCustomOrder
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(ControlBarIconCatalog.MovableIds.Contains)
            .ToList();
        foreach (var id in custom)
        {
            if (!_hidden.Contains(id) && !_shown.Contains(id))
            {
                _shown.Add(id);
            }
        }
        foreach (var id in ControlBarIconCatalog.MovableIds)
        {
            if (!_hidden.Contains(id) && !_shown.Contains(id))
            {
                _shown.Add(id);
            }
        }

        ApplyLocalizedCaptions();
        Render();
    }

    private static IEnumerable<string> ParseTokens(string? value) =>
        value?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private void ApplyLocalizedCaptions()
    {
        var lang = AppContext.AppLang;
        FixedCaption.Text = lang.SettingsControlBarFixedGroup;
        ShownCaption.Text = lang.SettingsControlBarShown;
        AvailableCaption.Text = lang.SettingsControlBarAvailable;
    }

    // ===== Rendering =====

    private void Render()
    {
        FixedBar.Children.Clear();
        foreach (var (id, label, glyph) in ControlBarIconCatalog.FixedButtons)
        {
            FixedBar.Children.Add(BuildCell(glyph, label, id, fixed_: true));
        }

        ShownBar.Children.Clear();
        foreach (var id in _shown)
        {
            var (_, label, glyph) = CatalogOf(id);
            var cell = BuildCell(glyph, label, id, fixed_: false);
            cell.Tag = id;
            ShownBar.Children.Add(cell);
        }

        AvailableBar.ItemsSource = null;
        var available = _hidden
            .Where(ControlBarIconCatalog.MovableIds.Contains)
            .Select(id => new AvailableCell(id, CatalogOf(id).Label, CatalogOf(id).Glyph))
            .ToList();
        AvailableBar.ItemsSource = available;
        AvailableBar.ItemTemplate = BuildAvailableTemplate();
    }

    private sealed record AvailableCell(string Id, string Label, string Glyph);

    private DataTemplate BuildAvailableTemplate()
    {
        var template = new DataTemplate();
        template.SetValue(FrameworkElement.TagProperty, null);
        // DataTemplate built from XAML string keeps it simple and AOT-safe.
        var xaml =
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "  <Border Width=\"80\" Height=\"80\" CornerRadius=\"6\" Background=\"{ThemeResource CardBackgroundFillColorSecondaryBrush}\" Padding=\"6\">" +
            "    <StackPanel Spacing=\"4\" VerticalAlignment=\"Center\">" +
            "      <FontIcon FontSize=\"20\" Glyph=\"{Binding Glyph}\" HorizontalAlignment=\"Center\" />" +
            "      <TextBlock FontSize=\"10\" Text=\"{Binding Label}\" TextWrapping=\"Wrap\" TextAlignment=\"Center\" MaxLines=\"2\" TextTrimming=\"CharacterEllipsis\" />" +
            "    </StackPanel>" +
            "  </Border>" +
            "</DataTemplate>";
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private static (string Id, string Label, string Glyph) CatalogOf(string id)
    {
        foreach (var item in ControlBarIconCatalog.MovableButtons)
        {
            if (item.Id == id)
            {
                return item;
            }
        }
        return (id, id, "\uE7C3");
    }

    private Border BuildCell(string glyph, string label, string id, bool fixed_)
    {
        var cell = new Border
        {
            Width = fixed_ ? 72 : 72,
            Height = 72,
            CornerRadius = new CornerRadius(6),
            Background = fixed_
                ? ThemeResource.Brush(this, "ControlFillColorTertiaryBrush")
                : ThemeResource.Brush(this, "CardBackgroundFillColorSecondaryBrush"),
            Padding = new Thickness(6),
            Tag = fixed_ ? $"fixed:{id}" : id,
        };

        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(icon, 0);
        panel.Children.Add(icon);

        var caption = new TextBlock
        {
            Text = label,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Grid.SetRow(caption, 1);
        panel.Children.Add(caption);

        if (fixed_)
        {
            var lockIcon = new FontIcon
            {
                Glyph = "\uE72E",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            panel.Children.Add(lockIcon);
        }
        else
        {
            // ✕ in the top-right corner: hide this button (add to the hidden set).
            var close = new Button
            {
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new FontIcon { Glyph = "\uE711", FontSize = 9 },
                Background = null,
                BorderThickness = new Thickness(0),
                Tag = id,
            };
            close.Click += (_, _) => HideButton(id);
            panel.Children.Add(close);
        }

        cell.Child = panel;
        return cell;
    }

    // ===== Actions =====

    private void HideButton(string id)
    {
        _shown.Remove(id);
        if (!_hidden.Contains(id))
        {
            _hidden.Add(id);
        }
        Save();
        Render();
    }

    private void ShowButton(string id, int atIndex)
    {
        _hidden.Remove(id);
        if (!_shown.Contains(id))
        {
            _shown.Insert(Math.Clamp(atIndex, 0, _shown.Count), id);
        }
        Save();
        Render();
    }

    private void MoveShown(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= _shown.Count)
        {
            return;
        }
        var id = _shown[fromIndex];
        _shown.RemoveAt(fromIndex);
        _shown.Insert(Math.Clamp(toIndex, 0, _shown.Count), id);
        Save();
        Render();
    }

    private void Save()
    {
        if (_style == "modernx")
        {
            AppContext.AppSetting.ControlBarHiddenIconsModernX = string.Join(',', _hidden);
        }
        else
        {
            AppContext.AppSetting.ControlBarHiddenIconsClassic = string.Join(',', _hidden);
        }
        AppContext.AppSetting.ControlBarCustomOrder = string.Join(',', _shown);
        AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarHiddenIconsModernX), null);
        AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarCustomOrder), string.Join(',', _shown));
    }

    // ===== Drag (manual pointer; handlers on the container so border capture
    //       cannot swallow moves — same rule as the playlist drag) =====

    private const double DragThreshold = 8;

    private void OnAvailablePressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        if (e.OriginalSource is FrameworkElement fe
            && fe.DataContext is AvailableCell cell)
        {
            _dragSourceId = cell.Id;
            _dragFromAvailable = true;
            _dragActive = false;
            _dragStart = e.GetCurrentPoint(RootPanel).Position;
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        // Shown bar cell (a Border whose Tag is the movable id).
        var id = FindCellId(e.OriginalSource as DependencyObject);
        if (id is not null)
        {
            _dragSourceId = id;
            _dragFromAvailable = false;
            _dragActive = false;
            _dragStart = e.GetCurrentPoint(RootPanel).Position;
        }
    }

    private static string? FindCellId(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: string tag } && tag is not null && !tag.StartsWith("fixed:", StringComparison.Ordinal))
            {
                return tag;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSourceId is null)
        {
            return;
        }
        if (!_dragActive)
        {
            var p = e.GetCurrentPoint(RootPanel).Position;
            var dx = p.X - _dragStart.X;
            var dy = p.Y - _dragStart.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
            {
                return;
            }
            _dragActive = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSourceId is null)
        {
            return;
        }
        var sourceId = _dragSourceId;
        var fromAvailable = _dragFromAvailable;
        var wasDrag = _dragActive;
        _dragSourceId = null;
        _dragActive = false;
        if (!wasDrag && !fromAvailable)
        {
            return; // a plain click on a shown cell
        }

        var position = e.GetCurrentPoint(RootPanel).Position;

        // Dropping on the shown bar: sort (from drawer → insert at index).
        var targetIndex = HitShownIndex(position);
        if (targetIndex >= 0)
        {
            if (fromAvailable)
            {
                ShowButton(sourceId, targetIndex);
            }
            else
            {
                var fromIndex = _shown.IndexOf(sourceId);
                if (fromIndex >= 0)
                {
                    // Inserting before the target when moving down keeps order.
                    var to = fromIndex < targetIndex ? targetIndex + 1 : targetIndex;
                    MoveShown(fromIndex, to);
                }
            }
            return;
        }

        // Dropping on the drawer: hide (only from the shown bar).
        if (!fromAvailable && IsOver(AvailableBar, position, RootPanel))
        {
            HideButton(sourceId);
        }
    }

    private int HitShownIndex(Point position)
    {
        for (var i = 0; i < ShownBar.Children.Count; i++)
        {
            if (ShownBar.Children[i] is FrameworkElement cell && IsOver(cell, position, RootPanel))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsOver(FrameworkElement element, Point position, FrameworkElement relativeTo)
    {
        try
        {
            var transform = element.TransformToVisual(relativeTo);
            var origin = transform.TransformPoint(new Point(0, 0));
            var rect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            return rect.Contains(position);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
