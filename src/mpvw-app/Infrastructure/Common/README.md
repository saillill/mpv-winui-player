# 分区 I2 · 基础设施 · 公共（Common）

## 职责

跨功能区共享的**机制**，不是功能。这里没有"用户能看到的东西"，只有别的区用来
拼装 UI 的零件。

> **它不是杂物间。** 判断一个文件该不该放这里，只有一个问题：
> 「它服务的是某一个功能，还是所有功能？」只有后者才进来。

## 内容

按实际目录列出（`git ls-files src/mpvw-app/Infrastructure/Common`，共 28 个文件，
其中 README 1 个，代码 27 个）。

| 目录 | 文件数 | 内容 |
|---|---:|---|
| `Common/`（根） | 3 | `ThemeKeys.cs`、`DefaultSynchronizationContext.cs`、本 README |
| `Common/Utils/` | 11 | `AtomicFile`（原子写）、`ConfigWriteQueue`（串行 + 合并写）、`HashUtil`、`MediaKinds`（扩展名→媒体类型）、`UpdateChecker`、`LoggerHelper`、`DispatcherTimerDebouncer`、`DebounceUtil`、`TaskExtensions`、`DispatcherExtensions`、`ClipboardHelper` |
| `Common/View/` | 13 | `BaseWindow`、`WindowStyleManager`（云母/亚克力）、`WindowExtensions`、`ThemeResource`、`IconFonts`、`FluentIcons.cs` + `.Gen.cs`（图标几何）、`HighlightTextBlock.xaml(.cs)`（无外部消费者）、4 个 `I*Support` 接口 |
| `Common/Media/` | 1 | `FileItem.cs` |

**本层之外还有三个平行目录**，命名空间与上面不同，不要混为一谈：

| 目录 | 文件数 | 命名空间 | 说明 |
|---|---:|---|---|
| `Infrastructure/Controls/` | 4 | `mpv_winui.Infrastructure.Controls` | `SliderExt`、`CheckBoxExt`、`TickBar`、`ControlBarIconCatalog` |
| `Infrastructure/Converters/` | 3 | 混合：`BindConvertor` 在 `Infrastructure.Converters`；`AspectHeightConverter`、`IndexLetterOpacityConverter` 在 `Infrastructure.Common.View` | 命名空间与目录不一致，见下 |
| `Infrastructure/Motion/` | 5 + README | `mpv_winui.Infrastructure.Motion` | 见 `Motion/README.md` |

`WindowsManager` 与 `Win32WindowHelper` **不在本层**，物理位置是
`Platform/Win32/`，但声明的命名空间分别是 `Infrastructure.Common.View` 与
`Infrastructure.Common.Utils`。引用时用命名空间，找文件时去 `Platform/Win32/`。

## 对外接口（被别的区实际用到的）

| 类型 | 成员 | 实际消费者（实测） |
|---|---|---|
| `AtomicFile` | `WriteAllText` / `WriteAllTextAsync` / `WriteAllLines` / `WriteAllLinesAsync` | `ManagedMpvConfig`、`PluginConfigWriter`、`SettingsLayout`、`SettingsPage.Actions.Shortcuts`、`ConfigDeployer`、`FileService`；另有 `AtomicFileTests` |
| `ConfigWriteQueue` | `Enqueue` / `EnqueueCoalescing` | 仅 `Shell/AppContext.cs`（4 处） |
| `FluentIcons` | `Geometry(pathData)`，以及 `FluentIcons.Gen.cs` 里的常量 | 8 个调用文件：`LibraryEntry`、`VideoLibraryTypes`、`ControlBarIcons`、`MpvPlayerPage_Library`、`MpvPlayerPage.Library.Video`、`MpvPlayerPage_PlaylistPanel`、`OptionCheckListControl`、`MainWindow.xaml.cs`。`CenterPlayerControlStyle` / `CompactPlayerControlStyle` 把路径常量**复制**进了 Style（不是调用本类）；各 XAML 与 `LibraryEntry.Selection.cs` 里的提及都在注释中；`IconFonts.cs` 的 `SegoeFluentIcons` 是字体名字符串，不是对本类的引用 |
| `MediaKinds` | `IsVideo` / `IsAudio` / `IsKnownMedia`，常量 `VideoExtensions` / `AudioExtensions` | `LibraryEntry`、`MpvPlayerPage_Drop`、`MpvPlayerPage_Library`、`MpvPlayerPage.Library.Music`、`MpvPlayerPage_Music`、`SettingsPage.Actions` |
| `WindowsManager` | `Open(key, factory, parent, parentSizePercent, minWidth, minHeight)`、`Open(key, factory, minWidth, minHeight)`、`Close`、`UpdateTheme`、`UpdateBackdrop` | **仅 `Shell/MainWindow.xaml.cs`**：3 处 `Open`（mpv.conf 编辑器、媒体信息、菜单编辑器）。设置窗口不走它（`MainWindow._settingsWindow` 自己判空复用） |
| `BaseWindow` | 窗口基类 | 6 个子类：`MainWindow`、`SettingsWindow`、`MediaInfoWindow`、`MpvConfEditorWindow`、`MpvConfTextWindow`、`MenuEditorWindow`。`PiPWindow` 不继承 |
| `WindowStyleManager` | 云母/亚克力 | 8 个文件 |
| `ThemeResource` | 主题键取值 | 28 个文件 |

`MediaKinds.IsPlaylist` **不存在**。实际 API 是 `IsVideo`、`IsAudio`、`IsKnownMedia`
（`Utils/MediaKinds.cs:40,42,50`）。需要判断播放列表请自行按扩展名处理。

## 依赖

