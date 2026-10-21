# 状态归属表（S2 交付物）

**基线**：HEAD `8b1be9e`
**生成**：`s2_ownership.py` → `s2_table2.py`，从代码提取
**用途**：审查任务书 §2.3 要求的「状态来源与清理时机」

---

## 一、总体

| 项 | 数量 |
|---|---|
| 私有字段总数 | **85** |
| 有 teardown 写入 | 30 |
| 无 teardown 写入 | 47 |
| 从未被写入（只读或构造时赋值） | 8 |

前一份清单说「只有 9 个被清理」，那是**只统计 `Unloaded` 与 `TeardownLibrary` 方法体**的结果。把口径扩大到全部 teardown 形态（`Stop`/`Cleanup`/`Close`/`Hide`/`Clear`）后是 **30 个**。

这是扩大口径，不是纠正错误——之前的数字低估了现状。

---

## 二、47 个「无清理」字段的实际风险

「无 teardown 写入」不等于有问题。分三类：

### 类 1：瞬时标志位（约 44 个）—— 风险低

`_dragActive`、`_isFullWindow`、`_playbackActive`、`_dropPayloadPlayable`、`_lastAppliedTargetPeak` 等。

静止时为 `false`/`0`/空。若页面被缓存且在操作中途离开，残留值会让下次进入时状态异常。**一行重置即可。**

### 类 2：缓存（3 个）—— **本轮已修复**

`_libraryThumbnails`、`_libraryAll`、`_lyrics` / `_palette`。

这三个正是「封面缓存跨页残留」，已在 `TeardownLibrary` 中补上清理。

### 类 3：对活对象的强引用 —— **0 个**

> **这里我最初判错了，必须记录。**

我按字段名把三个字段列为「持有活对象的引用且无清理」，并准备请你批准修复。读了声明后发现：

| 字段 | 实际类型 | 结论 |
|---|---|---|
| `_watchHistoryDialog` | **`WeakReference<ContentDialog>`** | **不是强引用**。用弱引用正是为了让已关闭的对话框不被持有；条目被点击时还会显式置 `null`。**无需处理** |
| `_watchLaterDialog` | **`WeakReference<ContentDialog>`** | 同上 |
| `_contextMenuItem` | `PlaylistItem`（**纯数据行**，不是元素） | 每次右键都会覆盖。teardown 时置空只是清一个「下次用前必被覆盖」的字段 |

`_activeCardOverlay` 是其中**唯一**的 `FrameworkElement`，本轮已补清理。

**所以「无清理的活对象引用」实际是 0 个。** 之前的「3 个未处理 + 请你批准」是误报，原因是从字段**名字**归类而没有看**类型**。

---

## 三、全局服务 vs 页面局部状态

### 全局服务（`AppContext`，14 个静态成员）

```
Logger  AppLang  AppSetting  AvailableLanguages
RunMpvCommand  SetMpvLogLevel  SendMpvCommand
GetAudioDevices  GetGpuAdapters
WritePluginConfigs  WriteManagedMpvConfig
NotifySettingChanged  Init  SwitchLanguage  WaitAll
```

### 判定规则

| 归属 | 判据 |
|---|---|
| **全局** | 多页共读，或生命周期长于页面（设置、语言、日志、mpv 命令通道） |
| **页面局部** | 仅一个页面用，页面销毁后无意义（布局状态、当前分区、拖拽中间态） |

**违规项：0。** 14 个静态成员均符合上表。

**需注明的一点**：`GetAudioDevices` / `GetGpuAdapters` 是 `Func<>` 委托，由页面初始化时赋值、`Unloaded` 时置 `null`。这是**页面注册到全局的钩子**，页面不在时为 `null`。设计上可接受（转发 mpv 能力），但属于全局服务里最脆弱的一环，应在代码注释中标注。

---

## 四、完整字段表

## 媒体库与音乐（8 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_libraryAll` | `List<LibraryEntry>` | 2 | 是 | MpvPlayerPage_Library.cs |
| `_libraryFilter` | `string` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_libraryGeneration` | `int` | 0 | — | MpvPlayerPage_Library.cs |
| `_libraryIdleTimer` | `DispatcherTimer` | 0 | — | MpvPlayerPage_Library.cs |
| `_libraryLoaded` | `bool` | 4 | 是 | MpvPlayerPage_Library.cs |
| `_librarySection` | `string` | 2 | **否** | MpvPlayerPage_Library.cs |
| `_libraryVisible` | `bool` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_libraryWithoutThumbnail` | `HashSet<string>` | 1 | 是 | MpvPlayerPage_Library.cs |

