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
/// Drag canvas for the control-bar buttons, rendered from the real bar state
/// as one horizontal row split into the real bar's forced zones (原版: left
/// transport + right cluster; 居中: left cluster | centered transport | right
/// cluster), using the same glyphs and fonts as the real bar. Hidden buttons
/// keep an anonymous grey slot on the strip; the drawer below lists only the
/// remaining hidden buttons (icon + label), never empty frames. A long-press
/// (hold) starts the drag: dragging a strip cell reorders it (insertion
/// indicator), dragging a drawer card onto a strip cell snaps to its center
/// with a blue edge highlight and replaces that cell (the two cards swap),
/// and dragging a cell down onto the drawer hides it. A plain click —
/// including the ✕ close button — never starts a drag.
/// </summary>
public sealed partial class ControlBarCanvasControl : OptionControlBase
{
    private const double CellSize = 30;
    private const double DragThreshold = 5;
    private const double HoldMilliseconds = 250;
    private const double GhostScale = 1.2;
    private const string IndicatorTag = "fixed:indicator";
    private const string SlotPrefix = "slot:";

    private readonly DispatcherQueueTimer _holdTimer;
    private bool _holdElapsed;

    private string _style = "classic";
    private readonly List<string> _shown = [];   // movable ids in display order
    private readonly List<string> _hidden = [];  // movable ids hidden
    private readonly List<string> _custom = [];  // saved custom order
    private readonly List<(int Zone, bool Fixed, string Id)> _barOrder = [];

    private string? _dragSourceId;
    private bool _dragFromAvailable;
    private bool _dragActive;
    private bool _movedBeyondThreshold;
    private Point _dragStart;
    private Point _lastPointer;
    private Border? _ghost;
    private Border? _dragSourceCell;
    private Border? _dropIndicator;
    private Panel? _dropIndicatorParent;
    private Border? _snapTarget;
    private Brush? _snapTargetBrush;
    private Thickness _snapTargetThickness;

