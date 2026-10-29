# 04 · 侧栏：弹窗改右侧滑出大侧栏

> **现状（实测，2026-10-29 已改）**
> - 实现：`PlaylistPanelSurface`（`MpvPlayerPage.xaml`，逻辑在
>   `MpvPlayerPage_PlaylistPanel.cs`）——**已是 in-layout 网格列，不再是 Popup**
> - 形态：`ContentHost` 两列，`StageHost` 在列 0，面板在列 1；`Visibility` 切换，
>   滑入用 `MotionBehaviors.SettleFrom`（167ms move 曲线）
> - 尺寸：`MpvPlaylistPanelWidth=320`、`MpvPlaylistPanelHeaderHeight=48`
>   （`MpvPlaylistPanelMaxHeight` 已随 Popup 一起删除——列没有高度上限）
> - 方向：**已按用户 2026-10-29 的决定改回 in-layout**，注释已重写并记录两次反转的理由
>
> **方向历史（两次反转，都记在 XAML 注释里）**：in-layout → pop-out（理由：docked
> 面板占画面宽度、被窗口裁切）→ in-layout（理由：需求就是要它成为布局的一部分）。
> 第一次反转的代价是面板得手算 clamp 才能留在屏内，第二次反转把这个 clamp 连同
> `PlaylistPanelPlacement` 一起删了——列不可能跑出窗口。

---

## 一、线索清单

### 1.1 本仓库内可直接复用的既有实现

| 来源 | 内容 | 可复用程度 |
|---|---|---|
| **`ShellNavPaneSurface`**（`MainWindow.xaml:66`） | 导航面板的独立表面，`PaneDisplayMode="Auto"`，`CompactPaneLength={StaticResource MpvNavPaneRailWidth}`（`:164,173`） | **高**。注释已声明播放列表面板"与导航面板共用同一表面、同一 header、同一 scrim"，改大侧栏应继续对齐这个基准 |
| **`RepositionThemeTransition`** | 现有滑入动画，167ms token | **高**。已实现，不需要重做 |
| **`PlacementFor()`**（`PlayerControl.ControlPanel.cs:150`） | 把 popup 约束在显示器工作区内的横向放置算法，含多显示器与 DPI 处理 | **高**。侧栏变宽后更容易越界，这段逻辑直接可用 |
| 三个 popup 的共用机制 | 轨道选择器 / 控制面板 / 播放列表都用 `Popup + ShouldConstrainToRootBounds=False` | 大侧栏若继续用 Popup，可沿用同一套锚定与关闭规则 |

### 1.2 WinUI 平台侧

