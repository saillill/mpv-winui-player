# mpvw 清理清单（待确认）

**状态**：草案，等你逐项批准后才执行
**前置**：`docs/audit-charter.md`（审查任务书）
**基线**：HEAD `aab1a9f`
**原则**：本文档中每一项都附「引用证据」；没有证据的项不列入

---

## 0. 你的六项决策（已记录，将写入任务书）

| 编号 | 你的选择 | 影响面 |
|---|---|---|
| B | **B2** 严格执行 4/8/12/16/24/48 | 65 个属性（重测值）；已改 14 处数值 + 9 处令牌化 |
| D | **D2 严格版** 律动也改主题色 | 2 个文件，视觉变化 |
| H | **H2** 删除弹簧，改用 Fluent SoftLanding | 4 个文件，动画手感变化 |
| E | **再加 ConnectedAnimation** | 新增跨页动画机制 |
| A | **改造为原生 NavigationView** | 中等风险重构 |
| J | **迁移到 OnNavigatedFrom** | 清理时机变更 |

**我必须先指出的三处风险**，因为它们会在实施中造成返工：

1. **B2 会改变视觉基线。** `2` 用了 8 次、`6` 用了 31 次，全部改大意味着图标与文字的缝变松、列表行变高。这不是「合规化」，是**重新设计**。建议 B2 单独提交并截图对比。

2. **A + E 会互相干扰。** `ConnectedAnimation` 依赖页面实例在导航中存活（`NavigationCacheMode`），而迁到 `NavigationView` 会改变导航栈与缓存策略。两者同批实施会产生难以定位的动画失效。**建议顺序：A 先落地并稳定，再做 E。**

3. **H2 会让 `SpringPop` 失去回弹。** Fluent 的 `SoftLanding` 是减速曲线，没有过冲。若你希望保留「按下回弹」的手感，H2 达不到；需要确认是否接受。

---

## P0：明确可删（1 项）

### P0-1 `Modules/Player/ResizeGrip.xaml` + `.xaml.cs`（22 行）

**证据**：全库检索 6 种引用机制（XAML 标签、`new` 构造、`typeof`、引号名、`x:Class`、资源合并），零命中。

**该文件自述**：「Transparent hit-test surface with a west-east resize cursor, used as the playlist width grip」——但没有任何地方实例化它。

**风险**：低。它是纯 UI 壳，不参与编译期契约。

**处置**：删除两个文件。

---

## P1：需你逐项确认

### P1-1 间距令牌（B2 决策的直接后果）

| 令牌 | 现值 | B2 后 | 使用次数 | 影响 |
|---|---|---|---|---|
| `MpvSpaceXxs` | 2 | 删除或 → 4 | 8 | 图标与文字的缝 |
| `MpvSpaceCompact` | 6 | 删除或 → 8 | 31 | 紧凑行距（影响最大） |

**其余 16 个非标准值**（`10`×9、`7`×7、`28`×3、`18`×3、`20`×2、`200`×2、`1`×3、`5`、`32`、`150`、`3`、`50` 等）。

其中 `200`/`150`/`80` 出现在 `Margin`/`Width` 里，是**尺寸**而非间距，不应套用间距体系——需人工区分。

**待确认**：这 16 个值里，哪些是真正的间距（要改），哪些是尺寸（不改）？我可以逐条列出位置，但归类需要你或我逐项判断。

### P1-2 硬编码颜色：重测 29 处内联（初测 61 含注释与合法主题画刷）

按用途分三档：

| 档 | 处数 | 位置 | 建议 |
|---|---|---|---|
| a. 遮罩/阴影半透明黑 | ~35 | `Styles.xaml`、`MpvPlayerPage.xaml`、`PiPWindow.xaml` | 改 `SmokeFillColorDefaultBrush` 或具名令牌 |
| b. 代码高亮色板 | 22 | `MpvConfTextWindow.xaml` | **保留**，迁移为具名资源并注解。理由：代码配色不应随主题变，官方无对应画刷 |
| c. 音量条峰值/均值的双色标记 | 2 | `PlayerControl.xaml:244,253` | 见下 |

**待确认**：b 档是否同意保留？（我强烈建议保留，否则等宽文本编辑器在 Light 主题下会变成白底白字。）

**c 档已查明**：两处都是 14 DIP 高、`CornerRadius="1"`、`Visibility="Collapsed"` 的 `Border`，位于 `Canvas` 内。从形状与容器判断，是**音量条上的峰值保持 / 当前值双标记**（类似专业音频表的 peak-hold 指示），默认隐藏。

**处置建议**：这两色是**数据可视化色**，语义是"峰值"与"当前值"，不是 UI 主题色。改为主题色会丧失区分度。建议**保留**，但提取为具名资源 `MpvAudioMeterPeakBrush` / `MpvAudioMeterValueBrush` 并注解。

**待确认**：是否同意保留？

### P1-3 手写 FontSize：重测 97 处，其中 58 个是图标尺寸

`34`(14) `23`(12) `16`(21) `11`(6) `20`(3) `18`(2) `24`(2) `64` `96` `30` `34` `13`

**这不是批量替换项。** 需逐处判断：
- 能映射到官方 `TextBlock` 样式的 → 换样式
- 属于**图形尺寸**的（`64`/`96` 的封面占位、`34` 的标题图标）→ 标注为尺寸，不当作排版字号

**待确认**：是否接受"逐处判断、分批提交"？这会产生较多小提交。

### P1-4 状态清理（J 决策的直接后果）

**实测：85 个私有字段，只有 9 个在 teardown 中被清理。**

高风险项（缓存了 UI 对象或数据，且无清理）：

