# 本地 vs 云端参考仓库 差异审计（2026-10-21）

> 审计对象：本地 `mpvw`（WinUI 3 + C++/WinRT + libmpv）与云端参考仓库 `saillill/mpv-winui-player`。
> 全部命令在 `C:\Users\z2637\Documents\DeepSeek\mpv\_cloud\mpv-winui-player` 内执行；未启动 mpvw.exe，未改代码，未做任何 git 写操作。

---

## 1. 基准说明

### 1.1 三者关系

| 引用 | 提交 | 时间 | 说明 |
|---|---|---|---|
| `local/main` | `7b42fd5` | 2026-10-21 12:56 | 本地 HEAD |
| `origin/main` | `3053352` | 2026-09-19 14:37 | 云端 `saillill/mpv-winui-player` |
| `origin/upstream-merge` | `7b502ad` | — | 云端另一分支 |

```
$ git rev-list --left-right --count origin/main...local/main
0       116          # 云端独有 0，本地独有 116 → 本地是云端的严格后继
```

**前提成立且已复验**：云端不存在本地缺失的提交。因此本审计不存在"云端有、本地悄悄丢了"的整提交级别风险，只剩下**文件级删除 11 个**需要逐个交代。

### 1.2 比较口径

```
$ git diff --name-status origin/main local/main | Group-Object 首字符
M  150    A   88    D   11     （合计 249 文件）
$ git diff --shortstat origin/main local/main
249 files changed, 50876 insertions(+), 6919 deletions(-)
```

- **强断言口径**：只有当命令输出或 `file:line` 直接支持时才下结论。
- **反证优先**：对"云端有、本地没有"的候选，先查云端该文件是否**真的被引用**（死代码则本地删除正确），再查本地是否有等价物。
- **注意工具可靠性**：本环境里 `git grep -c`、`Measure-Object -Line` 与管道混用会给出错误计数（曾把 `console` 报成 4 个文件而直接检索为 0）。本报告所有数字均来自直接检索或 `--numstat`，不用聚合计数。
- **期望项先核实再写"云端无"**：任务清单里"控制台输出""状态栏"两项，经核实**云端本身没有**（见 §2.13、§2.16），不是本地丢的功能。

### 1.3 一个必须声明的基准未知项

本地文档 `docs/settings-design-contract.md:13` 声称其基线是 **`d0a06d9`（领先 `origin/main` 26 个提交）**，而本次审计的实测基线是 `7b42fd5`（领先 **116** 个）。也就是说本地有若干历史文档是在提交数远少于今天的时刻写的：

```
$ git log --format='%h %ci %s' -1 local/main -- docs/settings-design-contract.md
c7412d8 2026-10-19 23:21:31 Localization: a checkable language scope, and the two defects it exposed
```

**结论**：本地 `docs/` 里所有"相对上游的偏离清单"都是**时间点快照**，不是当前事实。本报告不引用它们的结论，只把它们列为待清理对象（§6.2）。凡本报告与那些文档冲突处，以本报告的实测命令为准。

### 1.4 116 个本地提交的大致构成

```
$ git log --format='%s' origin/main..local/main | cut -d: -f1 | sort | uniq -c
26 settings   15 docs   4 music   4 tools   4 player   3 Shell ...
```

跨度 2026-09-19 → 2026-10-21。主体是三类：① `music`/library 全新子系统；② `Motion` 动效子系统；③ 设置页 schema 生成层 + 布局重构。文档另加 12 篇。

---

## 2. 云端功能基准清单（按功能域，逐条附证据）

> 判定依据是**云端 `origin/main` 的实际代码**，不是云端 README 的自我描述。凡 README 与代码不符者单独标注。路径前缀统一省略 `mpv-winui/mpv-winui/`。

### 2.1 设置卡片 / 文件夹

| 能力 | 证据 |
|---|---|
| 设置按「分类 → 节卡片 → 行」三层组织 | `Modules/Settings/Controls/SectionCardItem.cs:20` `public sealed class SectionCardItem : INotifyPropertyChanged` |
| 分类/节清单集中定义 | `Modules/Settings/Layout/SettingsSections.cs`（云端 142 行级；本地同文件 +122/-20） |
| 节卡片宿主与折叠 | `Modules/Settings/Controls/SectionHeaderItem.cs`、`SectionCardItem.cs` |
| 用户自建节与自建选项 | `Modules/Settings/Layout/SettingsLayout.cs:70` `CustomSection`、`:129` `CustomOption` |
| 树形构建 + 缺映射即崩（fail-loud） | `AGENTS.md:72` "Missing sectionMap entry throws at startup (fail-loud)" |
| 选项渲染器族 | `Controls/Option{Boolean,StringList,Integer,Double,Color,CheckList,Layout,ShaderList}Control`（`AGENTS.md:74`） |

**云端有一处已修好的重复**：`e1f40dd` 把 `OptionListControl.xaml` 里重复 11 次的 9 行 `Border` 收成一个 `Border` 样式（提交信息原文："spelled out the same nine-line Border eleven times, once per hosted control"）。

### 2.2 设置行重排 / 重命名 / 隐藏

| 能力 | 证据 |
|---|---|
| 行重排（上移/下移 + 拖拽） | `Modules/Settings/SettingsPage.Customize.cs:61` `list.OrderChanged += keys => …; :68 StoreOrder(keys)`；`:818` `CustomizeMoveUp`；`:821` `CustomizeMoveDown` |
| 分类/节重排 | `SettingsPage.CustomizeTree.cs:389` `StoreTreeOrder()`、`:406` `CategoryOrder = …`、`:427` `StoreSectionOrder(sectionIds)` |
| 行重命名（含描述、可跨语言） | `SettingsPage.Customize.cs:287` `RenameRowInteractiveAsync`、`:405` `ApplyRowRename(optionKey,label,description,allLanguages)`、`:420` `entry.Scope = allLanguages ? RenameScopes.All : RenameScopes.Current` |
| 节/分类重命名 | `:433` `RenameSectionInteractiveAsync`、`:543` `RenameCategoryInteractiveAsync` |
| 重命名作用域常量 | `Layout/SettingsLayout.cs:312` `public static class RenameScopes`、`:359` 比较 `RenameScopes.All` |
| 隐藏行/节/分类 + 恢复 | `Customize.cs:49/57` `SetHidden(option,true/false)`、`:82` `SectionHiddenChanged`、`:839` `RestoreHiddenCategories()`、`CustomizeTree.cs:92` `IsHidden = … _layout.HiddenSections.Contains(sectionId)` |
| 落盘字段 | `SettingsLayout.cs:56` `public bool Hidden { get; set; }` |

### 2.3 播放控制栏：自适应布局

| 能力 | 证据 |
|---|---|
| 分档隐藏表 | `Modules/Player/ControlBarAdaptiveLayout.cs:46` `internal static class ControlBarAdaptiveLayout`、`:88` `AllAdaptiveIds => HiddenFromTier.Keys` |
| 布局引擎 / 语法 | `ControlBarLayoutEngine.cs:14`、`ControlBarLayoutGrammar.cs:14` |
| 每次 apply 重建三根 CommandBar | `AGENTS.md:67` "`ApplyBarOrders` rebuilds three CommandBars from canonical lists on every apply" |
| 四档层级 | `ControlBarAdaptiveLayout.cs` 中 `ControlBarTier.Compact / Narrow …` |

### 2.4 播放控制栏：章节标记

