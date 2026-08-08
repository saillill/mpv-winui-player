using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Writes the settings-managed keys into script-opts/*.conf so bundled Lua
/// plugins pick them up on the next mpv start. Existing keys are preserved.
/// </summary>
public static class PluginConfigWriter
{
    private static readonly Dictionary<string, Dictionary<string, string>> Managed = new()
    {
        ["hdr_auto.conf"] = new()
        {
            ["log"] = "no",
        },
        ["metadata_osd.conf"] = new()
        {
            ["enable_on_start"] = "yes",
            ["autohide_timeout_sec"] = "5",
            ["show_chapternumber"] = "no",
        },
        ["coverart.conf"] = new()
        {
            ["prefer_embedded"] = "no",
            ["always_scan_coverart"] = "no",
        },
        ["thumbfast.conf"] = new()
        {
            ["quality"] = "1",
            ["network"] = "no",
            ["min_duration"] = "0",
            ["precise"] = "0",
        },
    };

    public static async Task WriteAllAsync()
    {
        var s = AppContext.AppSetting;
        Managed["hdr_auto.conf"]["log"] = s.HdrAutoLog ? "yes" : "no";
        Managed["metadata_osd.conf"]["enable_on_start"] = s.MetadataOsdEnabled ? "yes" : "no";
        Managed["metadata_osd.conf"]["autohide_timeout_sec"] = s.MetadataOsdAutohideTimeout.ToString();
        Managed["metadata_osd.conf"]["show_chapternumber"] = s.MetadataOsdShowChapter ? "yes" : "no";
        Managed["coverart.conf"]["prefer_embedded"] = s.CoverArtPreferEmbedded ? "yes" : "no";
        Managed["coverart.conf"]["always_scan_coverart"] = s.CoverArtAlwaysScan ? "yes" : "no";
        Managed["thumbfast.conf"]["quality"] = s.ThumbfastQuality.ToString();
        Managed["thumbfast.conf"]["network"] = s.ThumbfastNetwork ? "yes" : "no";
        Managed["thumbfast.conf"]["min_duration"] = s.ThumbfastMinDuration.ToString();
        Managed["thumbfast.conf"]["precise"] = s.ThumbfastPrecise.ToString();

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui",
            "mpv",
            "script-opts");
        Directory.CreateDirectory(dir);

        foreach (var (file, entries) in Managed)
        {
            await WriteFileAsync(Path.Combine(dir, file), entries);
        }
    }

    private static async Task WriteFileAsync(string path, Dictionary<string, string> entries)
    {
        var lines = File.Exists(path) ? await File.ReadAllLinesAsync(path) : [];
        var kept = lines
            .Where(line => !TryParseKey(line, out var key) || !entries.ContainsKey(key))
            .ToList();

        kept.Add("# Managed by mpv-winui settings (edited from the settings window)");
        kept.AddRange(entries.Select(kv => $"{kv.Key}={kv.Value}"));

        await File.WriteAllLinesAsync(path, kept);
    }

    private static bool TryParseKey(string line, out string key)
    {
        key = string.Empty;
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return false;
        }

        var index = trimmed.IndexOf('=');
        if (index <= 0)
        {
            return false;
        }

        key = trimmed[..index].Trim();
        return key.Length > 0;
    }
}
