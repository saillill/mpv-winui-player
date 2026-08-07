using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings;
using NLog;
using System;
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
