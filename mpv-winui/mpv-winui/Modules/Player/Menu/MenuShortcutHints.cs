using System;
using System.Collections.Generic;
using System.IO;

namespace mpv_winui.Modules.Player.Menu;

/// <summary>
/// Resolves display-only shortcut hints for menu items from input.conf (the
/// single source of truth for bindings). Hints are plain text on purpose:
/// real KeyboardAccelerators would double-fire, because the app forwards
/// every key to mpv through a thread-level WH_KEYBOARD hook regardless of
/// XAML focus.
/// </summary>
public static class MenuShortcutHints
{
    private static Dictionary<string, string>? _hintsByCommand;

    public static string? FindForCommand(string? mpvCommand)
    {
        if (string.IsNullOrWhiteSpace(mpvCommand))
        {
            return null;
        }

        _hintsByCommand ??= Load();
        return _hintsByCommand.TryGetValue(Normalize(mpvCommand), out var key) ? key : null;
    }

    private static Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui", "mpv", "input.conf");
        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var separator = line.IndexOfAny([' ', '\t']);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var command = line[separator..].Trim();
            // Strip trailing "#..." comments (dyn_menu "menu:" annotations
            // included) so only the executable command part is compared.
            var hash = command.IndexOf('#');
            if (hash >= 0)
            {
                command = command[..hash].Trim();
            }

            if (key.Length == 0 || command.Length == 0 || !IsKeyboardKey(key))
            {
                continue;
            }

            // First keyboard binding wins; later duplicate commands (e.g. a
            // second row re-binding the same command for the mpv menu) keep
            // the primary key.
            if (!result.ContainsKey(Normalize(command)))
            {
                result[Normalize(command)] = key;
            }
        }

        return result;
    }

    /// <summary>Only keyboard bindings read naturally as menu hints; mouse
    /// buttons, wheel gestures and the "_" menu-only placeholder are skipped.</summary>
    private static bool IsKeyboardKey(string key) =>
        !key.StartsWith("MBTN_", StringComparison.OrdinalIgnoreCase)
        && !key.StartsWith("WHEEL_", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(key, "_", StringComparison.Ordinal);

    private static string Normalize(string command) =>
        string.Join(' ', command.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
}