| 线索 | 来源 | 可参考部分 | 备注 |
|---|---|---|---|
| **`NavigationView` 的 OpenPaneLength** | [NavigationView 文档](https://learn.microsoft.com/windows/apps/design/controls/navigationview) | 大侧栏的宽度语义、`PaneDisplayMode` 三态（Left / Top / Auto） | 本仓库 Shell 已在用 |
| **`SplitView`** | WinUI 平台控件 | `DisplayMode="Overlay"` + `PanePlacement="Right"` 即是"从右侧覆盖滑出"的原生形状 | **未在本仓库使用**，是"改回 in-layout"时最轻的一条路 |
| **TeachingTip / FlyoutPresenter** | WinUI 平台 | 遮罩与轻触关闭的官方行为 | 对照现有 `IsLightDismissEnabled` |
| **`AppWindow` + owned window** | [WindowsAppSDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/windowing/windowing-overview) | 若侧栏需要超出主窗口边界，可用独立 owned window 而非 Popup | 与第 01 项的 airspace 讨论相关 |
| WinUI Gallery | 本机 `winapp find-ui "navigationview"` | 现行示例代码 | 可离线查 |

### 1.3 同类项目

| 项目 | 许可 | 可参考部分 |
|---|---|---|
| **Fusion** [zoinve/Fusion](https://github.com/zoinve/Fusion) | **MIT** | WinUI 3 的侧边面板/抽屉式布局实现，可直接读 |
| **Tauon** [Taiko2k/Tauon](https://github.com/Taiko2k/Tauon) | GPL | 播放队列的**独立面板**形态；UI 可高度定制，含 widget 概念 |
| **MusicBee** | 闭源 | [Fluent Wall](https://getmusicbee.com/forum/index.php?topic=30818.0) 等"覆盖式面板"的用户反馈 |
| **mpv.net** | **GPL v2** | 播放列表面板；**只能读不能抄** |

---

## 二、适用性评估

### 三个候选形态（2026-10-29 已选定 C）

| 形态 | 实现路径 | 与现有代码的距离 | 主要代价 |
|---|---|---|---|
| **A. 继续 Popup，只是加宽** | 改 `MpvPlaylistPanelWidth` 320 → 更宽 | 最小 | Popup 不参与布局，**不挤压视频**；但要手算 clamp 才能留在屏内（见 10a0d31 修的纵向越界） |
| **B. SplitView Overlay + 右置** | 新增 `SplitView`，`PanePlacement="Right"`、`DisplayMode="Overlay"` | 中 | Overlay 面板**默认不超出窗口边界**；动画与遮罩变成平台行为，和本仓库的 motion 令牌脱钩 |
| **C. 改回 in-layout 分区** ✅ | `ContentHost` 加两列，面板是列 1 | 已完成（`741199f`） | 视频宽度被永久压缩——这正是本次想要的 |

### 风险（按严重度排序）——改完之后的状态

1. ~~**需求方向与上一次相反。**~~ **已解决**：用户 2026-10-29 明确选择改回 in-layout，
   注释已重写并记录两次反转的理由。后续改动请读 `MpvPlayerPage.xaml` 里那段注释，
   不要再"发现"一次方向矛盾然后反转回去。
2. ~~**宽度与屏幕。** `PlacementFor()` 横向可用，**纵向没有对应逻辑**。~~
   **已消失**：列不可能跑出窗口。`PlaylistPanelPlacement` 与它的 8 个测试随之删除。
3. **Light dismiss 与大面板的冲突。** 改 in-layout 后**不再有 light dismiss**——
   面板用标题栏的关闭按钮收起。误触外部关闭的风险不存在了，但"点外面关掉"的
   手势也没了；这是这次改形的实际行为变化，需要在人工验收时确认符合预期。
4. **`check-scroll-maxwidth.py` / `check-typography-standard.py`。** 前者仍绿。
   后者是**按行号匹配基线**的：把 InfoBar 包进 `StageHost` 让它的发现从 1053 移到 1065，
   基线条目要跟着挪。同一次改动里已处理。

### 与其他项的交叉

- 与 **第 05 项（状态栏弹窗）**：本面板不再是 Popup，**第五项里"三个 Popup 共用关闭规则"
  现在只剩两个**（控制面板、轨道选择器）
- 与 **第 09 项（播放列表）** 共用 `PlaylistItems` 集合，注释已声明"一份集合、两个读视图"
- 与 **第 01 项** 有交集：若将来走 HWND 嵌入，另外两个 Popup 仍要改成 owned window

---

## 三、检索提示词

### 3.1 搜索式

```
WinUI 3 SplitView right pane overlay slide in
WinUI 3 NavigationView OpenPaneLength large pane layout
"RepositionThemeTransition" WinUI 3 popup slide
WinUI 3 popup vs SplitView overlay vs owned window which for side panel
site:github.com WinUI3 "SplitView" PanePlacement Right
WinUI 3 light dismiss large panel UX guidance
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。播放列表侧栏当前是
> `Popup` + `ShouldConstrainToRootBounds="False"`，宽 320，有 167ms 的
> `RepositionThemeTransition` 滑入动画（`MpvPlayerPage.xaml:2416`、
> `MpvPlayerPage_PlaylistPanel.cs`、`Styles.xaml:1541`）。
>
> **重要背景**：XAML 注释（`MpvPlayerPage.xaml:2398-2415`）记录这是一次返工——
> 原本是 in-layout 的右侧分区面板，被明确要求改成"pop out over the window"。
> 现在的新需求是"改成从右侧滑出的大尺寸侧栏"，**方向与上次相反**。
>
> 请先回答，不要写代码：
> 1. 新需求是要"更宽的 Popup"，还是要"重新变回参与布局的分区"？两者对视频宽度的
>    影响完全不同。
> 2. 若不参与布局（继续 Popup），大宽度在 1366×768 与分屏下的越界如何处理？
>    现有 `PlacementFor()` 只处理横向。
> 3. `SplitView`（Overlay + Right）能否替代 Popup？它不超出窗口边界这一限制是否可接受？
> 4. 面板变大后，light dismiss 的误触风险如何缓解？
>
> 请先给出三个形态的对照表与判据，再谈实现。

### 3.3 深挖方向

- 读 `ShellNavPaneSurface` 的完整定义，确认"共用表面"具体共用哪些资源
- 查 `mpv-clean`（751 提交）里 `0b17cc0` 与更早的原型，看 in-layout 版本长什么样
- 在 `docs/runtime-facts.md` §3 附近找 Popup 的截图限制（`PrintWindow` 拍不到 popup）
