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
    ///    as "&lt;name&gt;.bak-&lt;timestamp&gt;", so a local edit is recoverable;
    ///  - a file this class deployed and that the bundle no longer ships is
    ///    removed again (see <see cref="PruneRemoved"/>), so a layout change -
    ///    a file moving into a subdirectory, say - reaches existing installs
    ///    instead of leaving the old copy behind forever.
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

            var pruned = PruneRemoved(targetDir, deployed, current);
            WriteManifest(manifestPath, current);

            if (firstRun)
            {
                _logger.Info("mpv config layer deployed to {} ({} files)", targetDir, added);
            }
            else if (updated > 0 || added > 0 || pruned > 0)
            {
                _logger.Info("mpv config layer synced to {} ({} updated, {} added, {} removed)",
                    targetDir, updated, added, pruned);
            }
        }

        /// <summary>
        /// Removes files this class deployed earlier that the bundle no longer
        /// ships - the other half of a layout change. Adding and updating alone
        /// would leave the old copy in place forever, so moving a file into a
        /// subdirectory would silently double it on every existing install
        /// (a 9 MB tool binary is the obvious case).
        ///
        /// Only "ours and untouched" is ever deleted: the path must be in the
        /// previous manifest, the file must still hash to what was deployed, and
        /// user-owned names and directories are skipped outright. A file whose
        /// contents changed is somebody's edit and is kept - and, like the
        /// update path, is then no longer claimed as ours.
        ///
        /// A legacy install with no manifest cannot be pruned, because there is
        /// no record of which files were ours; that is the conservative reading
        /// and matches how the update path treats the same situation.
        /// </summary>
        private static int PruneRemoved(
            string targetDir,
            Dictionary<string, string> deployed,
            Dictionary<string, string> current)
        {
            var pruned = 0;
            foreach (var (rel, deployedHash) in deployed)
            {
                if (current.ContainsKey(rel))
                {
                    continue;   // still part of the bundle
                }

                // Same exclusions as the copy path: runtime state must never be
                // created, copied or removed.
                if (IsExcludedDir(rel))
                {
                    continue;
                }

                var parts = rel.Split(Path.DirectorySeparatorChar);
                var userOwned = Array.IndexOf(UserOwnedDirs, parts[0]) >= 0
                    || Array.IndexOf(UserOwnedFiles, Path.GetFileName(rel)) >= 0;
                if (userOwned)
                {
                    continue;
                }

                var target = Path.Combine(targetDir, rel);
                try
                {
                    if (!File.Exists(target))
                    {
                        continue;
                    }

                    if (!string.Equals(HashFile(target), deployedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Info("config layer file no longer shipped, kept as user-modified: {}", rel);
                        continue;
                    }

                    File.Delete(target);
                    RemoveEmptyParentDirs(targetDir, target);
                    _logger.Info("config layer file removed, no longer shipped: {}", rel);
                    pruned++;
                }
                catch (Exception ex)
                {
                    // A locked file must not abort the sync, same as the copy path.
                    _logger.Warn(ex, "config layer file could not be removed: {}", rel);
                }
            }

            return pruned;
        }

        /// <summary>
        /// Deletes directories this prune just emptied, and no others:
        /// <see cref="Directory.Delete(string, bool)"/> with recursive=false
        /// throws while a directory still has content, which is the guard.
        /// Stops at the target root so the config directory itself survives.
        /// </summary>
        private static void RemoveEmptyParentDirs(string targetDir, string filePath)
        {
            var root = targetDir.TrimEnd(Path.DirectorySeparatorChar);
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir) && dir.Length > root.Length)
            {
                try
                {
                    Directory.Delete(dir, recursive: false);
                }
                catch (IOException)
                {
                    return;   // still has content, or still in use
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                dir = Path.GetDirectoryName(dir);
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
