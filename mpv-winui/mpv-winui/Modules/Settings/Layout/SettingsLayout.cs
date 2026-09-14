using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mpv_winui.Modules.Settings.Layout;

/// <summary>
/// User customization for one settings row: reordered position is expressed by
/// <see cref="SettingsLayout.Order"/>, everything else lives here. Every field
/// is optional — a row with no entry renders exactly as the code defined it.
/// </summary>
public sealed class SettingsLayoutEntry
{
    /// <summary>Localized label the user typed, overriding the built-in one.</summary>
    public string? Label { get; set; }

    /// <summary>Description the user typed, overriding the built-in one.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Raw mpv option the row publishes, e.g. "hwdec". Empty when the row has
    /// no mpv mapping (pure UI rows such as the language picker).
    /// </summary>
    public string? MpvKey { get; set; }

    /// <summary>
    /// Raw value the user forced for <see cref="MpvKey"/>. When set it wins
    /// over whatever the row's own control would send.
    /// </summary>
    public string? MpvValue { get; set; }

    /// <summary>Row hidden from the page (its stored setting value is kept).</summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Section (2nd-level folder) this row was moved into, by stable section id.
    /// Null while the row stays in the section the code assigned it.
    /// </summary>
    public string? SectionId { get; set; }
}

/// <summary>
/// A section (2nd-level folder) the user created. Its name is the user's own
/// text, so unlike the built-in sections it is not translatable — acceptable
/// because it is content rather than a label the app owns.
/// </summary>
public sealed class CustomSection
{
    /// <summary>Stable id; also what rows store in <see cref="SettingsLayoutEntry.SectionId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Category the folder lives in, by stable category key.</summary>
    public string CategoryKey { get; set; } = "program";

    /// <summary>Folder name shown to the user.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// An option the user added from the customize mode. It is not backed by an
/// AppSettings property: the value lives here and is pushed to mpv directly
/// through <see cref="MpvKey"/>, which is what makes arbitrary mpv options
/// reachable without a code change.
/// </summary>
public sealed class CustomOption
{
    /// <summary>Stable id, also used as the <see cref="Controls.Option.Key"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Category key this entry is filed under (resolved to the localized name at build time).</summary>
    public string CategoryKey { get; set; } = "program";

    public string Section { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Raw mpv option name, e.g. "volume-max".</summary>
    public string MpvKey { get; set; } = string.Empty;

    /// <summary>Which control the row renders: Text, Choice or Boolean.</summary>
    public string Kind { get; set; } = CustomOptionKinds.Text;

    /// <summary>Preset values offered when <see cref="Kind"/> is Choice.</summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>Current value, pushed to mpv as `set {MpvKey} {Value}`.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Control kinds a user-added option can render as.</summary>
public static class CustomOptionKinds
{
    public const string Text = "Text";
    public const string Choice = "Choice";
    public const string Boolean = "Boolean";

    public static readonly string[] All = [Text, Choice, Boolean];
}

/// <summary>
/// Persisted customization of the settings page, stored next to menus.json in
/// the mpv config folder so it travels with the rest of the user's config and
/// stays editable by hand.
/// </summary>
public sealed class SettingsLayout
{
    public const int CurrentVersion = 2;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Display order, by <see cref="Controls.Option.Key"/>. Keys absent from
    /// this list keep their built-in relative order and follow the listed ones.
    /// </summary>
    [JsonPropertyName("order")]
    public List<string> Order { get; set; } = [];

    [JsonPropertyName("entries")]
    public Dictionary<string, SettingsLayoutEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Sidebar order, by category key. Unlisted categories follow, in built-in order.</summary>
    [JsonPropertyName("categoryOrder")]
    public List<string> CategoryOrder { get; set; } = [];

    /// <summary>Sidebar categories the user removed from the pane.</summary>
    [JsonPropertyName("hiddenCategories")]
    public List<string> HiddenCategories { get; set; } = [];

    /// <summary>Options the user added by hand.</summary>
    [JsonPropertyName("added")]
    public List<CustomOption> Added { get; set; } = [];

    /// <summary>
    /// Section (2nd-level) order, by stable section id — the AppLang property
    /// name (e.g. "SectionVideoDecode"), NOT the localized caption. Keying on
    /// the caption would break the layout on a language switch.
    /// </summary>
    [JsonPropertyName("sectionOrder")]
    public List<string> SectionOrder { get; set; } = [];

    /// <summary>Sections the user removed from the page, by stable section id.</summary>
    [JsonPropertyName("hiddenSections")]
    public List<string> HiddenSections { get; set; } = [];

    /// <summary>Sections (2nd-level folders) the user created.</summary>
    [JsonPropertyName("customSections")]
    public List<CustomSection> CustomSections { get; set; } = [];

    /// <summary>True when nothing was customized, so callers can skip the work.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Order.Count == 0
        && Entries.Count == 0
        && CategoryOrder.Count == 0
        && HiddenCategories.Count == 0
        && Added.Count == 0
        && SectionOrder.Count == 0
        && HiddenSections.Count == 0
        && CustomSections.Count == 0;

    /// <summary>The user-created folder with this id, if any.</summary>
    public CustomSection? FindSection(string? id) =>
        id is null ? null : CustomSections.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    public CustomOption? FindAdded(string id) =>
        Added.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    public SettingsLayoutEntry EntryFor(string key)
    {
        if (!Entries.TryGetValue(key, out var entry))
        {
            entry = new SettingsLayoutEntry();
            Entries[key] = entry;
        }
        return entry;
    }

    /// <summary>Drops an entry once the user restored every field to default.</summary>
    public void Prune(string key)
    {
        if (Entries.TryGetValue(key, out var entry)
            && entry.Label is null
            && entry.Description is null
            && entry.MpvKey is null
            && entry.MpvValue is null
            && !entry.Hidden)
        {
            Entries.Remove(key);
        }
    }
}

/// <summary>
/// Reads/writes <see cref="SettingsLayout"/>. A missing or broken file yields
/// an empty layout rather than failing startup — same contract as
/// MenuDefinitionSource.
/// </summary>
public static class SettingsLayoutStore
{
    /// <summary>%LOCALAPPDATA%\mpv-winui\mpv\settings-layout.json</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mpv-winui", "mpv", "settings-layout.json");

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SettingsLayout Load()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return new SettingsLayout();
            }

            var layout = JsonSerializer.Deserialize<SettingsLayout>(File.ReadAllText(path));
            if (layout is null)
            {
                return new SettingsLayout();
            }

            // A layout written by a newer build may use fields this one does
            // not know; keep the parts that still apply rather than discarding
            // the user's whole customization.
            layout.Order ??= [];
            layout.Entries ??= new Dictionary<string, SettingsLayoutEntry>(StringComparer.Ordinal);
            layout.CategoryOrder ??= [];
            layout.HiddenCategories ??= [];
            layout.Added ??= [];
            layout.SectionOrder ??= [];
            layout.HiddenSections ??= [];
            layout.CustomSections ??= [];
            return layout;
        }
        catch
        {
            return new SettingsLayout();
        }
    }

    public static void Save(SettingsLayout layout)
    {
        try
        {
            var path = FilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (layout.IsEmpty)
            {
                // Nothing customized: remove the file so the page falls back to
                // its code-defined layout instead of reading an empty override.
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }

            layout.Version = SettingsLayout.CurrentVersion;
            File.WriteAllText(path, JsonSerializer.Serialize(layout, WriteOptions));
        }
        catch
        {
            // Customization is a convenience; never break the settings page.
        }
    }
}
