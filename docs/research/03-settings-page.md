# 03 · 设置页：信息架构与页面布局线索

> **现状（实测）**：独立 `SettingsWindow`（`BaseWindow` 子类），内部是
> **NavigationView 左侧图标面板**（`SettingsPage.xaml:31,49`），内容区由
> **356 个选项**（`SettingsPage.Options.Gen.cs` 中 `Key = nameof` 计数）与
> **18 个 `Option*Control` 渲染器**（`Controls/`）驱动。
> `SettingsPage.Options.cs` 86KB 里有 `sectionMap` / `sectionOrder` / `optionOrder` / `categoryOrder`
> 四张表——**信息架构已经存在，只是由代码而非设计驱动**。
> 另有 `SettingsPage.Options.Gen.cs` **259KB 为生成文件**，不可手改。
>
> **所以本文的落点是"重排信息架构与视觉层次"，不是"重写设置控件"。**

---

## 一、线索清单

### 1.1 官方与社区标准控件

| 项 | 来源 | 技术栈 | 核心功能 | 可参考 / 可复用 | 与本项目的关系 |
|---|---|---|---|---|---|
| **SettingsCard / SettingsExpander** | [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) · [SettingsExpander 文档](https://learn.microsoft.com/dotnet/communitytoolkit/windows/settingscontrols/settingsexpander) · [NuGet](https://packages.nuget.org/packages/CommunityToolkit.Uwp.Controls.SettingsControls/) | WinUI 3（`CommunityToolkit.WinUI.*`） | `SettingsCard`：左侧图标+标题+描述，右侧操作控件；`SettingsExpander`：继承 SettingsCard，可展开一组卡片 | **整个仓库带 `settings.xamlstyler`**（见第 07 项）；卡片可直接嵌进 Expander | **本项目当前未引用该包**（`mpv-winui.csproj` 只有 CsWin32/CsWinRT/WASDK/NAudio/NLog/System.Management）。**属于新增依赖，需单独决策** |
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
| **SettingsCard / SettingsExpander** | **高（若允许加依赖）** | **中**：需新增 NuGet 包 + 改造 18 个 `Option*Control` 的外壳 | ①本项目零 toolkit 依赖，引入即新增一条供应链；②现有 `sectionMap`/`folding` 逻辑（`Option.AdvancedSection`、`FoldedSections`、`CollapsedSectionIds` 三处同步）需与 Expander 对齐，**这是三个必须同步的地方**，改错会静默失效 |
| Windows 11 分类体系 | 高 | 低（纯设计参考） | 本项目的 356 个选项与 Windows 设置的领域不同，**分类不可照搬**，但"两级导航 + 每页不超过 N 项"的约束可借 |
| `SettingsPage.Options.Gen.cs` | — | — | **259KB 生成文件，禁止手改**（`tools/gen-schema-options.py` 会覆盖）。任何 IA 调整必须落在生成器的输入上 |
| 现有 18 个 Option 控件 | — | — | 已是完整体系，**优先级应低于 IA 重排**；先动架构再动控件 |

### 已识别的取舍点（供后续会话决策）

1. **是否引入 CommunityToolkit**：这是本项唯一的硬决策，影响供应链与许可
2. **356 个选项怎么分层**：沿用现有 `sectionMap`，还是按 Windows 设置的两级模型重排
3. **折叠语义**：`AdvancedSection` / `FoldedSections` / `CollapsedSectionIds` 三处必须同步，
   若引入 Expander，是把三处收敛到一处还是继续三处
4. **搜索**：设置页已有 `AutoSuggestBox` + `SettingsSearchHistory`（注册表里可见），
   重排后搜索索引（`BuildSearchEntry`）是否需要同步调整

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

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`），设置页是独立窗口，
> 用 NavigationView + 356 个选项，选项由 `SettingsPage.Options*.cs` 里的
> `sectionMap` / `sectionOrder` / `optionOrder` / `categoryOrder` 四张表驱动，
> 18 个 `Option*Control` 负责渲染，`SettingsPage.Options.Gen.cs`（259KB）是生成文件。
>
> 目标：大幅重构设置页的信息架构与页面布局。
>
> 请先回答，不要写代码：
> 1. 引入 `CommunityToolkit.WinUI.Controls.SettingsControls` 的收益是否值得新增一条依赖？
>    本项目当前零 toolkit 依赖。
> 2. 356 个选项如何分层？现有 `sectionMap` 的分区粒度是否合理？给出判据。
> 3. `AdvancedSection` / `FoldedSections` / `CollapsedSectionIds` 三处同步是已知的脆弱点，
>    若改用 SettingsExpander，这三处能否收敛为一处？
> 4. 重排后 `BuildSearchEntry` 的搜索索引需要怎样同步？
>
> 注意：`SettingsPage.Options.Gen.cs` 禁止手改，改动必须落在 `tools/gen-schema-options.py` 的输入上。

### 3.3 深挖方向

- 通读 `SettingsPage.Options.cs` 的四张表，统计每分区实际选项数 → 找出"一页塞太多"的证据
- `docs/settings-contract.md` 是本仓库对设置项的权威约束，重构前必读
- 查 `check-sections.py` / `check-empty-sections.py` / `check-settings-drift.py` 三个检查器，
  它们会约束任何 IA 变更
