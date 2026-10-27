# 术语对照表

> 规则：`我的说法 | UI 实际文案 | 代码标识 | 文件:行号 | 说明`
> 代码中确实不存在的，写"当前无对应实现"，不拿最近似的顶替。
> 证据等级：【已运行】/【代码】/【推断】/【未知】

基线：`main` @ `0d0e889`。UI 文案取自 `zh-CN.json` 与运行时 UIA 读取。

---

## 一、页面

| 我说的词 | UI 实际文案 | 代码标识 | 位置 | 说明 |
|---|---|---|---|---|
| 主页 | 主页 | `LibrarySectionHome = "home"`；`ShellNav_home` | `MpvPlayerPage_Library.cs:57`；`MainWindow.xaml.cs:187` | 不是独立页面，是播放页的一个 section |
| 音乐库 | 音乐库 | `LibrarySectionMusic = "music"`；`ShellNav_music` | `MpvPlayerPage_Library.cs:59`；`MainWindow.xaml.cs:188` | 同上；内部含专辑/艺术家/歌曲三种视图 |
| 视频库 | 视频库 | `LibrarySectionVideos = "videos"`；`ShellNav_videos` | `MpvPlayerPage_Library.cs:61`；`MainWindow.xaml.cs:189` | 同上；含文件夹视图 |
| 播放列表 | — | `LibrarySectionPlaylists = "playlists"` | `MpvPlayerPage_Library.cs:70` | **当前无 UI 入口**：常量与 switch 分支都在，导航里没有它 |
| 播放队列 | 播放队列 | `LibrarySectionQueue = "queue"`；`ShellNav_queue`；`QueueEmptyTitle` | `MpvPlayerPage_Library.cs:68`；`MainWindow.xaml.cs:190` | 有导航入口 |
| 播放页 | — | `MpvPlayerPage`（`Page`，2418 行 XAML） | `Modules/Player/MpvPlayerPage.xaml` | 唯一 Page，媒体库与播放器**合体**在同一页 |
| 设置页 | 设置 | `SettingsPage` / `SettingsWindow` | `Modules/Settings/SettingsPage.xaml`；`ShellSettingsButton` | 独立 Window 内承载 Page |
| 主窗口 | — | `MainWindow` | `Shell/MainWindow.xaml` | 外壳 |
| （画中画） | 画中画（迷你播放器） | `PiPWindow`；`PiPButton` | `Modules/Player/PiPWindow.xaml` | UIA 名称已确认 |

---

## 二、区域

| 我说的词 | UI 实际文案 | 代码标识 | 位置 | 说明 |
|---|---|---|---|---|
| 导航栏 / 侧栏 | — | `ShellNav`（`NavigationView`）、`ShellNavPaneSurface` | `Shell/MainWindow.xaml:160`；`:167` | 同一物。`PaneDisplayMode="Auto"`：宽窗完整面板 / 窄窗 48px 图标栏 / 更窄时浮层 |
| 导航栏折叠按钮 | — | `ShellPaneToggleButton` | `MainWindow.xaml`；逻辑在 `UpdateShellPaneChrome` | 自绘，不是框架自带的 |
| 状态栏 / 播放条 | — | `ShellPlayerBar`、`ShellPlayerBarSurface`、`ShellBarRow`、`ShellBarSolid` | `Shell/MainWindow.xaml` | 底部播放控制条；全窗口模式下整行搬到 Row 1 |
| 悬浮栏 / 控制条 | — | `PlayerControl`（1052 行）+ `CenterPlayerControlStyle` / `CompactPlayerControlStyle` | `Modules/Player/PlayerControl.xaml` | 播放器内部的浮动控制条，有 1.5s 空闲隐藏 |
| 标题栏 | — | `ShellTitleBar`、`ShellTitleText`、`TitleBarRow` | `Shell/MainWindow.xaml` | 含返回、标题、搜索、设置 |
| 搜索框 | 搜索 | `ShellLibrarySearch` | `Shell/MainWindow.xaml`；UIA 已确认 | 全局媒体库搜索 |
| 卡片 | — | `AlbumCard*` / `Card*` / `SongCard*` 前缀一族 | `MpvPlayerPage.xaml` | 无统一"Card"基类，各视图各有一套 x:Name |
| 封面 | — | `AlbumCardCoverImage`、`SongCardCoverImage`、`AlbumArtwork`、`FolderCoverImage`、`AlbumArtworkFallbackIcon` | `MpvPlayerPage.xaml` | 各自独立 Image 元素 |
| 菜单（右键） | — | `PlaylistContextMenu`（MenuFlyout）+ `dyn_menu.lua` 的 `menu-data` | `MpvPlayerPage.xaml:16` | 由 mpv 侧数据构建，不是 C# 静态菜单 |
| 菜单（三个点） | — | `CardMoreButton`、`AlbumCardMoreButton` | `MpvPlayerPage.xaml` | 卡片右上角 |
| 侧面板（播放列表面板） | — | `PlaylistPanelButton`、`PlaylistPanelPopup`、`PlaylistPanelSurface`、`PlaylistPanelView`、`PlaylistPanelCloseButton` | `MpvPlayerPage.xaml:1010,2411,2415,2452,2438` | **不是常驻侧栏**，是 Popup |
| 字母栏 | — | `LibraryIndexRail` + `IndexRailHover.cs` | `MpvPlayerPage.xaml`；`Modules/Player/IndexRailHover.cs`（94 行） | 音乐库有，视频库已移除（`db34c78`） |
| 选择栏 | — | `LibrarySelectionBar`、`LibrarySelectionCount`、`LibrarySelectionClearText` | `MpvPlayerPage.xaml` | 多选模式下的批量操作条 |

