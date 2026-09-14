using System.Text.Json.Serialization;

namespace mpv_winui.Modules.Settings.Layout;

/// <summary>
/// Source-generated serialization for the settings layout.
///
/// The app is published trimmed and AOT-compatible, which turns reflection-based
/// serialization off entirely: <c>JsonSerializer.Serialize&lt;T&gt;</c> throws
/// <see cref="System.InvalidOperationException"/> rather than falling back. The
/// customize session snapshots the layout on every edit, so a reflection call
/// there does not merely lose data — it takes the settings page down.
///
/// Declaring the shapes here is what keeps the draft stack, the undo/redo
/// history and the on-disk store working under the trimming settings.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(SettingsLayout))]
[JsonSerializable(typeof(SettingsLayoutEntry))]
[JsonSerializable(typeof(CustomSection))]
[JsonSerializable(typeof(CustomCategory))]
[JsonSerializable(typeof(CustomOption))]
internal partial class SettingsLayoutJsonContext : JsonSerializerContext
{
}

/// <summary>Indented variant, for the file the user can open and edit.</summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(SettingsLayout))]
internal partial class SettingsLayoutPrettyJsonContext : JsonSerializerContext
{
}
