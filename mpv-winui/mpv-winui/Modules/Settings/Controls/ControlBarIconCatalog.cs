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
    public const string FluentFont = mpv_winui.Modules.Common.View.IconFonts.FluentSystemIconsUri;

    /// <summary>True for ids whose glyph only exists in the Fluent font (shuffle, pip).</summary>
    public static bool IsFluent(string id) => id is "random" or "pip";

    /// <summary>Fixed transport buttons, in 原版 (classic) order.</summary>
    public static IReadOnlyList<(string Id, string Label, string Glyph)> FixedButtons { get; } =
    [
        ("play", "Play/Pause", "\uF5B0"),
        ("previous", "Previous", "\uF8AC"),
        ("next", "Next", "\uF8AD"),
        ("skip-back", "Skip Backward", "\uE627"),
        ("skip-forward", "Skip Forward", "\uE628"),
    ];

    /// <summary>Reorderable/hideable buttons (the canvas's movable set).</summary>
    public static IReadOnlyList<(string Id, string Label, string Glyph)> MovableButtons { get; } =
    [
        ("volume", "Volume", "\uE995"),
        ("tracks", "Tracks", "\uED1F"),
        ("random", "Random", "\uEF37"),
        ("panel", "Control panel", "\uE713"),
        ("aspect", "Aspect ratio", "\uE799"),
        ("pip", "Picture-in-picture", "\uE97E"),
        ("fullwindow", "Full window", "\uF16B"),
        ("fullscreen", "Full screen", "\uE740"),
    ];

    /// <summary>All movable ids in catalog order.</summary>
    public static IReadOnlyList<string> MovableIds { get; } =
    [
        "volume", "tracks", "random", "panel",
        "aspect", "pip", "fullwindow", "fullscreen",
    ];

    /// <summary>Transport order for 原版: play, previous, next, skip-back, skip-forward.</summary>
    public static IReadOnlyList<string> TransportClassic { get; } =
        ["play", "previous", "next", "skip-back", "skip-forward"];

    /// <summary>Transport order for 居中: previous, skip-back, play, skip-forward, next (real bar's middle cluster).</summary>
    public static IReadOnlyList<string> TransportModernX { get; } =
        ["previous", "skip-back", "play", "skip-forward", "next"];

    /// <summary>居中 left zone: volume, tracks, random, speed (reorderable), then the fixed tail.</summary>
    public static IReadOnlyList<string> ModernXLeft { get; } =
        ["volume", "tracks", "random", "panel"];

    /// <summary>居中 right zone: aspect, pip, fullwindow, fullscreen.</summary>
    public static IReadOnlyList<string> ModernXRight { get; } =
        ["aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>原版 left zone (after the transport cluster): repeat, random.</summary>
    public static IReadOnlyList<string> ClassicLeft { get; } =
        ["panel", "random"];

    /// <summary>原版 right zone: volume, speed, tracks, aspect, pip, fullwindow, fullscreen, then the fixed tail.</summary>
    public static IReadOnlyList<string> ClassicRight { get; } =
        ["volume", "tracks", "aspect", "pip", "fullwindow", "fullscreen"];

    /// <summary>Fixed tail appended after a layout's movable partition (equalizer, delay).</summary>
    public static IReadOnlyList<string> FixedTail { get; } =
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
        return (id, Label(id), "\uE7C3");
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
