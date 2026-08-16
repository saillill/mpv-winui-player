using mpv_winui.Modules.FileSystem;
using NLog;
using System.Text;

namespace mpv_winui.Modules.Common.Utils
{
    public static class LoggerHelper
    {
        public static void SetupLogger()
        {
            ApplyLogLevel();
        }

        /// <summary>Re-applies the log level from the current setting (live toggle support).</summary>
        public static void ApplyLogLevel()
        {
            LogManager.Setup().LoadConfiguration(builder =>
            {
#if DEBUG
                var level = AppContext.AppSetting.EnableDebugLog ? LogLevel.Trace : LogLevel.Debug;
                builder.ForLogger().FilterMinLevel(level).WriteToDebug();
#else
                var level = AppContext.AppSetting.EnableDebugLog ? LogLevel.Debug : LogLevel.Error;
#endif
                builder.ForLogger()
                    .FilterMinLevel(level)
                    .WriteToFile(fileName: AppData.Current.ResolveLocalData("logs\\mpv-winui.${shortdate}.log.txt"), encoding: Encoding.UTF8, keepFileOpen: false, maxArchiveDays: 15);
            });

            // Keep the native mpv log forwarding at warn by default; raise it
            // to info only while debug logging is enabled so the per-message
            // cross-thread forwarding cost stays low during normal playback.
            AppContext.SetMpvLogLevel?.Invoke(AppContext.AppSetting.EnableDebugLog ? "info" : "warn");
        }
    }
}
