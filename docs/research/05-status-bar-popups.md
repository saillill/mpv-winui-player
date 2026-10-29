# 05 · 状态栏弹窗：尺寸与排版统一

> **现状（实测）**
> 播放页里与"状态栏"相关的浮层其实是**四类不同的东西**，各有各的尺寸来源：
>
> | 类型 | 数量 | 尺寸现状 |
> |---|---|---|
> | `Popup`（自绘浮层） | **2 个**：控制面板 / 轨道选择器（播放列表面板 2026-10-29 已改回 in-layout 网格列，见 04） | 560×460（chrome 40，内容 420）/ 560×≤420 |
> | `ContentDialog` | **23 处**（全仓 `new ContentDialog` 计数），其中播放页 8 处 | **已实测全部合规**，见下 |
> | `Flyout` | 3 个文件共 6 处 | 平台默认 |
> | `InfoBar` | 1 处（`PlaybackInfoBar`，`MaxWidth=560`） | 唯一显式给了宽度 |
>
> **23 处 ContentDialog 的实际状态（实测，2026-10-29）**：0 处缺 `CloseButtonText`、
> 0 处缺 `XamlRoot`；只有 2 处显式设了尺寸（`MinWidth` 420 / 360），其余 **21 处按内容
> 自适应**——而内容自适应正是 WinUI 3 的默认行为，官方文档说 `FullSizeDesired` 才是例外。
> 所以"23 处未统一"这个判断在数据上**不成立**：它们统一地遵守了默认规则。
> 按钮组合分布：`close+primary` 16 处、`close only` 6 处、三按钮 1 处，没有一处缺安全按钮。
>
> **两套 Popup 尺寸（560×460 / 560×≤420）仍不统一**，机械原因也仍在：控制面板的
> 宽高是 `PlayerControl.ControlPanel.cs:35,36` 的 `const double`，轨道选择器宽度是 `:142`
> 的另一个常量，而播放列表用的是 `Styles.xaml` 令牌。

---

## 一、线索清单

### 1.1 官方规范（可直接作为尺寸判据）

