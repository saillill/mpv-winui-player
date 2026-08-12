using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Drag canvas for the control-bar buttons, rendered from the real bar state
/// as a single horizontal row (like the real progress bar): the fixed
/// transport cells plus the shown movable cells in the real layout order,
/// with small gaps where the real bar separates its groups. Cells are small
/// (bar-scale). Each movable cell has a ✕ in the top-right corner to hide it;
/// dragging between cells reorders (a floating scale-up ghost follows the
/// pointer), dragging onto the candidate drawer hides it, and dragging a
/// drawer icon onto the bar shows it. Removing a button makes the neighbours
/// close up automatically.
/// </summary>
public sealed partial class ControlBarCanvasControl : OptionControlBase
{
    private const double CellSize = 30;
    private const double GroupGap = 14;
    private const double DragThreshold = 5;
    private const double GhostScale = 1.2;

    private string _style = "classic";
    private readonly List<string> _shown = [];   // movable ids in display order
    private readonly List<string> _hidden = [];  // movable ids hidden
    private readonly List<(bool Fixed, string? Id)> _barOrder = []; // null Id = group separator

    private string? _dragSourceId;
    private bool _dragFromAvailable;
    private bool _dragActive;
    private Point _dragStart;
    private Border? _ghost;
    private Border? _dragSourceCell;

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

