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
