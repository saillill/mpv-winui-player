# 分区 1 · 播放页 / Player

## 职责

把「打开了一个媒体」这件事画出来：视频表面、自定义控制栏、菜单栏、全屏、画中画、
队列侧面板、进度条缩略图预览、音乐播放视图、库页面宿主。

**负责**：`MpvPlayerPage` 的全部 UI 与其行为；`PlayerControl` 三套皮肤；PiP 窗口；
库（Home / Videos / Music / Queue）的**视图**。

**不负责**：库文件夹的持久化（`Domains/SettingsModel/AppSettings.LibraryFolders.cs`）、
媒体信息窗口（`Domains/MediaInfo`）、mpv 命令的真正执行（`mpv_winrt`）。

## 入口文件

| 文件 | 作用 |
|---|---|
| `MpvPlayerPage.xaml(.cs)` | 页面根。`NavigationView` 的内容区宿主，五个 `Visibility` 切换的容器 |
| `PlayerControl.xaml(.cs)` | 控制栏根。三套皮肤共用一套逻辑，皮肤是 `Style` |
| `ControlBarAdaptiveLayout.cs` | 分档规则（纯函数，链接进 `mpv-conf-test`，**不要靠肉眼验证**） |
| `PiPWindow.xaml(.cs)` + `.Overlay.cs` + `.Resize.cs` | 画中画窗口 |
| `MpvPlayerPage_Library.cs` | 库视图（126 KB，最大的手写文件） |
| `MpvPlayerPage_Music.cs` | 音乐播放视图 + 专辑页 |
| `MpvPlayerPage_PlaylistPanel.cs` | 覆盖式队列侧面板 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MpvPlayerPage` | `ShowSection(string key)` 等 section 切换 | `Shell/MainWindow.xaml.cs` 的 `ShellNav_SelectionChanged` |
| `MpvPlayerPage` | `HandleFullScreenRequest()` / `HandleFullWindowRequest()` | `Shell/MainWindow.xaml.cs` |
| `MpvPlayerPage` | `EmptyStateVisibleChanged` | 外壳据此隐藏「打开文件」按钮 |
| `PlayerControl` | `MediaPlayer` 属性 | PiP、设置预览 |

## 依赖

- 依赖：`Shell/AppContext`（唯一全局出口）、`Settings/AppSettings`（读设置）、
  `Domains/MpvConf/MpvConfOverrides`（判断选项是否被 mpv.conf 接管）、
  `Infrastructure/Motion`、`Infrastructure/Common`。
- **不依赖**：`Menu/MenuEditor`、`MpvConf` 的编辑窗口、`MediaInfo`。

## 关键状态

| 状态 | 存放 | 生命周期 |
|---|---|---|
| 当前 section | `_librarySection`（字符串常量 `LibrarySectionHome/Music/Videos/Queue`） | 页面存活期 |
| 播放中 | mpv `idle-active` 属性 | mpv 会话 |
| 队列 | mpv `playlist` 属性 | mpv 会话 |
| 库文件夹 | `AppSettings.VideoLibraryFolders` / `MusicLibraryFolders`（注册表） | 持久 |
| 缩略图缓存 | `MpvPreviewer`（原生第二实例） | mpv 会话 |
| 皮肤 | `AppSettings.PlayerStyle`（Standard/Centre/Compact） | 持久 |

## 已知坑（读代码前先看）

- **鼠标输入不会到达 libmpv**：合成模式下键盘/鼠标由 `MpvPlayerPage_InputForwarding.cs` 转发。
- **控制栏三根 `CommandBar` 每次 apply 重建**（`ApplyBarOrders`），不要按当前 items 过滤。
- **弹层 chrome 四处共用**：音量条、轨道选择器、快捷面板、More 菜单共享
  `MpvPlayerPopupPresenterStyle`；改一处必须一起改。
- **播放模式按钮**循环 不循环→顺序→单曲循环→随机，`RepeatButton` 隐藏、`ShuffleButton` 复用。

## 上下文裁剪提示

只改控制栏时读 `PlayerControl.ControlBar.cs` + `ControlBarAdaptiveLayout.cs` + 对应皮肤
`.xaml`，不必打开 `MpvPlayerPage_Library.cs`（126 KB 与它们无关）。
