using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mpv_winui.Modules.Settings;

/// <summary>A named set of setting overrides (built-in or user-saved).</summary>
public sealed class SettingPreset
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public Dictionary<string, string> Values { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SettingPreset))]
[JsonSerializable(typeof(List<SettingPreset>))]
internal partial class PresetJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Built-in and user setting presets. Built-ins ship in the bundled
/// presets\ folder; user presets live in the mpv config directory
/// (presets\user-*.json) and are excluded from config deployment so
/// they survive upgrades. Applying a preset writes the stored values
/// into AppSettings and re-applies the mapped mpv commands live.
/// </summary>
public static class PresetService
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(PresetService));

    public static string BundledPresetsPath => Path.Combine(System.AppContext.BaseDirectory, "Presets");

    public static string UserPresetsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mpv-winui", "mpv", "presets");

    public static IReadOnlyList<SettingPreset> GetPresets()
    {
        var result = new List<SettingPreset>();
        try
        {
            if (Directory.Exists(BundledPresetsPath))
            {
                foreach (var file in Directory.GetFiles(BundledPresetsPath, "*.json"))
                {
                    if (TryRead(file) is { } preset)
                    {
                        result.Add(preset);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "loading bundled presets failed");
        }
        try
        {
            if (Directory.Exists(UserPresetsDir))
            {
                foreach (var file in Directory.GetFiles(UserPresetsDir, "user-*.json"))
                {
                    if (TryRead(file) is { } preset)
                    {
                        result.Add(preset);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "loading user presets failed");
        }
        return result;
    }

    private static SettingPreset? TryRead(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, PresetJsonContext.Default.SettingPreset);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "preset load failed, path={}", path);
            return null;
        }
    }

    /// <summary>Saves the current settings as a user preset; returns the file path.</summary>
    public static string SaveUserPreset(string name)
    {
        var export = AppContext.AppSetting.ExportAll();
        var preset = new SettingPreset
        {
            Name = name,
            Values = export.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? ""),
        };
        Directory.CreateDirectory(UserPresetsDir);
        var path = Path.Combine(UserPresetsDir, $"user-{Sanitize(name)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, PresetJsonContext.Default.SettingPreset));
        _logger.Info("user preset saved: {}", path);
        return path;
    }

    public static void DeleteUserPreset(string path)
    {
        if (File.Exists(path) && Path.GetFileName(path).StartsWith("user-", StringComparison.Ordinal))
        {
            File.Delete(path);
        }
    }

    /// <summary>Applies a preset: writes each value into AppSettings (typed) and re-applies the live mpv mapping.</summary>
    public static void Apply(SettingPreset preset)
    {
        var settings = AppContext.AppSetting;
        foreach (var (key, raw) in preset.Values)
        {
            var prop = typeof(AppSettings).GetProperty(key);
            if (prop is null || !prop.CanWrite)
            {
                continue;
            }
            var value = ConvertValue(raw, prop.PropertyType);
            if (value is null)
            {
                continue;
            }
            prop.SetValue(settings, value);
        }

        MpvSettings.ApplyAll(AppContext.SendMpvCommand);
        AppContext.WriteManagedMpvConfig();
        AppContext.WritePluginConfigs();
    }

    private static object? ConvertValue(string raw, Type target)
    {
        try
        {
            if (target == typeof(bool))
            {
                return raw is "true" or "True" or "yes" or "1";
            }
            if (target == typeof(int))
            {
                return int.Parse(raw);
            }
            if (target == typeof(double))
            {
                return double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
            return raw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