实测（`git grep 'using mpv_winui'` 于 `Infrastructure/Common/`）：

- 依赖 **`Platform`** 2 处：`Utils/LoggerHelper.cs:1` → `Platform.FileSystem`，
  `Utils/UpdateChecker.cs:3` → `Platform.AppModel`。
  这与 `Platform` 反过来依赖本层 3 处（`ActivationService`、`ConfigDeployer`、
  `FileService`）构成一个双向边；见 `docs/architecture.md` §3.1。
- **不依赖** `Domains/*`、`Shell`、`Lexicon`、`Motion`（四者实测均为 0 处）。
- `Motion` 反过来也不依赖本层，这两个目录互不相干，不是父子关系。

**禁止**：依赖任何 `Domains/<功能域>`、`Shell`、`Lexicon`。
若某天需要，说明那个东西本该留在功能域里。

## 关键约定

- **`FluentIcons` 不要缓存 `Geometry`，每次调用返回新实例。**
  WinUI 的 `Geometry` 一生只能赋给一个 `PathIcon`；第二次赋值——换图标、
  甚至同一个图标换掉旧几何后再赋——抛 `E_INVALIDARG`。后台线程创建的几何
  不能在 UI 线程赋值。所以 `FluentIcons.Geometry` 无缓存，每次都重新解析
  （`FluentIcons.cs:25-46`）。调用方必须在 UI 线程调用，且一个实例只用一次。
  数据模板里的正确写法是绑定**路径字符串**、每次绑定应用时新建几何，
  参考 `LibraryEntry.MediaIcon`。
  图标常数一律经 `tools/gen-fluent-icons.py` 生成到 `FluentIcons.Gen.cs`，
  **禁止手写路径**。
- **`PathIcon` 不按 24px 之外的尺寸缩放几何**：`PathIcon` 按原生 24 绘制，
  小于该尺寸的元素会被裁掉。需要 17px 之类的尺寸时用 `Viewbox`
  设定外框、内层 `PathIcon` 设 `Width`/`Height`——实例见
  `Domains/Playback/CenterPlayerControlStyle.xaml:232`（注释与用法都在那一处）。
- **写文件一律走 `AtomicFile`**：`File.WriteAllText` 先截断再填充，中途崩溃、
  强制关机或磁盘满会留下半个文件，下次启动读到的是解析不了的配置。
  `AtomicFile` 写同目录临时文件 → flush → 改名覆盖，NTFS 上同目录改名是原子的。
  它**不是**持久性保证：flush 返回之后的失败仍会丢数据；也不做多文件事务
  （`mpv.conf` 与其状态文件是分两次写的）。
- **`ConfigWriteQueue` 负责串行与合并**：`Enqueue` 排进同一条队列，
  `EnqueueCoalescing` 把连发的多次写入并成一次。改设置时用它，不要直接并发写。

## 上下文裁剪提示

改图标 → 只读 `FluentIcons.Gen.cs` 的头部注释 + `FluentIcons.cs` 的类注释 +
`tools/gen-fluent-icons.py`。
改窗口行为 → `BaseWindow` + `WindowStyleManager`；`WindowsManager` 在
`Platform/Win32/` 下。
改图标字体 → `IconFonts.cs`（10 行）：它只是三条字体 URI/名字常量，
几何走 `FluentIcons`。

## 本文的验证命令

> 所有 `git grep` 都必须限定 `*.cs` / `*.xaml`。不限定会把本 README 里的示例文本
> 也算进去，读数会偏大（实测 `Common -> Platform` 从 2 变成 3）。

```powershell
# 目录与文件数
git ls-files 'src/mpvw-app/Infrastructure/Common/*'            # 28（含 README）
git ls-files 'src/mpvw-app/Infrastructure/Common/Utils/*'      # 11
git ls-files 'src/mpvw-app/Infrastructure/Common/View/*'       # 13
git ls-files 'src/mpvw-app/Infrastructure/Common/Media/*'      # 1
git ls-files 'src/mpvw-app/Infrastructure/Controls/*'          # 4
git ls-files 'src/mpvw-app/Infrastructure/Converters/*'        # 3
git ls-files 'src/mpvw-app/Infrastructure/Motion/*.cs'         # 5

# 依赖方向（只数 .cs）
git grep -l 'using mpv_winui.Platform' -- 'src/mpvw-app/Infrastructure/Common/*.cs'  # 2
git grep -l 'using mpv_winui.Domains'  -- 'src/mpvw-app/Infrastructure/Common/*.cs'  # 无输出
git grep -l 'using mpv_winui.Lexicon'  -- 'src/mpvw-app/Infrastructure/Common/*.cs'  # 无输出
git grep -l 'using mpv_winui.Infrastructure.Motion' -- 'src/mpvw-app/Infrastructure/Common/*.cs'  # 无输出
git grep -l 'using mpv_winui.Infrastructure.Common' -- 'src/mpvw-app/Platform/*/*.cs'            # 3

# 消费者
git grep -l 'MediaKinds\.'       -- '*.cs'
git grep -l 'WindowsManager'     -- '*.cs'      # 2：定义 + MainWindow
git grep -n '\.Open("'           -- '*.cs'      # 3，全在 MainWindow.xaml.cs
git grep -l 'ConfigWriteQueue\.' -- '*.cs'      # 1：AppContext.cs
git grep -l 'FluentIcons'        -- '*.cs' '*.xaml'
git grep -l 'AtomicFile\.'       -- '*.cs'

# 命名空间与目录不一致的三处
Select-String -Path src/mpvw-app/Infrastructure/Converters/*.cs -Pattern '^namespace'
Select-String -Path src/mpvw-app/Platform/Win32/*.cs          -Pattern '^namespace'
```
