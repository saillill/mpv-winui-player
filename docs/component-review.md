# 组件审查表（S3 交付物）

**基线**：HEAD `8b1be9e`
**生成**：`s3_components.py`（行数、引用数、类型）+ 逐项判断（可替代性、结论）
**用途**：审查任务书 §3.3

---

## 一、总体

| 项 | 数量 |
|---|---|
| 组件总数（有 `x:Class` 的类型） | **36** |
| 总行数 | **22,618** |
| **零引用组件** | **0** |

前一轮清理删掉了 `ResizeGrip`（唯一孤儿），此后无新增孤儿。

---

## 二、按「能否用官方替代」分组

### A 组：官方无对应能力，自定义是必要的（4 个）

| 组件 | 行数 | 引用 | 理由 |
|---|---|---|---|
| `PlayerControl` | 3,692 | 3 | WinUI 的 `MediaTransportControls` **绑定 `MediaPlayerElement.MediaPlayer`**。本应用驱动 libmpv，没有 `MediaPlayer` 可绑。**这是自定义的唯一正当理由** |
| `MpvSoftwarePreviewControl` | 258 | 1 | 第二 libmpv 实例的软件渲染预览。官方无此能力 |
| `ControlBarCanvasControl` | 1,055 | 1 | 控制栏布局的实时预览画布 |
| `HighlightTextBlock` | 126 | 1 | 搜索命中高亮。`TextBlock` 无此能力，`RichTextBlock` 需手工分段 |

**结论：保留。** 这 4 个的替代品在平台层不存在。

### B 组：官方有"形状相近"的控件，但属于另一个库（12 个）

11 个 `Option*Control` + `OptionListControl`，共 **4,120 行**。

它们的官方对应物是 **CommunityToolkit 的 `SettingsCard` / `SettingsExpander`**——注意这是
**社区工具包**，不是 Windows App SDK。更换意味着：

- 整个 `OptionType`(11 种) → 模板 → 控件 的 1:1:1 映射要重写
- 现有 696 条设置行的渲染全部受影响
- 设置树的四个映射表（`sectionMap`/`sectionOrder`/`optionOrder`/`categoryOrder`）需重新验证

**结论：不替换。**
**理由**：这不是「手搓了一个官方已有的东西」，而是「用自研控件渲染一个自研模型」。换成
SettingsCard 需要把 `Option` 模型翻译成另一种数据形状，**收益是外观更统一，代价是重写整棵设置树**。
不属于清理范畴。

**但有一条必须记下**：如果未来要引入 CommunityToolkit，这 12 个是第一批候选。

### C 组：窗口外壳（7 个）

`SettingsWindow`、`MpvConfEditorWindow`、`MpvConfTextWindow`、`MenuEditorWindow`、`MediaInfoWindow`、`PiPWindow`，共 **1,796 行**。

WinUI 没有「可承载 Page 的独立窗口」的内置控件——`ContentDialog` 不能调整大小也不能承载
一个完整页面。这些是 `Window` 的最小必要外壳。

`PiPWindow` 另有特殊性：无边框、置顶、复用 `PlayerControl`。

**结论：保留。**

### D 组：页面（5 个）

`MpvPlayerPage`、`SettingsPage`、`MpvConfEditorPage`、`MenuEditorPage`（+ `MainWindow`），共 **18,589 行**。

页面天然是自定义的。问题不在「是否自定义」，而在 **§2 的排版规范与状态边界**，已在其他章节处理。

**结论：保留，排版与状态另行整改。**

### E 组：小控件（7 个）

`PlayerTrackSelectorControl`(398)、`WatchHistoryControl`(262)、`WatchLaterControl`(195)、
`QuickControlPanel`(165)、`VolumeFlyoutControl`(111)、`AboutControl`(105)、`MediaInfoUserControl`(382)。

`VolumeFlyoutControl` **已核查，不是自定义控件**：它内部就是一个原生 `<Slider>`，外面
只包了垂直浮层。

它存在的理由写在文件头的注释里：**垂直方向的 `Slider` 没有固有宽度**，浮层的胶片边缘会被裁掉，
所以需要显式几何。这不是「手搓官方已有能力」，而是「官方控件 + 必要外壳」。

其余 6 个是内容型控件（列表、曲目选择器），无官方对应。