| 能力 | 证据 |
|---|---|
| 进度条章节刻度 | `Modules/Player/PlayerControl.ProgressMarks.cs:109` 注释 "Draws thin tick marks on the progress bar at chapter starts" |
| 绘制实现 | 同上 `:120` `UpdateChapterMarks(bool force=false)`、`:147` `ChapterMarksCanvas.Children.Clear()`、`:154-157` 逐章建 tick |
| 章节源 | `:134` `MediaPlayer?.GetChapters() is { Count: > 0 } chapters` |
| 缓存与失效 | `PlayerControl.xaml.cs:137` `_lastChapterMarkWidth`、`:571/:1444/:2303` 三处调用点 |

### 2.5 播放控制栏：A-B 循环

| 层 | 证据 |
|---|---|
| C++/WinRT 属性读取 | `mpv-winrt/MpvPlayer.cpp:1074` `AbLoopA()`、`:1088` `AbLoopB()`（读 `ab-loop-a` / `ab-loop-b`） |
| 换片重置 | `MpvPlayer.cpp:128` `SetOption("reset-on-next-file","pause,ab-loop-a,ab-loop-b")` |
| IDL 导出 | `mpv-winrt/MpvPlayer.idl:92-93` `Double AbLoopA(); Double AbLoopB();` |
| UI 交互 | `PlayerControl.ProgressMarks.cs:28` `AbLoopButton_Click`、`:42` `ToggleAbLoop()`、`:49-50` 读 A/B |
| 设置项 | en-US.json 有 `SettingsAbLoopA/B/Count`、`SettingsHelpAbLoopA/B/Count`（如 `Languages/en-US.json:1210`） |

### 2.6 播放控制栏：动画面板

| 能力 | 证据 |
|---|---|
| 面板动画 | `Modules/Player/PlayerControl.PanelAnimation.cs`（`AGENTS.md:66` 记录了 overlay 1.5s 空闲计时） |
| 动画基础 | `Modules/Player/PlayerControl.Animation.cs` |
| 浮出面板 | `Modules/Player/PlayerControl.Flyouts.cs` |
| 浮层可见性模型 | `AGENTS.md:66` "activity-based visibility with 1.5s idle timer. ControlPanelGradient Height=120 only in overlay mode" |

### 2.7 音频 / 字幕 / 视频快捷面板

| 能力 | 证据 |
|---|---|
| 三面板 + 构建器 | `Modules/Player/QuickControlPanel.xaml`、`.xaml.cs`、`QuickControlPanel.Audio.cs`、`.Subtitles.cs`、`.Video.cs`、`.Builders.cs` |
| 轨道选择器 | `Modules/Player/PlayerTrackSelectorControl.xaml`、`.xaml.cs` |
| 音量浮出 | `Modules/Player/VolumeFlyoutControl.xaml`、`.xaml.cs` |

### 2.8 画中画

| 能力 | 证据 |
|---|---|
| 独立无边框置顶窗口 | `Shell/MainWindow_PiP.cs`、`Modules/Player/MpvPlayerPage_PiP.cs:23` `EnterPiP()`、`:69` `ExitPiP()` |
| 复用 PlayerControl | `AGENTS.md:65` "dedicated borderless always-on-top window reusing `PlayerControl`" |
| **可调大小（8 向 + 光标 + 钳制）** | `Modules/Player/PiPWindow.Resize.cs:26` `partial class PiPWindow`、`:37` `WM_NCHITTEST = 0x0084`、`:58-76` 四种 resize 光标、`:108` 命中测试、`:121` `HTBOTTOMRIGHT`、`:156-171` min/max 钳制 |
| 上次尺寸记忆 | `PiPWindow.Resize.cs` 中 `_resizeMinW/_resizeMaxW` 与持久化字段 |

### 2.9 缩略图预览（含 osc-preview-api）

| 能力 | 证据 |
|---|---|
| 进程内第二个 libmpv 实例 | `mpv-winrt/MpvPreviewer.idl` `runtimeclass MpvPreviewer`；`MpvPreviewer.cpp/.h` 均存在 |
| 预览器接口 | `MpvPreviewer.idl`：`Initialize(Image,UInt32 w,UInt32 h)`、`LoadFile`、`SetPosition`、`Pause`、`SetEdition`、`SetDiscPath`、`SetVideoTrack` |
| 原生后端（mpv 自绘） | `mpv-winrt/MpvPlayer.idl:140-141` `SetHoverSec(Double)`、`SetDrawPreview(Int32 x,y,w,h)` |
| 双后端共用一条指针管线 | `Modules/Player/MpvPlayerPage_Preview.cs` 类注释："Two rendering back ends share one pointer pipeline … Native (`EnableVideoPreview`) … Built-in (`EnableVideoBuiltInPreview`)" |
| 合并节流 | 同上：`DispatcherQueueTimer` 合并高频指针流 |
| 卡片 | `Modules/Player/PlayerControl.Preview.cs` |
| 可调尺寸/间隔 | `AGENTS.md:94` "size (180/248/320px) and update interval (40–600ms)"；`Languages/en-US.json:1266-1267` `SettingsThumbnailInterval/Size` |

**`osc-preview-api` —— 云端无实现，只有 README 一句话。**

```
$ git grep -n -i 'osc-preview' origin/main -- .
origin/main:README.md:21:the `osc-preview-api`.
```

全仓库（含 `.cs`/`.idl`/`.cpp`/`.lua`/`.conf`/`mpv-options.json`）**没有第二个命中**。云端 `README.md:20-21` 声称 "Thumbnail preview is built in and also supports plugins implementing the `osc-preview-api`"，但代码里既没有该 API 的探测，也没有插件对接路径——预览只有上文两条内置后端。**这是云端的文档缺陷，不是本地的功能缺口。** 本地 `local/main:README.md:21` 原样继承了这句话（见 §6 待清理）。

### 2.10 HDR / VSR 自动切换

| 层 | 证据 |
|---|---|
| 配置层脚本（Lua） | `mpv-winui-lazy/scripts/hdr_auto.lua`、`vsr_auto.lua`、`mpvw_hdr_override.lua` |
| 脚本选项 | `mpv-winui-lazy/script-opts/hdr_auto.conf`、`mpvw_hdr_override.conf` |
| 覆盖模式 | `mpvw_hdr_override.lua:3` "mode= 留空=跟随 App；HDR=强制 HDR；SDR=强制 SDR" |
| App 侧显示器判定 | `Modules/Player/MpvPlayerPage_Display.cs:103` `ReadColorKind()`、`:112-114` 映射 `DisplayAdvancedColorKind.HDR/WCG/SDR` |
| SDR 白点真值 | 同上 `:199` `SdrWhiteLevelInNits`、`:414` `ReadSdrWhiteNits()` |
| 强制切换 | `mpv.conf:62` `target-colorspace-hint = yes` |
| 附带 shader 包 | `mpv-winui-lazy/vs/*.vpy`（RIFE/MEMC/SR/去交错等 20+ 个） |

### 2.11 封面艺术

| 能力 | 证据 |
|---|---|
| Lua 脚本 | `mpv-winui-lazy/scripts/coverart.lua` + `script-opts/coverart.conf` |
| mpv 内嵌封面 | `mpv.conf:103` `audio-display = embedded-first` |
| 字幕/元数据 OSD | `scripts/metadata_osd.lua` |

### 2.12 最近文件

