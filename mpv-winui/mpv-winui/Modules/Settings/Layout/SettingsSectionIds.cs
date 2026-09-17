using mpv_winui.Modules.Language;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace mpv_winui.Modules.Settings.Layout;

/// <summary>
/// Maps a localized section caption back to a stable id.
///
/// Sections are declared as localized strings (sectionMap holds
/// AppLang.SectionXxx values), so the caption itself cannot key a persisted
/// layout: switching the UI language would orphan it. The AppLang property name
/// is stable across languages, so that is what gets stored.
///
/// Only genuine section captions participate — the "SectionDesc*" properties
/// are explanatory sentences, and one of them happens to share its text with
/// the section it describes, which would otherwise make the lookup ambiguous.
/// </summary>
internal static class SettingsSectionIds
{
    private static Dictionary<string, string>? _byCaption;

    /// <summary>Stable id for a section caption, or null when it is unknown.</summary>
    internal static string? IdFor(string? caption)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return null;
        }

        return Map().TryGetValue(caption, out var id) ? id : null;
    }

    /// <summary>Current-language caption for a stable section id.</summary>
    internal static string? CaptionFor(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var pair in Map())
        {
            if (string.Equals(pair.Value, id, StringComparison.Ordinal))
            {
                return pair.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Stable key of the settings category a section belongs to. Mirrors
    /// <c>SettingsPage.CategoryKeys</c>; kept here so a user-created folder can
    /// record which category it lives in by stable key rather than by a
    /// localized label.
    /// </summary>
    private static readonly string[] CategoryKeys =
    [
        "program", "playback", "video", "audio", "subtitles",
        "window", "network", "shortcuts", "osd", "screenshot",
    ];

    /// <summary>Stable id for a category caption (a localized label), or null.</summary>
    internal static string? CategoryKeyFor(string? caption)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return null;
        }

        var lang = AppContext.AppLang;
        var captions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [lang.SettingsCategoryProgram] = CategoryKeys[0],
            [lang.SettingsCategoryPlayback] = CategoryKeys[1],
            [lang.SettingsCategoryVideo] = CategoryKeys[2],
            [lang.SettingsCategoryAudio] = CategoryKeys[3],
            [lang.SettingsCategorySubtitles] = CategoryKeys[4],
            [lang.SettingsCategoryWindow] = CategoryKeys[5],
            [lang.SettingsCategoryNetwork] = CategoryKeys[6],
            [lang.SettingsCategoryShortcuts] = CategoryKeys[7],
            [lang.SettingsCategoryOsd] = CategoryKeys[8],
            [lang.SettingsCategoryScreenshot] = CategoryKeys[9],
        };

        return captions.TryGetValue(caption, out var key) ? key : null;
    }

    /// <summary>Current-language caption for a stable category key, or null.</summary>
    internal static string? CategoryCaptionFor(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var index = Array.IndexOf(CategoryKeys, key);
        if (index < 0)
        {
            return null;
        }

        var lang = AppContext.AppLang;
        return index switch
        {
            0 => lang.SettingsCategoryProgram,
            1 => lang.SettingsCategoryPlayback,
            2 => lang.SettingsCategoryVideo,
            3 => lang.SettingsCategoryAudio,
            4 => lang.SettingsCategorySubtitles,
            5 => lang.SettingsCategoryWindow,
            6 => lang.SettingsCategoryNetwork,
            7 => lang.SettingsCategoryShortcuts,
            8 => lang.SettingsCategoryOsd,
            _ => lang.SettingsCategoryScreenshot,
        };
    }

    private static Dictionary<string, string> Map()
    {
        if (_byCaption is not null)
        {
            return _byCaption;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(AppLang).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(string) || !property.CanRead)
            {
                continue;
            }
            if (!property.Name.StartsWith("Section", StringComparison.Ordinal)
                || property.Name.StartsWith("SectionDesc", StringComparison.Ordinal))
            {
                continue;
            }

            if (property.GetValue(AppContext.AppLang) is string caption
                && !string.IsNullOrEmpty(caption)
                && !map.ContainsKey(caption))
            {
                map[caption] = property.Name;
            }
        }

        _byCaption = map;
        return map;
    }

    /// <summary>Drops the cache so a language switch rebuilds the caption map.</summary>
    internal static void Invalidate() => _byCaption = null;
}
