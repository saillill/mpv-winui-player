# 03 · 设置页：信息架构与页面布局线索

> **现状（实测 2026-10-30，`bd38e29` 之后）**：独立 `SettingsWindow`（`BaseWindow` 子类），
> 内部是 **NavigationView 左侧图标面板**（`SettingsPage.xaml:31,49`），内容区由
> **677 个选项**驱动——手写 320（`SettingsPage.Options.*.cs`）+ schema 生成 357
> （`SettingsPage.Options.Gen.cs`）——经 **11 个 `Option*Control` 渲染器**
> （`Controls/`；第 12 个 `OptionListControl` 是列表容器，不是渲染器）与
> **11 个 `DataTemplate`** 一一对应。
> `SettingsPage.Options.cs`（87KB）里有 `sectionMap` / `sectionOrder` / `optionOrder` /
> `categoryOrder` 四张表；`SettingsPage.Options.Gen.cs` **376KB 为生成文件**，不可手改。
>
> **本文的落点原本是"重排信息架构与视觉层次"。这件事已经做完了**——见下面的
> 「已完成」一节。现在本文的价值是留存当时的线索与判据，不是在提议一件事。

## 已完成（本文件写作时仍是提议的部分）

| 当时的提议 | 落地情况 |
|---|---|
| 是否引入 CommunityToolkit | **已引入** `CommunityToolkit.WinUI.Controls.SettingsControls` 8.3.260402-preview2（MIT，已登记 `data/licenses/THIRD_PARTY_NOTICES.md`） |
| 设置项怎么分层 | 分区 69 → **87**，分类 6 → **8**（新增「快捷键」「输入行为」） |
| 折叠语义的三处同步 | **已收为两处**：`Option.AdvancedSection` + `SettingsPage.FoldedSections`；`CollapsedSectionIds` 这个粘性依赖属性已删除，折叠状态改由 `SectionCardItem.IsFolded` 承载 |
| 行容器 | 全部换 `SettingsCard`，标题与描述由卡片绘制，渲染器只留交互控件 |
| 展开粒度 | `SectionShapes.cs`：**30 个分区原地展开，24 个卡片跳转**，按语义归属而非行数判断 |

六个 commit：`1f0261a`（生成器重排）、`25277b9`（数据层）、`bf6a60a`（视觉层 + 三个分类）、
`7ddc5d4`（导航按 caption 配对、行不再播报类名）、`f13712a`（11 条生成行升到一级）、
`bd38e29`（D13 语义展开）。

**本文以下内容保持原样**，作为当时的线索记录与判据留存；其中「待决策」项已全部落定，
逐条结论见上表。


---

## 一、线索清单

### 1.1 官方与社区标准控件

