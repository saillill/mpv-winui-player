using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace mpv_winui.Modules.FileSystem
{
    /// <summary>
    /// Deployment of the bundled mpv config layer (mpv-winui-lazy) into
    /// %LOCALAPPDATA%\mpv-winui\mpv.
    ///
    /// The layer is app-owned infrastructure and is therefore kept in sync on
    /// every launch instead of being written once. The previous behaviour - a
    /// total no-op as soon as mpv.conf existed - meant a config fix could never
    /// reach an existing install: the shipped [mpvw-hdr] profile stayed at its
    /// first-run revision forever, so HDR output options that were fixed in the
    /// repository never took effect anywhere.
    ///
    /// Safety rules:
    ///  - user-owned files and the state the app itself rewrites at runtime are
    ///    never touched once they exist (see <see cref="UserOwnedFiles"/> /
    ///    <see cref="UserOwnedDirs"/>);
    ///  - a layer file is only refreshed when the copy on disk still matches
    ///    what this class last deployed (recorded in a manifest) or when no
    ///    manifest exists yet - a legacy install whose layer was written by an
    ///    older build;
    ///  - anything overwritten that was not ours is first preserved next to it
    ///    as "&lt;name&gt;.bak-&lt;timestamp&gt;", so a local edit is recoverable.
    /// </summary>
    public static class ConfigDeployer
    {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(ConfigDeployer));

        /// <summary>Records what this class deployed, to tell "ours, untouched" from "user-edited".</summary>
        private const string ManifestName = "mpv-winui-deploy.manifest";

        // Mirrors the exclusion list in mpv-winui-lazy/deploy-config.ps1: pure
        // runtime data that must never be created, copied or removed.
        private static readonly string[] ExcludedDirs = { "cache", "_cache", "watch_later", "gpu_cache", "icc_cache" };

        // Never auto-updated once present: files the user edits (mpv.conf,
        // input.conf, the menu overrides) and runtime state.
        private static readonly string[] UserOwnedFiles =
        {
            "mpv.conf", "input.conf", "menus.json", "custom_menu.json",
            "saved-props.json", "recent.json", "deploy-config.ps1",
        };

        // script-opts/*.conf are rewritten by the settings window
        // (hdr_auto.conf, coverart.conf, mpvw_hdr_override.conf, ...), so
        // mirroring the bundle over them would reset plugin settings on every
        // launch. Missing files are still created.
        private static readonly string[] UserOwnedDirs = { "script-opts" };

        public static Task EnsureDeployedAsync(string targetMpvConfigDir)
        {
            try
            {
                return Task.Run(() => Sync(targetMpvConfigDir));
            }
            catch (Exception ex)
            {
                // A failed deploy must not block playback; the app keeps running
                // with whatever config dir is on disk (mpv falls back to defaults).
                _logger.Warn(ex, "config layer sync failed, dir={}", targetMpvConfigDir);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Synchronous variant used from AppContext.Init: the queued config
        /// writers (PluginConfigWriter, ManagedMpvConfig) start right after Init
        /// and merge into mpv.conf, so the layer must be on disk first.
        /// </summary>
        public static void EnsureDeployed(string targetMpvConfigDir)
        {
            try
            {
                Sync(targetMpvConfigDir);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "config layer sync failed, dir={}", targetMpvConfigDir);
            }
        }

        private static void Sync(string targetDir)
        {
            var sourceDir = Path.Combine(System.AppContext.BaseDirectory, "mpv-winui-lazy");
            if (!Directory.Exists(sourceDir))
            {
                // Developer checkout / source run: no bundled layer, nothing to do.
                return;
            }

            var firstRun = !File.Exists(Path.Combine(targetDir, "mpv.conf"));
            var manifestPath = Path.Combine(targetDir, ManifestName);
            var manifestExisted = File.Exists(manifestPath);
            var deployed = ReadManifest(manifestPath);

            Directory.CreateDirectory(targetDir);

            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, dir);
                if (IsExcludedDir(rel))
                {
                    continue;
                }
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var updated = 0;
            var added = 0;

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, file);
                var parts = rel.Split(Path.DirectorySeparatorChar);
                if (IsExcludedDir(parts[0]))
                {
                    continue;
                }

                var name = Path.GetFileName(file);
                if (file.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || name.Contains(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    // Backups (including the ones PreserveBackup writes next to
                    // a replaced file) are never part of the layer.
                    continue;
                }

                var target = Path.Combine(targetDir, rel);
                var userOwned = Array.IndexOf(UserOwnedDirs, parts[0]) >= 0
                    || Array.IndexOf(UserOwnedFiles, name) >= 0;

                try
                {
                    if (!File.Exists(target))
                    {
                        File.Copy(file, target, overwrite: false);
                        current[rel] = HashFile(target);
                        added++;
                        continue;
                    }

                    if (userOwned)
                    {
                        // Exists and is the user's: leave it exactly as it is and
                        // do not claim it as ours.
                        continue;
                    }

                    var sourceHash = HashFile(file);
                    var targetHash = HashFile(target);
                    if (string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
                    {
                        current[rel] = sourceHash;
                        continue;
                    }

                    // The copy differs from the bundle. Refresh it only when we
                    // can still recognise it as ours (untouched since the last
                    // deploy), or when there is no manifest yet - a legacy
                    // install, where every layer file came from an older build.
                    var stillOurs = deployed.TryGetValue(rel, out var deployedHash)
                        && string.Equals(deployedHash, targetHash, StringComparison.OrdinalIgnoreCase);

                    if (!stillOurs && manifestExisted)
                    {
                        _logger.Warn("config layer file kept as user-modified, not updated: {}", rel);
                        continue;
                    }

                    if (!stillOurs)
                    {
                        // Legacy install: the local copy is a stale layer file
                        // rather than an intentional edit. Preserve it anyway.
                        PreserveBackup(target, rel);
                    }

                    File.Copy(file, target, overwrite: true);
                    current[rel] = sourceHash;
                    updated++;
                }
                catch (IOException ex)
                {
                    // A single locked/unwritable file must not abort the sync.
                    _logger.Warn(ex, "config layer file skipped: {}", rel);
                }
            }

            WriteManifest(manifestPath, current);

            if (firstRun)
            {
                _logger.Info("mpv config layer deployed to {} ({} files)", targetDir, added);
            }
            else if (updated > 0 || added > 0)
            {
                _logger.Info("mpv config layer synced to {} ({} updated, {} added)", targetDir, updated, added);
            }
        }

        private static bool IsExcludedDir(string rel)
        {
            if (rel.Length == 0)
            {
                return false;
            }
            var first = rel.Split(Path.DirectorySeparatorChar)[0];
            if (Array.IndexOf(ExcludedDirs, first) >= 0)
            {
                return true;
            }
            return rel.StartsWith("cache" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static void PreserveBackup(string target, string rel)
        {
            try
            {
                var backup = target + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                if (!File.Exists(backup))
                {
                    File.Copy(target, backup, overwrite: false);
                    _logger.Info("config layer file replaced, previous copy kept at {}", Path.GetFileName(backup));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "could not back up {}", rel);
            }
        }

        private static string HashFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        // Line format: <sha256-hex>\t<relative path with '/'>. A plain text
        // manifest keeps this dependency-free (System.Text.Json needs a source
        // generator under AOT) and easy to inspect when a sync misbehaves.
        private static Dictionary<string, string> ReadManifest(string manifestPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(manifestPath))
                {
                    return result;
                }

                foreach (var line in File.ReadAllLines(manifestPath))
                {
                    var tab = line.IndexOf('\t');
                    if (tab <= 0 || tab == line.Length - 1)
                    {
                        continue;
                    }
                    var hash = line[..tab];
                    var rel = line[(tab + 1)..].Replace('/', Path.DirectorySeparatorChar);
                    result[rel] = hash;
                }
            }
            catch (Exception ex)
            {
                // A damaged manifest must not break startup; treating it as
                // absent is the conservative reading (nothing is "ours").
                _logger.Warn(ex, "config layer manifest unreadable, treating as absent");
                result.Clear();
            }

            return result;
        }

        private static void WriteManifest(string manifestPath, Dictionary<string, string> entries)
        {
            try
            {
                var lines = new List<string>(entries.Count);
                foreach (var pair in entries)
                {
                    lines.Add(pair.Value + "\t" + pair.Key.Replace(Path.DirectorySeparatorChar, '/'));
                }
                lines.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(manifestPath, lines);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "config layer manifest write failed");
            }
        }
    }
}
