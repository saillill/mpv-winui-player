# 09 · 播放列表：线索

> **现状（实测）**
> - **真正的播放队列在 `Domains/Playback/MpvPlayerPage_Playlist.cs`**（不是 `Domains/Playlist/`）
> - `Domains/Playlist/` 里只有**观看历史**与**稍后观看**两个控件（`WatchHistoryControl`、`WatchLaterControl`）
> - 队列能力已相当完整：
>   - 增量 diff 刷新（`ApplyPlaylistDiff` + `PlaylistItemChanged`，`:139,:211`）
>   - 节流定时器（`CreatePlaylistRefreshTimer`，`:78`）
>   - **导入 / 导出**（`ImportPlaylistAsync` / `ExportPlaylistAsync`，`:266,:284`）
>   - 清空 / 添加 / 打开文件 / 上下文菜单（`:343,:397,:419,:453`）
>   - 当前项自动选中（`SelectCurrentPlayListItem`，`:230`）
> - **一份集合、两个读视图**：队列 section 与右侧 Popup 都绑 `PlaylistItems`
>   （`MpvPlayerPage_Playlist.cs:62`，XAML 注释 `MpvPlayerPage.xaml:2413-2415` 明确要求二者不可漂移）
>
> **所以本项不是"从零做播放列表"，是打磨既有实现 + 补齐缺失的交互。**

---

## 一、线索清单

### 1.1 本仓库已具备的（优先复用，不要重写）

| 能力 | 位置 | 说明 |
|---|---|---|
| `PlaylistItems`（`ObservableCollection`） | `MpvPlayerPage_Playlist.cs:62` | 唯一数据源，两个视图共用 |
| `ApplyPlaylistDiff` | `:139` | 与 `_allPlaylistItems` 比对后**就地增删**，避免整体重建导致的选中丢失 |
| `PlaylistItemChanged` | `:211` | 逐字段比较，判断是否需要刷新 |
| 节流刷新定时器 | `:78-101` | mpv 的 `playlist` 属性变化可能非常频繁 |
| 导入 / 导出 | `:266,:284` | 播放列表可在会话间搬运 |
| 上下文菜单 | `:453` | 已有右键操作入口 |
| 共享行模板 | `MpvPlayerPage.xaml:2413` | 队列 section 与 Popup 用同一模板 |

### 1.2 可参考的播放列表 UI 范式

