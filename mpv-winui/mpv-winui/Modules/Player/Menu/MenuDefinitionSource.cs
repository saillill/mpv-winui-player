using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mpv_winui.Modules.Player.Menu;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<MenuDefinition>))]
internal partial class MenuJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Loads the menu bar definition: a user override in the mpv config directory
/// wins, otherwise the bundled default is used. A broken/missing file falls
/// back without crashing.
/// </summary>
public static class MenuDefinitionSource
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(MenuDefinitionSource));

    public static string BundledPath => Path.Combine(System.AppContext.BaseDirectory, "Menus", "menus.json");

    public static string UserPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mpv-winui", "mpv", "menus.json");

    public static IReadOnlyList<MenuDefinition>? TryLoad()
    {
        var user = TryRead(UserPath);
        if (user is not null)
        {
            return user;
        }
        return TryRead(BundledPath);
    }

    private static IReadOnlyList<MenuDefinition>? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, MenuJsonContext.Default.ListMenuDefinition);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "menu definition load failed, path={}", path);
            return null;
        }
    }
}
