using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace MpvMenuHost;

/// <summary>
/// 单进程 MenuFlyout 菜单宿主：宿主窗口 1×1 透明置顶，仅作为 Flyout 的放置目标；
/// 根菜单与全部子菜单由 WinUI 官方 MenuFlyout 渲染（图标/级联/动画/圆角/亚克力/
/// 键盘导航/Esc/点击外部关闭均为平台内置），点击叶子经 mpv JSON IPC 执行命令。
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MenuOptions _opts;
    private readonly MenuData _data;
    private IntPtr _hwnd;
    private bool _closed;
    private bool _executing;
    private int _targetX;
    private int _targetY;
    // 与可见条目一一对应的点击动作（用于 --test-select 无头验证）
    private readonly List<Action> _itemActions = new();

    public MainWindow(MenuOptions opts, MenuData? data = null)
    {
        _opts = opts;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null) ForceBorderless(presenter);

        _data = data ?? (_opts.ItemsPath != null ? MenuData.Load(_opts.ItemsPath) ?? SampleMenu() : SampleMenu());
        if (data == null && _opts.ItemsPath != null && File.Exists(_opts.ItemsPath))
            TryDeleteItemsFile(_opts.ItemsPath);

        if (_opts.Theme is "dark" or "light")
            Root.RequestedTheme = _opts.Theme == "dark" ? ElementTheme.Dark : ElementTheme.Light;

        Root.Loaded += OnLoaded;
        PositionWindow();
        MakeWindowInvisible();
        Log($"START items={_data.Items.Count} path={_opts.ItemsPath} pipe={_opts.Pipe} test={_opts.TestSelect}");
    }

    /// <summary>宿主窗口定位到弹出点（1×1 不可见，只承载 Flyout）。</summary>
    private void PositionWindow()
    {
        try
        {
            GetCursorPos(out var pt);
            int x = pt.X, y = pt.Y;
            if (_opts.Pos is (int px, int py)) { x = px; y = py; }
            _targetX = x;
            _targetY = y;
            AppWindow.Resize(new SizeInt32(1, 1));
            AppWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex) { Log("POS ERR " + ex.Message); }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= OnLoaded;
        try
        {
            // 再确保置顶/无边框样式（WinUI 显示时可能重置），并抬一次置顶
            ApplyWindowStyles();
            // 只重新断言位置与不可见性；不再对已显示窗口做尺寸收缩
            // （避免“白框带渲染缩为小球”的可见动画）
            AppWindow.Move(new PointInt32(_targetX, _targetY));
            MakeWindowInvisible();
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            ShowFlyout();
            if (_opts.TestSelect is int sel && sel >= 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(_opts.TestDelayMs);
                    _ = DispatcherQueue.TryEnqueue(() => InvokeItem(sel));
                });
            }
            // 看门狗：菜单是瞬态窗口，任何异常滞留都会吞输入，60 秒强制退出
            _ = Task.Run(async () =>
            {
                await Task.Delay(60_000);
                try { Environment.Exit(0); } catch { }
            });
        }
        catch (Exception ex)
        {
            Log("ERR " + ex);
            Close(4);
        }
    }

    private void ShowFlyout()
    {
        var flyout = new MenuFlyout();
        BuildItems(flyout.Items, _data.Items);
        // 点击项后 Flyout 先关闭再执行命令，_executing 防止关闭事件抢先退出进程
        flyout.Closed += (_, _) => { if (!_executing) Close(0); };
        var placement = _opts.Anchor == "bottom"
            ? FlyoutPlacementMode.BottomEdgeAlignedLeft
            : FlyoutPlacementMode.TopEdgeAlignedLeft;
        flyout.ShowAt(Root, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(0, 0),
            Placement = placement,
            ShowMode = FlyoutShowMode.Standard,
        });
        Log($"FLYOUT shown items={_data.Items.Count} pos={_targetX},{_targetY} anchor={_opts.Anchor}");
    }

    private void BuildItems(IList<MenuFlyoutItemBase> target, List<MenuItemModel> items, string? inheritGlyph = null)
    {
        bool prevSeparator = false;
        foreach (var item in items)
        {
            if (item.Hidden) continue;
            if (item.Type == "separator")
            {
                // 连续分隔线去重（参考 ikas-mc/mpv-winui-player 的菜单渲染）
                if (!prevSeparator) target.Add(new MenuFlyoutSeparator());
                prevSeparator = true;
                continue;
            }
            prevSeparator = false;
            if (item.Submenu is { Count: > 0 })
            {
                var sub = new MenuFlyoutSubItem { Text = DisplayText(item.Title), IsEnabled = !item.Disabled };
                var gs = IconMap.For(item.Title) ?? inheritGlyph;
                if (gs is not null)
                    sub.Icon = new FontIcon { Glyph = gs, FontFamily = new FontFamily(IconMap.Font) };
                BuildItems(sub.Items, item.Submenu, gs);
                if (sub.Items.Count == 0) continue; // 空子菜单不显示（含全部 hidden 的情况）
                target.Add(sub);
                _itemActions.Add(() => { }); // 子菜单项不产生命令动作
                continue;
            }
            var cmd = MenuData.CmdToList(item.Cmd);
            if (item.Checked)
            {
                var ti = new ToggleMenuFlyoutItem
                {
                    Text = DisplayText(item.Title),
                    IsChecked = true,
                    IsEnabled = !item.Disabled,
                };
                var gc = IconMap.For(item.Title) ?? inheritGlyph;
                if (gc is not null)
                    ti.Icon = new FontIcon { Glyph = gc, FontFamily = new FontFamily(IconMap.Font) };
                if (!string.IsNullOrEmpty(item.Shortcut)) SetAcceleratorText(ti, item.Shortcut);
                var cap = cmd;
                ti.Click += (_, _) => Execute(cap);
                target.Add(ti);
                _itemActions.Add(() => Execute(cap));
                continue;
            }
            var mi = new MenuFlyoutItem
            {
                Text = DisplayText(item.Title),
                IsEnabled = !item.Disabled,
            };
            var g = IconMap.For(item.Title) ?? inheritGlyph;
            if (g is not null)
                mi.Icon = new FontIcon { Glyph = g, FontFamily = new FontFamily(IconMap.Font) };
            if (!string.IsNullOrEmpty(item.Shortcut)) SetAcceleratorText(mi, item.Shortcut);
            var cap2 = cmd;
            mi.Click += (_, _) => Execute(cap2);
            target.Add(mi);
            _itemActions.Add(() => Execute(cap2));
        }
    }

    /// <summary>
    /// mpv 菜单标题还原：dyn_menu.lua 的 escape_title 会把字面 & 写成 &amp;&amp;
    /// （Win32 菜单把 & 当助记符），而 WinUI MenuFlyout 不解释 &，需还原为单 &。
    /// </summary>
    private static string DisplayText(string? title) =>
        title is not null && title.Contains("&&", StringComparison.Ordinal)
            ? title.Replace("&&", "&")
            : title ?? "";

    private void InvokeItem(int index)
    {
        if (index < 0 || index >= _itemActions.Count)
        {
            Log("TEST-SELECT out of range " + index);
            Close(1);
            return;
        }
        Log("TEST-SELECT invoke " + index);
        _itemActions[index]();
    }

    private void Execute(List<string>? cmd)
    {
        _executing = true;
        Log($"EXEC cmd={(cmd == null ? "null" : string.Join(" ", cmd))}");
        if (_opts.NoConnect || cmd == null)
        {
            Close(0);
            return;
        }
        _ = SendAndCloseAsync(cmd);
    }

    private async Task SendAndCloseAsync(List<string> cmd)
    {
        using var ipc = new MpvIpc();
        bool connected = false;
        for (int i = 0; i < 10 && !connected; i++)
        {
            Log($"IPC attempt {i + 1}");
            connected = await ipc.ConnectAsync(_opts.Pipe ?? "", 1500);
            Log($"IPC attempt {i + 1} result={connected}");
            if (!connected) await Task.Delay(250);
        }
        if (!connected)
        {
            Log("IPC connect failed");
            Close(3);
            return;
        }
        Log("IPC connected");
        var resp = await ipc.SendAsync(cmd.ToArray(), 1, 2000);
        Log("IPC resp=" + (resp ?? "<none>"));
        await Task.Delay(200);
        Close(0);
    }

    private void Close(int code)
    {
        if (_closed) return;
        _closed = true;
        Log($"EXIT code={code}");
        try { Log("CLOSE stack: " + Environment.StackTrace); } catch { }
        // WAS 2.3.1 未打包模式：Application.Exit 的 XAML 拆除路径会原生访问冲突，
        // 直接 Environment.Exit 硬退出最稳
        Environment.Exit(code);
    }

    /// <summary>
    /// 只设置右侧快捷键文本（KeyboardAcceleratorTextOverride），不创建真实 KeyboardAccelerator。
    /// mpv 本体已处理快捷键，宿主无需注册加速键；而 WinUI 解析非法键文本
    /// （Alt+-、·、TAB、Del、MKV 等）在展开子菜单时会卡死/崩溃（microsoft-ui-xaml #708），
    /// 文本覆盖是微软 Terminal 项目同款规避方案，且 Presenter 依据该文本即可显示右侧标签。
    /// </summary>
    private static void SetAcceleratorText(MenuFlyoutItem item, string shortcut)
    {
        try
        {
            item.KeyboardAcceleratorTextOverride = shortcut;
        }
        catch { }
    }

    private void TryDeleteItemsFile(string path)
    {
        try
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith("mpv-menu-host-items", StringComparison.OrdinalIgnoreCase)) return;
            File.Delete(path);
            Log("ITEMS deleted " + path);
        }
        catch (Exception ex) { Log("ITEMS DELETE ERR " + ex.Message); }
    }

    private void Log(string s)
    {
        if (_opts.LogPath == null) return;
        try
        {
            File.AppendAllText(_opts.LogPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + " [" + Environment.ProcessId + "] " + s + Environment.NewLine);
        }
        catch { }
    }

    private static MenuData SampleMenu()
    {
        var data = new MenuData { Title = "mpv test menu" };
        data.Items.Add(new MenuItemModel { Title = "Play/Pause", Cmd = Json("[\"cycle\",\"pause\"]") });
        data.Items.Add(new MenuItemModel { Title = "Seek +5s", Cmd = Json("[\"seek\",\"5\",\"exact\"]") });
        data.Items.Add(new MenuItemModel { Title = "Separator sample" });
        data.Items.Add(new MenuItemModel { Type = "separator" });
        data.Items.Add(new MenuItemModel { Title = "Quit", Cmd = Json("[\"quit\"]") });
        return data;
    }

    private static System.Text.Json.JsonElement? Json(string s) =>
        System.Text.Json.JsonDocument.Parse(s).RootElement.Clone();

    private void ApplyWindowStyles()
    {
        long ex = GetWindowLongPtrW(_hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        ex &= ~WS_EX_APPWINDOW;
        SetWindowLongPtrW(_hwnd, GWL_EXSTYLE, new IntPtr(ex));

        long style = GetWindowLongPtrW(_hwnd, GWL_STYLE).ToInt64();
        long mask = WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
        if ((style & mask) != 0)
        {
            SetWindowLongPtrW(_hwnd, GWL_STYLE, new IntPtr(style & ~mask));
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// 宿主窗口设为 1×1 空区域：窗口被系统最小尺寸钳制时会露出白底，
    /// 空区域让窗口彻底不可见（MenuFlyout 弹层是独立窗口，不受影响）。
    /// </summary>
    private void MakeWindowInvisible()
    {
        try
        {
            // 窗口级 alpha=0：从第一帧起就是全透明，不会闪白底
            long ex = GetWindowLongPtrW(_hwnd, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_LAYERED) == 0)
                SetWindowLongPtrW(_hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_LAYERED));
            SetLayeredWindowAttributes(_hwnd, 0, 0, LWA_ALPHA);
            // 去掉 CS_DROPSHADOW：不可见内容 + 矩形阴影会表现为“透明框”
            int cls = GetClassLongPtrW(_hwnd, GCL_STYLE).ToInt32();
            if ((cls & CS_DROPSHADOW) != 0)
                SetClassLongPtrW(_hwnd, GCL_STYLE, new IntPtr(cls & ~CS_DROPSHADOW));
            var rgn = CreateRectRgn(0, 0, 1, 1);
            if (rgn != IntPtr.Zero) SetWindowRgn(_hwnd, rgn, true);
        }
        catch { }
    }

    private void ForceBorderless(OverlappedPresenter presenter)
    {
        try
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            int ncPolicy = 1; // DWMNCRP_DISABLED
            DwmSetWindowAttribute(_hwnd, DWMWA_NCRENDERING_POLICY, ref ncPolicy, sizeof(int));
        }
        catch (Exception ex) { Log("BORDERLESS ERR " + ex.Message); }
    }

    // ---- Win32 ----
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int GCL_STYLE = -26;
    private const int CS_DROPSHADOW = 0x00020000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x00000002;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int DWMWA_NCRENDERING_POLICY = 2;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr GetClassLongPtrW(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern IntPtr SetClassLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
