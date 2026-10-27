using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace MpvMenuHost;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    "UNHANDLED " + e.Message + "\n" + e.Exception + "\n");
            }
            catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    "APPDOMAIN " + e.ExceptionObject + "\n");
            }
            catch { }
        };
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var raw = Environment.GetCommandLineArgs();
        var argv = raw.Length > 1 ? raw[1..] : Array.Empty<string>();
        _window = new MainWindow(MenuOptions.Parse(argv));
        _window.Activate();
    }
}
