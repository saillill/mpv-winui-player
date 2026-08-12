using Microsoft.UI.Dispatching;
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
/// Control-bar strip for the layout cards, rendered from the real bar state
/// as one horizontal row split into the real bar's forced zone frames
/// (原版: left transport + right cluster, no center frame; 居中: left cluster |
/// centered transport | right cluster) with the same glyphs and fonts as the
/// real bar (shuffle/pip use the Fluent font). Each movable frame ends with a
/// "+" card placeholder: it never collapses to a dot, and clicking it pops up
/// that frame's addable (hidden) buttons — one click adds the button to the
/// frame. There is no persistent drawer. Collapsed the strip is view-only
/// (frames + icons + "+" placeholders); expanded (SetEditable) it shows a ✕ on
/// every movable cell and enables dragging: drag onto another cell swaps them
/// (对调), drop into a gap inserts with neighbours compacting, dragging out of
/// the strip hides the button. Cells are cached per id and the zones use
/// RepositionThemeTransition, so hide/show/reorder slide instead of jumping.
/// </summary>
public sealed partial class ControlBarCanvasControl : OptionControlBase
{
    private const double CellSize = 30;
    private const double DragThreshold = 5;
    private const double HoldMilliseconds = 250;
    private const double GhostScale = 1.2;
    private const string IndicatorTag = "fixed:indicator";
    private const string AddTag = "fixed:add";

    private readonly DispatcherQueueTimer _holdTimer;
    private readonly Dictionary<string, Border> _cellCache = [];
    private readonly Dictionary<FrameworkElement, Rect> _boundsCache = [];
    private bool _holdElapsed;
    private bool _editable;

    private string _style = "classic";
    private readonly List<string> _shown = [];   // movable ids in display order
    private readonly List<string> _hidden = [];  // movable ids hidden
    private readonly List<string> _custom = [];  // saved custom order
    private readonly List<(int Zone, bool Fixed, string Id)> _barOrder = [];
    private readonly Dictionary<string, int> _zoneOf = []; // per-id zone (0/2); 1 is the fixed transport

    private string? _dragSourceId;
    private bool _dragActive;
    private bool _movedBeyondThreshold;
    private Point _dragStart;
    private Point _lastPointer;
    private Border? _ghost;
    private Border? _dragSourceCell;
    private Border? _dropIndicator;
    private Panel? _dropIndicatorParent;
    private int _lastIndicatorIndex = -1;