| 能力 | 证据 |
|---|---|
| 脚本 | `mpv-winui-lazy/scripts/recentmenu.lua` |
| 观看历史 UI | `Modules/Player/History/WatchHistoryControl.xaml`、`WatchHistoryItem.cs`、`WatchHistoryParser.cs` |
| 稍后观看 UI | `Modules/Player/History/WatchLaterControl.xaml`、`WatchLaterItem.cs`、`WatchLaterParser.cs` |
| 行为分片 | `Modules/Player/MpvPlayerPage_History.cs` |
| 断点续播 | `mpv.conf:54` `save-position-on-quit = yes`、`:104` `resume-playback`、`:98` `watch-later-dir` |

### 2.13 播放统计

| 能力 | 证据 |
|---|---|
| `stats.lua` | `mpv-winui-lazy/scripts/stats.lua` + `script-opts/stats.conf` |
| MediaInfo 版统计 | `scripts/stats_mediainfo.lua` + `script-opts/stats_mediainfo.conf`（`:61` 指向 `~~/tools/MediaInfo.exe`） |
| 内置 OSD 统计被关闭 | `mpv.conf:196` `load-stats-overlay = no`（注释：避免与现代化 UI 冲突） |

### 2.14 控制台输出

**云端无「App 内控制台窗口」。** 云端只有 mpv 侧的 console 脚本与它的样式配置：

| 证据 | 内容 |
|---|---|
| `mpv-winui-lazy/scripts/console.lua` | mpv 官方 console 脚本 |
| `mpv-winui-lazy/script-opts/console.conf`、`script-opts.conf:10-18` | 字体/边距/历史去重等样式项 |
| `mpv.conf:199` | `load-console = no` —— 内置 console **被显式禁用**，改用上面那份 `scripts/console.lua` |

云端**没有任何** `ConsoleWindow` / `ShowConsole` / `open_console` 的 C# 或 XAML：

```
$ git grep -n -i -E 'ConsoleWindow|ShowConsole|open_console' origin/main -- 'mpv-winui/mpv-winui/**/*.cs' 'mpv-winui/mpv-winui/**/*.xaml'
（无输出）
```

控制台靠 mpv 自绘呈现，不是 Windows 原生窗口。见 §3 判定与 §4 的"被误解的云端功能"。

### 2.15 菜单与菜单编辑器

| 能力 | 证据 |
|---|---|
| 菜单数据通道 | `Modules/Player/MpvPlayerPage_MpvMenu.cs`（从 `dyn_menu.lua` 的 `menu-data` 属性构建）；`AGENTS.md:82` |
| 菜单定义/来源/快捷键提示 | `Modules/Player/Menu/MenuDefinition.cs`、`MenuDefinitionSource.cs`、`MenuShortcutHints.cs`、`MenuBarBuilder.cs` |
| 菜单栏服务 | `Modules/Menu/MenuBar/MenuBarService.cs` |
| 菜单编辑器（树形 + 拖拽 + 预览 + 写回） | `Modules/Menu/MenuEditor/MenuEditorPage.xaml`、`MenuEditorPage_Drag.cs`、`_Preview.cs`、`_TreeView.cs`、`_Conf.cs`、`MenuEditorWindow.xaml`、`MenuTreeItem.cs`、`MenuType.cs` |
| 编辑器操作 | `MenuEditorPage.xaml:39` `"Delete"`、`:48` `"Delete children"`、`:110-111` `Preview`、`:331-341` `PreviewStates`（`PreviewMenubar` / `PreviewMpvMenu` 双预览） |
| `.conf` 解析/写回 | `Modules/Menu/MpvMenu/MenuConfParser.cs`、`MenuConfWriter.cs`、`MpvMenuItem.cs` |
| 右键菜单渲染限制（云端自述） | `AGENTS.md:84` "No icons, no filtering, no renames — plain text rendering"；`AGENTS.md:85` `HiddenMenuTitles` 已移除 |

### 2.16 文件关联

| 能力 | 证据 |
|---|---|
| 官方 WindowsAppSDK 关联 API（非手写注册表） | `Modules/Activation/ActivationRegistrationService.cs:12-14` 注释 "through the official WindowsAppSDK API (ActivationRegistrationManager) instead of hand-written registry keys" |
| 注册/注销文件类型 | 同文件 `:63` `RegisterForFileTypeActivation(...)`、`:89` `UnregisterForFileTypeActivation(...)` |
| 注册/注销协议 | `:115` `RegisterForProtocolActivation(...)`、`:139` `UnregisterForProtocolActivation(...)` |
| shell 命令自检修复 | `:504` `HasUsableShellCommand()`、`:508` `RepairShellCommand()` |
| 设置页入口 | `Modules/Settings/SettingsPage.Actions.cs:469` `ActivationRegistrationService.Instance`、`:562` `UnregisterAsync(extensions)`、`:565` `UnregisterProtocolAsync("mpv-winui")` |
| 打包清单声明 | `Package.appxmanifest` |

**注意：云端有新旧两套同名实现。** 见 §4.1。

### 2.17 媒体信息

| 能力 | 证据 |
|---|---|
| 原生 MediaInfo 封装 | `Modules/MediaInfo/NativeMediaInfo.cs`、`MediaInfoReport.cs` |
| 控件/窗口 | `MediaInfoUserControl.xaml(.cs)`、`MediaInfoWindow.xaml(.cs)` |
| 事件 | `mpv-winrt/Events/MediaInfoChangedEventArgs.idl` |
| 外部工具 | `mpv-winui-lazy/tools/MediaInfo.exe` |

### 2.18 截屏

| 能力 | 证据 |
|---|---|
| 一键截屏 | `Modules/Player/MpvPlayerPage.xaml.cs:329` `TopBarScreenshot_Click`、`:331` `SendMpvCommand("screenshot")` |
| 无字幕截屏 | `MpvPlayerPage_MenuBar.cs:58-59` `RunCommandAsync(["screenshot"])`、`RunCommandAsync(["screenshot","video"])` |
| 菜单项定义 | `MpvPlayerPage.xaml:188-193` `Tag="screenshot"` / `Tag="screenshot-no-sub"` |
| 快捷键与 `#menu:` 标注 | `mpv-winui-lazy/input.conf`：`screenshot scaled+subtitles` / `window` / `video` 三档 |
| 设置项 | `Modules/Settings/SettingsPage.Options.Screenshot.cs`；`mpv.conf:270-287` 格式/质量/模板 |
| 相关 IDL | `MpvPlayer.idl` 截屏模式枚举 |

### 2.19 拖放

| 能力 | 证据 |
|---|---|
| XAML 挂点 | `Modules/Player/MpvPlayerPage.xaml:466` `AllowDrop="True"`、`:469` `DragOver="OnDragOver"` |
| 处理实现 | `Modules/Player/MpvPlayerPage_Drop.cs:40` `OnDragOver`、`:61` `GetStorageItemsAsync()` |
| 启动参数路径 | `Modules/Player/MpvPlayerPage_Open.cs:91` `package.GetStorageItemsAsync()` |

### 2.20 导航与状态栏

**导航**：云端只有一处 `NavigationView`，且仅用于设置页内部：

```
$ git grep -c 'NavigationView' origin/main -- 'mpv-winui/mpv-winui/**/*.xaml'
origin/main:mpv-winui/mpv-winui/Modules/Settings/SettingsPage.xaml:11
```

播放页没有导航面板——播放列表是**固定列**（见下），主窗口没有导航骨架。

**状态栏：云端无。**

```
$ git grep -n -i 'statusbar' origin/main -- 'mpv-winui/mpv-winui/**' 'mpv-winui/mpv-winrt/**'
（无输出）
```

### 2.21 主题 / 皮肤