| 项 | 来源 | 技术栈 | 核心功能 | 可参考 / 可复用 | 与本项目的关系 |
|---|---|---|---|---|---|
| **SettingsCard / SettingsExpander** | [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) · [SettingsExpander 文档](https://learn.microsoft.com/dotnet/communitytoolkit/windows/settingscontrols/settingsexpander) · [NuGet](https://packages.nuget.org/packages/CommunityToolkit.Uwp.Controls.SettingsControls/) | WinUI 3（`CommunityToolkit.WinUI.*`） | `SettingsCard`：左侧图标+标题+描述，右侧操作控件；`SettingsExpander`：继承 SettingsCard，可展开一组卡片 | **整个仓库带 `settings.xamlstyler`**（见第 07 项）；卡片可直接嵌进 Expander | **已引入**：`CommunityToolkit.WinUI.Controls.SettingsControls` 8.3.260402-preview2，`SettingsCard` 是设置行的容器。**但 `SettingsExpander` 未被采用**——它折叠时不隐藏内容（`IsExpanded=False` 仍把 `ItemsSource` 的行全部画出），且 `ItemTemplate` 只接受 `DataTemplate`（`ItemTemplateSelector` 在 XML 文档里有、类型上没有，WMC0011）。原地展开改用 WinUI 原生 `Expander`。详见 `bd38e29` 的提交信息 |
| **SettingsExpander 设计沿革** | [PR #253 (Labs-Windows)](https://github.com/CommunityToolkit/Labs-Windows/pull/253) | — | 记录了这个控件为什么继承 SettingsCard、为什么 Items 可增删 | 读设计动机，避免重造 | 说明"卡片+可展开"是社区验证过的组合 |
| 官方选型原则 | Toolkit README | — | 「一旦 Windows SDK 自身提供等价能力，该组件标记为 deprecated」 | 判断何时该用 platform 而非 toolkit | 决定引入依赖前应先查 WASDK 是否已内建等价物 |

### 1.2 Windows 11 设置应用的信息架构

| 项 | 来源 | 可参考部分 |
|---|---|---|
| Windows 11 设置应用分类体系 | [Wikipedia: Settings (Windows)](https://en.wikipedia.org/wiki/Settings_(Windows)) | 顶层分类：System / Bluetooth & devices / Network & internet / Personalization / Apps / Accounts / Time & language / Gaming / Accessibility / Privacy & security / Windows Update。**这是微软自己用一个 1000+ 设置项的应用验证过的分层** |
| 多级导航模式 | 同上，及 [MS Learn 培训页](https://learn.microsoft.com/training/modules/explore-common-configuration-options/4-use-settings-app) | 「左侧持久导航栏 + 分类页内子导航」的两级模型 |

### 1.3 官方设计规范

| 文档 | 用途 |
|---|---|
| [ItemsView 指南](https://learn.microsoft.com/windows/apps/design/controls/itemsview) | 若设置项改用卡片网格时的集合控件 |
| WinUI Gallery（`winapp find-ui`） | 本机可查 `SettingsCard` 类示例的现行写法；`winapp find-ui "settings card"` |

### 1.4 同类开源实现

| 项目 | 许可 | 可参考部分 |
|---|---|---|
| [Fusion](https://github.com/zoinve/Fusion) | **MIT** | 它的设置页规模小，但**分层方式**（Core/Api/UI）可对照 `SettingsPage.Options.*` 的分部类组织 |
| [Rhythm](https://github.com/FallenDeity/Rhythm) | 见仓库 | 有独立 Settings 页；仓库带 `Settings.XamlStyler` |

---

## 二、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| **SettingsCard / SettingsExpander** | **已采用**（`SettingsCard`）；`SettingsExpander` 只作参考 | **已完成**：新增 NuGet 包 + 11 个渲染器瘦身为只留交互控件 | ①引入了一条供应链（MIT，已登记）；②当时的 `sectionMap`/`folding` 三处同步（`Option.AdvancedSection`、`FoldedSections`、`CollapsedSectionIds`）**现已收敛为两处**——`CollapsedSectionIds` 这个粘性依赖属性已删除。教训：粘性属性本身是脆弱点，折叠状态属于分区模型，不属于控件 |
| Windows 11 分类体系 | 高 | 低（纯设计参考） | 本项目的 677 个选项与 Windows 设置的领域不同，**分类不可照搬**，但"两级导航 + 每页不超过 N 项"的约束可借 |
| `SettingsPage.Options.Gen.cs` | — | — | **376KB 生成文件，禁止手改**（`tools/gen-schema-options.py` 会覆盖）。任何 IA 调整必须落在生成器的输入上 |
| 现有 11 个 Option 渲染器 | — | — | 已是完整体系；本会话只把它们的外壳换成 `SettingsCard`，**没有重写控件本身** |

### 已识别的取舍点（供后续会话决策）

> **四项已全部落定**，结论见下表；保留原文是为了记录当时为什么这样问。

| # | 当时的问题 | 结论 |
|---|---|---|
| 1 | 是否引入 CommunityToolkit | 引入了 `SettingsCard`；`SettingsExpander` 试用后放弃（折叠不隐藏内容），改用 WinUI 原生 `Expander` |
| 2 | 677 个选项怎么分层 | 分区重排到 87、分类到 8；常用项靠 `pinnedOverviewOptions` 升到一级（本会话新增 11 条生成行） |
| 3 | 折叠语义三处同步 | 收敛为两处，删掉 `CollapsedSectionIds`；新增 `SectionShapes` 决定原地展开还是跳转 |
| 4 | 搜索索引是否需要同步 | 需要。语言切换走 `RebuildLocalizedContent`，搜索索引随之重建；就地刷新方案评估后放弃（见下） |

**第 4 项的细节，值得记下来**：原计划"语言切换就地刷新，不重建树"。实测表明分类名与
分区名本身就是分组用的字符串（`Option.Category` / `Option.Section`），就地刷新只能换
`Label`/`Description`/`Warning`，树仍会中英混杂。最终保留 `RebuildLocalizedContent`，
但让它用 `SectionId` 记住并恢复用户钻入的分区——收益相同，改动只有十几行。


---

## 三、检索提示词

### 3.1 搜索式

```
CommunityToolkit.WinUI SettingsCard SettingsExpander WinUI 3
WinUI 3 settings page information architecture many settings
"SettingsExpander" vs "Expander" WinUI settings UI
Windows 11 Settings app navigation design guidelines categories
WinUI settings search AutoSuggestBox BuildSearchEntry pattern
CommunityToolkit WinUI license MIT
```

### 3.2 交给后续会话的提示词

> **这一节已作废。** 它描述的任务（引入 toolkit、重排分层、收敛三处同步、同步搜索索引）
> 已于 2026-10-30 完成，原文保留只为记录当时的问题形态。
>
> 若你现在接手设置页，需要的上下文是：
>
> - 行容器是 `SettingsCard`，标题/描述由卡片绘制，11 个渲染器只留交互控件
> - 折叠分区由 `SectionCardItem.IsFolded` 表示，是否原地展开由 `SectionShapes` 决定
> - 分类与分区都是**按 caption 字符串**分组的，所以任何新增分类必须给它独立的 caption，
>   否则会和别人合并（这是「快捷键」当初混在「界面」里的原因）
> - `SettingsPage.Options.Gen.cs`（376KB）禁止手改，改动落在 `tools/gen-schema-options.py`
> - 已知缺口：`vo` / `ao` / `gpu-api` 因 mpv 声明为 `Object settings list`，生成器不产出行，
>   设置页里**根本没有这三个选项**。见 `tools/pick-overview-rows.py:208` 的注释


### 3.3 深挖方向

- 通读 `SettingsPage.Options.cs` 的四张表，统计每分区实际选项数 → 找出"一页塞太多"的证据
- `docs/settings-contract.md` 是本仓库对设置项的权威约束，重构前必读
- 查 `check-sections.py` / `check-empty-sections.py` / `check-settings-drift.py` 三个检查器，
  它们会约束任何 IA 变更
- **现在可直接用的两个工具**：
  - `python tools\list-folded-sections.py` —— 每个分区所属分类、行数、是否折叠
  - `python tools\pick-overview-rows.py` —— 常用项清单与"哪些进不了一级"的原因
  - `python tools\ui-design-audit.py --board settings` —— 断言导航栏与行语义（会启动应用）