---

## 三、控件

| 我说的词 | UI 实际文案 | 代码标识 | 位置 | 说明 |
|---|---|---|---|---|
| 播放按钮 | 播放 | `PlayPauseButton` | UIA 已确认 | — |
| 上一项 / 下一项 | 上一项 / 下一项 | `PreviousTrackButton` / `NextTrackButton` | UIA 已确认 | — |
| 快退 / 快进 | 后退 / 前进 | `SkipBackwardButton` / `SkipForwardButton` | UIA 已确认 | 文案是"后退/前进"不是"快退/快进" |
| 循环模式 | 单曲循环（动态） | `ShuffleButton` | UIA 已确认 | 复用同一个按钮循环四种模式，**名字与功能不符** |
| 控制面板 | 控制面板 | `ControlPanelButton` | UIA 已确认 | 打开 QuickControlPanel |
| 轨道 | 轨道 | `TrackSelectionButton` | UIA 已确认 | PlayerTrackSelectorControl |
| 缩放 / 画中画 / 全窗口 / 全屏 | 缩放 / 画中画（迷你播放器）/ 全窗口 / 全屏 | `ZoomButton` / `PiPButton` / `FullWindowButton` / `FullScreenButton` | UIA 已确认 | — |
| 静音 / 音量 | 静音 | `VolumeMuteButton` | UIA 已确认 | 音量条是 `VolumeFlyoutControl` |
| 菜单按钮（三个点） | — | `CardMoreButton`、`AlbumCardMoreButton`、`MoreButton` | `MpvPlayerPage.xaml`；`PlayerControl.xaml` | 曾用错字体 glyph（`9a6096d` 修） |
| 字母排序 / 字母栏 | — | `LibraryIndexRail` | 见上 | "字母排序"不是独立控件，是 Rail + 索引算法 |
| 排序下拉框 | — | **不是下拉框**：`SortByNameAsc`、`SortByNameDesc`、`SortByNewest`、`SortByOldest`、`VideoSortButton` | `MpvPlayerPage.xaml` | 一组按钮 + 一个弹出菜单，`x:Name` 里没有 ComboBox |
| 文件夹卡片 | — | `VideoFolderGroups`、`FolderCoverImage` | `MpvPlayerPage.xaml` | 视频库文件夹视图 |
| 专辑卡片 | — | `AlbumCardCoverImage`、`AlbumCardTitleText`、`AlbumCardArtistText`、`AlbumCardPlayButton`、`AlbumCardHoverOverlay`、`AlbumCardSelectionBox` | `MpvPlayerPage.xaml` | — |
| 艺术家卡片 | — | `MusicArtist`、`AlbumCardArtistText` | `MpvPlayerPage.xaml` | 艺术家视图复用专辑卡片模板 |
| 打开文件按钮 | 打开文件 | `ShellOpenFileButton`、`LibraryOpenFileButton`、`VideoAddFolderButton` | `MainWindow.xaml`；`MpvPlayerPage.xaml` | **三个并存**，按 section 显隐；`ShellOpenFileButton` 现恒隐藏 |
| 设置按钮 | 设置 | `ShellSettingsButton`、`ShellSettingsText` | UIA 已确认 | — |
| 返回按钮 | 返回主页 | `ShellBackButton` | UIA 已确认 | 在标题栏内 |
| 添加文件夹 | 添加文件夹 | `VideoAddFolderButton`（视频库）/ 音乐库同名按钮 | UIA 已确认 | — |
| 清空队列 | — | `QueueClearButton` | `verify_e2e.py` 断言 | 队列节 |