| 能力 | 证据 |
|---|---|
| 三段主题（跟随系统/亮/暗） | `Modules/Common/View/WindowStyleManager.cs:94` `ThemeType_Dark => ElementTheme.Dark`、`:95` `ThemeType_Light`、`:96` `_ => App.Current.RequestedTheme …` |
| 标题栏配色联动 | 同文件 `:148` `titleBar.ButtonForegroundColor = …` |
| 主题资源取值 | `Modules/Common/View/ThemeResource.cs:15` `element.ActualTheme == ElementTheme.Dark ? "Dark" : "Light"` |
| 播放器浮层强制暗色 | `Modules/Player/PiPWindow.xaml.cs:147`、`PlayerControl.PanelAnimation.cs:144` |
| 资源字典 | `Styles/Styles.xaml`、`Themes/Generic.xaml` |

**云端无换肤 / 无自定义强调色 / 无主题包机制**——只有跟随系统的亮暗两态。"皮肤"在云端不存在。

---

## 3. 差异矩阵（核心）

> 判定只取六值之一。证据列省略公共前缀 `mpv-winui/mpv-winui/`。

| # | 功能域 | 云端已有实现（证据） | 本地现状（证据） | 判定 | 理由 |
|---|---|---|---|---|---|
| 1 | 设置卡片/文件夹 | `Controls/SectionCardItem.cs:20`；`Layout/SettingsSections.cs` | `SectionCardItem.cs:20`（同签名，去掉 `INotifyPropertyChanged`）；`SettingsSections.cs` +122/-20 | 需适配 | 模型同源，但本地给 `SettingsSections` 加了 LibraryFolders 等新节（`SettingsSections.cs:91`），直接覆盖会丢节且 `sectionMap` fail-loud 崩在启动 |
| 2 | 设置行重排/重命名/隐藏 | `SettingsPage.Customize.cs:61/68/818/821`、`:287/405`、`:49/57/839` | 同文件 `:148/150`、`:324/443`、存在（+153/-106） | 两边一致 | 能力集合相同，本地只是重排了行号并扩了 47 行；无需移植 |
| 3 | 控制栏-自适应 | `ControlBarAdaptiveLayout.cs:46/88`；`ControlBarLayoutEngine.cs:14`；`ControlBarLayoutGrammar.cs:14` | 引擎/语法**逐字节一致**；`ControlBarAdaptiveLayout.cs` +125/-19 | 本地扩展 | 结构未动，只重写了分档表：云端在窄档丢掉 prev/next/tracks/aspect/fullscreen，本地让 pitch 让位、仅四类依次降级 |
| 4 | 控制栏-章节标记 | `PlayerControl.ProgressMarks.cs:109/120/134/147` | 同文件，无差异 | 两边一致 | 文件未改，章节 tick 与缓存逻辑原样保留 |
| 5 | 控制栏-A-B 循环 | `MpvPlayer.cpp:1074/1088`；`MpvPlayer.idl:92-93`；`ProgressMarks.cs:28/42` | 同上，`ProgressMarks.cs` 与 idl 的 A-B 成员无删除 | 两边一致 | C++ 属性读取 + UI 切换 + 设置项三层齐全，本地未触及 |
| 6 | 控制栏-动画面板 | `PlayerControl.PanelAnimation.cs`；`Animation.cs`；`PlayerControl.Flyouts.cs` | `PanelAnimation.cs` 被大幅替换（-40/+4 行）→ 移到别处；`Animation.cs` +246/-174；新增 `Modules/Motion/*` 5 文件 | 本地扩展 | 本地把动效抽成独立子系统（`MotionTokens`/`MotionEasing`/`SpringMath`/`MotionGate`/`MotionBehaviors`），面板动画改由 token 驱动 |
| 7 | 音频/字幕/视频快捷面板 | `QuickControlPanel.{xaml,xaml.cs,Audio,Subtitles,Video,Builders}.cs`；`PlayerTrackSelectorControl` | 六个文件全在，改动极小（`Audio` 2/2、`Subtitles` 3/3、`Video` 1/1、`Builders` 24/11） | 两边一致 | 面板主体未重写；`Builders` 的 24/11 是锚点/图标类微调 |
| 8 | 画中画 | `MpvPlayerPage_PiP.cs:23/69`；`PiPWindow.Resize.cs:26/37/108/121` | `MpvPlayerPage_PiP.cs` **逐字节一致**；`PiPWindow.Resize.cs` 未被修改（+9/-4 `Overlay`、+10 `xaml.cs`） | 需适配 | 云端 `PiPWindow.Resize.cs` 是独立 partial；本地 `PiPWindow.xaml.cs` 又加了 10 行，合并须确认 partial 边界未重叠 |
| 9 | 缩略图预览 + osc-preview-api | `MpvPreviewer.idl/.cpp/.h`；`MpvPlayer.idl:140-141` `SetHoverSec/SetDrawPreview`；`MpvPlayerPage_Preview.cs` | `MpvPreviewer.*` 与 `MpvPlayerPage_Preview.cs`、`PlayerControl.Preview.cs` **全部逐字节一致**；`SetHoverSec/SetDrawPreview` 仍在 `MpvPlayer.idl:140-141` | 两边一致 | 双后端预览完整保留。**osc-preview-api 云端本就无实现**（`git grep` 仅命中 `README.md:21`），不构成差距 |
| 10 | HDR/VSR 自动切换 | `scripts/hdr_auto.lua`、`vsr_auto.lua`、`mpvw_hdr_override.lua`；`MpvPlayerPage_Display.cs:103/112` | 三个 `.lua` **逐字节一致**；`Display.cs` +112/-22，新增 `:385-414` 按 `SdrWhiteLevelInNits` 真值写入 | 本地扩展 | 配置层原样继承；本地把"参考白点"从配置层写死改为按显示器实测写入（`profiles.conf` 已删除 `hdr-reference-white`/`hdr-peak-percentile`） |
| 11 | 封面艺术 | `scripts/coverart.lua`（逐字节一致）；`mpv.conf:103` `audio-display=embedded-first` | 新增 `Modules/Player/Audio/{AlbumColorExtractor,ArtworkPalette}.cs`；`MpvPlayer.idl` 新增 `TryGetCoverArt`/`TryGetCoverArtForPath`/`GetCoverArtMimeType`/`GetMediaTags` | 本地扩展 | 云端只有 mpv 内嵌显示；本地从 MediaInfo 取原始 JPEG/PNG 字节做专辑页配色（IDL 注释解释为何不用 `screenshot-raw`） |
| 12 | 最近文件 | `scripts/recentmenu.lua`（逐字节一致）；`Modules/Player/History/*`；`MpvPlayerPage_History.cs` | `History/*` 仅两处 2/2、1/1 微调；新增 `MpvPlayerPage_Library.cs`(2931 行)、`LibraryEntry.cs` | 本地扩展 | 历史/稍后观看保留；本地另建本地媒体库与索引字母导航 |
| 13 | 播放统计 | `scripts/stats.lua`、`stats_mediainfo.lua`、`script-opts/stats*.conf` | 三个文件逐字节一致 | 两边一致 | 统计完全靠脚本与 `script-opts`，本地未改 |
| 14 | 控制台输出 | `scripts/console.lua` + `console.conf`；`mpv.conf:199` `load-console=no` | 同级文件逐字节一致；同样 `load-console=no` | 两边一致 | **云端无 App 内控制台窗口**（grep 无命中），两端都只有 mpv 自绘控制台；无功能可移植 |
| 15 | 菜单与菜单编辑器 | `Modules/Menu/MenuEditor/*`（8 文件）、`Menu/MpvMenu/*`、`Player/Menu/*` | `Modules/Menu/**` **整体零差异**（`git diff --stat` 无输出） | 本地扩展 | 编辑器一字未动；本地重写了右键菜单本体：`MpvPlayerPage_MpvMenu.cs` +51/-97，把硬编码英文标题换成 `AppLang`（云端该文件有 `Text = "File"`/`"Open File"` 等 20+ 处字面量） |
| 16 | 文件关联 | `Modules/Activation/ActivationRegistrationService.cs:63/89/115/139`（官方 API）；另有**孤儿目录** `Modules/ActivationRegistration/*`（6 文件，无外部引用） | 仅保留官方 API 版并扩到 13959 字节（云端 12210）；新增 `Modules/Activation/MpvProtocolUri.cs`；删除孤儿目录 | 本地扩展 | 云端两份同名实现，旧的那份是死代码；本地删掉死代码、保留并加强现役那份 |
| 17 | 媒体信息 | `Modules/MediaInfo/{NativeMediaInfo,MediaInfoReport,MediaInfoUserControl,MediaInfoWindow}` | 四个文件全在，仅 `MediaInfoUserControl.xaml` +14/-3 | 两边一致 | 原生 MediaInfo 封装与窗口未重写 |
| 18 | 截屏 | `MpvPlayerPage.xaml.cs:329/331`；`MpvPlayerPage_MenuBar.cs:58-59`；`input.conf` 三档 | 云端顶栏入口改为 `MpvPlayerPage_PlaylistPanel.cs:171` 菜单项调用 `:193 TakeScreenshot(mode)` → `:221 RunCommandAsync(["screenshot-to-file", path, mode])` | 本地扩展 | 本地落盘自选路径（`screenshot-to-file`）而非交给 mpv 默认目录；`screenshot-raw` 在本机不可用已在 `MpvPlayerPage_Music.cs:261` 注明 |
| 19 | 拖放 | `MpvPlayerPage.xaml:466/469`；`MpvPlayerPage_Drop.cs:40/61` | `MpvPlayerPage.xaml:680/683`；`MpvPlayerPage_Drop.cs` +222/-35，新增 `InspectDropAsync`(`:194`) 与 `DropVerdict` | 本地扩展 | 云端 `DragOver` 每次重新枚举；本地改为判定一次并缓存结论（`:45`、`:74` 注释说明原因） |
| 20 | 导航与状态栏 | 仅 `Settings/SettingsPage.xaml` 一处 `NavigationView`(11 次)；**状态栏云端无** | `NavigationView` 扩到 4 文件（`SettingsPage.xaml` 14 次、`SettingsWindow.xaml` 2、`Shell/MainWindow.xaml` 23、`Styles/Styles.xaml` 2）；新增 `MpvPlayerPage_PlaylistPanel.cs` 浮出式播放列表 | 本地扩展 | 云端播放列表是固定 280–420px 列（`MpvPlayerPage.xaml:448-459` `PlaylistResizeGrip`），本地改为覆盖式面板，`MpvPlayerPage.xaml:2438` 注释确认"the playlist column ... no longer exists" |
| 21 | 主题/皮肤 | `WindowStyleManager.cs:94-96/148`；`ThemeResource.cs:15`；`Styles/Styles.xaml`；`Themes/Generic.xaml` | 同一套三段主题，`WindowStyleManager.cs` 位置行号后移 8 行 | 两边一致 | 云端**本来就没有换肤**；本地也未新增皮肤机制，只沿用亮/暗/跟随系统 |
| 22 | 本地化 | 9 语言 × 1374 键 | 9 语言基准 1526 键，`zh-CN` 2250 键；`check-localization.py` 已具备感知生成 partial 的能力 | 本地扩展 | 本地多出 `Schema*` 724 键且集中在 zh-CN（见 §6 待补充） |
| 23 | 构建部署 | `build.ps1`；`installer/product.wxs`；`.github/workflows/build.yml` | `build.ps1` +62/-6；`product.wxs` +41/-7（排除 `*.pdb`）；`build.yml` +92/-14 新增 8 步 | 本地扩展 | 云端 CI 无 XAML/生成物/事件接线检查；本地补了 4 个 CI 校验步 + 符号泄漏修复 |
| 24 | 工具链检查器 | **16 个**（`git ls-tree origin/main -- tools/` 全列）：`check-conf-lines`、`check-conflict-rules`、`check-empty-sections`、`check-localization`、`check-mpvconf-precedence`、`check-placeholders`、`check-search-index`、`check-sections`、`check-settings-drift`、**`check-ui-tooltips`**、**`check-xaml`**、`gen-mpv-options`、`generate-settings-category-map`、`report-missing-options`、`fetch-mpv-libs.ps1`、`menus.schema.json` | 16 个全部保留；仅删 `generate-settings-category-map.py`；新增 **21 个** | 本地扩展 | 检查器**纪律是继承的**（云端已有 11 个 `check-*.py`，含 `check-xaml.py`，不是本地发明）。本地的增量是把覆盖面对齐新的跨语言契约：`check-winrt-events`、`check-script-opts`、`check-resources`、`check-visual-state-layout`、`check-section-icons`、`check-scroll-maxwidth`，加生成链路 `gen-schema-options`、`apply-hand-labels`、`gen-fluent-icons`，加部署验证 `deploy-test.{ps1,sh}`、`compare-deploy.ps1` |
| 25 | 音乐/媒体库子系统 | 云端无（`git ls-tree origin/main -- mpv-winui/mpv-winui/Modules/Player/Audio` 输出为空） | 新增 **13** 个文件（`Modules/` 下 41 个新文件中最大的一簇）：`MpvPlayerPage_Library.cs`(2931)、`MpvPlayerPage_Music.cs`(652)、`LyricsLoader.cs`(389)、`LibraryEntry.cs`(326)、`SpectrumAnalyzer.cs`(259)、`AlbumColorExtractor.cs`(241)、`AudioCapture.cs`(235)、`LibraryIndexLetter.cs`(198)、`PinyinInitial.cs`(155)、`ArtworkPalette.cs`(131)、`LibraryEntry.Selection.cs`(93)、`LibraryTagsReader.cs`(82)、`MusicTags.cs` 等 | 本地扩展 | 纯增量，云端无对应实现 |
| 26 | 设置 schema 生成层 | 无：`AppSettings.Gen.cs` / `SettingsPage.Options.Gen.cs` 均不存在；选项靠手写 partial | `AppSettings.Gen.cs`(2202)、`MpvSettings.Gen.cs`(381)、`SettingsPage.Options.Gen.cs`(4787)、`SettingsPage.Sections.Gen.cs`(510)、`AppLang.Gen.cs`、`MpvConfValueCodec.cs`(323) | 本地扩展 | 选项行 317（云端手写）→ **682**（本地 = 362 生成 + 320 手写，交集 0）。注意本地文档写的 548/549/874/1098 均为旧数，见 §4.3 |
| 27 | `mpv.conf` 运行时双向同步 | 只有启动时 `LoadFromConfig`（`ManagedMpvConfig.cs` 类注释） | 新增 `Modules/Settings/MpvConfWatcher.cs`(242)，debounce 重读并刷新 `MpvConfOverrides`，带 generation 计数防写-读回环 | 本地扩展 | 云端"手改 mpv.conf"只在启动生效；本地补了运行期反向同步 |