| 项 | 来源 | 内容 | 可复用 |
|---|---|---|---|
| **对话框按钮的角色与顺序** | [MS Learn: Dialog controls](https://learn.microsoft.com/windows/apps/design/controls/dialogs-and-flyouts/dialogs) | **CloseButton 必须存在、且在最右**；PrimaryButton 最左；SecondaryButton 居中；三按钮应"克制使用" | **直接作为排版规则**。这是官方硬约束，不是偏好 |
| **ContentDialog 默认尺寸常量** | [StackOverflow 实测](https://stackoverflow.com/questions/79182728)、[Uno 主题文档](https://github.com/unoplatform/uno.themes/blob/974e752e7824d1e082e0bf1f37c37e3573bb717e/doc/styles/simple/ContentDialog.md) | `ContentDialogMinWidth` 320、`ContentDialogMaxWidth` 548（旧版）/ 800（新版）、`MinHeight` 184、`MaxHeight` 756 / 1000；Uno 的 `SimpleContentDialogMinWidth`=288 | **作为统一尺寸的取值依据**，不必自创数字 |
| **`FullSizeDesired`** | [ContentDialog.FullSizeDesired](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog.fullsizedesired) | 桌面应用**默认按内容自适应尺寸**；`FullSizeDesired=true` 才按比例铺满 | 解释"为什么现在大小不一"——因为默认就是内容驱动 |
| **Windows 11 几何规则** | [Geometry in Windows 11](https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry) | 顶层容器（窗口、浮出、对话框）统一 **8px 圆角**；`ContentDialog`/`Flyout`/`MenuFlyout`/`TeachingTip` 都算 | 本仓库已有 `OverlayCornerRadius` 资源，应确认是否等于 8 |
| **`XamlRoot` 必设** | 同上文档 | WinUI 3 中 `ContentDialog` 未设 `XamlRoot` 会在运行时抛异常；**同一窗口同时只能有一个** | 已知陷阱，检查 23 处是否都设了 |

### 1.2 本仓库已有的可复用资产

| 资产 | 位置 | 可复用部分 |
|---|---|---|
| `PlacementFor(anchor, popupWidth)` | `PlayerControl.ControlPanel.cs:150` | popup 横向放置算法：多显示器、工作区、DPI 缩放全处理。**任何新尺寸的 popup 都应复用它** |
| 尺寸资源集中处 | `Styles.xaml:1541-1548`（`MpvPlaylistPanel*`） | 播放列表尺寸已令牌化；另两个 popup 的尺寸仍是 `const double`（`PlayerControl.ControlPanel.cs:35,36,142`）——**这是"不统一"的机械原因：一类用令牌，一类用常量** |
| `MpvPopupContentPadding` / `MpvPopupListMargin` / `MpvPopupRowMinHeight` | `Styles.xaml:1631-1638` | 弹窗内部排版已有一组令牌 |
| 三个 Popup 的共用关闭规则 | `IsLightDismissEnabled=True` + `ShouldConstrainToRootBounds=False` | 统一行为 |

### 1.3 同类项目

| 项目 | 许可 | 可参考 |
|---|---|---|
| [Fusion](https://github.com/zoinve/Fusion) | **MIT** | WinUI 3 的对话框写法，可直接抄 |
| 官方 **WinUI Gallery** | 微软示例 | 本机 `winapp find-ui "contentdialog"` 取现行示例 |
| WinUI 3 Gallery 源 | [GitHub](https://github.com/microsoft/WinUI-Gallery) | 各类对话框的官方用法 |

---

## 二、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| 官方按钮角色/顺序规则 | **高** | 已完成 | 23 处实测全部满足：0 处缺 CloseButton，0 处缺 XamlRoot |
| 默认尺寸常量 | **高** | 低 | 21 处已是内容自适应（平台默认）；只有 2 处 `MinWidth` 是自选值，不是"统一"的障碍 |
| 8px 圆角 | 高 | 低 | 需核对现有 `OverlayCornerRadius` 的实际值 |
| `PlacementFor` | 高 | 零（已存在） | 只处理横向。播放列表面板曾是本项的最大风险点（纵向不 clamp），**改 in-layout 后该风险消失**；另两个 Popup 高度都 < 460，短屏下仍在工作区内 |
| 令牌化改造 | 中 | **低**（比原估计小） | 只剩两个 Popup 的 4 个 `const double`；播放列表那份已随 in-layout 改造删除 |

### 已识别的取舍点——2026-10-29 复核后

1. **统一到哪个基准**：只剩两个 Popup（560×460 / 560×≤420），宽度本来就一致，
   差的只有高度。**"统一"的实际工作量是 3 个常量，不是重新设计尺寸体系。**
2. ~~**`ContentDialog` 是否也要统一**~~：**不需要**。23 处已实测合规，动它们只会在
   21 处引入无理由的固定宽度，破坏内容自适应。
3. ~~**纵向越界**~~：**已修**（`10a0d31` 修 Popup 形态，`741199f` 改 in-layout 后该
   形态不存在）。另两个 Popup 高度 460 与 ≤420，1366×768 下都放得下。
4. **既有检查器**：`check-ui-tooltips.py` 仍会核对按钮的可访问名称与工具提示；
   改弹窗文字要重跑它（当前 TOTAL=0）。

---

## 三、检索提示词

### 3.1 搜索式

```
WinUI 3 ContentDialog ContentDialogMaxWidth ContentDialogMinWidth default values
WinUI 3 ContentDialog sizing guidelines consistent dialog width
Fluent Design dialog corner radius 8px Windows 11 geometry
WinUI 3 ContentDialog XamlRoot required exception
"FullSizeDesired" ContentDialog when to use
WinUI 3 popup vertical placement constrained to work area
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。播放页有**四类浮层**：
> `Popup`（3 个，尺寸分别是 560×460 / 560×≤420 / 320×≤640）、
> `ContentDialog`（全仓 23 处，播放页 8 处）、`Flyout`（6 处）、`InfoBar`（1 处）。
>
> 问题：每类浮层的按钮与组件不统一，尺寸也各自为政。
> 机械原因已查明：**一部分尺寸是 `Styles.xaml` 里的令牌（`MpvPlaylistPanel*`），
> 另一部分是 `PlayerControl.ControlPanel.cs:35,36,142` 里的 `const double`**。
>
> 请先回答，不要写代码：
> 1. 统一到哪个基准？按内容类型分别定，还是拉成一组？
> 2. 23 个 `ContentDialog` 是否都要动？它们的场景（确认/报错/选择）差异如何影响尺寸规则？
> 3. 官方规定 CloseButton 必须存在且在**最右**——现有 23 处有几处违反？给出检查方法。
> 4. `PlacementFor()` 只处理横向；短屏幕下 640 高的面板越界怎么办？
> 5. 把 `const double` 搬进 `Styles.xaml` 会牵动 `OpenControlPanel` 的两个赋值点，
>    改完必须重跑 `tools/test-control-panel-tabs.ps1` 及其注入测试，确认页签高度仍稳定。
>
> 参考：官方对话框规范 <https://learn.microsoft.com/windows/apps/design/controls/dialogs-and-flyouts/dialogs>

### 3.3 深挖方向

- 逐个列出 23 处 `ContentDialog` 的场景与当前尺寸，做成表格再谈统一
- 核对本机 WASDK 2.5.1 的 `ContentDialogMaxWidth` 实际值（查 `generic.xaml`）
- 查 `check-ui-tooltips.py` 的判定逻辑，确认改按钮文字的影响面