---

## 四、交互

| 我说的词 | UI 实际文案 | 代码标识 | 位置 | 说明 |
|---|---|---|---|---|
| 悬浮态 | — | `AlbumCardHoverOverlay`、`CardHoverOverlay` | `MpvPlayerPage.xaml` | 卡片悬浮蒙层 |
| 选中态 | — | `AlbumCardSelectionBox`、`CardSelectionBox`、`LibrarySelectionBar` | 同上 | 多选模式 |
| 展开动画 | — | `Motion/MotionBehaviors.cs`、`MotionEasing.cs`、`MotionTokens.cs`、`SpringMath.cs` | `Modules/Motion/` | 无叫"展开动画"的单一实现，是一组动效 token + 行为 |
| 波浪渐变 | — | `IndexRailHover.cs`（94 行，纯算术）+ `IndexLetterOpacityConverter.cs` | `Modules/Player/IndexRailHover.cs`；`Modules/Common/View/IndexLetterOpacityConverter.cs` | 字母栏悬浮时字母按距离放大/加深 |
| 返回 | 返回主页 | `ShellBackButton`；`ShellNav_SelectionChanged` 恢复上次 section | `MainWindow.xaml.cs:566` `_lastNavKey` | 返回的是导航层，不是浏览器式历史 |
| 全窗口 | 全窗口 | `FullWindowButton`、`_fullWindow`、`ChangeFullWindow()` | `MainWindow.xaml.cs` | 与"全屏"是两个不同状态 |
| 面板自动隐藏 | — | 1.5s 空闲计时器 | `PlayerControl` | — |

---

## 五、播放

| 我说的词 | UI 实际文案 | 代码标识 | 位置 | 说明 |
|---|---|---|---|---|
| 自绘封面 | — | `AlbumCardCoverImage` / `SongCardCoverImage` + `ArtworkPalette` / `AlbumColorExtractor` | `MpvPlayerPage.xaml`；`Modules/Player/Audio/` | 从标签/内嵌图取封面，并提取主色给页面染色 |
| MPV 原始界面 | — | **不存在**：`MpvPlayer` 以 composition swapchain 嵌进 XAML，无独立 mpv 窗口 | `mpv-winrt/MpvPlayer.cpp` | 所以"原始界面"这个概念在本项目里没有对应物 |
| 灰底卡片 | — | `AlbumArtworkFallbackIcon`、`AlbumCardFallbackIcon`、`SongCardFallbackIcon` | `MpvPlayerPage.xaml` | 无封面时的回退图标；"灰底"是回退视觉，不是独立控件 |
| 封面加载 | — | `MpvPlayerPage_Library.cs` 中 57 处 thumbnail 处理 + 原生层导出 | `MpvPlayerPage_Library.cs` | 曾整批丢封面（`45723df`、`cc5c670` 都在修这一条） |
| 缩略图预览（进度条） | — | `MpvPreviewer` + `MpvPlayerPage_Preview.cs` + `MpvSoftwarePreviewControl` | `mpv-winrt/`；`Modules/Player/` | 独立第二个 libmpv 实例，软件渲染 |
| 频谱 / 节奏条 | — | `AudioCapture.cs`、`SpectrumAnalyzer.cs` | `Modules/Player/Audio/` | 有单测 |

---

## 六、代码里没有对应实现的说法

| 我的说法 | 结论 | 备注 |
|---|---|---|
| "播放页"作为独立页面 | 当前无对应实现 | 与媒体库同属一个 `MpvPlayerPage` |
| "排序下拉框" | 当前无对应实现 | 实际是按钮组 + 弹出菜单 |
| "MPV 原始界面" | 当前无对应实现 | 视频走 composition 嵌入，无独立窗口 |
| 播放列表页面 | **有常量无入口** | `LibrarySectionPlaylists` 存在但不可达 |
| 常驻侧栏（播放列表） | 当前无对应实现 | 是 Popup，不是侧栏 |
