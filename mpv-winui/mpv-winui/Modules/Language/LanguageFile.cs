using System;
using System.IO;
using System.Text.Json;

namespace mpv_winui.Modules.Language;

/// <summary>
/// Reads a language file once for its several consumers.
///
/// The same JSON feeds two tables: AppLang's settable properties and
/// <see cref="CustomizeStrings"/>, which carries the customize-mode captions
/// AppLang resolves by name rather than by property. Each used to read and
/// parse the file itself — ~90 KB of JSON, twice, on every load.
///
/// The parse must happen once, so the document is owned here and handed out as
/// a cloned element. A <see cref="JsonElement"/> is a view into its owning
/// <see cref="JsonDocument"/>'s pooled buffer and dies with it; each consumer
/// therefore gets its own clone, which is independent and safe to keep.
/// </summary>
internal static class LanguageFile
{
    /// <summary>
    /// Parsed root object of <paramref name="path"/>, or a clone-safe
    /// <see cref="JsonValueKind.Undefined"/> when the file is missing or
    /// malformed — callers treat that as "nothing to apply" and keep defaults.
    /// </summary>
    internal static JsonElement ReadRoot(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return default;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            // Clone: the document is disposed when this frame returns.
            return doc.RootElement.Clone();
        }
        catch
        {
            return default;
        }
    }
}
