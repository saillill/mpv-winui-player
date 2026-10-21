# 分区 I2 · 基础设施 · 公共（Common）

## 职责

跨功能区共享的**机制**，不是功能。这里没有"用户能看到的东西"，只有别的区用来
拼装 UI 的零件。

> **它不是杂物间。** 判断一个文件该不该放这里，只有一个问题：
> 「它服务的是某一个功能，还是所有功能？」只有后者才进来。
> 反例：`MpvConfValueCodec.cs` 名字像工具，但只有设置树用，所以它在 `Settings/`。

## 内容

| 子目录 | 文件 | 作用 |
|---|---:|---|
| `Utils/` | 14 | `AtomicFile`（原子写）、`ConfigWriteQueue`（合并写）、`HashUtil`、`MediaKinds`（扩展名→媒体类型）、`UpdateChecker`、`LoggerHelper`、`DispatcherTimerDebouncer`、`DebounceUtil`、`TaskExtensions`、`DispatcherExtensions`、`ClipboardHelper`、`Win32WindowHelper` |
| `View/` | 12 | `BaseWindow`、`WindowsManager`（窗口去重/单例）、`WindowStyleManager`（云母/亚克力）、`WindowExtensions`、`ThemeResource`、`IconFonts`、`FluentIcons.cs` + `.Gen.cs`（图标几何）、4 个 `I*Support` 接口 |
| `View/Controls/` | 3 | `SliderExt`、`CheckBoxExt`、`TickBar` |
| `View/HighlightTextBlock.xaml(.cs)` | 2 | 搜索命中高亮 |
| `Convertor/` | 1 | `BindConvertor` |
| `View/*Converter.cs` | 2 | `AspectHeightConverter`、`IndexLetterOpacityConverter` |
| `Threading/` | 1 | `DefaultSynchronizationContext` |

## 对外接口（被别的区实际用到的）

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `WindowsManager` | `Open(key, factory, owner, ...)` | 所有窗口（设置、媒体信息、mpv.conf 编辑器、菜单编辑器、PiP 之外） |
| `AtomicFile` | `Write(path, bytes)` | 配置写回 |
| `ConfigWriteQueue` | `EnqueueCoalescing(task)` | `AppContext` 写 mpv.conf / script-opts |
| `FluentIcons` | `Geometry(name)` | 所有 `PathIcon`。**每次返回新实例**（Geometry 一次性规则） |
| `MediaKinds` | `IsAudio/IsVideo/IsPlaylist(path)` | 拖放、库扫描、打开文件 |

## 依赖

- 只依赖 `Motion`（无）与 .NET / WinUI。
- **禁止**依赖任何 `Modules/<功能域>`、`Shell`、`Language`。
  若某天需要，说明那个东西本该留在功能域里。

## 关键约定

- **`FluentIcons` 不要缓存 Geometry**：WinUI 的 `Geometry` 只能赋给一个 `PathIcon`，
  第二次赋值抛 `E_INVALIDARG`；后台线程创建的在 UI 线程赋也会失败。
  图标一律经 `tools/gen-fluent-icons.py` 生成，**禁止手写路径**。
- **`PathIcon` 不缩放几何**：非 24px 的场景要用 `Viewbox` 包住，否则会被裁切。
- **写文件一律走 `AtomicFile` / `ConfigWriteQueue`**：直接 `File.WriteAllText` 会在
  断电或崩溃时留下半个配置文件（历史上 mpv.conf 曾涨到 896 MB）。

## 上下文裁剪提示

改图标 → 只读 `FluentIcons.Gen.cs` 的头部注释 + `tools/gen-fluent-icons.py`。
改窗口行为 → `BaseWindow` + `WindowsManager` + `WindowStyleManager` 三个文件。
