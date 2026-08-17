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
    private const string ManagedMarker = "# Managed by mpv-winui settings (edited from the settings window)";

    /// <summary>Builds the managed key map with defaults; each write starts from a fresh copy.</summary>
    private static Dictionary<string, Dictionary<string, string>> CreateManaged()
    {
        return new Dictionary<string, Dictionary<string, string>>
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
                ["enable_for_audio"] = "yes",
                ["enable_for_audio_withalbumart"] = "yes",
                ["enable_for_video"] = "no",
                ["enable_for_image"] = "no",
                ["autohide_for_audio"] = "no",
                ["autohide_for_audio_withalbumart"] = "no",
                ["autohide_for_video"] = "yes",
                ["autohide_for_image"] = "yes",
                ["autohide_statusosd_timeout_sec"] = "5",
                ["show_albumtracknumber"] = "no",
                ["osd_message_maxlength"] = "96",
            },
            ["coverart.conf"] = new()
            {
                ["prefer_embedded"] = "no",
                ["always_scan_coverart"] = "no",
                ["load_from_filesystem"] = "yes",
                ["preload"] = "no",
                ["names"] = "cover;folder;album;front",
                ["imageExts"] = "jpg;jpeg;png;bmp;gif;webp",
            },
            ["mpvw_hdr_override.conf"] = new()
            {
                ["mode"] = "",
            },
        };
    }

    public static async Task WriteAllAsync()
    {
        try
        {
            await WriteAllCoreAsync();
        }
        catch (Exception ex)
        {
            // A config write must never crash the app; log and keep the last
            // known file intact (the marker-based merge is idempotent).
            AppContext.AppLogger.Error(ex, "plugin config write failed");
        }
    }

    private static async Task WriteAllCoreAsync()
    {
        var s = AppContext.AppSetting;
        var managed = CreateManaged();
        managed["hdr_auto.conf"]["log"] = s.HdrAutoLog ? "yes" : "no";
        managed["metadata_osd.conf"]["enable_on_start"] = s.MetadataOsdEnabled ? "yes" : "no";
        managed["metadata_osd.conf"]["autohide_timeout_sec"] = s.MetadataOsdAutohideTimeout.ToString();
        managed["metadata_osd.conf"]["show_chapternumber"] = s.MetadataOsdShowChapter ? "yes" : "no";
        managed["metadata_osd.conf"]["enable_for_audio"] = s.MetadataOsdEnableForAudio ? "yes" : "no";
        managed["metadata_osd.conf"]["enable_for_audio_withalbumart"] = s.MetadataOsdEnableForAudioWithAlbumArt ? "yes" : "no";
        managed["metadata_osd.conf"]["enable_for_video"] = s.MetadataOsdEnableForVideo ? "yes" : "no";
        managed["metadata_osd.conf"]["enable_for_image"] = s.MetadataOsdEnableForImage ? "yes" : "no";
        managed["metadata_osd.conf"]["autohide_for_audio"] = s.MetadataOsdAutohideForAudio ? "yes" : "no";
        managed["metadata_osd.conf"]["autohide_for_audio_withalbumart"] = s.MetadataOsdAutohideForAudioWithAlbumArt ? "yes" : "no";
        managed["metadata_osd.conf"]["autohide_statusosd_timeout_sec"] = s.MetadataOsdAutohideStatusTimeout.ToString();
        managed["metadata_osd.conf"]["show_albumtracknumber"] = s.MetadataOsdShowAlbumTrack ? "yes" : "no";
        managed["metadata_osd.conf"]["osd_message_maxlength"] = s.MetadataOsdMessageMaxLength.ToString();
        managed["coverart.conf"]["prefer_embedded"] = s.CoverArtPreferEmbedded ? "yes" : "no";
        managed["coverart.conf"]["always_scan_coverart"] = s.CoverArtAlwaysScan ? "yes" : "no";
        managed["coverart.conf"]["load_from_filesystem"] = s.CoverArtLoadFromFilesystem ? "yes" : "no";
        managed["coverart.conf"]["preload"] = s.CoverArtPreload ? "yes" : "no";
        managed["coverart.conf"]["names"] = s.CoverArtNames;
        managed["coverart.conf"]["imageExts"] = s.CoverArtImageExts;
        managed["mpvw_hdr_override.conf"]["mode"] = s.HdrOverrideMode;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui",
            "mpv",
            "script-opts");
        Directory.CreateDirectory(dir);

        foreach (var (file, entries) in managed)
        {
            var path = Path.Combine(dir, file);
            await WriteFileAsync(path, entries);
            await VerifyFileAsync(path, entries);
        }
    }

    /// <summary>
    /// Post-write readback: every managed key must be present with the exact
    /// value we wrote. A mismatch (e.g. a concurrent writer or a path issue)
    /// is logged as an error instead of silently leaving the plugin with the
    /// previous values.
    /// </summary>
    private static async Task VerifyFileAsync(string path, Dictionary<string, string> entries)
    {
        try
        {
            if (!File.Exists(path))
            {
                AppContext.AppLogger.Error("plugin config missing after write: {}", path);
                return;
            }
            var lines = await File.ReadAllLinesAsync(path);
            var written = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                if (TryParseKey(line, out var key))
                {
                    var eq = line.IndexOf('=');
                    written[key] = line[(eq + 1)..].Trim();
                }
            }
            foreach (var (key, expected) in entries)
            {
                if (!written.TryGetValue(key, out var actual) || actual != expected)
                {
                    AppContext.AppLogger.Error(
                        "plugin config readback mismatch: {} key={} expected={} actual={}",
                        Path.GetFileName(path), key, expected, actual ?? "(missing)");
                }
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "plugin config readback failed: {}", path);
        }
    }

    private static async Task WriteFileAsync(string path, Dictionary<string, string> entries)
    {
        var lines = File.Exists(path) ? await File.ReadAllLinesAsync(path) : [];
        var kept = lines
            .Where(line => line != ManagedMarker && (!TryParseKey(line, out var key) || !entries.ContainsKey(key)))
            .ToList();

        kept.Add(ManagedMarker);
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
