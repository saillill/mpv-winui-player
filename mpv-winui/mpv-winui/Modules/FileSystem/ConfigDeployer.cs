using NLog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace mpv_winui.Modules.FileSystem
{
    /// <summary>
    /// First-run deployment of the mpv config layer. The packaged app ships the
    /// bundled config (mpv-winui-lazy) next to the exe; on first launch we copy
    /// it into %LOCALAPPDATA%\mpv-winui\mpv so the player works without a manual
    /// deploy-config.ps1 step (portable zip and MSI alike).
    /// The deployment only happens when the target has no mpv.conf yet, and it
    /// never overwrites files the user has changed afterwards.
    /// </summary>
    public static class ConfigDeployer
    {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(ConfigDeployer));

        // Mirrors the exclusion list in mpv-winui-lazy/deploy-config.ps1 so the
        // runtime data dirs and user overrides are never clobbered.
        private static readonly string[] ExcludedDirs = { "cache", "_cache", "watch_later", "gpu_cache", "icc_cache" };
        private static readonly string[] ExcludedFiles = { "saved-props.json", "recent.json", "menus.json", "deploy-config.ps1" };

        public static Task EnsureDeployedAsync(string targetMpvConfigDir)
        {
            try
            {
                var sourceDir = Path.Combine(System.AppContext.BaseDirectory, "mpv-winui-lazy");
                if (!Directory.Exists(sourceDir))
                {
                    // Developer checkout / source run: no bundled layer, nothing to do.
                    return Task.CompletedTask;
                }

                var mpvConf = Path.Combine(targetMpvConfigDir, "mpv.conf");
                if (File.Exists(mpvConf))
                {
                    // Already initialized (first run done, or the user deployed
                    // manually); never overwrite their config.
                    return Task.CompletedTask;
                }

                return Task.Run(() =>
                {
                    CopyConfigLayer(sourceDir, targetMpvConfigDir);
                    _logger.Info("mpv config layer deployed to {}", targetMpvConfigDir);
                });
            }
            catch (Exception ex)
            {
                // A failed deploy must not block playback; the app keeps running
                // with an empty config dir (mpv falls back to defaults).
                _logger.Warn(ex, "first-run config deployment failed, dir={}", targetMpvConfigDir);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Synchronous variant used from AppContext.Init: the queued config
        /// writers (PluginConfigWriter, ManagedMpvConfig) start right after
        /// Init and merge into mpv.conf, so the layer must be on disk first.
        /// No-op (fast) once mpv.conf exists.
        /// </summary>
        public static void EnsureDeployed(string targetMpvConfigDir)
        {
            try
            {
                var sourceDir = Path.Combine(System.AppContext.BaseDirectory, "mpv-winui-lazy");
                if (!Directory.Exists(sourceDir))
                {
                    return;
                }

                var mpvConf = Path.Combine(targetMpvConfigDir, "mpv.conf");
                if (File.Exists(mpvConf))
                {
                    return;
                }

                CopyConfigLayer(sourceDir, targetMpvConfigDir);
                _logger.Info("mpv config layer deployed to {}", targetMpvConfigDir);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "first-run config deployment failed, dir={}", targetMpvConfigDir);
            }
        }

        private static void CopyConfigLayer(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, dir);
                if (Array.IndexOf(ExcludedDirs, rel) >= 0 || rel.StartsWith("cache" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, file);
                var parts = rel.Split(Path.DirectorySeparatorChar);
                if (Array.IndexOf(ExcludedDirs, parts[0]) >= 0)
                {
                    continue;
                }
                if (Array.IndexOf(ExcludedFiles, Path.GetFileName(file)) >= 0
                    || file.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    File.Copy(file, Path.Combine(targetDir, rel), overwrite: false);
                }
                catch (IOException)
                {
                    // A single pre-existing file (e.g. a script-opts conf the
                    // app writer created earlier) must not abort the deploy.
                }
            }
        }
    }
}
