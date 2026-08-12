using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Canonical control-bar icon catalog for the drag canvas. Glyphs match the
/// real control bar (PlayerControl.xaml): Segoe glyphs for most buttons,
/// Fluent glyphs for shuffle/pip. The partition table mirrors the real
/// classic / modernx layouts so the canvas renders exactly like the bar.
/// </summary>
public static class ControlBarIconCatalog
{
    /// <summary>Fixed transport buttons, in order (never reorderable/hideable).</summary>
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

    /// <summary>ModernX partitions: which movable ids sit on the left / right of the bar.</summary>
    public static IReadOnlyList<string> ModernXLeft { get; } =
        ["tracks", "random", "repeat", "speed", "equalizer", "delay", "volume"];

    public static IReadOnlyList<string> ModernXRight { get; } =
        ["aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>Classic partitions: left holds the transport cluster + shuffle/repeat; right holds the rest.</summary>
    public static IReadOnlyList<string> ClassicLeft { get; } =
        ["random", "repeat"];

    public static IReadOnlyList<string> ClassicRight { get; } =
        ["volume", "speed", "tracks", "equalizer", "delay", "aspect", "pip", "fullwindow", "fullscreen"];
}