| 字段 | 类型 | 风险 |
|---|---|---|
| `_libraryAll` | `List<LibraryEntry>` | 媒体库全量数据 |
| `_libraryThumbnails` | `Dictionary<string, BitmapImage>` | **缩略图缓存，内存占用最大** |
| `_albumTracks` | `List<LibraryEntry>` | 专辑页数据 |
| `_lyrics` | `IReadOnlyList<LyricLine>` | 歌词 |
| `_palette` | `AlbumColorExtractor.Palette` | 取色结果 |
| `_groupHeaders` | `Dictionary<string, FrameworkElement>` | **持有了 UI 元素引用** |
| `_audioCapture` | `AudioCapture?` | **持有 WASAPI 捕获，未释放可能占用设备** |
| `_pipWindow` | `PiPWindow?` | 窗口引用 |
| `_hHook` | `HHOOK?` | **键盘钩子** |

**低风险项**（不需要清理）：各种 `bool`/`int`/`double` 标志位与时间戳（约 40 个），如 `_dragActive`、`_isFullScreen`、`_lastMouseMoveSent`。

**已核查释放路径，三项结论：**

| 字段 | 有释放路径？ | 位置 |
|---|---|---|
| `_audioCapture` | **有** | `StopRhythm()` 调 `Dispose()` 并置 null（`Music.cs:556`），由 `CloseAlbum` 与音乐页退出调用 |
| `_hHook` | **有** | `CleanupKeyboardInput()` 调 `UnhookWindowsHookEx` 并置 null，由 `Unloaded` 调用（`xaml.cs:224`） |
| `_groupHeaders` | **没有** | `TeardownLibrary` 只退订 2 个事件 + 停 1 个计时器，**不清任何缓存** |

`TeardownLibrary` 全文只有 7 行：

```csharp
LibraryGrid.ElementPrepared -= LibraryGrid_ElementPrepared;
_libraryIdleTimer.Stop();
_libraryIdleTimer.Tick -= LibraryIdleTimer_Tick;
if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
    cardMenu.Closed -= LibraryCardMenu_Closed;
```

**真正的问题不是"三个泄漏"，而是一个更准确的结论：`

> `TeardownLibrary` 拆的是**事件**，不是**数据**。

它退订了该退订的，但 `_libraryAll`（全量条目）、`_libraryThumbnails`（缩略图字典，内存占用最大）、`_groupHeaders`（持有 `FrameworkElement` 引用）、`_albumTracks`、`_lyrics`、`_palette` 全部保留。

**这解释了"封面切换菜单后消失"那个历史缺陷的成因**：缓存与条目生命周期没有统一归属，所以替换 entry 时漏搬缩略图。

**待确认**：是否批准在 `TeardownLibrary` 中补上这 6 个缓存字段的清理？

**注意**：这不是"泄漏"（页面被回收时字段随之回收），而是**在页面存活期间切换分区/重新加载时，旧数据仍然占着内存且可能被误用**。修复价值在于一致性与内存，不在于阻止崩溃。

### P1-5 TODO 10 条

只有 1 条是纯噪声（`MpvPlayerPage_MpvMenu.cs:124` 的空 `//TODO`），可删。
其余 9 条是作者的重构笔记，**建议保留**——删掉等于删信息。

---

## P2：保留，需整改但非清理

| 项 | 原因 |
|---|---|
| `Modules/Motion/` 全部 | 在用；H2 决策后需改造而非删除 |
| `Themes/Generic.xaml` | `SliderExt`/`CheckBoxExt` 的隐式样式必需 |
| `MpvConfTextWindow` 代码高亮色板 | 见 P1-2 b 档 |
| 6 个 `ControlTemplate` | 已核查全部合规 |

---

## 实施顺序建议（与你的决策有冲突，请裁决）

你的选择里 A（NavigationView）与 E（ConnectedAnimation）都是结构性变更。我建议：

| 批次 | 内容 | 理由 |
|---|---|---|
| **① P0** | 删除 `ResizeGrip` | 零风险，立即做 |
| **② P1-5** | 删 1 条噪声 TODO | 零风险 |
| **③ P1-2a/b** | 颜色分档处理 | 独立，无依赖 |
| **④ P1-4** | 状态清理 + J 迁移 | 独立，但需先确认三个资源释放路径 |
| **⑤ P1-1** | 间距 B2 | 视觉变化，单独提交并截图 |
| **⑥ P1-3** | 字号 | 分批 |
| **⑦ A** | NavigationView 改造 | 结构性，单独一批 |
| **⑧ E** | ConnectedAnimation | **必须在 A 稳定之后** |
| **⑨ H2** | 弹簧改 SoftLanding | 独立 |

---

## 我需要你回答的问题

1. **P1-2 的 b 档（代码高亮色板）是否保留？**
2. **P1-2 的 c 档**：我查出 `#FFFF8C00`/`#FF00BFFF` 的用途后是否需要你再确认？
3. **P1-1**：16 个非标准值中"间距 vs 尺寸"的归类，你希望我逐条列出后你判断，还是授权我自行判断并在提交信息里说明依据？
4. **P1-4**：是否批准在 `TeardownLibrary` 中补齐 6 个缓存字段的清理？（`_audioCapture` 与 `_hHook` 已有释放路径，不需要动）
5. **实施顺序**是否接受"⑧ 必须在 ⑦ 之后"这一约束？

---

## 附：本文档的自我限制

- 我没有改动任何代码，`git status` 应只有本文件与任务书。
- 上面每个数字都能用 `audit_measure.py`、`audit_owners2.py`、`s2_state.py` 复现。
- P1-4 的"高风险"判断基于字段类型与名称，**尚未**逐条追踪真实读写路径。若你批准进入实施，我会先补这一步再动手。
