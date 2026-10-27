using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
using NLog;
using System;

namespace mpv_winui
{
    public partial class App : Application
    {
        private static readonly Logger _logger = LogManager.GetLogger("App");

        public static Window? Window
        {
            get; private set;
        }

        public App()
        {
            InitializeComponent();

            // Registered before Init so a failure inside startup is reported at
            // all; Init sets the logger up as its first act.
            UnhandledException += (_, e) =>
            {
                // Flush what is about to be lost. A crash here kills the
                // process, and NLog's file target buffers: without this the last
                // - and most useful - lines never reach disk.
                try
                {
                    _logger.Error(e.Exception, "Unhandled XAML exception");
                    NLog.LogManager.Flush();
                }
                catch
                {
                    // Reporting must not become the crash.
                }
            };

            AppContext.Init();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var window = new MainWindow();
            Window = window;
            window.Open();

            // `Activate()` is what actually makes the window appear -- skipping it entirely
            // leaves no window at all (measured: the harness then times out with "no window
            // after 20s"). So the choice is not whether to activate, but whether the activation
            // is allowed to take the FOREGROUND.
            //
            // MVPW_NO_ACTIVATE=1 shows the window without taking the foreground. Why that
            // matters: the verification harness launches the app, waits for its window, and only
            // then parks it off-screen, so between those steps the window sits on a monitor and
            // is active for the three to five seconds the main window takes to appear. A game in
            // exclusive fullscreen loses its display mode and its controller the instant a window
            // takes the foreground, which is the user's report of the app "taking over the
            // controller". The app opens no gamepad device at all (`gamepad`, `XInput`, `SDL2`,
            // `hid.dll` occur zero times in mpv-2.dll, mpvw.dll and mpv_winrt.dll, and no gamepad
            // module loads at runtime), so the foreground flash is the whole mechanism.
            if (SuppressStartupActivation)
            {
                // SW_SHOWNOACTIVATE: visible, laid out, and the foreground stays where it was.
                Win32WindowHelper.ShowNoActivate(window);
            }
            else
            {
                window.Activate();
            }
        }

        /// <summary>
        /// True when this process was started for automation and must not take the foreground.
        /// Read from the environment rather than a setting so it cannot be left enabled by
        /// accident: a normal, user-started launch has no such variable.
        /// </summary>
        private static bool SuppressStartupActivation =>
            Environment.GetEnvironmentVariable("MVPW_NO_ACTIVATE") == "1";

        public void OnActivated(AppActivationArguments args)
        {
            if (_logger.IsDebugEnabled)
            {
                _logger.Debug("OnActivated, kind={}", args.Kind);
            }

            if (Window is MainWindow mainWindow)
            {
                mainWindow?.Refresh(args);
            }

            Window?.ShowWindow();
        }
    }
}
