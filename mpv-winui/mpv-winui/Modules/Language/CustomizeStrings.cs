using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace mpv_winui.Modules.Language;

/// <summary>
/// Translations for the customize-mode shell.
///
/// AppLang's public properties are the *fallback* vocabulary and are read by
/// name via reflection, so they cannot also carry translations: reflection
/// cannot tell "the English property default" from "a value a user set",
/// and a JSON entry named like a property would overwrite the property rather
/// than accompany it.
///
/// This table is the companion surface. Keys that exist for every language but
/// are not AppLang properties live here, and the nine Languages/*.json files
/// ship them under the same names. AppLang delegates its <c>Customize*</c>
/// accessors to <see cref="Get"/>, so a missing table — a language that has not
/// been translated yet — silently keeps the English default instead of showing
/// a raw resource key.
/// </summary>
internal static class CustomizeStrings
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal);

    private static bool _loaded;

    /// <summary>Localized text for <paramref name="key"/>, or null when untranslated.</summary>
    internal static string? Get(string key) =>
        Values.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>
    /// Reads the customize table out of the active language file. Called by
    /// <see cref="LanguageManager"/> after AppLang has loaded, so the file is
    /// parsed once even though two consumers read it.
    /// </summary>
    internal static void Load(string? path)
    {
        Values.Clear();
        _loaded = true;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Only the keys the shell asks about; anything else belongs to
                // AppLang and was already applied there.
                if (prop.Name.StartsWith("Customize", StringComparison.Ordinal)
                    || string.Equals(prop.Name, "ApplyAndExit", StringComparison.Ordinal))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        Values[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Some entries are nested { en: ... }; take the level a
                        // flat lookup would have produced rather than dropping
                        // the key and falling back to English.
                        foreach (var inner in prop.Value.EnumerateObject())
                        {
                            if (inner.Value.ValueKind == JsonValueKind.String)
                            {
                                Values[prop.Name] = inner.Value.GetString() ?? string.Empty;
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // A broken file already fell back to defaults inside AppLang; the
            // customize table does the same by staying empty.
        }
    }

    /// <summary>Whether a load has happened, so an uninitialized table is distinguishable.</summary>
    internal static bool IsLoaded => _loaded;
}
