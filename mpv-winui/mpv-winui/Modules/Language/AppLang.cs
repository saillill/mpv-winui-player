using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace mpv_winui.Modules.Language
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public partial class AppLang
    {
        public string AppName { get; } = "MPV";
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
        public string SettingsHwdec { get; set; } = "Hardware decoding";
        public string SettingsVolumeMax { get; set; } = "Max volume (%)";
        public string SettingsKeepOpen { get; set; } = "After playback ends";
        public string SettingsLoopFile { get; set; } = "Loop current file";
        public string SettingsDeinterlace { get; set; } = "Deinterlace";
        public string SettingsAspect { get; set; } = "Aspect ratio";
        public string SettingsSubFontSize { get; set; } = "Subtitle font size";
        public string SettingsSubDelay { get; set; } = "Subtitle delay (s)";
        public string SettingsVideoPreview { get; set; } = "Video preview thumbnails";
        public string SettingsCategoryGeneral { get; set; } = "General";
        public string SettingsCategoryPlayback { get; set; } = "Playback";
        public string SettingsCategoryVideo { get; set; } = "Video";
        public string SettingsCategoryAudio { get; set; } = "Audio";
        public string SettingsCategorySubtitle { get; set; } = "Subtitle";
        public string SettingsCategoryPaths { get; set; } = "Paths";
        public string SettingsSpeed { get; set; } = "Default speed";
        public string SettingsSubPos { get; set; } = "Subtitle position (%)";
        public string SettingsAudioLanguage { get; set; } = "Preferred audio language";
        public string SettingsSubtitleLanguage { get; set; } = "Preferred subtitle language";
        public string SettingsAudioDevice { get; set; } = "Audio output device";
        public string SettingsScreenshotDirectory { get; set; } = "Screenshot folder";
        public string SettingsScreenshotTemplate { get; set; } = "Screenshot filename template";
        public string SettingsCacheDir { get; set; } = "Cache folder";
        public string RestartRequiredTitle { get; set; } = "Restart required";
        public string RestartRequiredMessage { get; set; } = "{0}: this setting takes effect after restart. Restart now?";
        public string RestartNow { get; set; } = "Restart";
        public string RestartLater { get; set; } = "Later";
        public string HelpMpvDocs { get; set; } = "mpv Official Manual";
        public string SettingsCategoryScreenshot { get; set; } = "Screenshot";
        public string SettingsSavePositionOnQuit { get; set; } = "Save playback position";
        public string SettingsScreenshotFormat { get; set; } = "Screenshot format";
        public string SettingsScreenshotJpegQuality { get; set; } = "Screenshot JPEG quality";
        public string SettingsVideoSync { get; set; } = "Video sync mode";
        public string SettingsInterpolation { get; set; } = "Frame interpolation";
        public string SettingsCorrectDownscaling { get; set; } = "Correct downscaling";
        public string SettingsAudioChannels { get; set; } = "Audio channels";
        public string SettingsAudioDelay { get; set; } = "Audio delay (s)";
        public string SettingsSubAssOverride { get; set; } = "ASS override mode";
        public string SettingsSubBlur { get; set; } = "Subtitle blur";
        public string SettingsCacheSecs { get; set; } = "Network cache (s)";

        // ===== Generic UI strings for settings controls =====
        public string Yes { get; set; } = "Yes";
        public string No { get; set; } = "No";
        public string Browse { get; set; } = "Browse...";

        // ===== Localized option values (raw mpv values stay machine-readable) =====
        public string OptionValueAuto { get; set; } = "Auto";
        public string OptionValueYes { get; set; } = "Yes";
        public string OptionValueNo { get; set; } = "No";
        public string OptionValueOn { get; set; } = "On";
        public string OptionValueOff { get; set; } = "Off";
        public string OptionValueAlways { get; set; } = "Always";
        public string OptionValueStereo { get; set; } = "Stereo";
        public string OptionValueMono { get; set; } = "Mono";
        public string OptionValueSurround51 { get; set; } = "5.1";
        public string OptionValueSurround71 { get; set; } = "7.1";
        public string OptionValueVideoSyncAudio { get; set; } = "Audio (default)";
        public string OptionValueVideoSyncDisplayResample { get; set; } = "Resample to display refresh";
        public string OptionValueVideoSyncDisplayResampleVdrop { get; set; } = "Resample + drop frames";
        public string OptionValueVideoSyncDisplayAdrop { get; set; } = "Drop audio frames";
        public string OptionValueVideoSyncCfr { get; set; } = "CFR (fixed frame rate)";
        public string OptionValueAssOverrideNo { get; set; } = "No (keep original styles)";
        public string OptionValueAssOverrideYes { get; set; } = "Yes (override styles)";
        public string OptionValueAssOverrideForce { get; set; } = "Force (full override)";
        public string OptionValueAssOverrideScale { get; set; } = "Scale (recommended)";
        public string OptionValueAssOverrideStrip { get; set; } = "Strip (remove styles)";
        public string OptionValueSubAutoNo { get; set; } = "No";
        public string OptionValueSubAutoExact { get; set; } = "Exact match";
        public string OptionValueSubAutoFuzzy { get; set; } = "Fuzzy match (recommended)";
        public string OptionValueSubAutoAll { get; set; } = "All";
        public string OptionValueAudioFileAutoNo { get; set; } = "No";
        public string OptionValueAudioFileAutoExact { get; set; } = "Exact match";
        public string OptionValueAudioFileAutoFuzzy { get; set; } = "Fuzzy match (recommended)";
        public string OptionValueAudioFileAutoAll { get; set; } = "All";
        public string OptionValueKeepOpenYes { get; set; } = "Keep window open (yes)";
        public string OptionValueKeepOpenNo { get; set; } = "Close when finished (no)";
        public string OptionValueKeepOpenAlways { get; set; } = "Always keep open";
        public string OptionValueDeinterlaceAuto { get; set; } = "Auto";
        public string OptionValueDeinterlaceYes { get; set; } = "Yes";
        public string OptionValueDeinterlaceNo { get; set; } = "No";
        public string OptionValueAspectAuto { get; set; } = "Auto";
        public string OptionValueChannelsAuto { get; set; } = "Auto";
        public string OptionValueBackdropAcrylic { get; set; } = "Acrylic";
        public string OptionValueBackdropMica { get; set; } = "Mica";
        public string OptionValueToneMapBt2390 { get; set; } = "BT.2390";
        public string OptionValueToneMapBt2446a { get; set; } = "BT.2446a";
        public string OptionValueToneMapMobius { get; set; } = "Möbius";
        public string OptionValueToneMapClip { get; set; } = "Clip";
        public string OptionValueToneMapReinhard { get; set; } = "Reinhard";
        public string OptionValueRotateNo { get; set; } = "No rotation";
        public string OptionValueRotate90 { get; set; } = "90° clockwise";
        public string OptionValueRotate180 { get; set; } = "180°";
        public string OptionValueRotate270 { get; set; } = "90° counterclockwise";
        public string OptionValueDitherAuto { get; set; } = "Auto";
        public string OptionValueDitherNo { get; set; } = "Off";
        public string OptionValueCacheAuto { get; set; } = "Auto";
        public string OptionValueLoopPlaylistNo { get; set; } = "No";
        public string OptionValueLoopPlaylistYes { get; set; } = "Loop all (yes)";
        public string OptionValueLoopPlaylistInf { get; set; } = "Loop forever (inf)";
        public string OptionValueLoopPlaylistForce { get; set; } = "Force loop (force)";
        public string OptionValueCodePageGb18030 { get; set; } = "GB18030 (Chinese)";
        public string OptionValueCodePageUtf8 { get; set; } = "UTF-8";
        public string OptionValueCodePageUtf16 { get; set; } = "UTF-16";
        public string OptionValueCodePageCp1252 { get; set; } = "CP1252 (Western)";
        public string OptionValueCodePageShiftJis { get; set; } = "Shift-JIS (Japanese)";
        public string OptionValueCodePageEucKr { get; set; } = "EUC-KR (Korean)";
        public string OptionValueCodePageCp1251 { get; set; } = "CP1251 (Cyrillic)";

        // ===== Additional option labels =====
        public string SettingsLoopPlaylist { get; set; } = "Loop playlist";
        public string SettingsVolume { get; set; } = "Startup volume";
        public string SettingsScale { get; set; } = "Upscaling algorithm";
        public string SettingsDScale { get; set; } = "Downscaling algorithm";
        public string SettingsVideoRotate { get; set; } = "Video rotation";
        public string SettingsDeband { get; set; } = "Deband";
        public string SettingsLinearDownscaling { get; set; } = "Linear downscaling";
        public string SettingsSigmoidUpscaling { get; set; } = "Sigmoid upscaling";
        public string SettingsToneMapping { get; set; } = "HDR tone mapping";
        public string SettingsDitherDepth { get; set; } = "Dither depth";
        public string SettingsHrSeek { get; set; } = "HR seek";
        public string SettingsAudioExclusive { get; set; } = "Exclusive audio mode";
        public string SettingsAudioPitchCorrection { get; set; } = "Audio pitch correction";
        public string SettingsAudioNormalizeDownmix { get; set; } = "Normalize downmix";
        public string SettingsAudioFileAuto { get; set; } = "Auto-load audio files";
        public string SettingsSubAuto { get; set; } = "Auto-load subtitles";
        public string SettingsSubFont { get; set; } = "Subtitle font";
        public string SettingsSubCodePage { get; set; } = "Subtitle codepage";
        public string SettingsSubOutlineSize { get; set; } = "Subtitle outline";
        public string SettingsSubShadowOffset { get; set; } = "Subtitle shadow";
        public string SettingsSubEmbeddedFonts { get; set; } = "Embedded fonts";
        public string SettingsSubUseMargins { get; set; } = "Subtitles in margins";
        public string SettingsScreenshotPngCompression { get; set; } = "PNG compression level";
        public string SettingsScreenshotWebpQuality { get; set; } = "WebP quality";
        public string SettingsScreenshotHighBitDepth { get; set; } = "High bit depth screenshots";
        public string SettingsScreenshotTagColorspace { get; set; } = "Tag screenshot colorspace";
        public string SettingsScreenshotTemplateDefault { get; set; } = "Default (mpv.conf)";
        public string SettingsScreenshotTemplateMpv { get; set; } = "MPV style (time + counter)";
        public string SettingsScreenshotTemplateFileTime { get; set; } = "Filename + time";
        public string SettingsScreenshotTemplateFileTimeCounter { get; set; } = "Filename + time + counter";
        public string SettingsScreenshotTemplateTimeCounter { get; set; } = "Time + counter";

        // ===== Help texts (shown behind the info button) =====
        public string SettingsHelpTheme { get; set; } = "Choose the color theme. Auto follows the Windows app mode.";
        public string SettingsHelpBackdrop { get; set; } = "Background material of the main window. Mica is lighter on GPU usage.";
        public string SettingsHelpDebugLog { get; set; } = "Write debug logs to %LOCALAPPDATA%\\mpv-winui\\logs. Useful when reporting issues.";
        public string SettingsHelpLanguage { get; set; } = "Language of the settings UI and right-click menus. Takes effect after restart.";
        public string SettingsHelpHwdec { get; set; } = "Hardware decoding API. Auto picks the best available; Off uses software decoding.";
        public string SettingsHelpVolumeMax { get; set; } = "Upper limit of the volume control (%). Values above 100 allow amplifier-style boost.";
        public string SettingsHelpKeepOpen { get; set; } = "What mpv does after the playlist finishes.";
        public string SettingsHelpLoopFile { get; set; } = "Repeat the current file endlessly.";
        public string SettingsHelpLoopPlaylist { get; set; } = "Repeat the whole playlist.";
        public string SettingsHelpSavePositionOnQuit { get; set; } = "Remember playback position and restore it next time.";
        public string SettingsHelpSpeed { get; set; } = "Default playback speed applied when starting playback.";
        public string SettingsHelpInterpolation { get; set; } = "Reduce judder by interpolating frames (only effective with display resample video sync).";
        public string SettingsHelpVideoPreview { get; set; } = "Show video preview thumbnails when hovering the progress bar.";
        public string SettingsHelpCacheSecs { get; set; } = "Maximum seconds of network data kept in memory. 0 disables the limit.";
        public string SettingsHelpVolume { get; set; } = "Volume applied when the player starts.";
        public string SettingsHelpDeinterlace { get; set; } = "Remove interlacing artifacts from interlaced content.";
        public string SettingsHelpAspect { get; set; } = "Override the displayed aspect ratio.";
        public string SettingsHelpVideoSync { get; set; } = "How playback is synchronized to the display refresh rate.";
        public string SettingsHelpCorrectDownscaling { get; set; } = "Preserve color accuracy when scaling down.";
        public string SettingsHelpScale { get; set; } = "Upscaling algorithm used when the video is larger than the window.";
        public string SettingsHelpDScale { get; set; } = "Downscaling algorithm used when the video is smaller than the window.";
        public string SettingsHelpLinearDownscaling { get; set; } = "Use linear light for downscaling to improve color accuracy.";
        public string SettingsHelpSigmoidUpscaling { get; set; } = "Apply a sigmoid curve before upscaling to reduce ringing.";
        public string SettingsHelpDeband { get; set; } = "Remove color banding. Not effective with some hardware decoding paths.";
        public string SettingsHelpVideoRotate { get; set; } = "Rotate the video output.";
        public string SettingsHelpToneMapping { get; set; } = "Algorithm used to map HDR content to SDR.";
        public string SettingsHelpDitherDepth { get; set; } = "Bit depth used for dithering when converting to lower bit depth.";
        public string SettingsHelpHrSeek { get; set; } = "High-resolution seeking. On can be slower but more accurate.";
        public string SettingsHelpAudioLanguage { get; set; } = "Preferred audio language codes (e.g. eng, chi, jpn). Empty means system default.";
        public string SettingsHelpAudioDevice { get; set; } = "Audio output device name returned by mpv (e.g. auto or wasapi/...).";
        public string SettingsHelpAudioChannels { get; set; } = "Channel layout forced on the audio output.";
        public string SettingsHelpAudioDelay { get; set; } = "Audio delay in seconds. Negative values make audio earlier.";
        public string SettingsHelpAudioExclusive { get; set; } = "WASAPI exclusive mode. Can reduce latency but blocks other apps from the device.";
        public string SettingsHelpAudioPitchCorrection { get; set; } = "Keep pitch when changing playback speed.";
        public string SettingsHelpAudioNormalizeDownmix { get; set; } = "Normalize volume when downmixing multi-channel audio to fewer channels.";
        public string SettingsHelpAudioFileAuto { get; set; } = "Automatically load external audio files with the same name as the video.";
        public string SettingsHelpSubFontSize { get; set; } = "Font size of text subtitles.";
        public string SettingsHelpSubDelay { get; set; } = "Subtitle delay in seconds.";
        public string SettingsHelpSubPos { get; set; } = "Vertical subtitle position (%).";
        public string SettingsHelpSubtitleLanguage { get; set; } = "Preferred subtitle language codes (e.g. chi, eng). Empty means system default.";
        public string SettingsHelpSubAssOverride { get; set; } = "How strongly ASS/SSA subtitle styles are overridden by the player.";
        public string SettingsHelpSubBlur { get; set; } = "Gaussian blur applied to subtitles (0 disables).";
        public string SettingsHelpSubAuto { get; set; } = "Automatically load external subtitle files with the same name as the video.";
        public string SettingsHelpSubFont { get; set; } = "Font used for text subtitles.";
        public string SettingsHelpSubCodePage { get; set; } = "Encoding used to decode text subtitle files.";
        public string SettingsHelpSubOutlineSize { get; set; } = "Outline width of text subtitles.";
        public string SettingsHelpSubShadowOffset { get; set; } = "Shadow offset of text subtitles.";
        public string SettingsHelpSubEmbeddedFonts { get; set; } = "Use fonts embedded in the media container (e.g. MKV).";
        public string SettingsHelpSubUseMargins { get; set; } = "Render text subtitles in the black margins when available.";
        public string SettingsHelpScreenshotDirectory { get; set; } = "Folder where screenshots are saved. Use Browse to pick a folder.";
        public string SettingsHelpScreenshotTemplate { get; set; } = "Filename template. %F filename, %P time, %n counter, %w/%h size, %f format.";
        public string SettingsHelpScreenshotFormat { get; set; } = "Screenshot file format.";
        public string SettingsHelpScreenshotJpegQuality { get; set; } = "JPEG quality (0-100). Only used when the format is JPEG.";
        public string SettingsHelpScreenshotPngCompression { get; set; } = "PNG compression level (0-9). Higher saves space but costs time.";
        public string SettingsHelpScreenshotWebpQuality { get; set; } = "WebP quality (0-100). Only used when the format is WebP.";
        public string SettingsHelpScreenshotHighBitDepth { get; set; } = "Preserve high bit depth in screenshots. Can produce very large files.";
        public string SettingsHelpScreenshotTagColorspace { get; set; } = "Tag screenshots with the correct colorspace metadata.";
        public string SettingsHelpCacheDirectory { get; set; } = "Folder mpv uses for cache files. Leave empty for the built-in default.";

        /// <summary>Native (autonym) name of an app UI language, e.g. zh-CN → 中文.</summary>
        public static string NativeLanguageName(string code)
        {
            return code switch
            {
                "en-US" => "English",
                "zh-CN" => "中文",
                "ja-JP" => "日本語",
                "ko-KR" => "한국어",
                "de-DE" => "Deutsch",
                "fr-FR" => "Français",
                "es-ES" => "Español",
                "ru-RU" => "Русский",
                _ => code,
            };
        }

        /// <summary>Display name for an ISO 639 audio/subtitle language code (autonym).</summary>
        public static string LanguageCodeName(string code)
        {
            return code switch
            {
                "auto" => "Auto",
                "eng" => "English",
                "chi" or "zho" => "中文",
                "jpn" => "日本語",
                "kor" => "한국어",
                "deu" or "ger" => "Deutsch",
                "fra" or "fre" => "Français",
                "spa" => "Español",
                "rus" => "Русский",
                "ita" => "Italiano",
                "por" => "Português",
                "ara" => "العربية",
                "hin" => "हिन्दी",
                "tha" => "ไทย",
                "vie" => "Tiếng Việt",
                "ind" => "Bahasa Indonesia",
                "tur" => "Türkçe",
                "nld" or "dut" => "Nederlands",
                "pol" => "Polski",
                "swe" => "Svenska",
                "dan" => "Dansk",
                "nor" => "Norsk",
                "fin" => "Suomi",
                "ces" or "cze" => "Čeština",
                "hun" => "Magyar",
                "ukr" => "Українська",
                "ell" or "gre" => "Ελληνικά",
                "ron" or "rum" => "Română",
                "bul" => "Български",
                _ => code,
            };
        }

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