**判定分布**：`本地扩展` 12 项、`两边一致` 9 项、`需适配` 2 项、`本地已有更优` 0 项（并入"本地扩展"）、`可直接移植` 0 项、`云端已废弃` 0 项。

---

## 4. 被忽略的云端功能 / 重复造轮子

**结论先行：没有发现"把云端已有实现重写得更差"的情况。** 88 个新增文件里 41 个是云端完全没有的能力（音乐库、动效、schema 生成），47 个是生成物/测试/工具/文档。11 个删除逐个核对如下，**无一丢失能力**。

### 4.1 11 个删除文件的逐个交代

| 删除文件 | 云端是否有引用 | 本地替代 | 判定 |
|---|---|---|---|
| `Modules/ActivationRegistration/ActivationRegistrationService.cs` | 无外部引用 | `Modules/Activation/ActivationRegistrationService.cs`（12210→13959 字节） | 云端已废弃（死代码） |
| `Modules/ActivationRegistration/FileAssociationControl.xaml(.cs)` | 仅 `SettingsPage.xaml:5` 的 `xmlns:ar=` 声明 | 文件关联改走 `SettingsPage.Actions.cs:469/562` | 云端已废弃 |
| `Modules/ActivationRegistration/FileAssociationItem.cs` | 同上 | — | 云端已废弃 |
| `Modules/ActivationRegistration/ProtocolAssociationControl.xaml(.cs)` | 同上 | `MpvProtocolUri.cs` 承接 URI 解析 | 云端已废弃 |
| `Modules/Common/Utils/Win32CommandLineParser.cs` | **无任何引用** | `ActivationService.cs:54` 改用 `Environment.GetCommandLineArgs()` | 云端已废弃 |
| `Modules/Player/ResizeGrip.xaml(.cs)` | 类型从未被实例化 | `PiPWindow.Resize.cs:37/108/121` 用 `WM_NCHITTEST` 实现 | 云端已废弃 |
| `Modules/Settings/Controls/OptionGroup.cs` | **仅自身定义，零引用** | 无（不需要） | 云端已废弃 |

