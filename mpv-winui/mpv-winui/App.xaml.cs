using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using mpv_winui.Modules.Common.View;
using NLog;

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
            window.Activate();
        }

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
