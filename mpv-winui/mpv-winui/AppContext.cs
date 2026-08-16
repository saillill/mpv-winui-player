using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings;
using mpv_winrt;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace mpv_winui
{
    public class AppContext
    {
        public static readonly Logger AppLogger = LogManager.GetLogger("App");

        public static AppLang AppLang { get; } = new();

        public static AppSettings AppSetting { get; } = new();

        private static Task? _task;

        /// <summary>由播放页在 mpv 初始化后挂接，用于把设置即时下发到 mpv。</summary>
        public static Action<string>? RunMpvCommand { get; set; }

        /// <summary>由播放页挂接，日志级别切换时同步 mpv 的 log 请求级别。</summary>
        public static Action<string>? SetMpvLogLevel { get; set; }

        /// <summary>
        /// True while a UI slider (progress/volume) owns keyboard focus. The
        /// global keyboard hook must then NOT forward arrow keys to mpv, or a
        /// single keypress would seek twice (WinUI slider + mpv binding).
        /// </summary>
        public static volatile bool UiFocusInSlider;

        /// <summary>由播放页挂接，用于设置页动态枚举 mpv 音频输出设备。</summary>
        public static Func<IReadOnlyList<MpvAudioDevice>>? GetAudioDevices { get; set; }

        /// <summary>DXGI display adapters (settings d3d11-adapter list), set
        /// by the player page so enumeration never blocks the UI thread.</summary>
        public static Func<IReadOnlyList<MpvGpuAdapter>>? GetGpuAdapters { get; set; }

        /// <summary>Runtime mpv profile list (profile-list property), used by
        /// the settings Profile manager when a player is initialized.</summary>
        public static Func<IReadOnlyList<MpvProfile>>? GetMpvProfiles { get; set; }

        public static event Action<string, object?>? SettingChanged;

        public static event Action? LanguageChanged;

        public static void SendMpvCommand(string cmd)
        {
            if (AppSetting?.TestMpvCommandLog == true)
            {
                AppLogger.Info("mpv: " + cmd);
            }
            RunMpvCommand?.Invoke(cmd);
        }

        /// <summary>Writes settings-managed plugin options into script-opts/*.conf (next mpv start).</summary>
        public static void WritePluginConfigs()
        {
            _ = ConfigWriteQueue.EnqueueCoalescing(PluginConfigWriter.WriteAllAsync);
        }

        /// <summary>Writes config-only options (ytdl_hook script options) into the deployed mpv.conf.</summary>
        public static void WriteManagedMpvConfig()
        {
            _ = ConfigWriteQueue.Enqueue(ManagedMpvConfig.WriteAsync);
        }

        public static void NotifySettingChanged(string key, object? value)
        {
            SettingChanged?.Invoke(key, value);
        }

        /// <summary>枚举程序目录 Languages/*.json 作为可选语言；目录缺失时回退内置列表。</summary>
        public static string[] AvailableLanguages()
        {
            return LanguageManager.GetAvailableLanguages();
        }

        public static void Init()
        {
            LoadLanguage();
            SettingChanged += OnSettingChanged;

            // First-run config deployment must finish BEFORE the config writers
            // below start: they merge into mpv.conf (ManagedMpvConfig) and the
            // script-opts files, so the bundled layer has to be on disk first.
            // Synchronous and a fast no-op once mpv.conf exists.
            var mpvDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mpv-winui", "mpv");
            ConfigDeployer.EnsureDeployed(mpvDir);

            var loggerTask = Task.Run(LoggerHelper.SetupLogger);
            // Enqueue the config writes on the same serialized queue so the
            // startup path can await them before mpv reads the config dir.
            _task = Task.WhenAll(
                loggerTask,
                ConfigWriteQueue.EnqueueCoalescing(PluginConfigWriter.WriteAllAsync),
                ConfigWriteQueue.Enqueue(ManagedMpvConfig.WriteAsync));
        }

        private static void OnSettingChanged(string key, object? value)
        {
            if (key == nameof(AppSetting.EnableDebugLog))
            {
                LoggerHelper.ApplyLogLevel();
            }
        }

        public static void SwitchLanguage(string code)
        {
            var lang = string.IsNullOrWhiteSpace(code) ? "en-US" : code;
            AppSetting.CurrentLanguage = lang;
            LanguageManager.Load(AppLang, lang);
            SendMpvCommand($"set user-data/mpvw/language {lang}");
            LanguageChanged?.Invoke();
        }

        private static void LoadLanguage()
        {
            var lang = AppSetting.CurrentLanguage;
            if (string.IsNullOrWhiteSpace(lang))
            {
                lang = "en-US";
            }

            LanguageManager.Load(AppLang, lang);
        }

        public static async Task WaitAll()
        {
            // Capture the reference locally: the field may be read from
            // several threads, and awaiting a completed task again is safe,
            // so it is never nulled out here.
            if (_task is { } task)
            {
                await task;
            }
        }

    }
}
