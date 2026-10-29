# 04 · 侧栏：弹窗改右侧滑出大侧栏

> **现状（实测）**
> - 实现：`PlaylistPanelPopup`（`MpvPlayerPage.xaml:2416`，逻辑在
>   `MpvPlayerPage_PlaylistPanel.cs`，74 行）
> - 形态：`Popup` + `ShouldConstrainToRootBounds="False"` + `IsLightDismissEnabled="True"`
> - 尺寸：`MpvPlaylistPanelWidth=320`、`MpvPlaylistPanelMaxHeight=640`、
>   `MpvPlaylistPanelHeaderHeight=48`（`Styles.xaml:1541,1542,1548`）
> - 动画：**有**，`RepositionThemeTransition`，167ms（`MpvPlayerPage.xaml:2394-2396`）
> - 定位：`HorizontalOffset` / `VerticalOffset` 手动算，锚到触发按钮（`_PlaylistPanel.cs:118-120`）
>
> **关键历史发现**：XAML 注释明确记录这是一次**返工**——
> 「It used to be an in-layout side panel: a scrim plus a column pinned to the right
> edge of the content area. That is a docked panel wearing a slide animation ... What
> was asked for is the other shape: a panel that POPS OUT over the window」
> （`MpvPlayerPage.xaml:2398-2415`）
>
> **也就是说：本次要求的方向（右侧滑出大侧栏）与上一次的需求方向相反。**
> 这是本项最重要的线索，见 §二 的风险条目。

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

### 三个候选形态（供后续会话决策，本文不定案）

| 形态 | 实现路径 | 与现有代码的距离 | 主要代价 |
|---|---|---|---|
| **A. 继续 Popup，只是加宽** | 改 `MpvPlaylistPanelWidth` 320 → 更宽 | **最小**，改一个资源键 | Popup 不参与布局，**不会挤压视频**；宽度超过屏幕时靠 `PlacementFor` 兜底 |
| **B. SplitView Overlay + 右置** | 新增 `SplitView`，`PanePlacement="Right"`、`DisplayMode="Overlay"` | 中，需重写面板容器 | 遮挡关系由 WinUI 管理，动画与遮罩是平台行为；但 `SplitView` 的 Overlay 面板**默认不超出窗口边界** |
| **C. 改回 in-layout 分区** | 面板参与布局，挤压视频宽度 | **最大**，等于回退那次返工 | 即注释记录的"上一个被否决的方案"。视频宽度会被永久压缩 |

### 风险（按严重度排序）

1. **需求方向与上一次相反。** 注释记录上一次的明确要求是"pop out over the window"，
   并给出了否决 in-layout 的理由（"docked panel wearing a slide animation"）。
   **本项若要改成 in-layout 大侧栏，必须先确认这次需求确实推翻了上次的判断**，
   否则会再次返工。这是本项唯一需要产品决策的点。
2. **宽度与屏幕。** 320 → 大侧栏后，在 1366×768 或分屏窗口下极易越界。
   `PlacementFor()` 已处理横向，但**纵向没有对应逻辑**。
3. **Light dismiss 与大面板的冲突。** 面板越大，误触外部关闭的概率越高。
   Windows 设置类面板通常在关闭前不要求确认，但侧栏宽度意味着更高的误操作成本。
4. **`ColumnDefinitions` / `MaxWidth` 检查器。** `check-scroll-maxwidth.py` 会约束可横向滚动
   容器的子元素宽度；大侧栏若引入横向滚动需同步。

### 与其他项的交叉

- 与 **第 05 项（状态栏弹窗）** 共用 `Popup` 机制与关闭规则，改动应一起考虑
- 与 **第 09 项（播放列表）** 共用 `PlaylistItems` 集合，注释已声明"一份集合、两个读视图"
- 与 **第 01 项** 有交集：若将来走 HWND 嵌入，所有 Popup 都要改成 owned window

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
