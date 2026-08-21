using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Canonical control-bar icon catalog for the drag canvas. Glyphs and zone
/// partitions mirror the real control bar (PlayerControl.xaml +
/// ApplyControlBarOrder): every button uses a glyph from the bundled Fluent
/// system icons font, matching the real bar. 原版 (classic) splits the bar
/// into a left transport zone and a right cluster; 居中 (modernx) forces
/// three zones — left cluster, centered transport, right cluster.
/// </summary>
public static class ControlBarIconCatalog
{
    /// <summary>Font used by every control-bar glyph (same as the real bar).</summary>
    public const string FluentFont = mpv_winui.Modules.Common.View.IconFonts.FluentSystemIconsUri;

    /// <summary>Fixed transport buttons, in 原版 (classic) order.</summary>
    public static (string Id, string Label, string Glyph)[] FixedButtons { get; } =
    [
        ("play", "Play/Pause", "\uF606"),
        ("previous", "Previous", "\uF629"),
        ("next", "Next", "\uF56A"),
        ("skip-back", "Skip Backward", "\uEAFC"),
        ("skip-forward", "Skip Forward", "\uEB01"),
    ];

    /// <summary>Reorderable/hideable buttons (the canvas's movable set).</summary>
    public static (string Id, string Label, string Glyph)[] MovableButtons { get; } =
    [
        ("volume", "Volume", "\uEB43"),
        ("tracks", "Tracks", "\uEBCD"),
        ("random", "Random", "\uEF37"),
        ("panel", "Control panel", "\uF6AA"),
        ("aspect", "Aspect ratio", "\uEE8D"),
        ("pip", "Picture-in-picture", "\uF5FE"),
        ("fullwindow", "Full window", "\uF160"),
        ("fullscreen", "Full screen", "\uE685"),
    ];

    /// <summary>All movable ids in catalog order.</summary>
    public static string[] MovableIds { get; } =
    [
        "volume", "tracks", "random", "panel",
        "aspect", "pip", "fullwindow", "fullscreen",
    ];

    /// <summary>Transport order for 原版: play, previous, next, skip-back, skip-forward.</summary>
    public static string[] TransportClassic { get; } =
        ["play", "previous", "next", "skip-back", "skip-forward"];

    /// <summary>Transport order for 居中: previous, skip-back, play, skip-forward, next (real bar's middle cluster).</summary>
    public static string[] TransportModernX { get; } =
        ["previous", "skip-back", "play", "skip-forward", "next"];

    /// <summary>居中 left zone: volume, tracks, random, speed (reorderable), then the fixed tail.</summary>
    public static string[] ModernXLeft { get; } =
        ["volume", "tracks", "random", "panel"];

    /// <summary>居中 right zone: aspect, pip, fullwindow, fullscreen.</summary>
    public static string[] ModernXRight { get; } =
        ["aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>原版 left zone (after the transport cluster): repeat, random.</summary>
    public static string[] ClassicLeft { get; } =
        ["panel", "random"];

    /// <summary>原版 right zone: volume, speed, tracks, aspect, pip, fullwindow, fullscreen, then the fixed tail.</summary>
    public static string[] ClassicRight { get; } =
        ["volume", "tracks", "aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>Fixed tail appended after a layout's movable partition (equalizer, delay).</summary>
    public static string[] FixedTail { get; } =
        [];

    /// <summary>Looks an id up in the movable then the fixed catalog.</summary>
    public static (string Id, string Label, string Glyph) Find(string id)
    {
        foreach (var item in MovableButtons)
        {
            if (item.Id == id)
            {
                return (item.Id, Label(item.Id), item.Glyph);
            }
        }
        foreach (var item in FixedButtons)
        {
            if (item.Id == id)
            {
                return (item.Id, Label(item.Id), item.Glyph);
            }
        }
        return (id, Label(id), "\uF6AA");
    }

    /// <summary>Localized label for a control-bar id, falling back to the catalog label.</summary>
    public static string Label(string id)
    {
        var lang = mpv_winui.AppContext.AppLang;
        return id switch
        {
            "play" => lang.Play,
            "previous" => lang.MorePreviousTrack,
            "next" => lang.MoreNextTrack,
            "skip-back" => lang.MoreSkipBackward,
            "skip-forward" => lang.MoreSkipForward,
            "volume" => lang.ControlBarIconVolume,
            "tracks" => lang.ControlBarIconTracks,
            "random" => lang.ControlBarIconRandom,
            "panel" => lang.ControlBarIconPanel,
            "aspect" => lang.ControlBarIconAspect,
            "pip" => lang.ControlBarIconPiP,
            "fullwindow" => lang.ControlBarIconFullWindow,
            "fullscreen" => lang.ControlBarIconFullScreen,
            _ => id,
        };
    }

    /// <summary>Applies the shared Fluent font family to a control-bar FontIcon.</summary>
    public static FontIcon ApplyGlyphFont(FontIcon icon, string id)
    {
        icon.FontFamily = new FontFamily(FluentFont);
        return icon;
    }
}
