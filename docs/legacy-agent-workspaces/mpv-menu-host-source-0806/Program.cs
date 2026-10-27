using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MpvMenuHost;

/// <summary>
/// 自定义入口：在任何窗口创建前把进程设为 PerMonitorV2 DPI 感知。
/// mpv.exe（无 manifest）经 Lua 拉起的子进程会继承其 DPI 感知状态；若宿主自身
/// 不主动声明，真实使用时 scale=1、workW=1920（逻辑分辨率），菜单尺寸减半、
/// 靠右过早翻左、三级一路往左、窗口位置错乱。测试脚本此前靠父进程
/// SetProcessDpiAwarenessContext 掩盖了此问题。
/// </summary>
public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
        if (!SetProcessDpiAwarenessContext(new IntPtr(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)))
        {
            // 老系统回退：进程级 PerMonitor，再回退系统级 DPI aware
            if (SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE) != 0)
                SetProcessDPIAware();
        }

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int awareness);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
}
