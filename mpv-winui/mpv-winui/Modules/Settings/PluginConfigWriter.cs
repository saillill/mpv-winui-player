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
            ["enable_for_video"] = "no",
            ["enable_for_image"] = "no",
            ["autohide_statusosd_timeout_sec"] = "5",
            ["show_albumtracknumber"] = "no",
        },
        ["coverart.conf"] = new()
        {
            ["prefer_embedded"] = "no",
            ["always_scan_coverart"] = "no",
            ["load_from_filesystem"] = "yes",
            ["preload"] = "no",
        },
        ["thumbfast.conf"] = new()
        {
            ["quality"] = "1",
            ["network"] = "no",
            ["min_duration"] = "0",
            ["precise"] = "0",
            ["max_width"] = "200",
            ["max_height"] = "2000",
            ["spawn_first"] = "no",
            ["sw_threads"] = "6",
            ["frequency"] = "0.15",
        },
    };

    public static async Task WriteAllAsync()
    {
        var s = AppContext.AppSetting;
        Managed["hdr_auto.conf"]["log"] = s.HdrAutoLog ? "yes" : "no";
        Managed["metadata_osd.conf"]["enable_on_start"] = s.MetadataOsdEnabled ? "yes" : "no";
        Managed["metadata_osd.conf"]["autohide_timeout_sec"] = s.MetadataOsdAutohideTimeout.ToString();
        Managed["metadata_osd.conf"]["show_chapternumber"] = s.MetadataOsdShowChapter ? "yes" : "no";
        Managed["metadata_osd.conf"]["enable_for_video"] = s.MetadataOsdEnableForVideo ? "yes" : "no";
        Managed["metadata_osd.conf"]["enable_for_image"] = s.MetadataOsdEnableForImage ? "yes" : "no";
        Managed["metadata_osd.conf"]["autohide_statusosd_timeout_sec"] = s.MetadataOsdAutohideStatusTimeout.ToString();
        Managed["metadata_osd.conf"]["show_albumtracknumber"] = s.MetadataOsdShowAlbumTrack ? "yes" : "no";
        Managed["coverart.conf"]["prefer_embedded"] = s.CoverArtPreferEmbedded ? "yes" : "no";
        Managed["coverart.conf"]["always_scan_coverart"] = s.CoverArtAlwaysScan ? "yes" : "no";
        Managed["coverart.conf"]["load_from_filesystem"] = s.CoverArtLoadFromFilesystem ? "yes" : "no";
        Managed["coverart.conf"]["preload"] = s.CoverArtPreload ? "yes" : "no";
        Managed["thumbfast.conf"]["quality"] = s.ThumbfastQuality.ToString();
        Managed["thumbfast.conf"]["network"] = s.ThumbfastNetwork ? "yes" : "no";
        Managed["thumbfast.conf"]["min_duration"] = s.ThumbfastMinDuration.ToString();
        Managed["thumbfast.conf"]["precise"] = s.ThumbfastPrecise.ToString();
        Managed["thumbfast.conf"]["max_width"] = s.ThumbfastMaxWidth.ToString();
        Managed["thumbfast.conf"]["max_height"] = s.ThumbfastMaxHeight.ToString();
        Managed["thumbfast.conf"]["spawn_first"] = s.ThumbfastSpawnFirst ? "yes" : "no";
        Managed["thumbfast.conf"]["sw_threads"] = s.ThumbfastThreads.ToString();
        Managed["thumbfast.conf"]["frequency"] = s.ThumbfastFrequency.ToString("0.##");

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