关键反证命令：

```
$ git grep -n -E 'ActivationRegistration|FileAssociationControl|ProtocolAssociationControl' origin/main -- 'mpv-winui/mpv-winui/**' | 排除 Modules/ActivationRegistration/
→ 仅命中 Modules/Activation/ActivationRegistrationService.cs（现役那份）与 SettingsPage.xaml:5 的 xmlns:ar 声明
$ git show origin/main:.../SettingsPage.xaml | Select-String 'ar:'
→ 无输出（命名空间声明了但从未使用）
$ git grep -n 'Win32CommandLineParser' origin/main -- 'mpv-winui/mpv-winui/**' | 排除自身
→ 无输出
$ git grep -n -E '<(local:)?ResizeGrip[ />]' origin/main -- 'mpv-winui/mpv-winui/**/*.xaml'
→ 无输出（ResizeGrip 类型从未实例化；PlaylistResizeGrip 是另一个 Border 元素）
$ git grep -n 'OptionGroup' origin/main -- 'mpv-winui/mpv-winui/**'
→ 仅 Controls/OptionGroup.cs:7 自身
```

本地还顺手清掉了 `SettingsPage.xaml` 的 `xmlns:ar=`（`local/main` 第 5 行已是 `xmlns:ctl=`）。

### 4.2 逐子系统"是否重写得更差"

| 云端能力 | 本地是否重写 | 证据 | 结论 |
|---|---|---|---|
| 缩略图预览（双后端） | **否** | `MpvPreviewer.cpp/.h/.idl`、`PlayerControl.Preview.cs`、`MpvPlayerPage_Preview.cs` 全无 diff | 原样继承，未退化 |
| 播放列表列 + 拖拽改宽 | 是 | 云端 `MpvPlayerPage.xaml:448-459` 固定列 + `PlaylistResizeGrip`；本地改覆盖面板 | **不是变差**：`MpvPlayerPage_PlaylistPanel.cs` 类注释给出理由（"not a column that permanently takes width away from the video"），且 `MpvPlayerPage.xaml:2438` 明确记录旧列的 setter 已被 `check-visual-state-layout.py` 禁止。但**"改宽"这一能力本地方案不再提供**，属功能取舍，需产品确认（见 §6） |
| 右键菜单 | 是 | 云端 `MpvPlayerPage_MpvMenu.cs` 20+ 处 `Text = "File"` 等字面量；本地零字面量，14 个 `Menu*` 键 | **更优**：云端硬编码英文，9 语言形同虚设 |
| 顶栏截屏 | 是 | 云端 `SendMpvCommand("screenshot")`；本地 `screenshot-to-file` + 自选路径 | 更优（可控落盘位置） |
| 拖放 | 是 | 云端 `OnDragOver` 每次重枚举；本地 `InspectDropAsync` 缓存判定 | 更优（`_Drop.cs:74` 注释说明重复枚举问题） |
| PiP 可调大小 | 否 | `PiPWindow.Resize.cs` 未改 | 保留 |
| 设置自定义（重排/重命名/隐藏） | 否 | 能力集合一致，仅行号位移 | 保留 |
| 文件关联 | 是（删死代码） | 见 §4.1 | 更优（去重） |

### 4.3 生成层与手写层是否重复

担心"新增 4787 行生成选项 = 把云端手写选项抄一遍"。实测否定：

```
$ 本地 SettingsPage.Options.Gen.cs 中 `new Option` 构造数        362
$ 本地 SettingsPage.Options.Gen.cs 中 `Section =` 赋值数          362   （一一对应）
$ 本地手写 partials 中 Key = nameof(...) 条目数                   320
$ 两者交集（重复键）                                                0
$ 去重后总数                                                      682
$ AppLang.Gen.cs 中 SchemaLabel_ / SchemaDesc_ 属性数        362 / 362
```

零重复。生成层是**净增**能力，不是复制。

**顺带发现：本地文档与 `AGENTS.md` 的数字全部过期。** 实测与记载对照：

| 出处 | 记载 | 实测（`7b42fd5`） |
|---|---|---|
| `AGENTS.md`（本地） | "548 schema-generated rows" | **362** |
| `docs/settings-design-contract.md:24` | 874 字面量 / 871 唯一（322 手写 + 549 生成） | **682**（320 + 362） |
| `docs/settings-design-contract.md:26` | AppLang 属性 2439 | 未复算（302 键级差异，量级可疑） |
| `docs/settings-design-contract.md:27` | 9 语言各缺 **1098** 个 Schema 键 | 实为 **724**（362+362），见 §6.1 |
| `docs/settings-design-contract.md:36` | "检查器 17 个" | **36** |
| `docs/compare-upstream.md:39` | "本分支相对上游只偏离两个子系统" | 见 §4.4 |

这些数字都在 `7b42fd5` 的提交信息里被本人更新过一次（"Removed (11 options, 682 rows now)"），但 `AGENTS.md` 与 design contract 没有跟上。**`AGENTS.md` 是 agent 的入口文件，它给出的行数是错的**，属优先清理项。

### 4.4 对"只偏离两个子系统"这一说法的核验

本地 `docs/compare-upstream.md:39` 与 `docs/settings-design-contract.md:49-53` 都断言：相对 `3053352` 只偏离 ①schema 生成层 ②`MpvConfWatcher`，其余 24 个提交是缺陷修复/检查器/命名。

按 §1.3 已确认这是**更早时间点的快照**（基线 `d0a06d9`，领先 26 个提交；现在领先 116 个）。对该说法逐项核验：

