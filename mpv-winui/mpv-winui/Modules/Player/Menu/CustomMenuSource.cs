using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mpv_winui.Modules.Player.Menu;

/// <summary>One entry of the user's custom right-click menu section.</summary>
public sealed class CustomMenuItem
{
    public string? Label { get; set; }

    /// <summary>Raw mpv command executed on click.</summary>
    public string? MpvCommand { get; set; }

    public bool Separator { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<CustomMenuItem>))]
internal partial class CustomMenuJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Loads the user's custom right-click menu section from
/// custom_menu.json in the mpv config directory. The file is excluded from
/// config deployment so user edits survive upgrades; a missing/broken file
/// simply yields no custom items.
/// </summary>
public static class CustomMenuSource
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(CustomMenuSource));

    public static string UserPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mpv-winui", "mpv", "custom_menu.json");

    public static IReadOnlyList<CustomMenuItem>? TryLoad()
    {
        try
        {
            if (!File.Exists(UserPath))
            {
                return null;
            }
            var json = File.ReadAllText(UserPath);
            return JsonSerializer.Deserialize(json, CustomMenuJsonContext.Default.ListCustomMenuItem);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "custom menu load failed, path={}", UserPath);
            return null;
        }
    }
}
