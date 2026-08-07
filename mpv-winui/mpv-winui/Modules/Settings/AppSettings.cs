using mpv_winui.Modules.AppModel;

namespace mpv_winui.Modules.Settings
{
    public class AppSettings
    {
        private readonly IDataSetting _dataSetting;

        public AppSettings()
        {
            _dataSetting = PackageHelper.IsPackaged ? new AppDataSetting("app-settings") : new UnpackageAppDataSetting("app");
        }

        public const string ThemeType_Auto = "Auto";
        public const string ThemeType_Light = "Light";
        public const string ThemeType_Dark = "Dark";
        public string ThemeType
        {
            get => _dataSetting.GetValue(nameof(ThemeType), ThemeType_Auto);
            set => _dataSetting.SetValue(nameof(ThemeType), value);
        }

        public const string BackdropType_Acrylic = "Acrylic";
        public const string BackdropType_Mica = "Mica";
        public string BackdropType
        {
            get => _dataSetting.GetValue(nameof(BackdropType), BackdropType_Acrylic);
            set => _dataSetting.SetValue(nameof(BackdropType), value);
        }

        public bool EnableDebugLog
        {
            get => _dataSetting.GetValue(nameof(EnableDebugLog), false);
            set => _dataSetting.SetValue(nameof(EnableDebugLog), value);
        }

        public string CurrentLanguage
        {
            get => _dataSetting.GetValue(nameof(CurrentLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(CurrentLanguage), value);
        }

        public ulong AppVersion
        {
            get => _dataSetting.GetValue(nameof(AppVersion), (ulong)0);
            set => _dataSetting.SetValue(nameof(AppVersion), value);
        }

        public int PatchVersion
        {
            get => _dataSetting.GetValue(nameof(PatchVersion), 0);
            set => _dataSetting.SetValue(nameof(PatchVersion), value);
        }

        public int LastVideoVolume
        {
            get => _dataSetting.GetValue(nameof(LastVideoVolume), 50);
            set => _dataSetting.SetValue(nameof(LastVideoVolume), value);
        }

        public int LastAudioVolume
        {
            get => _dataSetting.GetValue(nameof(LastAudioVolume), 50);
            set => _dataSetting.SetValue(nameof(LastAudioVolume), value);
        }

        public string WindowPositionAndSize
        {
            get => _dataSetting.GetValue(nameof(WindowPositionAndSize), string.Empty);
            set => _dataSetting.SetValue(nameof(WindowPositionAndSize), value);
        }

        public bool EnableVideoPreview
        {
            get => _dataSetting.GetValue(nameof(EnableVideoPreview), false);
            set => _dataSetting.SetValue(nameof(EnableVideoPreview), value);
        }

        public string Hwdec
        {
            get => _dataSetting.GetValue(nameof(Hwdec), "auto");
            set => _dataSetting.SetValue(nameof(Hwdec), value);
        }

        public int VolumeMax
        {
            get => _dataSetting.GetValue(nameof(VolumeMax), 100);
            set => _dataSetting.SetValue(nameof(VolumeMax), value);
        }

        public string KeepOpen
        {
            get => _dataSetting.GetValue(nameof(KeepOpen), "yes");
            set => _dataSetting.SetValue(nameof(KeepOpen), value);
        }

        public bool LoopFile
        {
            get => _dataSetting.GetValue(nameof(LoopFile), false);
            set => _dataSetting.SetValue(nameof(LoopFile), value);
        }

        public string Deinterlace
        {
            get => _dataSetting.GetValue(nameof(Deinterlace), "auto");
            set => _dataSetting.SetValue(nameof(Deinterlace), value);
        }

        public string AspectRatio
        {
            get => _dataSetting.GetValue(nameof(AspectRatio), "auto");
            set => _dataSetting.SetValue(nameof(AspectRatio), value);
        }

        public int SubFontSize
        {
            get => _dataSetting.GetValue(nameof(SubFontSize), 42);
            set => _dataSetting.SetValue(nameof(SubFontSize), value);
        }

        public double SubDelay
        {
            get => _dataSetting.GetValue(nameof(SubDelay), 0.0);
            set => _dataSetting.SetValue(nameof(SubDelay), value);
        }

        public double Speed
        {
            get => _dataSetting.GetValue(nameof(Speed), 1.0);
            set => _dataSetting.SetValue(nameof(Speed), value);
        }

        public int SubPos
        {
            get => _dataSetting.GetValue(nameof(SubPos), 100);
            set => _dataSetting.SetValue(nameof(SubPos), value);
        }

        public string AudioLanguage
        {
            get => _dataSetting.GetValue(nameof(AudioLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioLanguage), value);
        }

        public string SubtitleLanguage
        {
            get => _dataSetting.GetValue(nameof(SubtitleLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(SubtitleLanguage), value);
        }

        public string AudioDevice
        {
            get => _dataSetting.GetValue(nameof(AudioDevice), "auto");
            set => _dataSetting.SetValue(nameof(AudioDevice), value);
        }

        public string ScreenshotDirectory
        {
            get => _dataSetting.GetValue(nameof(ScreenshotDirectory), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotDirectory), value);
        }

        public string ScreenshotTemplate
        {
            get => _dataSetting.GetValue(nameof(ScreenshotTemplate), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotTemplate), value);
        }

        public string CacheDir
        {
            get => _dataSetting.GetValue(nameof(CacheDir), string.Empty);
            set => _dataSetting.SetValue(nameof(CacheDir), value);
        }
    }
}
