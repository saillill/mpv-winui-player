using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings;
using mpv_winrt;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>由播放页挂接，用于设置页动态枚举 mpv 音频输出设备。</summary>
        public static Func<IReadOnlyList<MpvAudioDevice>>? GetAudioDevices { get; set; }

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
            _ = PluginConfigWriter.WriteAllAsync();
        }

        /// <summary>Writes config-only options (ytdl_hook script options) into the deployed mpv.conf.</summary>
        public static void WriteManagedMpvConfig()
        {
            _ = ManagedMpvConfig.WriteAsync();
        }

        public static void NotifySettingChanged(string key, object? value)
        {
            SettingChanged?.Invoke(key, value);
        }

        /// <summary>枚举程序目录 Languages/*.json 作为可选语言；目录缺失时回退内置列表。</summary>
        public static string[] AvailableLanguages()
        {
            var dir = Path.Combine(System.AppContext.BaseDirectory, "Languages");
            if (Directory.Exists(dir))
            {
                var names = Directory.GetFiles(dir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (names.Length > 0)
                {
                    return names!;
                }
            }

            return ["en-US", "zh-CN"];
        }

        public static void Init()
        {
            LoadLanguage();
            SettingChanged += OnSettingChanged;
            _task = Task.WhenAll([
                Task.Run(LoggerHelper.SetupLogger),
                Task.Run(PluginConfigWriter.WriteAllAsync),
                Task.Run(ManagedMpvConfig.WriteAsync)
            ]);
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
            AppLang.LoadFromJson(LanguageFilePath(lang));
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

            AppLang.LoadFromJson(LanguageFilePath(lang));
        }

        private static string LanguageFilePath(string lang)
        {
            var candidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mpv-winui", "languages", lang + ".json"),
                Path.Combine(System.AppContext.BaseDirectory, "Languages", lang + ".json"),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return candidates[0];
        }

        public static async Task WaitAll()
        {
            if (_task != null)
            {
                await _task;
            }

            _task = null;
        }

    }
}
