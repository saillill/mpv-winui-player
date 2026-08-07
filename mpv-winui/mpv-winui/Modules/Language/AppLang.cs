using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace mpv_winui.Modules.Language
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public partial class AppLang
    {
        public string AppName { get; } = "mpv winui";
        public string AppVersion { get; } = "1.0";
        public string About { get; set; } = "About";
        public string Add { get; set; } = "Add";
        public string Cancel { get; set; } = "Cancel";
        public string Help { get; set; } = "Help";
        public string Off { get; set; } = "Off";
        public string Ok { get; set; } = "OK";
        public string Paste { get; set; } = "Paste";
        public string Refresh { get; set; } = "Refresh";
        public string Confirm { get; set; } = "Confirm";
        public string AppHelpAndFeedBack { get; set; } = "Feedback";
        public string AppHelpAndFeedBackLink { get; set; } = "Feedback (Link)";
        public string AppSetting { get; set; } = "Settings";
        public string AppSettingTheme { get; set; } = "Theme";
        public string AppSettingStyle { get; set; } = "Appearance";
        public string ThemeDarkName { get; set; } = "Dark";
        public string ThemeLightName { get; set; } = "Light";
        public string Save { get; set; } = "Save";
        public string Test { get; set; } = "Test";
        public string Open { get; set; } = "Open";
        public string Reset { get; set; } = "Reset";
        public string Privacy { get; set; } = "Privacy";
        public string Download { get; set; } = "Download";
        public string Import { get; set; } = "Import";
        public string Export { get; set; } = "Export";
        public string Upload { get; set; } = "Upload";
        public string EnableMica { get; set; } = "Mica background";
        public string EnableUISound { get; set; } = "Enable UI Sound";
        public string Play { get; set; } = "Play";
        public string Stop { get; set; } = "Stop";
        public string Version { get; set; } = "Version";
        public string ClearTempFolder { get; set; } = "Delete temp files";
        public string ThemeAuto { get; set; } = "Auto";
        public string SettingLanguagesGroup { get; set; } = "Languages";
        public string SettingLanguages { get; set; } = "App language";
        public string SettingLanguagesDescription { get; set; } = "Restart required";
        public string SettingLanguagesHelp { get; set; } = "Help";
        public string SettingLanguagesShare { get; set; } = "Share or download languages";
        public string SettingLanguagesReloadTip { get; set; } = "Reload custom languages";
        public string SettingLanguagesExportTip { get; set; } = "Export current language";
        public string SettingLanguagesImportTip { get; set; } = "Import language";
        public string SettingLanguagesFolderOpenTip { get; set; } = "Open languages folder";
        public string Subtitles { get; set; } = "Subtitles";
        public string AudioTracks { get; set; } = "Audio Tracks";
        public string VideoTracks { get; set; } = "Video Tracks";
        public string SecondSubtitle { get; set; } = "Secondary Subtitle";

        // Right-click / menu bar strings (localized via JSON)
        public string File { get; set; } = "File";
        public string OpenFile { get; set; } = "Open File";
        public string OpenFolder { get; set; } = "Open Folder";
        public string OpenUrl { get; set; } = "Open URL";
        public string OpenFromClipboard { get; set; } = "Open from Clipboard";
        public string OpenWatchHistory { get; set; } = "Open Watch History";
        public string OpenWatchLater { get; set; } = "Open Watch Later";
        public string Playlist { get; set; } = "Playlist";
        public string Window { get; set; } = "Window";
        public string TogglePlaylist { get; set; } = "Toggle Playlist";
        public string ToggleFullScreen { get; set; } = "Toggle Full Screen";
        public string ToggleFullWindow { get; set; } = "Toggle Full Window";
        public string Quit { get; set; } = "Quit";
        public string Backdrop { get; set; } = "Backdrop";
        public string DebugLog { get; set; } = "Debug Log";
        public string SettingsTitle { get; set; } = "Settings";
        public string FileLoadSubtitle { get; set; } = "Add Subtitle";
        public string FileOpen { get; set; } = "Open File";
        public string FileOpenBd { get; set; } = "Open Blu-ray";
        public string FileOpenClipboard { get; set; } = "Open from Clipboard";
        public string FileOpenDvd { get; set; } = "Open DVD";
        public string FileOpenFolder { get; set; } = "Open Folder";
        public string FileOpenUrl { get; set; } = "Open URL";
        public string FileOpenWatchHistory { get; set; } = "Open Watch History";
        public string FileOpenWatchLater { get; set; } = "Open Watch Later";
        public string FileQuit { get; set; } = "Quit";
        public string FileRestart { get; set; } = "Restart";
        public string FileScreenshot { get; set; } = "Screenshot";
        public string FileScreenshotNoSub { get; set; } = "Screenshot (No Subtitles)";
        public string HelpAbout { get; set; } = "About";
        public string MenuFile { get; set; } = "File";
        public string MenuHelp { get; set; } = "Help";
        public string MenuView { get; set; } = "View";
        public string MoreFullScreen { get; set; } = "Full Screen";
        public string MoreFullWindow { get; set; } = "Full Window";
        public string MoreNextTrack { get; set; } = "Next Track";
        public string MorePlaybackRate { get; set; } = "Playback Rate";
        public string MorePreviousTrack { get; set; } = "Previous Track";
        public string MoreRepeat { get; set; } = "Repeat";
        public string MoreShuffle { get; set; } = "Shuffle";
        public string MoreSkipBackward { get; set; } = "Skip Backward";
        public string MoreSkipForward { get; set; } = "Skip Forward";
        public string MoreZoom { get; set; } = "Zoom";
        public string MoreZoomAuto { get; set; } = "Auto";
        public string PlaylistCopyPath { get; set; } = "Copy File Path";
        public string PlaylistCopyTitle { get; set; } = "Copy Title";
        public string PlaylistMoveBottom { get; set; } = "Move to Bottom";
        public string PlaylistMoveDown { get; set; } = "Move Down";
        public string PlaylistMoveTop { get; set; } = "Move to Top";
        public string PlaylistMoveUp { get; set; } = "Move Up";
        public string PlaylistOpenLocation { get; set; } = "Open File Location";
        public string PlaylistPlay { get; set; } = "Play";
        public string PlaylistRemove { get; set; } = "Remove";
        public string ViewConfFolder { get; set; } = "Open Conf Folder";
        public string ViewFullScreen { get; set; } = "Full Screen";
        public string ViewFullWindow { get; set; } = "Full Window";
        public string ViewMpvFolder { get; set; } = "Open mpv Folder";
        public string ViewOptions { get; set; } = "Options";
        public string ViewPlaylist { get; set; } = "Playlist";

        /// <summary>Loads string values from a JSON file ({ PropertyName: "value" }). Missing keys keep defaults.</summary>
        public void LoadFromJson(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return;
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.String) continue;
                    var p = GetType().GetProperty(prop.Name);
                    if (p is { CanWrite: true })
                    {
                        p.SetValue(this, prop.Value.GetString());
                    }
                }
            }
            catch
            {
                // A broken language file falls back to defaults.
            }
        }
    }
}
