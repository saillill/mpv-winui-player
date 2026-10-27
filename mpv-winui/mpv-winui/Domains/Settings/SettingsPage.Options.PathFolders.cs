using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Lexicon.Language;
using mpv_winui.Domains.Playback;
using mpv_winui.Domains.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Domains.Settings;

public sealed partial class SettingsPage
{
    private List<Option> BuildPathFoldersOptions()
    {
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Path folders =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.WatchLaterDir),
                Label = lang.SettingsWatchLaterDir,
                Category = playback,
                Description = lang.SettingsHelpWatchLaterDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "_cache", "watch_later")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.WatchLaterDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.WatchLaterDir), AppContext.AppSetting.WatchLaterDir = (string)v!)
            },

            // ===== Library folders =====
            //
            // One row per library. Each bound folder is a checked item, and unticking it
            // is the unbind - which is what makes this the missing half of the bind
            // button on the library page. Nothing in the app could remove a bound folder
            // before this row existed; the only way back was a full settings reset.
            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoLibraryFolders),
                Label = lang.SettingsVideoLibraryFolders,
                Category = video,
                Description = lang.SettingsHelpVideoLibraryFolders,
                Type = OptionType.CheckList,

                // Read at build time so a folder bound on the library page shows up here
                // without restarting the app.
                CheckItemsProvider = () => AppContext.AppSetting
                    .GetVideoLibraryFolders()
                    .Select(folder => new OptionCheckItem(
                        folder,
                        folder,
                        isChecked: true))
                    .ToList(),

                // Unticking is the only write. It goes through the settings API rather
                // than through a string join, so the dedupe and the case-insensitive
                // comparison that AddLibraryFolder uses apply here too.
                CheckChanged = (_, value, isChecked, _) =>
                {
                    if (isChecked)
                    {
                        return;
                    }

                    if (AppContext.AppSetting.RemoveLibraryFolder(music: false, value))
                    {
                        // Announced explicitly. A normal option row gets this from
                        // ApplyMpv, but this write goes through the folder API, so
                        // without the line here an open video library would keep showing
                        // the removed folder's files until it was re-entered.
                        AppContext.NotifySettingChanged(
                            nameof(AppContext.AppSetting.VideoLibraryFolders),
                            AppContext.AppSetting.VideoLibraryFolders);
                    }
                },

                // The generic machinery (search, reset, layout persistence) needs an
                // answer for this row. It reads the live list; the Setter is never the
                // path that removes a folder, because CheckChanged already did.
                Getter = () => AppContext.AppSetting.VideoLibraryFolders,
                Setter = v => AppContext.AppSetting.VideoLibraryFolders = (string)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MusicLibraryFolders),
                Label = lang.SettingsMusicLibraryFolders,
                Category = video,
                Description = lang.SettingsHelpMusicLibraryFolders,
                Type = OptionType.CheckList,
                CheckItemsProvider = () => AppContext.AppSetting
                    .GetMusicLibraryFolders()
                    .Select(folder => new OptionCheckItem(
                        folder,
                        folder,
                        isChecked: true))
                    .ToList(),
                CheckChanged = (_, value, isChecked, _) =>
                {
                    if (isChecked)
                    {
                        return;
                    }

                    if (AppContext.AppSetting.RemoveLibraryFolder(music: true, value))
                    {
                        AppContext.NotifySettingChanged(
                            nameof(AppContext.AppSetting.MusicLibraryFolders),
                            AppContext.AppSetting.MusicLibraryFolders);
                    }
                },
                Getter = () => AppContext.AppSetting.MusicLibraryFolders,
                Setter = v => AppContext.AppSetting.MusicLibraryFolders = (string)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCacheDir),
                Label = lang.SettingsGpuShaderCacheDir,
                Category = video,
                Description = lang.SettingsHelpGpuShaderCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "_cache", "shader")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.GpuShaderCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCacheDir), AppContext.AppSetting.GpuShaderCacheDir = (string)v!)
            },

        ];
    }
}
