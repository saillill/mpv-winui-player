using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Canonical control-bar icon catalog for the drag canvas. Glyphs and zone
/// partitions mirror the real control bar (PlayerControl.xaml +
/// ApplyControlBarOrder): most buttons use Segoe glyphs, shuffle/pip only
/// exist in the Fluent system icons font. 原版 (classic) splits the bar into
/// a left transport zone and a right cluster; 居中 (modernx) forces three
/// zones — left cluster, centered transport, right cluster.
/// </summary>
public static class ControlBarIconCatalog
{
    /// <summary>Font that contains the shuffle/pip glyphs (same as the real bar).</summary>
    public const string FluentFont = "ms-appx:///Assets/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular";

    /// <summary>True for ids whose glyph only exists in the Fluent font (shuffle, pip).</summary>
    public static bool IsFluent(string id) => id is "random" or "pip";

    /// <summary>Fixed transport buttons, in 原版 (classic) order.</summary>
    public static IReadOnlyList<(string Id, string Label, string Glyph)> FixedButtons { get; } =
    [
        ("play", "播放/暂停", "\uF5B0"),
        ("previous", "上一首", "\uF8AC"),
        ("next", "下一首", "\uF8AD"),
        ("skip-back", "快退", "\uE627"),
        ("skip-forward", "快进", "\uE628"),
    ];

    /// <summary>Reorderable/hideable buttons (the canvas's movable set).</summary>
    public static IReadOnlyList<(string Id, string Label, string Glyph)> MovableButtons { get; } =
    [
        ("volume", "音量", "\uE995"),
        ("tracks", "轨道", "\uED1F"),
        ("random", "随机", "\uEF37"),
        ("repeat", "循环", "\uE8EE"),
        ("speed", "速度", "\uEC57"),
        ("equalizer", "均衡器", "\uE8B1"),
        ("delay", "延迟", "\uE916"),
        ("aspect", "缩放", "\uE799"),
        ("pip", "画中画", "\uE97E"),
        ("fullwindow", "全窗口", "\uF16B"),
        ("fullscreen", "全屏", "\uE740"),
    ];

    /// <summary>All movable ids in catalog order.</summary>
    public static IReadOnlyList<string> MovableIds { get; } =
    [
        "volume", "tracks", "random", "repeat", "speed", "equalizer",
        "delay", "aspect", "pip", "fullwindow", "fullscreen",
    ];

    /// <summary>Transport order for 原版: play, previous, next, skip-back, skip-forward.</summary>
    public static IReadOnlyList<string> TransportClassic { get; } =
        ["play", "previous", "next", "skip-back", "skip-forward"];

    /// <summary>Transport order for 居中: previous, skip-back, play, skip-forward, next (real bar's middle cluster).</summary>
    public static IReadOnlyList<string> TransportModernX { get; } =
        ["previous", "skip-back", "play", "skip-forward", "next"];

    /// <summary>居中 left zone: volume, tracks, random, speed (reorderable), then the fixed tail.</summary>
    public static IReadOnlyList<string> ModernXLeft { get; } =
        ["volume", "tracks", "random", "speed"];

    /// <summary>居中 right zone: aspect, pip, fullwindow, fullscreen.</summary>
    public static IReadOnlyList<string> ModernXRight { get; } =
        ["aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>原版 left zone (after the transport cluster): repeat, random.</summary>
    public static IReadOnlyList<string> ClassicLeft { get; } =
        ["repeat", "random"];

    /// <summary>原版 right zone: volume, speed, tracks, aspect, pip, fullwindow, fullscreen, then the fixed tail.</summary>
    public static IReadOnlyList<string> ClassicRight { get; } =
        ["volume", "speed", "tracks", "aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>Fixed tail appended after a layout's movable partition (equalizer, delay).</summary>
    public static IReadOnlyList<string> FixedTail { get; } =
        ["equalizer", "delay"];

    /// <summary>Looks an id up in the movable then the fixed catalog.</summary>
    public static (string Id, string Label, string Glyph) Find(string id)
    {
        foreach (var item in MovableButtons)
        {
            if (item.Id == id)
            {
                return item;
            }
        }
        foreach (var item in FixedButtons)
        {
            if (item.Id == id)
            {
                return item;
            }
        }
        return (id, id, "\uE7C3");
    }

    /// <summary>
    /// Applies the Fluent font family to a FontIcon whose glyph only exists
    /// there (shuffle/pip), so the canvas renders exactly like the real bar.
    /// </summary>
    public static FontIcon ApplyGlyphFont(FontIcon icon, string id)
    {
        if (IsFluent(id))
        {
            icon.FontFamily = new FontFamily(FluentFont);
        }
        return icon;
    }
}
