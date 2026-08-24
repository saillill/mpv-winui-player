using System;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Display-only translation of mpv key names. The stored binding always keeps
/// the canonical mpv key (space, ctrl+q, ...); this class only prettifies the
/// label shown in the settings list.
/// </summary>
public static class ShortcutKeyLocalizer
{
    public static string Localize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var lang = mpv_winui.AppContext.AppLang;
        var parts = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var translated = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            translated[i] = parts[i].ToUpperInvariant() switch
            {
                "SPACE" => lang.KeySpace,
                "ENTER" or "RETURN" => lang.KeyEnter,
                "ESC" => lang.KeyEsc,
                "LEFT" => lang.KeyLeft,
                "RIGHT" => lang.KeyRight,
                "UP" => lang.KeyUp,
                "DOWN" => lang.KeyDown,
                "META" => lang.KeyMeta,
                "MBTN_LEFT" => lang.KeyMouseLeft,
                "MBTN_RIGHT" => lang.KeyMouseRight,
                "MBTN_MID" => lang.KeyMouseMiddle,
                "MBTN_BACK" => lang.KeyMouseBackward,
                "MBTN_FORWARD" => lang.KeyMouseForward,
                "MBTN_LEFT_DBL" => lang.KeyMouseLeftDouble,
                "WHEEL_UP" => lang.KeyWheelUp,
                "WHEEL_DOWN" => lang.KeyWheelDown,
                "WHEEL_LEFT" => lang.KeyWheelLeft,
                "WHEEL_RIGHT" => lang.KeyWheelRight,
                "PGUP" => lang.KeyPageUp,
                "PGDWN" => lang.KeyPageDown,
                "BS" => lang.KeyBackspace,
                _ when parts[i].ToUpperInvariant().StartsWith("KP", StringComparison.Ordinal)
                    && parts[i].Length == 3 && char.IsDigit(parts[i][2])
                    => lang.KeyKeypad + parts[i][2],
                _ => parts[i],
            };
        }
        return string.Join('+', translated);
    }
}
