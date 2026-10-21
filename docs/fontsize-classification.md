# 字号分类清单（待你判断）

**基线**：HEAD `00f0b52`
**数据**：`fontsize_inventory.py` → `fontsize_style.py`，从代码提取

---

## 一、数量的含义与任务书不同

任务书说「99 处手写 FontSize」。实测 **97 处**，但关键在于它们的元素类型：

| 元素 | 处数 | 性质 |
|---|---:|---|
| `FontIcon` | **58** | **图标尺寸**。`FontIcon` 正是靠 `FontSize` 定尺寸；官方图标尺寸（16/20/24/32）不属于文字 ramp |
| `TextBlock` | 37 | 文字 |
| `HyperlinkButton` | 1 | 文字 |
| `RichEditBox` | 1 | 文字 |

**真正需要处理的文字字号是 39 处，不是 97 处。**

文字字号的分布：

| 字号 | 处数 | 对应官方样式 |
|---:|---:|---|
| 11 | 6 | — |
| 12 | 13 | Caption |
| 13 | 1 | — |
| 14 | 5 | Body |
| 16 | 7 | — |
| 18 | 1 | — |
| 20 | 1 | Subtitle |
| 24 | 2 | — |
| 26 | 1 | — |
| 30 | 1 | — |
| 34 | 1 | — |

**19 处**恰好等于 ramp 值，**20 处**不在 ramp 上。

---

## 二、39 处全部没有 Style —— 这决定了不能脚本化替换

WinUI 的 `BaseTextBlockStyle` 不止设字号：

```xml
<Setter Property="FontFamily" Value="XamlAutoFontFamily" />
<Setter Property="TextTrimming" Value="CharacterEllipsis" />   <!-- 开始截断 -->
<Setter Property="TextWrapping" Value="Wrap" />                 <!-- 开始换行 -->
<Setter Property="LineStackingStrategy" Value="MaxHeight" />
<Setter Property="TextLineBounds" Value="Full" />
```

**「12 → CaptionTextBlockStyle」不是等价替换**：字号同为 12，但会**额外引入省略号截断与自动换行**。对原本单行的元素，这是可见变化。

---

## 三、19 处匹配 ramp —— 逐处核查后**只有 3 处可安全替换**

判定标准：该元素是否**已声明** `TextTrimming` 与 `TextWrapping`。两者都声明了，样式里的
setter 与元素自身一致，替换是无变化的；缺任一，样式会**新增**行为。

| 结论 | 处数 |
|---|---:|
| 已声明两者，**可安全替换** | **3** |
| 缺一，需补 | 4 |
| 两者都缺，**会新增换行/截断** | **12** |

**可安全替换的 3 处**（全部是 `TextBlock`，均已有完整声明）：
`WatchHistoryControl.xaml:78`、`WatchHistoryControl.xaml:87`、`WatchLaterControl.xaml:73`。

**会新增行为的 12 处**里，多数是**单行数值显示**：

| 位置 | 内容 | 加 `Wrap` 的后果 |
|---|---|---|
| `PlayerControl.xaml:31` | 控制栏数值 | 换行会使控制栏变高 |
| `QuickControlPanel.xaml:20` | 快速面板数值 | 同上 |
| `MpvPlayerPage.xaml:314` | A-Z 字母栏 | 单个字母被换行会撑破栏宽 |
| `OptionListControl.xaml:398` | 设置项说明 | 说明文字变高，行距改变 |

**所以「19 处可替换」是错的结论，实际是 3 处。**

以下是完整对照：

| 文件:行 | 元素 | 字号 | 对应样式 | 已声明 |
|---|---|---:|---|---|
| `AboutControl.xaml:19` | TextBlock | 20 | `SubtitleTextBlockStyle` | 无 |
| `AboutControl.xaml:38` | TextBlock | 14 | `BodyTextBlockStyle` | 无 |
| `AboutControl.xaml:62` | TextBlock | 14 | `BodyTextBlockStyle` | 无 |
| `ControlBarCanvasControl.xaml:14` | TextBlock | 12 | `CaptionTextBlockStyle` | wrap |
| `MediaInfoUserControl.xaml:75` | TextBlock | 14 | `BodyTextBlockStyle` | trim |
| `MediaInfoUserControl.xaml:81` | TextBlock | 12 | `CaptionTextBlockStyle` | 无 |
| `MediaInfoUserControl.xaml:181` | HyperlinkButton | 12 | `CaptionTextBlockStyle` | 无 |
| `MpvConfTextWindow.xaml:42` | TextBlock | 12 | `CaptionTextBlockStyle` | 无 |
| `MpvConfTextWindow.xaml:85` | RichEditBox | 12 | `CaptionTextBlockStyle` | wrap |
| `MpvPlayerPage.xaml:314` | TextBlock | 12 | `CaptionTextBlockStyle` | 无 |
| `OptionListControl.xaml:398` | TextBlock | 14 | `BodyTextBlockStyle` | 无 |
| `OptionShaderListControl.xaml:39` | TextBlock | 12 | `CaptionTextBlockStyle` | trim |
| `PlayerControl.xaml:31` | TextBlock | 12 | `CaptionTextBlockStyle` | 无 |
| `QuickControlPanel.xaml:20` | TextBlock | 12 | `CaptionTextBlockStyle` | 无 |
| `SettingsPage.xaml:444` | TextBlock | 14 | `BodyTextBlockStyle` | trim |
| `SettingsPage.xaml:474` | TextBlock | 12 | `CaptionTextBlockStyle` | wrap |
| `WatchHistoryControl.xaml:78` | TextBlock | 12 | `CaptionTextBlockStyle` | **两者** |
| `WatchHistoryControl.xaml:87` | TextBlock | 12 | `CaptionTextBlockStyle` | **两者** |
| `WatchLaterControl.xaml:73` | TextBlock | 12 | `CaptionTextBlockStyle` | **两者** |

