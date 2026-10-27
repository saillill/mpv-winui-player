using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Lexicon.Language;
using mpv_winui.Domains.MpvConf;
using mpv_winui.Domains.SettingsModel;
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

        /// <summary>
        /// Persists the settings window's values into the deployed mpv.conf.
        ///
        /// This is the write half of "mpv.conf is the source of truth": the
        /// window is its editor, so a change lands in the file rather than only
        /// reaching the running player. Coalesced because a slider drag fires
        /// per step and each write rewrites the whole block.
        ///
        /// The returned task completes after the write AND the ownership
        /// re-read, so a caller that needs the new verdict can chain onto it.
        /// </summary>
        public static Task WriteManagedMpvConfig() =>
            ConfigWriteQueue.Enqueue(ManagedMpvConfig.WriteAsync);

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
            // The logger comes up first, before anything that can fail. It used
            // to be set up ~20 lines down, after the language load, and that
            // order silently swallowed exactly the failures worth seeing: a
            // language file that cannot be parsed throws into a target that does
            // not exist yet, and the result is a startup that looks like it
            // simply did nothing. Nothing else in startup is worth this risk;
            // load-configuration is one call and reads no settings state beyond
            // the log level.
            LoggerHelper.SetupLogger();

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

            // Get the logger up before the read, not after. The read is the one
            // startup step that can fail in ways nothing else reveals - an
            // unreadable file, a value the property will not accept - and those
            // are reported through AppLogger. With the logger still unconfigured
            // every one of them was written into a target that did not exist yet
            // and vanished, which is indistinguishable from the read never having
            // run at all. Synchronous, so "before" is guaranteed and not merely
            // likely: it is a single load-configuration call, and the write it
            // would otherwise overlap with is queued below anyway.
            //
            // The setup itself now happens at the top of Init, together with the
            // same reasoning applied to the language load.

            // Read the file into the settings BEFORE anything writes it back.
            //
            // Order matters, and this is the only order that is lossless. Every
            // value the window can express already lives in mpv.conf, so the file
            // is the authority; loading it first makes the write below a no-op for
            // everything that already agrees, and for a hand edit - including one
            // made while the app was closed - it means the edit is carried into
            // memory and written back unchanged rather than clobbered by a stale
            // default. Doing it the other way round would overwrite the edit
            // first and have nothing left to read.
            MpvSettings.LoadFromConfig();

            // Enqueue the config writes on the same serialized queue so the
            // startup path can await them before mpv reads the config dir.
            _task = Task.WhenAll(
                ConfigWriteQueue.EnqueueCoalescing(PluginConfigWriter.WriteAllAsync),
                ConfigWriteQueue.Enqueue(ManagedMpvConfig.WriteAsync));
            // Which mpv options the config file already names decides whether a
            // settings-window change can actually take effect, so the read has
            // to land after the managed block is written.
            _ = _task.ContinueWith(
                _ => MpvConfOverrides.Refresh(),
                TaskScheduler.Default);
        }

        private static void OnSettingChanged(string key, object? value)
        {
            if (key == nameof(AppSetting.EnableDebugLog))
            {
                LoggerHelper.ApplyLogLevel();
            }

            // mpv.conf is the source of truth for every setting the file can
            // carry, so a change made anywhere - a context-menu toggle, not just
            // a settings row - has to land there. Coalesced, so a burst of
            // changes still costs one rewrite.
            WriteManagedMpvConfig();
        }

        public static void SwitchLanguage(string code)
        {
            // Sanitised here rather than only inside the loader: the value is
            // both persisted and interpolated into an mpv command, so what gets
            // stored and sent has to be the validated form.
            var lang = LanguageManager.Normalize(code);
            AppSetting.CurrentLanguage = lang;
            LanguageManager.Load(AppLang, lang);
            SendMpvCommand($"set user-data/mpvw/language {lang}");
            LanguageChanged?.Invoke();
        }

        private static void LoadLanguage()
        {
            LanguageManager.Load(AppLang, AppSetting.CurrentLanguage);
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