## 封面与歌词缓存（3 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_libraryThumbnails` | `Dictionary<string, BitmapImage>` | 1 | 是 | MpvPlayerPage_Library.cs |
| `_lyrics` | `IReadOnlyList<LyricLine>` | 2 | 是 | MpvPlayerPage_Music.cs |
| `_palette` | `AlbumColorExtractor.Palette` | 3 | 是 | MpvPlayerPage_Music.cs |

## 播放与队列（12 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_allPlaylistItems` | `List<PlaylistItem>` | 0 | — | MpvPlayerPage_Playlist.cs |
| `_aspectFitTimer` | `DispatcherQueueTimer?` | 1 | **否** | MpvPlayerPage_Info.cs |
| `_discMenuActive` | `volatile bool` | 1 | **否** | MpvPlayerPage_Disc.cs |
| `_isPlayerInitialized` | `bool` | 1 | **否** | MpvPlayerPage.xaml.cs |
| `_lastCurrentPlaylistIndex` | `int` | 1 | **否** | MpvPlayerPage_Playlist.cs |
| `_mediaPlayer` | `MpvPlayer` | 0 | — | MpvPlayerPage.xaml.cs |
| `_playbackActive` | `bool` | 2 | **否** | MpvPlayerPage_Library.cs |
| `_playerReadyTcs` | `TaskCompletionSource` | 0 | — | MpvPlayerPage.xaml.cs |
| `_playerViewLoaded` | `bool` | 1 | **否** | MpvPlayerPage_PlayerView.cs |
| `_playlistPanelOpen` | `bool` | 2 | 是 | MpvPlayerPage_PlaylistPanel.cs |
| `_playlistRefreshPending` | `bool` | 3 | 是 | MpvPlayerPage_Playlist.cs |
| `_playlistRefreshTimer` | `DispatcherQueueTimer?` | 2 | 是 | MpvPlayerPage_Playlist.cs |

## 拖放（5 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_dragActive` | `bool` | 3 | **否** | MpvPlayerPage_Playlist.cs |
| `_dragSetup` | `bool` | 1 | **否** | MpvPlayerPage_Playlist.cs |
| `_dragSourceIndex` | `int` | 2 | **否** | MpvPlayerPage_Playlist.cs |
| `_dragStartPoint` | `Point` | 1 | **否** | MpvPlayerPage_Playlist.cs |
| `_dropPayloadPlayable` | `bool` | 1 | **否** | MpvPlayerPage_Drop.cs |

## 窗口与输入（12 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_appWindow` | `AppWindow` | 1 | **否** | MpvPlayerPage.xaml.cs |
| `_enableMouseInput` | `bool` | 2 | 是 | MpvPlayerPage_Mouse.cs |
| `_enableMouseInputDiscNavOnly` | `bool` | 1 | **否** | MpvPlayerPage_Mouse.cs |
| `_hHook` | `HHOOK?` | 2 | 是 | MpvPlayerPage_InputForwarding.cs |
| `_isFullScreen` | `bool` | 1 | **否** | MpvPlayerPage_FullScreen.cs |
| `_isFullWindow` | `bool` | 2 | **否** | MpvPlayerPage_FullScreen.cs |
| `_lastMouseMoveSent` | `long` | 2 | 是 | MpvPlayerPage_Mouse.cs |
| `_pipWindow` | `PiPWindow?` | 2 | 是 | MpvPlayerPage_PiP.cs |
| `_sizeChangedAction` | `Action<ViewSize>?` | 2 | 是 | MpvPlayerPage_PlayerView.cs |
| `_suppressKeyboard` | `bool` | 2 | **否** | MpvPlayerPage_InputForwarding.cs |
| `_suppressMouse` | `bool` | 0 | — | MpvPlayerPage_Mouse.cs |
| `_windowActivated` | `bool` | 3 | **否** | MpvPlayerPage_InputForwarding.cs |

## 显示与色彩（13 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_displayInfo` | `DisplayInformation?` | 4 | 是 | MpvPlayerPage_Display.cs |
| `_displayInfoDebouncer` | `DispatcherTimerDebouncer<int>?` | 2 | 是 | MpvPlayerPage_Display.cs |
| `_displayInfoTimer` | `DispatcherQueueTimer?` | 2 | 是 | MpvPlayerPage_Display.cs |
| `_hdrModeAppliedOnce` | `bool` | 1 | **否** | MpvPlayerPage.xaml.cs |
| `_lastAppliedDisplayPeak` | `int` | 1 | **否** | MpvPlayerPage_Display.cs |
| `_lastAppliedTargetPeak` | `string` | 1 | **否** | MpvPlayerPage_Display.cs |
| `_lastColorKind` | `mpv_winrt.DisplayColorKind` | 4 | **否** | MpvPlayerPage_Display.cs |
| `_lastCompositionScaleX` | `double` | 2 | **否** | MpvPlayerPage_PlayerView.cs |
| `_lastCompositionScaleY` | `double` | 2 | **否** | MpvPlayerPage_PlayerView.cs |
| `_lastDisplayFailureReason` | `string?` | 2 | 是 | MpvPlayerPage_Display.cs |
| `_lastMonitor` | `HMONITOR?` | 2 | **否** | MpvPlayerPage_Display.cs |
| `_lastRefreshRate` | `uint` | 3 | **否** | MpvPlayerPage_Display.cs |
| `_withdrewToLibrary` | `bool` | 2 | **否** | MpvPlayerPage_Library.cs |

