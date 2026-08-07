using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings;
using NLog;
using System;
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

        public static void SendMpvCommand(string cmd) => RunMpvCommand?.Invoke(cmd);

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
            _task = Task.WhenAll([
                Task.Run(LoggerHelper.SetupLogger),
                AppBootstrap.RunAsync()
            ]);
        }

        private static void LoadLanguage()
        {
            var lang = AppSetting.CurrentLanguage;
            if (string.IsNullOrWhiteSpace(lang))
            {
                lang = "en-US";
            }

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
                    AppLang.LoadFromJson(path);
                    return;
                }
            }
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

    public static class AppBootstrap
    {
        public static async Task RunAsync()
        {
            await Task.Run(() => { });
        }
    }
}
