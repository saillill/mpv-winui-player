using mpv_winui.Platform.AppModel;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using ApplicationData = Microsoft.Windows.Storage.ApplicationData;

namespace mpv_winui.Platform.FileSystem
{
    public class AppData
    {
        private static readonly Lazy<AppData> _lazy = new(() => new AppData(), true);

        public static AppData Current => _lazy.Value;

        // ApplicationData identity. The upstream merge briefly adopted the
        // upstream values ("ikas-mc" / "mpvw"), which silently moved the
        // settings registry container AND the local-data folder (logs,
        // caches). Installs created by this fork keep both under
        // "mpv-winui", and so do the helper components that read those
        // paths, so keep the fork identity here.
        public const string AppDataId = "mpv-winui";

        public const string AppDataPublisher = "mpv-winui";

        /// <summary>Name of the mpv config folder inside the local-data root.</summary>
        public const string MpvConfigFolderName = "mpv";

        /// <summary>
        /// The mpv config directory, as a plain path: the folder mpv is given as
        /// <c>config-dir</c>, and therefore the only folder the settings window may
        /// write into.
        ///
        /// <para><b>One definition, because two existed and they disagreed.</b>
        /// <see cref="OpenOrCreateLocalDataFolderAsync"/> resolves under
        /// <see cref="EnsureLocalFolder"/>, i.e. under the ApplicationData root that
        /// <c>GetForUnpackaged(</c><see cref="AppDataPublisher"/>,
        /// <see cref="AppDataId"/><c>)</c> produces. With both identity strings being
        /// "mpv-winui" that root is <c>%LOCALAPPDATA%\mpv-winui\mpv-winui</c>.
        /// Six other call sites built the path by hand as
        /// <c>Path.Combine(LOCALAPPDATA, "mpv-winui", "mpv", …)</c> - one directory
        /// short - so the settings window wrote mpv.conf, its managed block, the
        /// state file and script-opts into a directory mpv never opened. Measured on
        /// a live install: the hand-built directory held a 719-line mpv.conf with
        /// both marker blocks plus a mpv-winui-managed.json; the directory mpv used
        /// as config-dir held a 294-line mpv.conf with neither. Every value a
        /// setting persisted was invisible to the player.</para>
        ///
        /// <para><b>Why this lives here.</b> The value has to agree with
        /// <see cref="EnsureLocalFolder"/> by construction, not by two lists of
        /// constants happening to match. That method returns either the packaged or
        /// the unpackaged root depending on <c>PackageHelper.IsPackaged</c>, so the
        /// only safe derivation is the one it already resolved.</para>
        /// </summary>
        public string MpvConfigDirectory => ResolveLocalData(MpvConfigFolderName);

        private static readonly Lazy<StorageFolder> _localFolder = new(EnsureLocalFolder, true);

        public string ResolveLocalData(string path)
        {
            return Path.Combine(_localFolder.Value.Path, path);
        }

        public async Task<StorageFolder> OpenOrCreateLocalDataFolderAsync(string path)
        {
            return await _localFolder.Value.CreateFolderAsync(path, CreationCollisionOption.OpenIfExists);
        }

        public Task<StorageFolder> OpenLocalDataFolderAsync()
        {
            return Task.FromResult(_localFolder.Value);
        }

        private static StorageFolder EnsureLocalFolder()
        {
            if (PackageHelper.IsPackaged)
            {
                return ApplicationData.GetDefault().LocalFolder;
            }
            else
            {
                //https://learn.microsoft.com/zh-cn/windows/windows-app-sdk/api/winrt/microsoft.windows.storage.applicationdata.getforunpackaged
                var applicationData = ApplicationData.GetForUnpackaged(AppDataPublisher, AppDataId);
                Directory.CreateDirectory(applicationData.LocalPath);
                return applicationData.LocalFolder;
            }
        }
    }
}