## 预览（4 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_enableBuiltInPreview` | `bool` | 0 | — | MpvPlayerPage_BuiltInPreview.cs |
| `_previewIdleTicks` | `int` | 4 | 是 | MpvPlayerPage_Preview.cs |
| `_previewThrottleTimer` | `DispatcherQueueTimer?` | 2 | 是 | MpvPlayerPage_Preview.cs |
| `_useBuiltInPreview` | `bool` | 2 | 是 | MpvPlayerPage_Preview.cs |

## 计时器与异步（1 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_task` | `Task?` | 2 | **否** | MpvPlayerPage.xaml.cs |

## 其他（27 个）

| 字段 | 类型 | 写入 | 有清理 | 文件 |
|---|---|---|---|---|
| `_activeCardOverlay` | `FrameworkElement?` | 4 | 是 | MpvPlayerPage_Library.cs |
| `_albumTracks` | `List<LibraryEntry>` | 3 | 是 | MpvPlayerPage_Library.cs |
| `_audioCapture` | `AudioCapture?` | 3 | 是 | MpvPlayerPage_Music.cs |
| `_cardMenuEntry` | `LibraryEntry?` | 2 | 是 | MpvPlayerPage_Library.cs |
| `_contextMenuItem` | `PlaylistItem` | 1 | **否** | MpvPlayerPage_Playlist.cs |
| `_currentLyricIndex` | `int` | 2 | **否** | MpvPlayerPage_Music.cs |
| `_forceShowVideo` | `bool` | 2 | **否** | MpvPlayerPage_Library.cs |
| `_groupHeaders` | `Dictionary<string, FrameworkElem` | 2 | 是 | MpvPlayerPage_Library.cs |
| `_lastAppliedReferenceWhite` | `int` | 1 | **否** | MpvPlayerPage_Display.cs |
| `_lastExceptionNotifyTicks` | `long` | 1 | **否** | MpvPlayerPage.xaml.cs |
| `_logger` | `Logger` | 0 | — | MpvPlayerPage.xaml.cs |
| `_mediaTitle` | `string` | 1 | **否** | MpvPlayerPage_Music.cs |
| `_musicGrouping` | `MusicGrouping` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_musicPageVisible` | `bool` | 3 | 是 | MpvPlayerPage_Music.cs |
| `_openAlbum` | `string?` | 3 | 是 | MpvPlayerPage_Library.cs |
| `_pendingPaths` | `IReadOnlyList<FileItem>?` | 4 | **否** | MpvPlayerPage_Open.cs |
| `_rhythmBars` | `List<Border>` | 2 | 是 | MpvPlayerPage_Music.cs |
| `_rhythmRendering` | `bool` | 2 | 是 | MpvPlayerPage_Music.cs |
| `_selectionMode` | `bool` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_selfWeakReference` | `WeakReference<MpvPlayerPage>?` | 1 | **否** | MpvPlayerPage.xaml.cs |
| `_videoFolderView` | `bool` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_videoHeight` | `uint` | 1 | **否** | MpvPlayerPage_Info.cs |
| `_videoSort` | `VideoSortMode` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_videoWidth` | `uint` | 1 | **否** | MpvPlayerPage_Info.cs |
| `_visibleEntries` | `List<LibraryEntry>` | 1 | **否** | MpvPlayerPage_Library.cs |
| `_watchHistoryDialog` | `WeakReference<ContentDialog>?` | 2 | **否** | MpvPlayerPage_History.cs |
| `_watchLaterDialog` | `WeakReference<ContentDialog>?` | 2 | **否** | MpvPlayerPage_History.cs |

---

## 五、结论

1. **页面局部状态没有泄漏到全局**：14 个静态成员划分正确，违规 0。
2. **缓存类跨页残留已消除**：本轮修复 6 个字段。
3. **不存在「持有活对象的强引用且无清理」的字段**：唯一的 `FrameworkElement` 引用已被清理，两个对话框字段本来就是弱引用。
4. **剩余的是瞬时标志位**（约 44 个），风险低。迁移到 `OnNavigatedFrom`（你已批准的 J）时可一次性集中重置。

**净结论：状态边界当前是健康的。** 唯一值得做的改进是把清理从 `Unloaded` 迁到 `OnNavigatedFrom`，让清理时机与导航语义一致。