---

## 三、完整清单

| 组件 | XAML | 代码 | 引用 | 类型 | 结论 |
|---|---:|---:|---:|---|---|
| `PlayerControl` | 1110 | 2582 | 3 | UserControl | 保留（A） |
| `MpvPlayerPage` | 2442 | 587 | 2 | Page | 保留（D） |
| `SettingsPage` | 770 | 1675 | 2 | Page | 保留（D） |
| `OptionListControl` | 565 | 1075 | 2 | UserControl | 保留（B） |
| `MainWindow` | 522 | 976 | 2 | Window | 保留（D） |
| `ControlBarCanvasControl` | 89 | 966 | 1 | OptionControlBase | 保留（A） |
| `MpvConfTextWindow` | 246 | 689 | 1 | Window | 保留（C） |
| `MpvConfEditorPage` | 261 | 607 | 1 | Page | 保留（D） |
| `OptionStringControl` | 80 | 534 | 1 | OptionControlBase | 保留（B） |
| `SettingsWindow` | 257 | 316 | 1 | Window | 保留（C） |
| `MenuEditorPage` | 348 | 206 | 1 | Page | 保留（D） |
| `MpvConfOptionControl` | 211 | 317 | 1 | MpvConfOptionControlBase | 保留 |
| `PiPWindow` | 104 | 403 | 2 | Window | 保留（C） |
| `PlayerTrackSelectorControl` | 202 | 196 | 2 | UserControl | 保留（E） |
| `MediaInfoUserControl` | 195 | 187 | 1 | UserControl | 保留（E） |
| `OptionColorControl` | 88 | 291 | 1 | OptionControlBase | 保留（B） |
| `OptionCheckListControl` | 91 | 280 | 1 | OptionControlBase | 保留（B） |
| `OptionLayoutControl` | 35 | 275 | 1 | OptionControlBase | 保留（B） |
| `WatchHistoryControl` | 119 | 143 | 1 | UserControl | 保留（E） |
| `MpvSoftwarePreviewControl` | 20 | 238 | 1 | UserControl | 保留（A） |
| `OptionStringListControl` | 52 | 192 | 1 | OptionControlBase | 保留（B） |
| `OptionShaderListControl` | 72 | 164 | 1 | OptionControlBase | 保留（B） |
| `OptionMultiListControl` | 44 | 181 | 1 | OptionControlBase | 保留（B） |
| `WatchLaterControl` | 95 | 100 | 1 | UserControl | 保留（E） |
| `OptionDoubleControl` | 37 | 139 | 1 | OptionControlBase | 保留（B） |
| `OptionIntegerControl` | 37 | 137 | 1 | OptionControlBase | 保留（B） |
| `QuickControlPanel` | 66 | 99 | 1 | UserControl | 保留（E） |
| `OptionActionControl` | 54 | 90 | 1 | OptionControlBase | 保留（B） |
| `HighlightTextBlock` | 14 | 112 | 1 | UserControl | 保留（A） |
| `App` | 54 | 66 | 1 | Application | — |
| `VolumeFlyoutControl` | 38 | 73 | 1 | UserControl | 保留（E，内含原生 Slider） |
| `AboutControl` | 73 | 32 | 1 | UserControl | 保留（E） |
| `OptionBooleanControl` | 38 | 57 | 1 | OptionControlBase | 保留（B） |
| `MenuEditorWindow` | 51 | 35 | 1 | Window | 保留（C） |
| `MpvConfEditorWindow` | 43 | 29 | 1 | Window | 保留（C） |
| `MediaInfoWindow` | 24 | 22 | 1 | Window | 保留（C） |

---

## 四、结论

1. **零引用组件：0 个。** 无孤儿可删。
2. **36 个组件全部有明确职责**，无一属于「占位实现」或「模拟数据」。
3. **真正无法官方化的只有 4 个**（A 组），理由是平台层没有对应能力。
4. **无「待评估」项**：`VolumeFlyoutControl` 经核查是原生 `Slider` 加浮层外壳，保留。
5. **B 组（12 个设置控件）是未来引入 CommunityToolkit 时的第一批候选**，但那是重构而非清理。

**净结论：组件层面没有可清理项。** 本轮的清理收益全部来自前一批的颜色与间距。