---

## 四、20 处不在 ramp 上

| 文件:行 | 元素 | 字号 |
|---|---|---:|
| `MediaInfoWindow.xaml:16` | TextBlock | 16 |
| `MenuEditorWindow.xaml:29` | TextBlock | 16 |
| `MpvConfEditorWindow.xaml:20` | TextBlock | 16 |
| `MpvConfTextWindow.xaml:51` | TextBlock | 11 |
| `MpvConfTextWindow.xaml:181` | TextBlock | 11 |
| `MpvConfTextWindow.xaml:200` | TextBlock | 11 |
| `MpvConfTextWindow.xaml:213` | TextBlock | 11 |
| `MpvConfTextWindow.xaml:226` | TextBlock | 11 |
| `MpvConfTextWindow.xaml:239` | TextBlock | 11 |
| `MpvPlayerPage.xaml:1800` | TextBlock | 30 |
| `MpvPlayerPage.xaml:1808` | TextBlock | 16 |
| `MpvPlayerPage.xaml:2104` | TextBlock | 34 |
| `MpvPlayerPage.xaml:2110` | TextBlock | 18 |
| `MpvPlayerPage.xaml:2115` | TextBlock | 13 |
| `MpvPlayerPage.xaml:2163` | TextBlock | 16 |
| `OptionListControl.xaml:499` | TextBlock | 16 |
| `SettingsPage.xaml:193` | TextBlock | 24 |
| `SettingsPage.xaml:205` | TextBlock | 26 |
| `SettingsPage.xaml:215` | TextBlock | 24 |
| `SettingsWindow.xaml:169` | TextBlock | 16 |

**其中成组的**：

- **11（6 处）全部在 `MpvConfTextWindow`**——刻意固定深色的代码编辑器，行号与注释用它。**建议保留。**
- **16（7 处）**分布在窗口外壳与少量控件，官方无 16 的标题样式。**建议提取具名令牌** `MpvWindowCaptionFontSize`。
- **24 / 26 / 30 / 34** 各 5 处，多为大标题与空态数字。官方 ramp 在 20 与 28 之间是空的。

---

## 五、建议

| 组 | 处数 | 建议 | 风险 |
|---|---:|---|---|
| `FontIcon` | 58 | **不算字号问题**，保留 | 无 |
| 代码编辑器 11 | 6 | 保留 + 加注释 | 无 |
| 窗口标题 16 | 7 | 提取具名令牌 | 低 |
| 匹配 ramp 且已声明两者 | **3** | 替换为对应样式 | **低** |
| 匹配 ramp 但缺声明 | 16 | **保留** `FontSize` + 注释 | 无 |
| 其余（13/18/24/26/30/34） | 7 | 提取具名令牌，不强行套 ramp | 低 |

**净可安全改动：3 处**（`WatchHistory` ×2、`WatchLater` ×1）。
加上 7 处窗口标题的具名令牌提取，共 10 处低风险改动。

---

## 六、需要你回答

> **问题 1**：接受「58 个图标不算字号问题」这个前提吗？
> 若你认为图标尺寸也该规范，那是**另一套体系**（官方图标 16/20/24/32），需要单独定义，不能套文字 ramp。

> **问题 2**（已由我查清，无需你回答）：19 处匹配 ramp 的逐处核查完毕，**只有 3 处可安全替换**，
> 其余 12 处会新增换行/截断，4 处缺一个声明。结论已写入 §3。

> **问题 3**：16 这个窗口标题字号，提取为 `MpvWindowCaptionFontSize` 令牌可以吗？还是希望统一到 14（Body）或 18（BodyLarge）？