    public ControlBarCanvasControl()
    {
        InitializeComponent();
        RootPanel.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        RootPanel.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        RootPanel.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        RootPanel.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnPointerReleased), true);
        AvailableBar.PointerPressed += OnAvailablePressed;

        // A long-press gates the drag so a quick click never spawns a ghost
        // or snap highlight. Moving past the threshold before the hold
        // completes does not cancel the press — the drag simply starts when
        // the hold elapses (the pointer already moved, so it is a drag).
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
        _custom.Clear();
        _custom.AddRange(AppContext.AppSetting.ControlBarCustomOrder
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

    private static bool IsRenderable(string style, string id) =>
        style == "modernx"
            ? ControlBarIconCatalog.ModernXLeft.Contains(id)
              || ControlBarIconCatalog.ModernXRight.Contains(id)
              || ControlBarIconCatalog.FixedTail.Contains(id)
            : ControlBarIconCatalog.ClassicLeft.Contains(id)
              || ControlBarIconCatalog.ClassicRight.Contains(id)
              || ControlBarIconCatalog.FixedTail.Contains(id);

    /// <summary>
    /// Builds the single-row zone order exactly like the real bar's command
    /// bars: classic = left transport + repeat/random | right cluster;
    /// modernx = left cluster | centered transport | right cluster.
    /// </summary>
    private void BuildBarOrder()
    {
        _barOrder.Clear();
        if (_style == "modernx")
        {
            AddMovableZone(0, ControlBarIconCatalog.ModernXLeft);
            AddMovableZone(0, ControlBarIconCatalog.FixedTail);
            AddFixedZone(1, ControlBarIconCatalog.TransportModernX);
            AddMovableZone(2, ControlBarIconCatalog.ModernXRight);
        }
        else
        {
            AddFixedZone(0, ControlBarIconCatalog.TransportClassic);
            AddMovableZone(0, ControlBarIconCatalog.ClassicLeft);
            AddMovableZone(2, ControlBarIconCatalog.ClassicRight);
            AddMovableZone(2, ControlBarIconCatalog.FixedTail);
        }
    }

    private void AddFixedZone(int zone, IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            _barOrder.Add((zone, true, id));
        }
    }

    /// <summary>
    /// Places the partition's cells: custom-ordered shown ids first (relative
    /// custom order within the partition), then the remaining ids in the
    /// partition's default (real bar) order — hidden ids keep their default
    /// position as empty slots.
    /// </summary>
    private void AddMovableZone(int zone, IReadOnlyList<string> partition)
    {
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in _custom)
        {
            if (partition.Contains(id) && _shown.Contains(id) && placed.Add(id))
            {
                _barOrder.Add((zone, false, id));
            }
        }
        foreach (var id in partition)
        {
            if (!placed.Contains(id) && placed.Add(id))
            {
                _barOrder.Add((zone, false, id));
            }
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
            if (fixedCell)
            {
                var (_, _, glyph) = ControlBarIconCatalog.Find(id);
                panel.Children.Add(BuildCell(glyph, id, fixed_: true));
            }
            else if (_hidden.Contains(id))
            {
                panel.Children.Add(BuildHiddenSlot(id));
            }
            else
            {
                var (_, _, glyph) = ControlBarIconCatalog.Find(id);
                var cell = BuildCell(glyph, id, fixed_: false);
                cell.Tag = id;
                panel.Children.Add(cell);
            }
        }

        // Drawer: only the remaining hidden buttons (icon + label below),
        // wrapped per row — no empty "+" frames for shown buttons.
        AvailableBar.Children.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        const int perRow = 8;
        foreach (var id in ControlBarIconCatalog.MovableIds)
        {
            if (!_hidden.Contains(id) || !IsRenderable(_style, id))
            {
                continue;
            }
            var (_, label, glyph) = ControlBarIconCatalog.Find(id);
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ControlBarIconCatalog.ApplyGlyphFont(icon, id);

            var frame = new Border
            {
                Width = CellSize,
                Height = CellSize,
                CornerRadius = new CornerRadius(5),
                Background = ThemeResource.Brush(this, "ControlFillColorTertiaryBrush"),
                BorderBrush = ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                Tag = id,
                Child = icon,
            };

            var item = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Top };
            item.Children.Add(frame);
            item.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 7,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
                Width = CellSize,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            row.Children.Add(item);
            if (row.Children.Count >= perRow)
            {
                AvailableBar.Children.Add(row);
                row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
            }
        }
        if (row.Children.Count > 0)
        {
            AvailableBar.Children.Add(row);
        }
    }

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
            Tag = fixed_ ? $"fixed:{id}" : null,
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

    /// <summary>
    /// A hidden button's slot on the strip: an anonymous grey frame of the
    /// same size as a shown cell — no icon, no label, no original position
    /// info. It stays as a drop target for the drawer cards.
    /// </summary>
    private Border BuildHiddenSlot(string id)
    {
        return new Border
        {
            Width = CellSize,
            Height = CellSize,
            CornerRadius = new CornerRadius(5),
            Background = ThemeResource.Brush(this, "ControlFillColorTertiaryBrush"),
            BorderBrush = ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Tag = SlotPrefix + id,
        };
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

    /// <summary>Shows the dragged drawer card at a hidden slot's position.</summary>
    private void ShowButtonAt(string id, int shownIndex)
    {
        if (!IsRenderable(_style, id))
        {
            return;
        }
        _hidden.Remove(id);
        if (!_shown.Contains(id))
        {
            _shown.Insert(Math.Clamp(shownIndex, 0, _shown.Count), id);
        }
        Save();
        BuildBarOrder();
        Render();
    }

    /// <summary>Replaces the target shown cell with the dragged drawer card (the two swap).</summary>
    private void ReplaceShown(string targetId, string draggedId)
    {
        var index = _shown.IndexOf(targetId);
        if (index < 0 || !IsRenderable(_style, draggedId))
        {
            return;
        }
        _shown[index] = draggedId;
        _hidden.Remove(draggedId);
        if (!_hidden.Contains(targetId))
        {
            _hidden.Add(targetId);
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

    // ===== Drag (manual pointer; long-press only) =====

    private void OnAvailablePressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        if (e.OriginalSource is FrameworkElement fe)
        {
            var slot = FindSlot(fe);
            if (slot is null
                || slot.Tag is not string id
                || !_hidden.Contains(id)
                || !IsRenderable(_style, id))
            {
                return; // empty frame or not in this layout: nothing to drag
            }
            _dragSourceId = id;
            _dragFromAvailable = true;
            _dragActive = false;
            _movedBeyondThreshold = false;
            _dragStart = e.GetCurrentPoint(RootPanel).Position;
            _lastPointer = _dragStart;
            StartHold();
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        // The drawer press handler already claimed this press.
        if (_dragSourceId is not null)
        {
            return;
        }
        // Presses on the ✕ close button must never start a drag.
        if (IsWithinButton(e.OriginalSource as DependencyObject))
        {
            return;
        }
        var id = FindCellId(e.OriginalSource as DependencyObject);
        if (id is not null && !_hidden.Contains(id))
        {
            _dragSourceId = id;
            _dragFromAvailable = false;
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

    private static FrameworkElement? FindSlot(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: string } slot && slot is not null)
            {
                return slot;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private static string? FindCellId(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: string tag }
                && tag is not null
                && !tag.StartsWith("fixed:", StringComparison.Ordinal)
                && !tag.StartsWith(SlotPrefix, StringComparison.Ordinal))
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
        if (_dragFromAvailable)
        {
            UpdateSnapTarget(p);
        }
        else
        {
            UpdateDropIndicator(p);
        }
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

    /// <summary>All strip cells in visual order (fixed, movable, hidden slots), skipping the drop indicator.</summary>
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
        ClearDropIndicator();
        if (!_dragActive)
        {
            return;
        }
        var insertIndex = HitBarInsertIndex(position);
        if (insertIndex < 0)
        {
            return;
        }

        // Insert the indicator before the cell that currently sits at
        // insertIndex (fixed cells and hidden slots are skipped in the count
        // but still bound the drop positions).
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
    }

    /// <summary>
    /// Center-snap for drawer → strip drops: the cell under the pointer (or
    /// the nearest one) gets a blue edge highlight. The dragged card replaces
    /// that cell on release.
    /// </summary>
    private void UpdateSnapTarget(Point position)
    {
        ClearSnapTarget();
        if (!_dragActive)
        {
            return;
        }
        var barBounds = BoundsOf(BarHost);
        if (position.Y < barBounds.Y - 12 || position.Y > barBounds.Bottom + 12)
        {
            return;
        }

        FrameworkElement? best = null;
        double bestDistance = double.MaxValue;
        foreach (var (cell, _) in BarCells())
        {
            if (!tagIsMovable(cell) && !tagIsSlot(cell))
            {
                continue;
            }
            var rect = BoundsOf(cell);
            if (position.X >= rect.X && position.X <= rect.Right)
            {
                best = cell; // strictly inside
                break;
            }
            var d = Math.Abs(position.X - (rect.X + rect.Width / 2));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cell;
            }
        }
        if (best is null)
        {
            return;
        }

        _snapTarget = (Border)best;
        _snapTargetBrush = _snapTarget.BorderBrush;
        _snapTargetThickness = _snapTarget.BorderThickness;
        _snapTarget.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x9D, 0xFF));
        _snapTarget.BorderThickness = new Thickness(2);
    }

    private void ClearSnapTarget()
    {
        if (_snapTarget is not null)
        {
            _snapTarget.BorderBrush = _snapTargetBrush
                ?? ThemeResource.Brush(this, "ControlStrokeColorDefaultBrush");
            _snapTarget.BorderThickness = _snapTargetThickness;
            _snapTarget = null;
            _snapTargetBrush = null;
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
            // A plain click (or a click on the ✕ that already hid the cell).
            ClearSnapTarget();
            return;
        }

        var position = e.GetCurrentPoint(RootPanel).Position;

        if (fromAvailable)
        {
            // Drawer → strip: center-snap replaces the target cell; a hidden
            // slot just fills in at that position.
            if (_snapTarget is not null)
            {
                if (_snapTarget.Tag is string tag && tagIsMovable(_snapTarget))
                {
                    ReplaceShown(tag, sourceId);
                }
                else if (_snapTarget.Tag is string slotTag && slotTag.StartsWith(SlotPrefix, StringComparison.Ordinal))
                {
                    ShowButtonAt(sourceId, SlotShownIndex(_snapTarget));
                }
            }
            ClearSnapTarget();
            return;
        }

        // Strip → strip: reorder with the insertion indicator.
        var insertIndex = HitBarInsertIndex(position);
        if (insertIndex >= 0)
        {
            MoveShown(sourceId, insertIndex);
            return;
        }

        if (IsOverAvailable(position))
        {
            HideButton(sourceId);
        }
    }

    /// <summary>Number of shown cells before the given slot (its insert position).</summary>
    private int SlotShownIndex(FrameworkElement slot)
    {
        var index = 0;
        foreach (var (cell, _) in BarCells())
        {
            if (ReferenceEquals(cell, slot))
            {
                return index;
            }
            if (tagIsMovable(cell))
            {
                index++;
            }
        }
        return index;
    }

    /// <summary>
    /// Returns the _shown insertion index the drop point maps to by scanning
    /// the strip left-to-right across the zones (fixed cells and hidden slots
    /// are skipped in the count but still bound the drop positions). -1 when
    /// the drop is outside the strip — both horizontally (left/right of every
    /// cell) and vertically (above/below the strip band, so a drop on the
    /// drawer below is never mistaken for an insertion).
    /// </summary>
    private int HitBarInsertIndex(Point position)
    {
        var barBounds = BoundsOf(BarHost);
        if (position.Y < barBounds.Y - 12 || position.Y > barBounds.Bottom + 12)
        {
            return -1; // outside the strip vertically (e.g. over the drawer)
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
        return -1;
    }

    private static bool tagIsMovable(FrameworkElement cell) =>
        cell.Tag is string tag
        && tag.Length > 0
        && !tag.StartsWith("fixed:", StringComparison.Ordinal)
        && !tag.StartsWith(SlotPrefix, StringComparison.Ordinal);

    private static bool tagIsSlot(FrameworkElement cell) =>
        cell.Tag is string tag && tag.StartsWith(SlotPrefix, StringComparison.Ordinal);

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
