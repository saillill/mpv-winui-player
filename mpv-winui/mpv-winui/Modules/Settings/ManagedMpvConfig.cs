using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Maintains a marked block of config-only mpv options (e.g. ytdl_hook script
/// options) inside the deployed mpv.conf. These options cannot be changed at
/// runtime, so they are written before the next mpv start.
/// </summary>
public static class ManagedMpvConfig
{
    private const string BeginMarker = "# === mpv-winui managed options (do not edit) ===";
    private const string EndMarker = "# === end mpv-winui managed options ===";

    public static async Task WriteAsync()
    {
        try
        {
            await WriteCoreAsync();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "managed mpv.conf write failed");
        }
    }

    private static async Task WriteCoreAsync()
    {
        var s = AppContext.AppSetting;
        var lines = new List<string>
        {
            BeginMarker,
            ScriptOpt("ytdl_hook-ytdl_path", s.YtdlPath),
            ScriptOpt("ytdl_hook-try_ytdl_first", s.YtdlTryFirst ? "yes" : "no"),
            ScriptOpt("ytdl_hook-all_formats", s.YtdlAllFormats ? "yes" : "no"),
            ScriptOpt("ytdl_hook-use_manifests", s.YtdlUseManifests ? "yes" : "no"),
            ScriptOpt("ytdl_hook-thumbnails", s.YtdlThumbnails),
            ScriptOpt("ytdl_hook-exclude", s.YtdlExclude),
            EndMarker,
        };

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui",
            "mpv",
            "mpv.conf");
        if (!File.Exists(path))
        {
            // Config is normally deployed by deploy-config.ps1; without it the
            // ytdl_hook options cannot take effect, so surface the condition.
            AppContext.AppLogger.Warn("mpv.conf not found at {}, managed options skipped", path);
            return;
        }

        var text = await File.ReadAllTextAsync(path);
        var start = text.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = text.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            text = text[..start] + string.Join(Environment.NewLine, lines) + text[(end + EndMarker.Length)..];
        }
        else
        {
            if (text.Length > 0 && !text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                text += Environment.NewLine;
            }
            text += string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
    }

    private static string ScriptOpt(string key, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"#script-opts={key}="
            : $"script-opts={key}={Quote(value)}";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
