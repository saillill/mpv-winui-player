using mpv_winui.Platform.FileSystem;
using NLog;
using System.Text;

namespace mpv_winui.Infrastructure.Common.Utils
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
                // Info, not Warn, and the change is deliberate.
                //
                // The play chain (Modules/Player/PlayChain.cs) is logged at Info
                // precisely so that a user reporting "the video does not play" does
                // not have to have had debug logging on for the report to be
                // diagnosable. At Warn those lines were filtered out, which is why
                // two days of logs could hold a complete "file loaded, nothing
                // appeared" failure and show only mpv's own warnings.
                //
                // The volume cost is bounded by what writes at Info: the play chain
                // (one line per link per load), settings load/ownership at startup,
                // and mpv's forwarded info lines when debug logging is on. mpv's own
                // chatter stays at Warn otherwise (see the SetMpvLogLevel call
                // below), so playback itself does not add lines.
                var level = AppContext.AppSetting.EnableDebugLog ? LogLevel.Debug : LogLevel.Info;
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