    public ControlBarCanvasControl()
    {
        InitializeComponent();
        RootPanel.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        RootPanel.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        RootPanel.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        RootPanel.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnPointerReleased), true);

        // A long-press gates the drag so a quick click never spawns a ghost
        // or highlight. Moving past the threshold before the hold completes
        // does not cancel the press — the drag simply starts when the hold
        // elapses (the pointer already moved, so it is a drag).
        _holdTimer = DispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(HoldMilliseconds);
        _holdTimer.Tick += (_, _) =>
        {
            _holdElapsed = true;
            _holdTimer.Stop();
            if (_dragSourceId is not null && !_dragActive && _movedBeyondThreshold)
            {
                BeginDrag(_lastPointer);
            }
        };
    }

    /// <summary>Whether the strip is in edit mode (✕, drag enabled).</summary>
    public bool IsEditable => _editable;

    /// <summary>Loads the state for the given layout style and re-renders.</summary>
    public void Load(string style)
    {
        _style = style;
        var hiddenSetting = style == "modernx"
            ? AppContext.AppSetting.ControlBarHiddenIconsModernX
            : AppContext.AppSetting.ControlBarHiddenIconsClassic;
        _hidden.Clear();
        _hidden.AddRange(ParseTokens(hiddenSetting));

        // Shown movable ids: custom order first (per partition), then catalog
        // order. The order is stored per layout style so editing 原版 never
        // reorders 居中 and vice versa.
        _shown.Clear();
        _custom.Clear();
        var orderKey = _style == "modernx"
            ? nameof(AppSettings.ControlBarCustomOrderModernX)
            : nameof(AppSettings.ControlBarCustomOrderClassic);
        _custom.AddRange((orderKey == nameof(AppSettings.ControlBarCustomOrderModernX)
                ? AppContext.AppSetting.ControlBarCustomOrderModernX
                : AppContext.AppSetting.ControlBarCustomOrderClassic)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(ControlBarIconCatalog.MovableIds.Contains));
        foreach (var id in _custom)
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

        BarHint.Text = AppContext.AppLang.SettingsControlBarCanvasHint;

        // Default per-id zone follows the layout's partition table; the zone
        // becomes dynamic after a cross-zone drop (see OnPointerReleased).
        _zoneOf.Clear();
        foreach (var id in ControlBarIconCatalog.MovableIds)
        {
            _zoneOf[id] = _style == "modernx"
                ? ControlBarIconCatalog.ModernXRight.Contains(id) ? 2 : 0
                : ControlBarIconCatalog.ClassicLeft.Contains(id) ? 0 : 2;
        }

        BuildBarOrder();

        // Rebuild _shown to match the visual (zone) order exactly. Insertion
        // indices are computed from the rendered order, so this keeps them in
        // sync — dragging across several cells cannot land on the wrong slot.
        var shownSet = new HashSet<string>(_shown, StringComparer.Ordinal);
        _shown.Clear();
        foreach (var (_, fixedCell, id) in _barOrder)
        {
            if (!fixedCell && shownSet.Contains(id))
            {
                _shown.Add(id);
            }
        }

        Render();
    }

    /// <summary>Re-reads the real bar state (used when another card changed it).</summary>
    public void Reload() => Load(_style);
    public void SetEditable(bool value)
    {
        _editable = value;
        BarHint.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Render();
    }

    private static IEnumerable<string> ParseTokens(string? value) =>
        value?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private static bool IsRenderable(string style, string id) =>
        style == "modernx"
            ? ControlBarIconCatalog.ModernXLeft.Contains(id)
              || ControlBarIconCatalog.ModernXRight.Contains(id)
              || ControlBarIconCatalog.FixedTail.Contains(id)
            : ControlBarIconCatalog.ClassicLeft.Contains(id)
              || ControlBarIconCatalog.ClassicRight.Contains(id)
              || ControlBarIconCatalog.FixedTail.Contains(id);

    /// <summary>
    /// Builds the single-row zone order: the fixed transport cluster sits in
    /// its layout position (classic left / modernx center) and every shown
    /// movable button follows the global _shown order in its current zone
    /// (per-id _zoneOf, which becomes dynamic after a cross-zone drop). Hidden
    /// ids are left out, so the strip compacts.
    /// </summary>
    private void BuildBarOrder()
    {
        _barOrder.Clear();
        if (_style == "modernx")
        {
            foreach (var id in _shown)
            {
                if (_zoneOf[id] == 0)
                {
                    _barOrder.Add((0, false, id));
                }
            }
            AddFixedZone(1, ControlBarIconCatalog.TransportModernX);
            foreach (var id in _shown)
            {
                if (_zoneOf[id] == 2)
                {
                    _barOrder.Add((2, false, id));
                }
            }
        }
        else
        {
            AddFixedZone(0, ControlBarIconCatalog.TransportClassic);
            foreach (var id in _shown)
            {
                if (_zoneOf[id] == 0)
                {
                    _barOrder.Add((0, false, id));
                }
            }
            foreach (var id in _shown)
            {
                if (_zoneOf[id] == 2)
                {
                    _barOrder.Add((2, false, id));
                }
            }
        }
    }

    private void AddFixedZone(int zone, IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            _barOrder.Add((zone, true, id));
        }
    }

    // ===== Rendering =====

    private void Render()
    {
        ZoneLeft.Children.Clear();
        ZoneCenter.Children.Clear();
        ZoneRight.Children.Clear();
        foreach (var (zone, fixedCell, id) in _barOrder)
        {
            var panel = ZoneOf(zone);
            var key = (_editable ? "e:" : "s:") + (fixedCell ? "fixed:" : "") + id;
            if (!_cellCache.TryGetValue(key, out var cell))
            {
                var (_, _, glyph) = ControlBarIconCatalog.Find(id);
                cell = BuildCell(glyph, id, fixedCell);
                cell.Tag = fixedCell ? $"fixed:{id}" : id;
                _cellCache[key] = cell;
            }
            panel.Children.Add(cell);
        }

        // Every movable zone keeps a "+" placeholder card when editing and it
        // still has hidden buttons to add — the frame never collapses to a
        // Every movable zone keeps a "+" placeholder card while hidden buttons
        // remain — clicking it pops up ALL hidden buttons (no per-zone limit),
        // added to this zone. The left frame's "+" sits at its right end, the
        // right frame's at its left end (mirrored). Collapsed the strip shows
        // only the frames and the plain icons, so the placeholders are edit-mode only.
        if (_editable && _hidden.Count > 0)
        {
            ZoneOf(0).Children.Add(BuildAddPlaceholder(0));
        }
        if (_editable && _hidden.Count > 0)
        {
            ZoneOf(2).Children.Insert(0, BuildAddPlaceholder(2));
        }

        // Layout changed: cached cell rects and the drop indicator state are stale.
        InvalidateBoundsCache();
        _lastIndicatorIndex = -1;
    }

    private void InvalidateBoundsCache() => _boundsCache.Clear();

    private StackPanel ZoneOf(int zone) => zone switch
    {
        1 => ZoneCenter,
        2 => ZoneRight,
        _ => ZoneLeft,
    };

    private Border BuildCell(string glyph, string id, bool fixed_)
    {
        var cell = new Border
        {
            Width = CellSize,
            Height = CellSize,
            CornerRadius = new CornerRadius(5),
            Background = ThemeResource.Brush(this, "ControlFillColorTertiaryBrush"),
            BorderBrush = ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
        };

        var panel = new Grid();
        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ControlBarIconCatalog.ApplyGlyphFont(icon, id);
        panel.Children.Add(icon);

        if (fixed_)
        {
            // The lock badge marks the fixed transport buttons — edit mode only,
            // so the collapsed strip shows plain icons.
            if (_editable)
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
        }
        else if (_editable)
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

    /// <summary>The "+" placeholder card at the end of a movable zone frame.</summary>
    private Button BuildAddPlaceholder(int zone)
    {
        var button = new Button
        {
            Width = CellSize,
            Height = CellSize,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            Background = ThemeResource.Brush(this, "ControlFillColorSecondaryBrush"),
            BorderBrush = ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Tag = AddTag,
            Content = new FontIcon
            {
                Glyph = "\uE710",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        button.Click += (_, _) => ShowAddFlyout(button, zone);
        return button;
    }

    /// <summary>
    /// Pops up ALL hidden buttons as a MenuFlyout (no per-zone limit) — the
    /// standard WinUI 3 menu (MenuFlyoutItem renders the icon + text with the
    /// platform's hover/focus states); one click adds the button to this zone.
    /// </summary>
    private void ShowAddFlyout(Button anchor, int zone)
    {
        var items = _hidden
            .OrderBy(id => Array.IndexOf(ControlBarIconCatalog.MovableIds.ToArray(), id))
            .ToList();
        // Standard WinUI 3 menu: MenuFlyoutItem renders icon + text with the
        // platform's hover/focus states, and the default MenuFlyoutPresenter
        // already applies the in-app acrylic background with rounded corners —
        // no custom style needed.
        var flyout = new MenuFlyout
        {
            AreOpenCloseAnimationsEnabled = true,
        };
        foreach (var id in items)
        {
            var (_, label, glyph) = ControlBarIconCatalog.Find(id);
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
            };
            ControlBarIconCatalog.ApplyGlyphFont(icon, id);
            var item = new MenuFlyoutItem
            {
                Text = label,
                Icon = icon,
                Tag = id,
            };
            item.Click += (_, _) => AddToZone(id, zone);
            flyout.Items.Add(item);
        }
        if (flyout.Items.Count > 0)
        {
            flyout.ShowAt(anchor);
        }
    }

    // ===== Actions =====

    /// <summary>Adds a hidden button (any id, no per-zone limit) to its zone, after the zone's other shown buttons.</summary>
    private void AddToZone(string id, int zone)
    {
        if (!_hidden.Contains(id))
        {
            return;
        }
        _hidden.Remove(id);
        _zoneOf[id] = zone;
        if (!_shown.Contains(id))
        {
            var insertAt = _shown.Count;
            for (var i = 0; i < _shown.Count; i++)
            {
                if (_zoneOf[_shown[i]] == zone)
                {
                    insertAt = i + 1;
                }
            }
            _shown.Insert(insertAt, id);
        }
        Save();
        BuildBarOrder();
        Render();
    }

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

    /// <summary>Inserts a strip cell at the given index; neighbours compact.</summary>
    private void MoveShown(string id, int insertIndex)
    {
        var fromIndex = _shown.IndexOf(id);
        if (fromIndex < 0)
        {
            return;
        }
        _shown.RemoveAt(fromIndex);
        // The hit index counts the dragged cell while it is still in place;
        // removing it shifts a rightward target down by one.
        var target = insertIndex > fromIndex ? insertIndex - 1 : insertIndex;
        _shown.Insert(Math.Clamp(target, 0, _shown.Count), id);
        Save();
        BuildBarOrder();
        Render();
    }

    private void Save()
    {
        if (_style == "modernx")
        {
            AppContext.AppSetting.ControlBarHiddenIconsModernX = string.Join(',', _hidden);
            AppContext.AppSetting.ControlBarCustomOrderModernX = string.Join(',', _shown);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarHiddenIconsModernX), null);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarCustomOrderModernX), string.Join(',', _shown));
        }
        else
        {
            AppContext.AppSetting.ControlBarHiddenIconsClassic = string.Join(',', _hidden);
            AppContext.AppSetting.ControlBarCustomOrderClassic = string.Join(',', _shown);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarHiddenIconsClassic), null);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarCustomOrderClassic), string.Join(',', _shown));
        }
        StateChanged?.Invoke();
    }

    /// <summary>Raised after the canvas persists a change so other cards' strips re-render.</summary>
    public static event Action? StateChanged;

    // ===== Drag (manual pointer; long-press only; edit mode only) =====

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_editable || e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        // Presses on the ✕ close or the "+" placeholder must never start a drag.
        if (IsWithinButton(e.OriginalSource as DependencyObject))
        {
            return;
        }
        var id = FindCellId(e.OriginalSource as DependencyObject);
        if (id is not null && !_hidden.Contains(id))
        {
            _dragSourceId = id;
            _dragActive = false;
            _movedBeyondThreshold = false;
            _dragStart = e.GetCurrentPoint(RootPanel).Position;
            _lastPointer = _dragStart;
            StartHold();
        }
    }

    private void StartHold()
    {
        _holdElapsed = false;
        _holdTimer.Stop();
        _holdTimer.Start();
    }

    private static bool IsWithinButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
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
        _lastPointer = p;
        if (!_dragActive)
        {
            var dx = p.X - _dragStart.X;
            var dy = p.Y - _dragStart.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance >= DragThreshold)
            {
                _movedBeyondThreshold = true;
            }
            if (_movedBeyondThreshold && _holdElapsed)
            {
                BeginDrag(p);
            }
            else
            {
                return; // still inside the hold gate
            }
        }
        if (_ghost is not null)
        {
            Canvas.SetLeft(_ghost, p.X - CellSize / 2);
            Canvas.SetTop(_ghost, p.Y - CellSize / 2);
        }

        // Pure insertion drag: the blue indicator follows the pointer across
        // every cell and zone, so dropping far away lands exactly where the
        // indicator is — no swap targets, no off-by-a-few-cells jumps.
        UpdateDropIndicator(p);
    }

    /// <summary>Starts the visual drag (ghost follows the pointer).</summary>
    private void BeginDrag(Point position)
    {
        if (_dragActive)
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
            var (_, _, glyph) = ControlBarIconCatalog.Find(_dragSourceId!);
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ControlBarIconCatalog.ApplyGlyphFont(icon, _dragSourceId!);
            _ghost = new Border
            {
                Width = CellSize,
                Height = CellSize,
                CornerRadius = new CornerRadius(5),
                Background = ThemeResource.Brush(this, "CardBackgroundFillColorSecondaryBrush"),
                Child = icon,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var scale = new ScaleTransform { ScaleX = GhostScale, ScaleY = GhostScale };
            _ghost.RenderTransform = scale;
            GhostOverlay.Children.Add(_ghost);
        }
        Canvas.SetLeft(_ghost, position.X - CellSize / 2);
        Canvas.SetTop(_ghost, position.Y - CellSize / 2);
    }

    private Border? FindSourceCell()
    {
        foreach (var (cell, _) in BarCells())
        {
            if (cell is Border { Tag: string tag } b && tag == _dragSourceId)
            {
                return b;
            }
        }
        return null;
    }

    /// <summary>
    /// All strip cells in visual order (fixed + movable + the "+" placeholders),
    /// skipping only the drag indicator. The "+" cards act as drop boundaries,
    /// so a cell can be dropped into the gap right next to a "+".
    /// </summary>
    private IEnumerable<(FrameworkElement Cell, StackPanel Zone)> BarCells()
    {
        foreach (var zone in new[] { ZoneLeft, ZoneCenter, ZoneRight })
        {
            foreach (var child in zone.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is not IndicatorTag)
                {
                    yield return (fe, zone);
                }
            }
        }
    }

    /// <summary>Shows a snap highlight at the insertion slot under the pointer (吸附效果).</summary>
    private void UpdateDropIndicator(Point position)
    {
        if (!_dragActive)
        {
            return;
        }
        var insertIndex = HitBarInsertIndex(position);
        if (insertIndex < 0)
        {
            ClearDropIndicator();
            return;
        }
        if (insertIndex == _lastIndicatorIndex && _dropIndicator is not null)
        {
            return; // unchanged — keep the existing indicator in place
        }
        ClearDropIndicator();

        // Insert the indicator before the cell that currently sits at
        // insertIndex (fixed cells are skipped in the count but still bound
        // the drop positions).
        var shownSeen = 0;
        FrameworkElement? lastCell = null;
        StackPanel? lastZone = null;
        foreach (var (cell, zone) in BarCells())
        {
            lastCell = cell;
            lastZone = zone;
            if (shownSeen == insertIndex)
            {
                _dropIndicator = BuildIndicator();
                _dropIndicatorParent = zone;
                zone.Children.Insert(zone.Children.IndexOf(cell), _dropIndicator);
                _lastIndicatorIndex = insertIndex;
                return;
            }
            if (tagIsMovable(cell))
            {
                shownSeen++;
            }
        }

        // Drop past the last cell: append at the end of the last zone.
        if (lastZone is not null)
        {
            _dropIndicator = BuildIndicator();
            _dropIndicatorParent = lastZone;
            lastZone.Children.Add(_dropIndicator);
            _lastIndicatorIndex = insertIndex;
        }
    }

    private Border BuildIndicator()
    {
        var indicator = new Border
        {
            Width = 3,
            Height = CellSize,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x00, 0x9D, 0xFF)),
            Tag = IndicatorTag,
        };
        return indicator;
    }

    private void ClearDropIndicator()
    {
        if (_dropIndicator is not null)
        {
            _dropIndicatorParent?.Children.Remove(_dropIndicator);
            _dropIndicator = null;
            _dropIndicatorParent = null;
        }
        _lastIndicatorIndex = -1;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSourceId is null)
        {
            return;
        }
        var sourceId = _dragSourceId;
        var wasDrag = _dragActive;
        _dragSourceId = null;
        _dragActive = false;
        _movedBeyondThreshold = false;
        _holdTimer.Stop();

        if (_ghost is not null)
        {
            GhostOverlay.Children.Remove(_ghost);
            _ghost = null;
        }
        if (_dragSourceCell is not null)
        {
            _dragSourceCell.Opacity = 1;
            _dragSourceCell = null;
        }
        ClearDropIndicator();

        if (!wasDrag)
        {
            return; // a plain click (or a click on the ✕ that already hid the cell)
        }

        var position = e.GetCurrentPoint(RootPanel).Position;

        // Insert at the indicator position (neighbours compact left/right),
        // or hide the button when it was dragged out of the strip. Dropping
        // into the other zone re-homes the button there, so every movable id
        // (including the fixed-tail equalizer/delay) can move freely.
        var insertIndex = HitBarInsertIndex(position);
        if (insertIndex >= 0)
        {
            _zoneOf[sourceId] = ZoneAt(position);
            MoveShown(sourceId, insertIndex);
            return;
        }
        HideButton(sourceId);
    }

    /// <summary>
    /// The zone a drop lands in: the zone of the cell that bounds the pointer
    /// (a "+" placeholder bounds like any other cell, so drops beside a "+"
    /// stay in that zone). The fixed center transport bounds both sides, so a
    /// drop there is split by the pointer's side.
    /// </summary>
    private int ZoneAt(Point position)
    {
        foreach (var (cell, panel) in BarCells())
        {
            var rect = BoundsOf(cell);
            if (position.X <= rect.Right)
            {
                var zone = ZoneIndexOf(panel);
                if (zone == 1)
                {
                    return position.X < rect.X + rect.Width / 2 ? 0 : 2;
                }
                return zone;
            }
        }
        return 2;
    }

    private int ZoneIndexOf(StackPanel panel) =>
        ReferenceEquals(panel, ZoneCenter) ? 1
        : ReferenceEquals(panel, ZoneRight) ? 2
        : 0;

    /// <summary>
    /// Returns the _shown insertion index the drop point maps to by scanning
    /// the strip left-to-right across the zones (fixed cells are skipped in
    /// the count but still bound the drop positions). -1 when the drop is
    /// outside the strip vertically (dragging out hides the button); within
    /// the band a drop right of every cell still inserts at the end.
    /// </summary>
    private int HitBarInsertIndex(Point position)
    {
        var barBounds = BoundsOf(BarHost);
        if (position.Y < barBounds.Y - 12 || position.Y > barBounds.Bottom + 12)
        {
            return -1; // outside the strip vertically → hide
        }
        var shownSeen = 0;
        foreach (var (cell, _) in BarCells())
        {
            var rect = BoundsOf(cell);
            if (position.X < rect.X)
            {
                return shownSeen; // dropped left of this cell
            }
            if (position.X <= rect.Right)
            {
                return shownSeen; // on this cell (insert before it)
            }
            if (tagIsMovable(cell))
            {
                shownSeen++;
            }
        }
        return shownSeen; // right of every cell: append at the end
    }

    private static bool tagIsMovable(FrameworkElement cell) =>
        cell.Tag is string tag && !tag.StartsWith("fixed:", StringComparison.Ordinal);

    private Rect BoundsOf(FrameworkElement element)
    {
        // The strip does not move during a drag, so the transform result is
        // cached — TransformToVisual per pointer move is the main drag cost.
        if (_boundsCache.TryGetValue(element, out var cached))
        {
            return cached;
        }
        try
        {
            var t = element.TransformToVisual(RootPanel);
            var origin = t.TransformPoint(new Point(0, 0));
            var rect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            _boundsCache[element] = rect;
            return rect;
        }
        catch (Exception)
        {
            return new Rect(0, 0, 0, 0);
        }
    }
}
