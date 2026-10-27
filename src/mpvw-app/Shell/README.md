# 分区 0 · 外壳 / Shell

## 职责

窗口本身：标题栏、导航面板、搜索框、内容区框架、窗口尺寸与宽高比、窗口样式
（云母/亚克力）、以及**唯一全局服务出口 `AppContext`**。

**不负责**：页面内容。外壳只负责"把哪个页面放进内容区"，页面内容归各功能域。

## 入口文件

| 文件 | 作用 |
|---|---|
| `MainWindow.xaml(.cs)` | 窗口根。`NavigationView` + `Frame`，导航项表 `ShellSections`，标题栏自绘 |
| `AppContext.cs` | **全局服务出口**：`AppLang`、`AppSetting`、`SettingChanged`、`RunMpvCommand`、`GetAudioDevices`、`GetGpuAdapters`、`SwitchLanguage` |
| `MainWindow_Size.cs` | 尺寸与最小尺寸 |
| `MainWindow_Aspect.cs` | `WM_SIZING` 子类化：只发布 `IsDragResizing`，**不做**宽高比约束 |
| `MainWindow_Style.cs` | 云母 / 亚克力 |
| `MainWindow_PiP.cs` | 为 PiP 窗口让位的窗口级逻辑 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `AppContext` | 见上 | 所有区 |
| `MainWindow` | `Open()` 打开文件 | `ActivationService` |
| `MainWindow` | `OpenMediaInfoWindow(path)` | 播放页菜单 |
| `AppContext` | `SendMpvCommand(cmd)` | 全应用 |

## 依赖

- 依赖：`Domains/Playback`（内容区）、`Domains/Settings`（窗口）、`Lexicon/Language`、`Infrastructure/Common`。
- 被依赖：**所有区都依赖 `AppContext`**（命名空间就是 `mpv_winui`，不是 `mpv_winui.Shell`），
  所以它的公共表面只允许增加、不允许改签名。

## 导航项（唯一来源）

`MainWindow.xaml.cs` 的 `ShellSections` 数组，**四项**：

| key | 图标 | 说明 |
|---|---|---|
| `home` | E80F | 主页 |
| `music` | E8D6 | 音乐库 |
| `videos` | E8B2 | 视频库 |
| `queue` | EA37 | 播放队列（`videos` 之后插入分隔线） |

`playlists` 已删除（与 queue 重复，注释记录在 `ShellSections` 上方）。
每项的 `AutomationId` 是 `ShellNav_<key>`（2026-10-22 加，供 MCP 断言用；
`AutomationProperties.SetAutomationId` 是附加属性，不是控件的 CLR 成员）。

## 关键约定

- **标题栏**：`Window.ExtendsContentIntoTitleBar = true` + `Window.SetTitleBar(控件)`。
  **不要**用 `AppWindow.TitleBar.ExtendsContentIntoTitleBar`（拖拽区域不生效），
  也**不要**手工 `SetRegionRects(Caption, 整栏)`（会吞掉按钮点击）。
- **侧栏**：`PaneDisplayMode="Auto"`，≥960 展开（240）、640–960 rail（48）、<640 覆盖式。
  收起按钮**只在非展开态**可见。rail 态框架会折叠整个 `PaneHeader`，
  所以 rail 专用按钮**不能**放在 `PaneHeader`，要用 `NavigationView.AutoSuggestBox` 槽位。
- **主题色**：只在 `App.xaml` 覆盖 7 个 `SystemAccentColor*` 色阶。
  画刷级覆盖实测无效。
- **主窗统一底色**：Body 根 `Grid` 刷 `SolidBackgroundFillColorBaseBrush`，
  因为库页面必须挡住正在播放的视频，透出云母会出现"内容区白框"。

## 上下文裁剪提示

改导航 → `MainWindow.xaml.cs` 的 `ShellSections` + `BuildShellNavigation`（两处，20 行）。
改标题栏 → `MainWindow.xaml` 的 `ShellTitleBar` 段 + `MainWindow.xaml.cs` 的 `UpdateShellPaneChrome`。
**不要**为了改导航去读 `MainWindow.xaml.cs` 剩余 1000+ 行。