| 文档声称"未偏离" | 实测 | 是否成立 |
|---|---|---|
| 架构（分类/分区 + 四张表 + 折叠） | `SettingsLayout.cs` 仅 5 行差异；`SettingsSections.cs` 类注释确与上游一致 | ✅ 成立 |
| 设置项自定义（拖动/重命名/隐藏） | `SettingsPage.Customize*.cs` 能力集合一致 | ✅ 成立 |
| schema 生成层 | 新增 6 文件约 7800 行 | ✅ 属于偏离（已承认） |
| `MpvConfWatcher` | 新增 242 行 | ✅ 属于偏离（已承认） |
| —— | `Modules/Player/Audio/*` 5 文件（频谱、封面配色、音频采集） | ❌ **未计入**：纯增量子系统 |
| —— | `Modules/Motion/*` 5 文件（动效 token/缓动/弹簧） | ❌ **未计入**：纯增量子系统 |
| —— | `MpvPlayerPage_Library.cs` 2931 行 + `LibraryEntry.cs` 326 行 + 音乐/歌词/拼音 3 文件 | ❌ **未计入**：音乐库子系统 |
| —— | `Modules/Common/View/FluentIcons.Gen.cs`、`check-winrt-events` 等 6 个新检查器 | ❌ **未计入** |

**判定：该说法已被 2026-10-19 之后的提交推翻**，至少还漏 4 个子系统。这解释了为什么本报告矩阵出现 12 项"本地扩展"而不是 2 项——不是本报告标准更严，是那份文档过期了。（该文档把"音乐库"归到"缺陷修复"是明显不成立的：`MpvPlayerPage_Library.cs` 2931 行是新增文件。）


---

## 5. `origin/upstream-merge` 与 WIP 架构提交

### 5.1 它是 main 的祖先，不是并行分支

```
$ git merge-base origin/main origin/upstream-merge
7b502ad321e447b3c655ee9fc74587f05058ed96
$ git rev-list --left-right --count origin/main...origin/upstream-merge
33      0                 # 左=仅 main：33；右=仅 upstream-merge：0
$ git merge-base --is-ancestor origin/upstream-merge origin/main
exit=0                     # 是祖先
```

`git log --oneline origin/main..origin/upstream-merge` **输出为空**。HEAD 为 `7b502ad "docs: rewrite the README for a personal fork, and record the upstream gap"`。

**结论：`upstream-merge` 已经整体进入 main，落后 33 个提交，没有任何独有工作。** 反向 diff 显示的是 main 相对它的增量（174 文件，+19765/-22095），其中 `MpvConfOptions/mpv-options.json` 被 +13520 行、`tools/*.py` 一排 -90~-409 行，都是 **main 新增**的内容在反向视角下呈现的"删除"。

### 5.2 那 33 个提交是什么

时间范围 2026-09-17 17:32 → 2026-09-19 14:37，即"重构 + 设置界面第一轮"：

| 类别 | 提交 |
|---|---|
| 架构去垫片 | `7f4db58 player: drop the wrapper-era shim and talk to the native API directly` |
| 目录重整 | `144650a reorganise: group the loose files in both projects by kind` |
| 设置界面 | `1f536f0` 生成 option schema、`e1f40dd` 统一 option card 外壳、`ad45f8e` 间距尺度、`40d37bc` 字号阶、`93bf0b4` 节身份、`1d7dfbd` 删死分类 |
| 权威模型 | `f341226` / `9fc370a` / `338c7be` mpv.conf 作为最高权威与逃生口 |
| 选项治理 | `a335c19` 新增 Advanced 分类 + 20 选项、`5e0cbcb` 字幕选项、`3053352` OSD 外观、`98d8e9c`/`a16c04f` 移除 yt-dlp |
| 工具 | `edcb3f1` XAML 良构检查、`f299569` 节目录 vs AppLang |
| 配置层 | `37d8f4c` 去内置 shader、留 VSR/RTX HDR |

### 5.3 本地是否已走另一条路

**部分重合，方向一致，取舍不同**：

| 云端 33 提交做的事 | 本地状态 |
|---|---|
| `7f4db58` 去 wrapper 垫片 | 已继承（本地 `MpvPlayer.idl` 121→125 成员，无垫片文件） |
| `1f536f0` 生成 option schema | **本地做得更远**：云端只在 `MpvConfOptions/mpv-options.json` + 编辑器侧，本地扩到 `AppSettings.Gen.cs`/`SettingsPage.Options.Gen.cs`/`SettingsPage.Sections.Gen.cs`/`MpvSettings.Gen.cs` 四层 6833 行 |
| `f341226`/`9fc370a` mpv.conf 权威 | 继承并加 `MpvConfWatcher.cs` 运行期同步 |
| `a335c19`/`5e0cbcb`/`3053352` 增选项 | 本地在 682 行规模上继续治理：`7b42fd5` 反而**砍掉 11 个"不构成决策"的选项**（`ad-lavc-o`、`vd-lavc-o`、`access-references` 等），并拆分两个 50 行大节 |
| `37d8f4c` 留 VSR/RTX HDR | 逐字节一致 |

### 5.4 该不该跟

**不该跟。** 三条硬理由：

1. **无处可跟**——它是 main 的祖先，`origin/main..origin/upstream-merge` 为空集，没有任何"合并 upstream-merge"的操作可执行；它相对 main 只有缺失。
2. **已被 116 个提交覆盖**——本地严格后继于 main，main 又后继于 `upstream-merge`，传递性上本地包含它的全部内容。
3. **它记录的"upstream gap"是错误基线**——本地 `docs/compare-upstream.md:3-8` 已实测记录：该文档 baseline 是 `ikas-mc` 原版而非 `saillill` 上游，且其中引用的 `276a7e0`/`753cabf`/`3ec602c` 在仓库里不存在（`git cat-file -t 276a7e0 → fatal: Not a valid object name`）。**这篇文档本身就是待清理对象**（见 §6）。

**唯一值得读的是它的 33 条提交信息**：作为"设置界面为什么这么设计"的决策记录，它们比代码更有信息量（例如 `e1f40dd` 明确写了"第二半（label/description/error/warning 栈）故意不做，因为这台机器 build 不了 C++/WinRT"——解释了云端为何在重构中途停手）。

---

## 6. 三张清单

### 6.1 待补充（本地缺、且判定为值得从云端取回的能力）

| 路径 / 对象 | 缺什么 | 依据 |
|---|---|---|
| `Modules/Player/PiPWindow.Resize.cs` + 新版 `PiPWindow.xaml.cs` | 云端 8 向 resize + 光标接管；本地该 partial 未改动但 `PiPWindow.xaml.cs` 已 +10 行，需确认 partial 合并边界 | §3 项 8，判定 `需适配` |
| `Layout/SettingsSections.cs` ↔ `SettingsPage.Sections.Gen.cs` | 云端新节需同步进生成链，否则 `sectionMap` 缺项会在启动时抛异常 | §3 项 1；`AGENTS.md:72` fail-loud |
| `Languages/{zh-TW,ja-JP,de-DE,es-ES,fr-FR,ko-KR,ru-RU}.json` 的 `Schema*` 键 | 724 个 schema 键（362 label + 362 desc）只有 zh-CN 有，其余 7 语言 0 个 | `grep -c '"Schema(Label\|Desc)_'` 实测：zh-CN 724；en-US/zh-TW/ja-JP/de-DE/es-ES/fr-FR/ko-KR/ru-RU 全为 0。本地 `check-localization.py` 文档字符串自承"all nine language files are missing every one of the 1098 generated keys" |
| `Modules/Player/PlaylistPanel` 的宽度可调 | 覆盖式面板不再提供拖拽改宽 | `MpvPlayerPage.xaml:2438` 注释 + `grep PlaylistWidth` 无命中 |

### 6.2 待清理