| 项目 | 技术栈 | 可参考部分 | 许可 |
|---|---|---|---|
| **mpv.net** | C# / WinForms | 播放列表的**拖拽重排**、当前项跟随、右键菜单结构 | **GPL v2，只读** |
| **Tauon Music Box** | Python / GTK | **播放队列 vs 播放列表**的概念区分；拖拽与多选 | GPL，只读 |
| **fooyin** | C++ / Qt | 类 foobar2000 的可定制播放列表分组 | 见 [LinuxLinks](https://www.linuxlinks.com/best-free-open-source-music-players/) |
| **Fusion** | **WinUI 3 / MIT** | WinUI 3 侧的列表样式、播放队列面板 | **可直接抄** |
| **Windows Media Player / Groove 的"正在播放"** | 闭源 | 官方播放器的队列侧栏形态（本仓库注释已声明照它做） | 仅参考 |

### 1.3 mpv 侧能力（可暴露但当前未暴露的）

| 能力 | mpv 属性/命令 | 本项目现状 |
|---|---|---|
| 随机 / 循环 | `shuffle`、`loop-playlist`、`loop-file` | **已有**（`MpvPlayerExtensions.GetRepeatState` / `SetRepeatState`） |
| 重排 | `playlist-move` | `MpvPlayer.PlaylistMove(from, to)` **已在 IDL**（`MpvPlayer.h:94` 附近） |
| 定位播放 | `playlist-play-index` | `MpvPlayer.PlaylistPlayIndex(index)` **已在 IDL** |
| 删除单项 | `playlist-remove` | 需确认是否暴露 |
| 上一项 / 下一项 | `playlist-next` / `playlist-prev` | **已有**（控制条按钮） |
| 播放列表持久化 | — | **导入/导出已有**，但"会话间自动记住"未确认 |

**关键点**：`PlaylistMove` 与 `PlaylistPlayIndex` **底层已就绪**，若 UI 还没有拖拽重排，缺的只是交互层。

---

## 二、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| 复用现有 `PlaylistItems` + diff 逻辑 | **必须** | 0 | 两个视图共享一份集合是明确的设计约束，新增视图**不得**另建集合 |
| 拖拽重排（`playlist-move`） | **高** | 中 | 底层 API 已在；WinUI 的拖拽需要 `CanReorderItems` 或 `DragItemsStarting/Drop`；**注意 mpv 的索引在拖拽期间可能变化**，需重新读一次 |
| 多选与批量删除 | 高 | 中 | `ListView` 已支持 `SelectionMode=Multiple`；需确认 mpv 侧 `playlist-remove` 的索引语义 |
| 会话间持久化 | 中 | 中 | mpv 自身有 `save-position-on-quit`，但播放列表本身未确认持久化 |
| mpv.net 的实现思路 | 低 | — | **GPL v2 禁止复制** |
| Fusion | 高 | 低 | MIT，WinUI 3 列表写法可直接参考 |

### 已识别的风险点

1. **索引漂移**：`playlist-move` / `playlist-remove` 都按索引操作，而 mpv 可能在
   `playlist` 属性变化时重排。**必须在每次操作前重新读取当前索引**，不能缓存。
2. **两个视图一致性**：XAML 注释明确要求"两份不可漂移"。任何改动都要同时验证
   `QueueView` 与 `PlaylistPanelPopup`。
3. **节流定时器的生命周期**：`CleanupPlaylistRefresh` 必须与页面卸载配对，
   否则页面离开后仍在轮询。
4. **导入/导出的格式**：需确认导出的是 m3u 还是纯路径列表；若与 mpv 的 `playlist` 属性格式
   不一致，重新导入可能丢序。

---

## 三、检索提示词

### 3.1 搜索式

```
mpv playlist-move playlist-remove index semantics
WinUI 3 ListView drag reorder CanReorderItems example
WinUI 3 ListView SelectionMode Multiple batch delete
mpv playlist persistence save on quit
WinUI 3 ObservableCollection incremental update vs rebuild
mpv.net playlist drag reorder implementation
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。
>
> **播放队列已相当完整**：`MpvPlayerPage_Playlist.cs` 里有 `PlaylistItems`
> （唯一数据源，被队列 section 与右侧 Popup 两个视图共享）、`ApplyPlaylistDiff` 增量刷新、
> 节流定时器、导入/导出、上下文菜单、当前项自动选中。
> 底层 `PlaylistMove(from,to)` 与 `PlaylistPlayIndex(index)` **已在 IDL 暴露**。
>
> **设计约束**：XAML 注释（`MpvPlayerPage.xaml:2413-2415`）明确要求两个视图
> "same template, same icons, same highlight — must not drift apart"。
>
> 请先回答，不要改代码：
> 1. 拖拽重排缺的是交互层还是 API？`playlist-move` 的索引在拖拽过程中会不会漂移？
> 2. 两个视图共享一份 `ObservableCollection`，新增第三种视图（如底部抽屉）是否安全？
> 3. 导入/导出的具体格式是什么？与 mpv `playlist` 属性格式是否一致？重新导入会丢序吗？
> 4. 会话间持久化现状如何？mpv 的 `save-position-on-quit` 是否覆盖播放列表？
> 5. 队列的两种视图（section / popup）已有 `tools/test-control-panel-tabs.ps1` 类似的
>    可验证性吗？若没有，怎么补最小验证？
>
> 参考可复用实现：`zoinve/Fusion`（WinUI 3，MIT）。

### 3.3 深挖方向

- 读 `MpvPlayerPage_Playlist.cs` 全文（约 460 行），列出所有"已实现但无 UI 入口"的能力
- 实测：播放中拖拽重排，观察 `playlist` 属性与 `PlaylistItems` 的索引是否一致
- 查 `mpv-clean`（751 提交）里 `0b17cc0` 记录的上游播放列表设计