        // Shown movable ids: custom order first (per partition), then catalog order.
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
        BuildBarOrder();
        Render();
    }

    private static IEnumerable<string> ParseTokens(string? value) =>
        value?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private void ApplyLocalizedCaptions()
    {
        var lang = AppContext.AppLang;
        BarHint.Text = lang.SettingsControlBarCanvasHint;
        AvailableCaption.Text = lang.SettingsControlBarAvailable;
    }

    /// <summary>Builds the single-row order exactly like the real bar:
    /// classic = transport + random/repeat | right cluster;
    /// modernx = left cluster | transport | right cluster.</summary>
    private void BuildBarOrder()
    {
        _barOrder.Clear();
        if (_style == "modernx")
        {
            AddPartition(ControlBarIconCatalog.ModernXLeft);
            Separator();
            AddFixed();
            Separator();
            AddPartition(ControlBarIconCatalog.ModernXRight);
        }
        else
        {
            AddFixed();
            AddPartition(ControlBarIconCatalog.ClassicLeft);
            Separator();
            AddPartition(ControlBarIconCatalog.ClassicRight);
        }
        // Trim trailing separators.
        while (_barOrder.Count > 0 && _barOrder[^1].Id is null)
        {
            _barOrder.RemoveAt(_barOrder.Count - 1);
        }
    }

    private void Separator() => _barOrder.Add((false, null));

    private void AddFixed()
    {
        foreach (var (id, _, _) in ControlBarIconCatalog.FixedButtons)
        {
            _barOrder.Add((true, id));
        }
    }

    private void AddPartition(IReadOnlyList<string> partition)
    {
        foreach (var id in _shown)
        {
            if (partition.Contains(id))
            {
                _barOrder.Add((false, id));
            }
        }
    }

    // ===== Rendering =====

    private void Render()
    {
        BarPanel.Children.Clear();
        foreach (var (fixedCell, id) in _barOrder)
        {
            if (id is null)
            {
                BarPanel.Children.Add(new Border { Width = GroupGap, Height = 1 });
                continue;
            }
            if (fixedCell)
            {
                var (_, _, glyph) = FixedCatalog(id);
                BarPanel.Children.Add(BuildCell(glyph, id, fixed_: true));
            }
            else
            {
                var (_, _, glyph) = CatalogOf(id);
                var cell = BuildCell(glyph, id, fixed_: false);
                cell.Tag = id;
                BarPanel.Children.Add(cell);
            }
        }

        AvailableBar.ItemsSource = null;
        AvailableBar.ItemsSource = _hidden
            .Where(ControlBarIconCatalog.MovableIds.Contains)
            .Select(id => new AvailableCell(id, CatalogOf(id).Label, CatalogOf(id).Glyph))
            .ToList();
        AvailableBar.ItemTemplate = BuildAvailableTemplate();
    }

    private sealed record AvailableCell(string Id, string Label, string Glyph);

    private DataTemplate BuildAvailableTemplate()
    {
        var xaml =
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "  <Border Width=\"44\" Height=\"44\" CornerRadius=\"6\" Background=\"{ThemeResource CardBackgroundFillColorSecondaryBrush}\" Padding=\"3\">" +
            "    <StackPanel Spacing=\"2\" VerticalAlignment=\"Center\">" +
            "      <FontIcon FontSize=\"14\" Glyph=\"{Binding Glyph}\" HorizontalAlignment=\"Center\" />" +
            "      <TextBlock FontSize=\"7\" Text=\"{Binding Label}\" TextWrapping=\"NoWrap\" TextAlignment=\"Center\" TextTrimming=\"CharacterEllipsis\" MaxLines=\"1\" />" +
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

    private static (string Id, string Label, string Glyph) FixedCatalog(string id)
    {
        foreach (var item in ControlBarIconCatalog.FixedButtons)
        {
            if (item.Id == id)
            {
                return item;
            }
        }
        return (id, id, "\uE7C3");
    }

    private Border BuildCell(string glyph, string id, bool fixed_)
    {
        var cell = new Border
        {
            Width = CellSize,
            Height = CellSize,
            CornerRadius = new CornerRadius(5),
            Background = fixed_
                ? ThemeResource.Brush(this, "ControlFillColorTertiaryBrush")
                : ThemeResource.Brush(this, "CardBackgroundFillColorSecondaryBrush"),
            Tag = fixed_ ? $"fixed:{id}" : id,
        };

        var panel = new Grid();
        panel.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (fixed_)
        {
            panel.Children.Add(new FontIcon
            {
                Glyph = "\uE72E",
                FontSize = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 1, 0),
            });
        }
        else
        {
            var close = new Button
            {
                Width = 11,
                Height = 11,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new FontIcon { Glyph = "\uE711", FontSize = 6 },
                Background = null,
                BorderThickness = new Thickness(0),
                Tag = id,
                Margin = new Thickness(0, 1, 1, 0),
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
        BuildBarOrder();
        Render();
    }

    private void ShowButton(string id, int insertIndex)
    {
        _hidden.Remove(id);
        if (!_shown.Contains(id))
        {
            _shown.Insert(Math.Clamp(insertIndex, 0, _shown.Count), id);
        }
        Save();
        BuildBarOrder();
        Render();
    }

    private void MoveShown(string id, int insertIndex)
    {
        var fromIndex = _shown.IndexOf(id);
        if (fromIndex < 0)
        {
            return;
        }
        _shown.RemoveAt(fromIndex);
        _shown.Insert(Math.Clamp(insertIndex, 0, _shown.Count), id);
        Save();
        BuildBarOrder();
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
        StateChanged?.Invoke();
    }

    /// <summary>Raised after the canvas persists a change so card previews can re-render.</summary>
    public static event Action? StateChanged;

    // ===== Drag (manual pointer; handlers on the container so border capture
    //       cannot swallow moves — same rule as the playlist drag) =====

    private void OnAvailablePressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is AvailableCell cell)
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
        var p = e.GetCurrentPoint(RootPanel).Position;
        if (!_dragActive)
        {
            var dx = p.X - _dragStart.X;
            var dy = p.Y - _dragStart.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
            {
                return;
            }
            _dragActive = true;
            _dragSourceCell = FindSourceCell();
            if (_dragSourceCell is not null)
            {
                _dragSourceCell.Opacity = 0.35;
            }
            if (_ghost is null)
            {
                var (_, _, glyph) = CatalogOf(_dragSourceId);
                _ghost = new Border
                {
                    Width = CellSize,
                    Height = CellSize,
                    CornerRadius = new CornerRadius(5),
                    Background = ThemeResource.Brush(this, "CardBackgroundFillColorSecondaryBrush"),
                    Child = new FontIcon { Glyph = glyph, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                var scale = new ScaleTransform { ScaleX = GhostScale, ScaleY = GhostScale };
                _ghost.RenderTransform = scale;
                RootPanel.Children.Add(_ghost);
                Canvas.SetZIndex(_ghost, 100);
            }
        }
        if (_ghost is not null)
        {
            Canvas.SetLeft(_ghost, p.X - CellSize / 2);
            Canvas.SetTop(_ghost, p.Y - CellSize / 2);
        }
    }

    private Border? FindSourceCell()
    {
        foreach (var child in BarPanel.Children)
        {
            if (child is Border { Tag: string tag } && tag == _dragSourceId)
            {
                return (Border)child;
            }
        }
        return null;
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

        if (_ghost is not null)
        {
            RootPanel.Children.Remove(_ghost);
            _ghost = null;
        }
        if (_dragSourceCell is not null)
        {
            _dragSourceCell.Opacity = 1;
            _dragSourceCell = null;
        }

        if (!wasDrag && !fromAvailable)
        {
            return; // a plain click on a shown cell
        }

        var position = e.GetCurrentPoint(RootPanel).Position;
        var insertIndex = HitBarInsertIndex(position);
        if (insertIndex >= 0)
        {
            if (fromAvailable)
            {
                ShowButton(sourceId, insertIndex);
            }
            else
            {
                MoveShown(sourceId, insertIndex);
            }
            return;
        }

        if (!fromAvailable && IsOverAvailable(position))
        {
            HideButton(sourceId);
        }
    }

    /// <summary>
    /// Returns the _shown insertion index the drop point maps to by scanning
    /// the single-row bar left-to-right (fixed cells and separators are
    /// skipped; the index counts the movable cells seen so far). -1 when the
    /// drop is outside the bar.
    /// </summary>
    private int HitBarInsertIndex(Point position)
    {
        var movableSeen = 0;
        foreach (var child in BarPanel.Children)
        {
            if (child is not FrameworkElement cell)
            {
                continue;
            }
            var rect = BoundsOf(cell);
            if (position.X < rect.X)
            {
                return movableSeen; // dropped left of this cell
            }
            if (position.X <= rect.Right)
            {
                return movableSeen; // on this cell (insert before it)
            }
            if (tagIsMovable(cell))
            {
                movableSeen++;
            }
        }
        return -1;
    }

    private static bool tagIsMovable(FrameworkElement cell) =>
        cell.Tag is string tag && !tag.StartsWith("fixed:", StringComparison.Ordinal);

    private Rect BoundsOf(FrameworkElement element)
    {
        try
        {
            var t = element.TransformToVisual(RootPanel);
            var origin = t.TransformPoint(new Point(0, 0));
            return new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
        }
        catch (Exception)
        {
            return new Rect(0, 0, 0, 0);
        }
    }

    private bool IsOverAvailable(Point position)
    {
        return BoundsOf(AvailableBar).Contains(position);
    }
}