| 路径 / 对象 | 问题 | 证据 |
|---|---|---|
| `AGENTS.md`（本地，"Settings architecture" 段） | 写"548 schema-generated rows"，实测 **362**。这是 agent 入口文件，数字错会误导后续所有工作 | `git show local/main:AGENTS.md \| grep -n 548` → 命中；`grep -c 'new Option' SettingsPage.Options.Gen.cs` → 362 |
| `docs/settings-design-contract.md` | §0 速览表 6 项数字过期：行数 874/871/549（实为 682/682/362）、AppLang 2439、翻译缺口 1098（实为 724）、检查器 17（实为 36） | §4.3 对照表 |
| `docs/compare-upstream.md` | baseline 写错（是 `ikas-mc` 不是 `saillill`），引用 3 个不存在的提交；§2 结论"只偏离两个子系统"已被推翻（§4.4）；文中 §3.1 描述的文件 `7f4db58` 已删 | 该文件自带警告块 `:3-8` 已自承，但正文未同步 |
| `README.md:20-21` | 声称支持 `osc-preview-api`，全仓库无实现（本地从云端原样继承） | `git grep -i 'osc-preview'` 仅命中 README |
| `SettingsPage.Options.*` 手写 partials（10 个） | 与 `Options.Gen.cs` 并存，682 行里 320 行仍手写；生成链迁移未完成 | 362 生成键 vs 320 手写键，交集 0 |
| `tools/generate-settings-category-map.py` | 已被 `SettingsPage.Sections.Gen.cs` 生成链取代，本地已删 | `git diff --name-status` 显示 `D` |
| `tools/check-ui-tooltips.py` | 云端有、本地仍在，但 `AGENTS.md` 的工具清单未提及它；连同 `check-conf-lines`/`check-mpvconf-precedence`/`check-search-index` 一起，本地文档只列了其中一部分 | 本地 36 个工具 vs `AGENTS.md` 点名 8 个 |
| `docs/` 12 篇新增文档 | `audit-cleanup-list.md`/`fontsize-classification.md`/`spacing-classification.md` 标题均标"待确认/待你判断"，是过程稿 | 各文件首行标题 |

### 6.3 合并对齐

| 动作 | 对象 | 说明 |
|---|---|---|
| **无需 git 合并** | 全部 | `rev-list --left-right --count origin/main...local/main = 0 116`，本地严格后继；`git merge origin/main` 是 no-op |
| 语义对齐检查点 1 | `Settings/Controls/SectionCardItem.cs` | 云端 `: INotifyPropertyChanged`，本地同签名但去掉接口（+21/-35），确认绑定未因此退化 |
| 语义对齐检查点 2 | `Settings/Layout/SettingsSections.cs` | 本地 +122/-20，确认云端 9 个节本地是否全部仍在（含"删掉的 6 个无选项节"是否真该删） |
| 语义对齐检查点 3 | `SettingsPage.Customize.cs`(+153/-106) / `CustomizeTree.cs`(+40/-33) / `CustomizeShell.cs`(+26/-15) | 重排/重命名/隐藏能力一致，但三文件都改动过，确认无行为分叉（如 `StoreOrder` 的持久化格式是否兼容旧 layout 文件） |
| 语义对齐检查点 4 | `piPWindow.Resize.cs` partial 边界 | 见 6.1 第 1 条 |
| 语义对齐检查点 5 | `PlayerControl.PanelAnimation.cs`(-40/+4) | 面板动画被搬走，确认 `ControlPanelGradient`/1.5s 空闲计时行为仍在（云端 `AGENTS.md:66` 记录的行为契约） |
| 记载而非对齐 | `origin/upstream-merge` | 作为祖先保留即可，不建跟踪分支（见 §5.4） |

---

## 附录 A：证据命令速查

```bash
# 基准
git rev-parse --short HEAD
git rev-list --left-right --count origin/main...local/main          # → 0  116
git diff --name-status origin/main local/main                        # 249 项：150 M / 88 A / 11 D
git diff --shortstat origin/main local/main                          # +50876 / -6919

# 死代码判定（§4.1）
git grep -n -E 'ActivationRegistration|FileAssociationControl' origin/main -- 'mpv-winui/mpv-winui/**'
git grep -n 'Win32CommandLineParser' origin/main -- 'mpv-winui/mpv-winui/**'
git grep -n -E '<(local:)?ResizeGrip[ />]' origin/main -- 'mpv-winui/mpv-winui/**/*.xaml'
git grep -n 'OptionGroup' origin/main -- 'mpv-winui/mpv-winui/**'

# osc-preview-api 不存在（§2.9）
git grep -n -i 'osc-preview' origin/main -- .
git grep -n -i 'osc-preview' local/main -- .

# 状态栏 / 控制台窗口不存在（§2.14、§2.20）
git grep -n -i 'statusbar' origin/main -- 'mpv-winui/mpv-winui/**'
git grep -n -i -E 'ConsoleWindow|ShowConsole|open_console' origin/main -- 'mpv-winui/mpv-winui/**/*.cs' 'mpv-winui/mpv-winui/**/*.xaml'

# 生成层与手写层零重复（§4.3）
git show local/main:mpv-winui/mpv-winui/Modules/Settings/SettingsPage.Options.Gen.cs | grep -c 'new Option'
git grep -h 'Key = nameof(AppContext.AppSetting.' local/main -- 'mpv-winui/mpv-winui/Modules/Settings/SettingsPage.Options.*.cs'
#   362 生成 / 362 Section= / 320 手写 / 交集 0 / 合计 682 / Schema 属性 362+362

# 工具链清单（§2.24）
git ls-tree -r --name-only origin/main -- tools/     # 16
git ls-tree -r --name-only local/main  -- tools/     # 36（新增 21、删除 1）
git ls-tree -r --name-only origin/main -- tools/check-xaml.py   # 云端已有，非本地发明

# 本地文档数字过期（§4.3）
git show local/main:AGENTS.md | grep -n '548'
git show local/main:docs/settings-design-contract.md | sed -n '18,37p'

# upstream-merge 是祖先（§5.1）
git merge-base --is-ancestor origin/upstream-merge origin/main ; echo $?
git rev-list --left-right --count origin/main...origin/upstream-merge   # → 33  0

# 逐字节未动的文件
git diff --quiet origin/main local/main -- <path> ; echo $?
```

## 附录 B：本次审计的已知边界

1. **未运行程序**——所有"实现存在"的判断基于源码与提交信息，未经运行时验证。按 `AGENTS.md` 的规定，行为正确性由项目所有者自测；本报告不做"功能已验证"的声明。
2. **未构建、未跑检查器**——未执行 `build.ps1`，也未执行 `tools/check-*.py`。报告中没有"检查器通过"这类断言；凡引用检查器的能力，引用的是其源码里声明的检查范围，不是它的执行结果。
3. **9 语言 schema 缺口**只统计了键数量。这些键在运行时是否 fallback 到 `AppLang.Gen.cs` 的 C# 属性默认值未验证；但 `AppLang.Gen.cs:12` 显示默认值本身是英文串，故非英文界面很可能显示英文——**这需要运行时确认，本报告不结论**。
4. **`PlaylistPanel` 取消拖拽改宽**是否为产品决策，任务方未给判定标准，本报告只陈述事实并列为待确认（§6.1）。
5. **本地文档的旧数字**（548/874/1098/17 等）在本报告中一律以实测覆盖，但**未逐一追溯每个数字的来源**——只核了 6 项（§4.3 表）。其余文档中的数字（如 `settings-design-contract.md:25` 的"815/820"、`:28` 的"35 折叠卡片"）未复核。
