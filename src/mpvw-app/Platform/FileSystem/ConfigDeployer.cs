using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using mpv_winui.Infrastructure.Common.Utils;

namespace mpv_winui.Platform.FileSystem
{
    /// <summary>
    /// Deployment of the bundled mpv config layer (data) into
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

        /// <summary>
        /// Written once, after the legacy config directory has been folded in, so the
        /// migration never runs twice.
        /// </summary>
        private const string MigrationStampName = "mpv-winui-migrated.stamp";

        // Mirrors the exclusion list in data/deploy-config.ps1: pure
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

        /// <summary>
        /// The directory older builds wrote into, and the one this build must move
        /// out of before it writes a single config file.
        ///
        /// <para><b>Why a migration and not just a fix.</b> Six call sites used to
        /// build the config path as
        /// <c>Path.Combine(LOCALAPPDATA, "mpv-winui", "mpv", …)</c> while mpv was
        /// given <c>config-dir</c> = <c>&lt;AppData root&gt;\mpv</c>, whose root is
        /// <c>%LOCALAPPDATA%\mpv-winui\mpv-winui</c>. Correcting the writers alone
        /// would leave an existing install's mpv.conf - the settings window's single
        /// source of truth, including every hand edit - stranded in a directory
        /// nothing reads any more. It is the user's file; it has to travel.</para>
        ///
        /// <para><b>What is deliberately not moved.</b> Runtime state that mpv or the
        /// deployer regenerates, and directories the layer excludes
        /// (<see cref="ExcludedDirs"/>). Moving a cache means copying gigabytes to
        /// have it rebuilt on the next launch.</para>
        ///
        /// <para><b>Overwrite policy depends on which side holds the user's data, and
        /// that is not the same rule for every file.</b> An install that hit the split
        /// has two copies of several files, and which one is authoritative differs:</para>
        ///
        /// <list type="bullet">
        /// <item><b><see cref="UserOwnedFiles"/> such as mpv.conf, input.conf and
        /// menus.json</b> - the legacy copy is the one the settings window was writing
        /// and the one the user edited. The destination copy is whatever
        /// <c>ConfigDeployer</c> put there from the bundle, untouched. The legacy copy
        /// therefore <b>overwrites</b>, after the destination has been preserved as
        /// <c>.bak-premigration</c>. Measured on an install that hit the split: legacy
        /// mpv.conf was 719 lines with both marker blocks, destination was the pristine
        /// 294-line bundle with neither. Keeping the destination would silently discard
        /// every persisted setting and every hand edit.</item>
        /// <item><b>Everything else</b> - layer files the bundle ships. The destination
        /// is deployed from the same bundle and is at least as new, so it wins and the
        /// legacy copy is left alone.</item>
        /// </list>
        /// </summary>
        private static void MigrateLegacyConfigDirectory(string targetDir)
        {
            string legacy;
            try
            {
                legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mpv-winui", "mpv");
            }
            catch
            {
                return;
            }

            // Normalise before comparing: the two paths are equal on any install
            // that never had the split, and comparing raw strings would then move
            // files onto themselves.
            static string Norm(string p) => Path.GetFullPath(p)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!Directory.Exists(legacy)
                || string.Equals(Norm(legacy), Norm(targetDir), StringComparison.OrdinalIgnoreCase)
                || File.Exists(Path.Combine(targetDir, MigrationStampName)))
            {
                return;
            }

            var moved = 0;
            var replaced = 0;
            var kept = 0;
            try
            {
                foreach (var source in Directory.EnumerateFiles(legacy, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(legacy, source);
                    var parts = rel.Split(Path.DirectorySeparatorChar);
                    if (IsExcludedDir(parts[0]))
                    {
                        continue;
                    }

                    var dest = Path.Combine(targetDir, rel);
                    var isUserOwned = Array.IndexOf(UserOwnedFiles, Path.GetFileName(source)) >= 0
                        || Array.IndexOf(UserOwnedDirs, parts[0]) >= 0;
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                        if (!File.Exists(dest))
                        {
                            File.Move(source, dest);
                            moved++;
                            continue;
                        }

                        if (!isUserOwned)
                        {
                            // The destination came from the same bundle and is
                            // therefore as new or newer. Leave the legacy copy.
                            kept++;
                            continue;
                        }

                        // The legacy copy is the authoritative one. Preserve what is
                        // at the destination first, then let the legacy file win.
                        var preserved = dest + ".bak-premigration";
                        if (!File.Exists(preserved))
                        {
                            File.Copy(dest, preserved, overwrite: false);
                        }
                        File.Copy(source, dest, overwrite: true);
                        replaced++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "legacy config file could not be migrated: {}", rel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "legacy config directory could not be scanned");
            }

            if (moved > 0 || replaced > 0 || kept > 0)
            {
                // Info, not Debug: this is the one line that explains why a
                // user's settings reappeared after an upgrade.
                _logger.Info(
                    "legacy config migration from {}: {} moved, {} user-owned replaced"
                    + " (destination preserved as *.bak-premigration), {} left alone",
                    legacy, moved, replaced, kept);
            }

            // Mark the migration done. Without this the check re-ran on every
            // launch and re-copied the same 13 user files each time: the legacy
            // directory is only emptied of what was MOVED, and a user-owned file is
            // copied rather than moved so the old copy survives as a fallback. That
            // is harmless for correctness but it made every launch rewrite mpv.conf
            // from a weeks-old source, which is not harmless at all -- a later edit
            // made in the settings window would be reverted by the next startup.
            try
            {
                var stamp = Path.Combine(targetDir, MigrationStampName);
                if (!File.Exists(stamp))
                {
                    AtomicFile.WriteAllText(
                        stamp,
                        $"migrated from {legacy} on {DateTime.UtcNow:O}{Environment.NewLine}",
                        new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "could not write the legacy-migration stamp");
            }
        }

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
            MigrateLegacyConfigDirectory(targetDir);

            var sourceDir = Path.Combine(System.AppContext.BaseDirectory, "data");
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

                // Atomic: the manifest is the record of what this layer deployed,
                // so a torn write would leave the next launch's diff against a
                // half-written list and it could redeploy or skip files.
                AtomicFile.WriteAllLines(manifestPath, lines);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "config layer manifest write failed");
            }
        }
    }
}
