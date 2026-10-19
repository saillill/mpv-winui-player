# 设置界面设计约束（唯一依据）

> **状态**：v1，2026-09-21 制定。
> **适用范围**：`mpv-winui/mpv-winui/Modules/Settings/**`、`Modules/Language/**`、
> `Styles/Styles.xaml`、`tools/**` 中与设置相关的检查器，以及 `mpv-winui-lazy/mpv.conf`
> 的托管区块。
>
> **效力**：本文件是设置界面后续**任何**改动的唯一依据。与本文件冲突的旧文档
> （含 `docs/settings-ui-review.md` 的改造计划章节、`docs/compare-upstream.md`
> 的比较结论、`.workbuddy/memory/*.md` 的经验条目）**一律以本文件为准**。
> 违反本文件的改动不得提交。
>
> **基线**：`d0a06d9`（本地 HEAD，领先 `origin/main` = `3053352` 26 个提交）。
> 文中每条约束都注明**为什么存在**（通常是踩过的坑）与**怎么验证**。

---

## 0. 现状速览（所有数字均在本机实测）

| 项 | 值 | 出处 |
|---|---:|---|
| 顶层分类 | **9** | `Layout/SettingsSections.cs` `Categories` |
| 分区 | **59**（46 手写 + 13 生成） | 同上 `All`；`check-sections` 复核 |
| 设置行总数 | **874** 字面量 = **871** 唯一静态键（322 手写 + 549 生成）+ 3 运行时模板 | 括号配平解析 + 独立正则交叉验证 |
| mpv 选项暴露 | **815 / 820**（未暴露 1：`glsl-shaders`） | `report-missing-options.py`（已修正） |
| AppLang 属性 | **2439**（手写 + 2 个生成 partial） | `check-localization` |
| 翻译缺口 | 9 个语言文件**各缺 1098 个**（`SchemaLabel_*` / `SchemaDesc_*`，即全部生成行） | `check-localization` |
| 折叠卡片数 | **35**（+6 被分类下限切平） | `diagnose-advanced-flag.py` |
| 卡后行数 / 总览 pin 行 | **767 / 5** | 同上 |
| 内容列最大宽度 | **1060** | `Styles.xaml` `MpvContentMaxWidth` |
| 间距令牌化率 | **74/109 = 67.9%**（25 个非零字面量 + 10 个字面 0） | 绑定审计 §6 |
| 资源引用 | 202 处 / 76 个键，**0 个解析失败**，2 个零引用 | 同上 §2 |
| `x:Name` | 127 个声明，**0 个断引用**，11 个零引用 | 同上 §1 |
| 重复 `Option` 键 | **0**（跨层与层内均为 0） | 选项审计 + §5.4 |
| `sectionOrder` 覆盖 | **46 / 46**，0 重复键、0 悬空 | `audit/check-section-order.py` |
| 检查器 | **17 个：15 通过 / 2 失败**（`check-settings-drift` 7 ERROR、`check-localization` 1098×9 缺键） | `run-baseline.py` |
| Release 构建 | **≈24–29s，0 警告 0 错误** | 本机实测（多次） |

---

## 1. 文件布局与职责边界

> **先把归属说清楚（否则会重复设计已有的东西）。**
> 「分类/分区表 + 四张映射表 + 折叠机制」这套架构**不是本分支发明的 ——
> 上游 `saillill/mpv-winui-player` 已经有**（上游提交 `93bf0b4`，是 `3053352`
> 的祖先）。`Layout/SettingsSections.cs` 的**类级文档注释与上游逐字节相同**；
> 本地相对上游只改了**表内容**（+58/−13 行）。
>
> ⇒ 本分支的偏离是**两个子系统 + 表内容**，不是架构：
> ① **schema 生成层**（549 行、757 KB、6 个文件）；
> ② **`MpvConfWatcher`**（运行时重新读取 `mpv.conf`，242 行）。
> 其余 24 个提交是**缺陷修复**（其中 6 个布局提交在修前 10 个提交造成的回归）、
> 检查器更新、或命名问题。
> **本文件因此描述"架构 + 本分支的偏离"，不是"本分支的设计"。**

```
Modules/Settings/
  SettingsPage.xaml(.cs)            页面外壳：三行内容列、导航、搜索、下钻、页脚
  SettingsPage.Options.cs           ★ 选项树中枢：BuildSettings() + 4 张表 + 2 个下限
  SettingsPage.Options.<域>.cs      手写选项行（按主题切分，见 §2.3）
  SettingsPage.Options.Gen.cs       ⚙ 生成：549 条 schema 行 —— 禁止手改
  SettingsPage.Sections.Gen.cs      ⚙ 生成：生成分区的 sectionMap/sectionOrder 注册
  SettingsPage.Actions*.cs          跨行动作：导入导出、快捷键、系统信息
  SettingsPage.Customize*.cs        自定义布局模式（拖动/重命名/隐藏/新建文件夹）
  SettingsPage.Options.Layout.cs    布局草稿的应用
  Layout/SettingsSections.cs        ★ 分类与分区目录（唯一身份表）
  Layout/SettingsLayout.cs          布局草稿的持久化模型
  Controls/Option*.xaml(.cs)        每种类型的行渲染器
  Controls/OptionListControl.*      ★ 列表宿主：卡片/表头发射、逐行可见性
  Controls/OptionTemplateSelector   类型 → DataTemplate 映射
  AppSettings*.cs                   属性与默认值（含 .Gen）
  MpvSettings.cs                    ★ key → mpv 命令/选项名（含 .Gen）
  MpvConfOverrides.cs               归属判定：这份 mpv.conf 行是谁写的
  MpvConfWatcher.cs                 mpv.conf 外部改动 → 界面同步
  ManagedMpvConfig.cs               托管区块的读写
```

**⚙ 生成文件的铁律**：带 `.Gen.` 的文件头都写着 *"Do not edit by hand"*。
改生成内容只有两个合法入口：**改生成器**（`tools/gen-schema-options.py`）或
**改 schema**（`MpvConfOptions/mpv-options.json`）。手改生成文件的下一轮生成会静默丢失。

---

## 2. 结构规范

### 2.1 内容列：三行必须同构

设置页内容区是**三行**，每行都是页面 `Grid` 的**直接子元素**，各自携带**同一对**
属性：

| 行 | 元素 | MaxWidth | 对齐 | 水平内缩 |
|---|---|---|---|---|
| 0 面包屑 | `BreadcrumbBar` (Grid) | `MpvContentMaxWidth` | `Stretch` | `MpvBreadcrumbBarMargin` 的 12 |
| 1 卡片列 | `BrowseHost` (ScrollViewer) | `MpvContentMaxWidth` | `Stretch` | `Margin="12,4,12,0"` |
| 2 页脚 | Border | `MpvContentMaxWidth` | `Stretch` | `MpvFooterPadding` 的 12 |

**三条不可违反的规则**：

1. **`MaxWidth` 必须配 `HorizontalAlignment="Stretch"`，不能配 `Center`。**
   `Center`（以及 `Left`/`Right`）会把那一行按**自身内容**定尺寸再摆放。三行的
   内容宽度天然不同：面包屑"返回箭头 + 标题"约 150px，`BrowseHost` 里的卡片列
   约 685px。各自居中就是各自一条左缘 —— 标题飘在窗口正中、卡片另起一列，
   三行同构的意义荡然无存。
   `Stretch` 让三行都拿到内容区的**有限**宽度，左缘必然重合；宽度超过 1060 时
   钳出来的盒子停在起始边、右侧留白，这正是**左对齐**要达到的效果。
   *验证*：`check-scroll-maxwidth.py`（拒绝被无限宽度测量的 MaxWidth 列）、
   `check-xaml.py`；目视三行左缘是否重合。
   *历史*：本条曾经写的是 `Center`，理由是"让钳制对称"。对称只在窗口宽到三行
   都触到 1060 时成立，窄窗口下退化成上面那种各居各的，实测即为此症状。
2. **`MaxWidth` 与水平内缩必须在同一个元素上。**
   曾经 `MaxWidth` 在内层 `StackPanel`、12px 内缩在外层 `ScrollViewer`，
   于是卡片列宽 1060 而面包屑/页脚宽 1036 —— 卡片比标题两侧各宽 12px。
   *验证*：目视三行左缘 + 宽度；`check-scroll-maxwidth.py` 报"不可居中的 MaxWidth 列"。
3. **不要为了"整理结构"给这三行加包装层。**
   一旦包进一个铺满全宽的 `Grid`，内层 `MaxWidth` 盒子就拿不到内容区宽度，
   于是它按**自身内容**定尺寸 —— 面包屑只有约 150px 宽，居中那个 150px 盒子，
   左缘跑到窗口中间。改布局属性时**优先改属性，不要改层级**。

> 反面案例记录在 `SettingsPage.xaml:63-82` 与 `:176-191` 的注释里，改这三行前先读。

### 2.2 滚动面：全页恰好一个

```
BrowseHost (ScrollViewer)              ← 全页唯一滚动面，持有垂直滚动
  └ StackPanel
      ├ OptionListControl               ← 内部 ListView 关闭自身滚动
      └ SectionsHost (ListView)         ← 关闭自身滚动
```

**规则**：**内层列表拒绝自己的滚动时，宿主必须有滚动；反之亦然。两者必须恰好一个。**

具体写法（三处都要，缺一即出问题）：

| 位置 | 必须设置 |
|---|---|
| `BrowseHost` | `HorizontalScrollMode="Disabled"` + `HorizontalScrollBarVisibility="Disabled"` |
| `OptionListControl` 内的 `OptionListView` | 四个滚动属性全部 `Disabled` |
| `SectionsHost` | 四个滚动属性全部 `Disabled` |

**为什么水平方向必须 Disabled**：`ScrollViewer` 默认横向 `Enabled`/`Auto` 时，
它会**用无限宽度去测量内容**（因为内容允许比视口宽）。于是 `MaxWidth` 盒子拿不到
有限父宽，`HorizontalAlignment` 被**静默忽略** —— 属性完全正确，就是不生效。

**为什么内层不能留滚动**：`StackPanel` 给子元素**无限高度**。此时若内层 `ListView`
还有一个可用的内部 `ScrollViewer`，它会报告**视口高度**而不是内容高度 ——
超出那一屏的行**既没被绘制，也没被算进外层滚动范围**。症状是「右侧没有滚动条、
页脚盖住剩下的内容」。

**`OptionListControl` 根 Grid 的行高必须是 `Auto`**：在无限约束下 `*` 行不是
"填满剩余"，它会退化。一个不自已滚动的列表要向外层贡献完整高度，就该是 `Auto`。

> ⚠️ **代价必须知情**：内容被以有限高度测量 ⇒ 内层 `ListView` **无法虚拟化**。
> 这个代价由折叠规则承担：最大的生成分区（205 行）是折叠分区，到达列表时是
> **1 张卡**而不是 205 行。**若有人取消折叠或大幅放开分类下限，必须重新评估这条。**

*验证*：`check-scroll-maxwidth.py`、`check-xaml.py`。

### 2.3 折叠状态：三处表示，必须同步

「这个分区折叠吗」由三处承载，**各有独立消费者，都不能删**：

| 层 | 载体 | 语义 | 消费者 |
|---|---|---|---|
| 行 | `Option.AdvancedSection` (bool) | **这一行在卡片后面** | `OptionListControl.xaml.cs` 逐行判断 |
| 页 | `SettingsPage.FoldedSections` (IReadOnlySet) | 分区是否折叠 | `SectionSummaries()` 分区级决策 |
| 控件 | `OptionListControl.CollapsedSectionIds` (**粘性 DP**) | 哪些 sectionId 出卡片 | `ApplyItemsSource()` 建卡 |

**不变量（必须遵守）**：

1. **行级标志的语义是「这一行在卡片后面」，不是「这一行的分区是高级的」。**
   所以一个折叠分区里，被 pin 的行是 `false`、其余是 `true` ——
   **同一分区内两种值并存是设计，不是 bug**。
2. **分区级决策只读 `FoldedSections`，绝不读行。**
   曾经 `SectionSummaries()` 用 `g.First().AdvancedSection` —— 一个分区的答案
   取决于**哪一行排第一**，列表任何重排都会让它在一张卡片和一堵行之间翻转。
   这就是「多动两下卡片就混乱」的第一个机制。
3. **任何给 `OptionList` 赋值的分支，都必须同时设置 `CollapsedSectionIds`。**
   它是粘性依赖属性，没有人替你清。目前有 **4 个分支**各自显式清空
   （搜索 / 分类总览 / 下钻 / 单分区兜底）。**漏一个就复现「卡片点开还是卡片」**。
   *这是本文件里最容易违反的一条 —— 新增分支时请对照现有四个。*
4. **行的 `AdvancedSection` 只在 `sectionMap` 解析完之后派生，且必须双向赋值。**
   生成行自带 `true`，必须能回到 `false`；否则被下限切平的分区会留下
   声称"被隐藏"的行，而卡片已经不存在。

### 2.4 分区连续性：排序不得打散分区

`ApplyLayout()` 的排序键是 `(Category, Section)` 分组，组内才用 `_layout.Order` 的
拖动 rank。

**规则**：**重排限定在行自己的分区内**。把行移到别的分区是**另一个手势**
（拖到文件夹上），它改写行的 `Section` 而不是 rank。
分区**顺序**由 `_layout.SectionOrder` 拥有，不在排序里定。

**第二层防御**：`OptionListControl.ApplyItemsSource()` 用
`HashSet<string> introduced` **按身份**判断"这个分区是否已发过卡片"，
而不是比较"当前行与上一行的 Section 是否不同"。
后者只在分区连续时成立；不连续时每次重新进入该分区都再发一张卡 ——
同一分区的卡片在页面里出现三次、位置互不相关。
**按身份计数让重复卡片不可表示**，无论列表怎么排。

*验证*：`diagnose-advanced-flag.py` 的 `check_order_keeps_sections_together()`
做的是**形状检查**（源码里必须出现 `GroupBy(x => (x.option.Category, x.option.Section))`）。
刻意不做行为检查：这段逻辑是纯 C#，在检查器里重实现一定会漂移。

### 2.5 两个下限

| 常量 | 值 | 回答的问题 | 作用 |
|---|---:|---|---|
| `FoldFloor` | **4** | 这张卡值得点开吗？ | 卡后幸存行 < 4 的分区**切平** |
| `CategoryFloor` | **8** | 这个分类还是一个页面吗？ | 分类可见行 < 8 时，按分区从小到大切平 |

> 两个数的推导依据（代码注释里写明）：`FoldFloor = 4` 是因为**卡片本身就是一行**，
> 藏两行的卡片比它藏的行还长；`CategoryFloor = 8` 是因为窗口高度下限
> **560px 要能显示"一屏行"**（`SettingsWindow.xaml.cs:45-48` 原文：
> "480 leaves room for about six rows -- fewer than one category holds"，
> 560 才是一屏）。**改 560 必须重算 `CategoryFloor`。**

- 两者**单向否决**：源码里的折叠名单是"人的意图"，下限只是否决权
  （可以取消折叠，不能凭空新增折叠）。
- `FoldFloor` 必须按 **pin 之后幸存的**行数判定，不能按分区名义大小 ——
  曾经 `ProgramTesting` 名义 4 行、3 行被 pin，卡片打开只有 1 行。
- `CategoryFloor` 必须先看 `FoldFloor` 的结果，且**切平最小的分区优先**
  （最少打扰卡片，大的深分区保持折叠）。分类**不能开空**：
  Network / OSD 都曾出现「打开是一片卡片、一个能直接调的项都没有」。
*验证*：`diagnose-advanced-flag.py`（模拟真实代码路径，含 pin 与
「隐藏行必须有卡片」不变量）、`report-fold-cards.py`。

### 2.6 行模板与卡片外壳

- **卡片外壳当前是 `OptionListControl.xaml` 的局部 `Style`（`OptionCardStyle`），
  不是应用级令牌。** 11 个 `DataTemplate` 各自只写三行
  （`Border Style=` + 宿主控件 + `x:Bind`）。**禁止在模板里重述圆角/内边距/背景/边框。**
- **另外两个"卡片外壳"是刻意的例外，因为它们内容不同**，不适用 `OptionCardStyle`：
  `OptionListControl.xaml:129-137`（自定义模式的分区行，Padding `12,8,12,8`）
  与 `:216-225`（分区卡片，Padding `12,12,12,12`）。
  *待办*：提升为应用级令牌时**不要直接 `BasedOn`** —— 两者 Padding 不同，
  需要各自覆盖（风险：中）。
- **类型 → 模板的映射只有一处**：`OptionTemplateSelector.SelectTemplateCore` 的
  switch。`OptionType` 目前 11 个成员，**全部已覆盖**；`default` 是防御性兜底。
  **新增 `OptionType` 必须同时加模板、加 selector 分支、加 `OptionCardStyle` 外壳。**
- **行的文本三角色**由三个样式统一，禁止在控件里重述：
  `MpvOptionLabelStyle`（1 行 + 省略号）、`MpvOptionDescriptionStyle`
  （1 行 + 省略号 + 次级前景）、`MpvOptionWarningStyle`（可换行 + 警示色）。

**为什么标签与描述都是 1 行 + `NoWrap` + `TextTrimming`**：
描述与控件在**同一个 Grid 行**里，多折一行就把整行拉高、把控件拽离垂直居中。
`MaxLines` 与 `TextTrimming` **必须同时用**：只 `MaxLines` 会静默截断；
只 `TextTrimming` 在换行文本上不触发。且必须 `NoWrap` ——
`Wrap` + `MaxLines=1` 会在"最后一个放得下的词"处断行，省略号落在那个断点、行尾参差。

---

## 3. 配置项命名与分组规则

### 3.1 身份与命名

| 概念 | 形态 | 例 | 说明 |
|---|---|---|---|
| 分类 key | `lowercase` 单词 | `program` `playback` `video` | 仅用于排序与布局持久化 |
| 分类 AppLang | `SettingsCategory<Name>` | `SettingsCategoryProgram` | 界面显示名 |
| 分区 id | **AppLang 属性名** | `SectionProgramInterface` | ★ 持久化的稳定身份 |
| 分区标题 | `Section<Name>` | `SectionProgramInterface` | 可翻译，**不可作身份** |
| 分区描述 | `SectionDesc<Name>` | `SectionDescProgramInterface` | 与标题**显式配对**，不是命名约定推出来的 |
| 生成分区 | `SectionGen<Theme>` | `SectionGenVideoRendering` | 前缀 `Gen` 标明来源 |
| 选项 key | `nameof(AppSettings.X)` | `nameof(AppSettings.Hwdec)` | 三种合法拼法见 §6.2 |
| 生成选项 | `AppSettings.Mpv_<下划线化> ` | `Mpv_scale_radius` | 下划线映射到 mpv 的连字符 |
| mpv 选项名 | 连字符 | `scale-radius` | 由 `ToMpvOptionName` 映射 |

**铁律**：

1. **分区的身份是 AppLang 属性名，不是它的译文。**
   曾经分区身份就是译文，于是图标、描述、持久化布局各写了 51 个字符串比较分支，
   项目为此打过三个互不知情的补丁。`Layout/SettingsSections.cs` 是唯一身份表。
2. **`SectionDesc*` 与 `Section*` 的配对是显式的**，不是把标题名加前缀推出来的 ——
   曾有一个描述与它描述的标题**文本相同**，按标题查表因此有歧义。
3. **一个分区只有一个拼写。** 曾经同一批 AppLang 字符串被声明成
   `sSubtitle*` 与 `sSub*` 两套变量，后果是分区有两个名字、检查器重复计数
   （报 77、实际 67）。**双别名一律视为缺陷。**

### 3.2 四个列表的位置与职责（都在 `SettingsPage.Options.cs`）

| 表 | 回答 | 键 | 值 |
|---|---|---|---|
| `categoryOrder` | 分类的**显示顺序** | 分类显示名 | 序号 |
| `sectionOrder` | 分区在**其分类内**的顺序 | 分区标题 | 序号（**每分类独立编号**） |
| `optionOrder` | 行在**其分区内**的顺序 | 选项 key | 序号 |
| `sectionMap` | 每一行**属于哪个分区** | 选项 key | 分区标题 |

**规则**：

1. **`sectionMap` 是分区的唯一来源**，行**不**内联 `Section`（生成行与三个手写文件除外）。
   **缺条目会让 `BuildSettings()` 抛异常** —— 这是**故意的守卫**，
   防止选项无声消失。症状是「点工具→选项，应用秒退」，
   WER 的 `P7` 会是 `0x80131509`（`COR_E_INVALIDOPERATION`）。
   *不要在不知情的情况下去掉这个 throw。*
2. **`sectionOrder` 的值只与同分类的其它 section 比较**（选项先按分类分组）。
   所以编号是**每分类独立的序列**，不是全局的。
3. `optionOrder` 同时当**排序键和分组标签**：同值表示"同属一组，保持源码顺序"。
   LINQ `OrderBy` 稳定排序所以效果正确，**副作用是顺序依赖代码行的物理位置**
   （`check-sections` 报 WARNING 让人知情，不当错误）。
   **正例**：三个 PiP 开关共用 193 表达"同属一组"。
4. **新增分区必须同时出现在四个地方**，缺一即出问题：
   `SettingsSections.All`（身份/图标/标题/描述）、`sectionOrder`、
   9 个语言文件的 `Section*` + `SectionDesc*`、以及至少一条 `sectionMap` 指向它。
   声明了但**没有任何选项指向**的分区是 **ERROR**（`check-sections`）。
5. 🔴 **反向也要对上：每个被映射的分区都必须在 `sectionOrder` 里有一条。**
   缺失不会报错，只会让该分区**静默落 `int.MaxValue` → 排到分类的最后**。
   **当前有 3 个分区缺条目（共 18 行）**：
   `sVideoGeometry`(5 行)、`sOsdText`(9 行)、`sWindowBehavior`(4 行)
   —— 见 §7 A-14。**这是从上游继承的（上游同样缺），不是本分支引入的。**
   *验证*：`check-sections.py` 目前**不查这条**；需新增判据（§5.4）。

### 3.3 图标

- 图标是 **Segoe Fluent Icons 码位**，写在 `SettingsSections.All`。
- **59 个分区 = 59 个互不相同的图标**，`check-sections` 强制。
- 同一张表**同时服务分区卡片和分类栏**，所以生成分区**不得复用其分类的图标**
  （否则上下两级出现同一个字形）。
- **转义用 `\uXXXX` 码位**，不要用 `IconVideo` 之类的名字 —— 曾经生成器发射
  了名字形态的图标，看起来正常、实际无人消费。
- **选图标必须先把候选字形渲染成图看过**，不要猜码位。
  且**先与在用的 59 个比对**（最合适的候选往往已被占用）。

**行前置图标列已移除（2026-10-14，阶段 5）。**

- 官方设置窗口的行内**没有**前置图标。第十一处核对后，`OptionListControl.xaml`
  的 **11 个选项行模板**（Boolean/Text/Integer/Double/TextList/Color/Action/
  CheckList/Layout/MultiList/ShaderList）删掉了「Auto 图标列 + FontIcon」，
  行现在是 `Border > 控件` 两层的直接结构；`Option.SectionGlyph` 与
  `Styles.xaml` 的 `MpvOptionRowIconSize` 一并删除（当时同款的动效演示页
  `DemoOptionRow` 也做了同样处理；该演示页及其入口已整体删除）。改后行内只剩
  标签/描述/值控件三列。
- **分区图标本身没有消失**：`SettingsSections.All` 仍是唯一来源，继续服务
  分类栏与**分区卡片**（折叠态总览卡片本来就带 glyph/标题/描述/chevron，
  这是与"选项行"不同的形态，不在本次删除范围）。所以 §3.3 与 `check-sections`
  的"59 分区 = 59 个互不相同图标"约束**照旧成立**。
- **两处刻意保留的自绘/例外**（登记备查，改动前先读这里）：
  1. **搜索胶囊自绘**（`MpvSearchPill*` 一族资源）——它修的是框架搜索框的真实
     缺陷，不是"重画 Fluent 已有物"，故保留。
  2. `Themes/Generic.xaml` 只保留 `SliderExt` / `CheckBoxExt` 的隐式样式（它们经
     `DefaultStyleKey` 自动加载，是活的）。原先同文件里的
     `<Style TargetType="local:PlayerControl">` 整块（含 play/pause/shuffle 等
     元素 id 的模板）是**死块**：`PlayerControl` 是带自己 XAML 的 `UserControl`，
     其隐式样式键是 `typeof(UserControl)`，永不匹配该 `TargetType`；证据是
     `PlayerControl.xaml` 里同名 `x:Name`（含 `PlayPauseButton`/`ShuffleSymbol`）
     才是运行期真正被填充的字段——若死块生效这些字段会是 null 并立刻抛错。
     已删除，`Generic.xaml` 由 706 行降到 13 行。
  3. **`LeftContentToggleSwitchStyle`：删过一次又装回来了——不要再删。**
     它与 `BasedOn` 的 `DefaultToggleSwitchStyle` 在**布局上是镜像**，不是抄写：
     - 框架默认：`Off/OnContentPresenter` 在 `Grid.Column="2"`，开关
       （`OuterBorder`/`SwitchKnobBounds`/`SwitchKnob`）落在隐式的 `0` →
       **`[开关][no|yes]`**。
     - 本样式：内容在 `Grid.Column="0"`，开关在 `Grid.Column="2"` →
       **`[no|yes][开关]`**。
     conf 编辑器每个布尔行都靠"文案在药丸左边"来读；删掉它会让这些行整体翻转
     （阶段 5 清理时删过一次，就是这么翻的，随后单项回退还原）。对照
     `DefaultToggleSwitchStyle` 时要看**哪个元素占哪一列**，只比属性集合会漏掉这一
     点——这条正是踩过的坑。
     `ToggleSwitchPreContentMargin`/`PostContentMargin`（=4）是**独立资源**，
     与该样式无关、照旧生效。`Styles.xaml` 里该样式上方已留警示注释。

**B 段：图标替换与自绘清理（2026-10-15，阶段 5）。**

- **三处手绘 Shuffle 已替换为官方生成字形**（`FluentIcons.ArrowShuffle`，由
  `tools/gen-fluent-icons.py` 生成）：`CenterPlayerControlStyle.xaml`（`ShuffleSymbol`
  与 `MoreShuffleIcon` 两处基础 `Data`）、`CompactPlayerControlStyle.xaml`
  （`MoreShuffleIcon`），以及 `ControlBarIcons.cs` 的 `Shuffle` 常量（改为引用生成
  常量，C# 侧零副本）。
- **激活态不再写 `Data`**：三个 `ShuffleState` setter 原本是
  `<Setter Target="…Data" Value="F1 M 16.08…">`——正是一处 `Data` 一次性的历史坑
  （字符串→Geometry 的转换只有运行时才知道成不成）。现在改为切 **`Foreground`**
  （`AccentTextFillColorPrimaryBrush`），关闭态沿用基底色。这与相邻的
  Repeat / Volume / FullScreen 状态按钮同构；它们用 `FontIcon.Glyph`（字符串，安全）
  换形态，而 shuffle 只有单一形态，故用颜色表达激活。
- **2 份 XAML 副本 + 1 处 C# 引用是刻意接受的**：Style 模板里不能 `x:Bind` 一个 C#
  常量（模板在资源字典中，没有代码上下文），而共享 `<PathGeometry>` 资源不可行
  （一次性几何规则：一个 Geometry 只能属于一个 PathIcon，该模板又会按控制实例化）。
  为避免引入本项目从未使用过的 `GetTemplateChild`/视觉树查找机制，接受这三处副本，
  每处都注明「来源=生成器，改图先跑 `tools/gen-fluent-icons.py`」。
- **播放按钮：保持圆环，不改形状。** 三套控制栏（默认 / Center / Compact）本来就是
  「品牌渐变 `Ellipse` 圆环 + 官方 `play_24_filled` / `pause_24_filled` 字形，以
  **Visibility** 切换」，形状与状态表达都已对齐、字形与比例也已取自官方。
  `play_circle` / `pause_circle` 虽已存在（本阶段已纳入生成器），但它们是**自带单色圆
  的一体字形**：采用即会把品牌渐变圆环换成平面单色圆并丢掉一个设计元素。故按
  「宁可少改」保留圆环，只更正了两处已过时的注释（旧注释称「图标集没有
  pause_circle」）。`MpvEmptyStateIconBrush` 与 `MpvTransportPlayRingBrush` 原样保留。
- `ControlBarAdaptiveLayout.OverflowEntries`（含 shuffle 的 id **`"random"`**）**一字未动**；
  `mpv-conf-test` 的 `EveryEntryIsRenderedByTheUiTable` 断言照旧（442 全绿）。

**项 3：分区/自定义模板的字面量令牌化（2026-10-15）。**

- **对齐（观感不变）**：`OptionListControl.xaml` 的 `CustomizeSplitterTemplate` 与
  `SectionCardTemplate` 描述行 `FontSize="12"` → `CaptionTextBlockStyle`
  （Caption = 12/16 Normal，与裸 12px 文本一致）；`SectionCardTemplate` 的
  `Spacing="2"` → `MpvSpaceXxs`（=2）。
- **刻意保留的字面量**（同样就地写在 `OptionListControl.xaml` 顶部那段
  "Deliberate literals" 注释里，改之前先读那段）：
  - `FontSize="14"`（折叠分区卡标题）、`FontSize="16"`（自定义栏标题，且带 SemiBold）：
    **没有既精确又不改行高的排版台阶**（Body 是 14/20）。
  - `MinHeight="72"`／`Margin="0,8,0,8"`／`ColumnSpacing="18"`／图标字号 20/14/16/12：
    **已于 2026-10-16 升格为具名令牌（值未变）** —— `MpvSectionCardMinHeight` /
    `MpvSectionCardMargin` / `MpvSectionCardColumnSpacing` / `MpvSectionCardIconSize`(20) /
    `MpvSectionCardChevronSize`(12) / `MpvSectionHeaderIconSize`(16) / `MpvInlineIconSize`(14)，
    均定义在 `Styles.xaml`。理由：让两份卡片模板不可能各自漂移，并让这些数字不再读作
    一次性取值；先在本节登记、再升格，正是这一步的用法（18 仍保留 18，未退到 16）。
  - 四值 `Padding="12,8,12,8"` / `"12,12,12,12"`：`MpvSpace*` 是 `x:Double`，表达不了
    `Thickness`——仍是字面量。
- **口径**：能对齐现有令牌**且不改变观感**的才对齐；对不上的保持原值并就地写理由，
  **不为对齐而改观感**。

### 3.4 分类归置原则

- 顶层 **9 个**（Program / Playback / Video / Audio / Subtitles / Window /
  Network / Shortcuts / OSD）。这个数量与 mpv.net（11）、IINA（8）、
  Haruna（9）同量级。
- 🛑 **禁止再出现"垃圾桶"分类。** `Advanced` 与 `Screenshots` 两个分类都因此被删除：
  「图形 API 是关于输出链的事实，不是关于"高级"的事实」——
  找它的人会去 Video 找。**同理：任何"其他 / 杂项 / General"分区都是设计缺陷。**
- **按主题归置，不按技术档次归置。** 判据是「用户会去哪里找它」，
  不是「它有多难」。
- 分区名必须**描述内容**（`SectionGpuD3d11` = D3D11 细节），
  不能描述难度（`SectionAdvancedRenderer`，已删）。

### 3.5 生成层与手写层的边界

| | 手写 | 生成 |
|---|---|---|
| 谁写 | 人 | `tools/gen-schema-options.py` |
| 判断依据 | 语义、用户可理解性 | mpv schema 的 group 字段 |
| 文案 | 精修、本地化 | 先英文 schema desc，中文后补 |
| 图标 | 逐个渲染挑选 | 从手写 46 个未占用的码点里取 |
| 分区归属 | 语义 | schema group → 主题（`General` 那 100 行被拆成 5 个主题） |

**规则**：

1. **手写优先。** `ToCommand` 的通用兜底在 switch 最后，所以**策展的分支赢**。
   要"升级"某个生成项，只需手写一条更好的行并删掉生成的那条 ——
   生成器不会重复发射已被手写映射的选项。
2. **生成器不得发射裸 `new Option { ... }`**（缺少 `list.Add`）。
   症状是**编译 0 error 却 549 个对象全被丢弃**。判据：
   `grep -c "list.Add(new Option"` == 项数，且 `grep -cP '^\s*new Option\s*$'` == 0。
3. **生成器不得发射 `AdvancedSection = true`。** 折叠标志统一由
   `BuildSettings()` 在 `sectionMap` 之后派生（§2.3 规则 4）。
4. **生成产物不得放进 `Languages/`。** `schema-labels.en.json` 曾放在那里，
   被 `check-localization.py` 当成语言包、报缺 1000+ 键。它在 `MpvConfOptions/`。

---

## 4. UI 与交互约束

### 4.1 间距

**两套刻度并存，各有适用面**（不要互相混用）：

| 刻度 | 令牌 | 适用 |
|---|---|---|
| 布局节奏 | `MpvSpaceXxs`=2 `Xs`=4 `Compact`=6 `S`=8 `M`=12 `L`=16 `Xl`=24 | 表面之间、区块之间 |
| 控件内部紧凑 | 同上 2/4/6 | 图标→文字这类内部间距 |

**规则**：

1. **优先用具名令牌，不写字面量。**
   ⚠️ **实测现状：并未完全做到。** 设置相关 XAML 里 109 个间距属性 =
   **74 个已令牌化（67.9%）+ 25 个非零字面量（22.9%）+ 10 个字面 `0`**。
   25 个字面量里有 **17 个集中在两个文件**（`SettingsPage.xaml` 9 个、
   `OptionListControl.xaml` 8 个）—— 所以这是**可以收敛的存量**，不是弥散问题。
   最干净的第一刀：`OptionListControl.xaml:329` 的 `Padding="16,16,16,16"`
   与 `MpvCardPadding` **逐字节相同**，令牌已存在、只是没接上（1 行改动）。
   另有一处重复值可合成一个令牌：`Margin="0,12,0,0"` 出现在
   `OptionCheckListControl.xaml:51`、`OptionColorControl.xaml:58`、
   `OptionLayoutControl.xaml:23` 三处。
   *验证*：`check-xaml.py`；改动间距属性不触碰任何绑定，风险低。
2. **字面量 `0` 保持 `0`** —— "没有间距"不是刻度上的一档。
3. **刻度外的值可以存在，但必须就地写理由。** 现有两个刻意的例外：
   树内联按钮的 `6,2`、自定义底栏的 `16,10`，都是按相邻控件高度手调的，
   两处代码里都写明了原因。**新增刻意外例外必须照此办理，否则改回刻度。**
   已知仍在刻度外的两个值（待决）：`OptionListControl.xaml:343`
   `ColumnSpacing="18"`、`OptionCheckListControl.xaml:64` `Margin="0,10,0,0"`。
4. **选项行内文字与控件之间的间距用 `MpvOptionControlGap`（24），
   不要用 `MpvSpaceXl`。** 它刻意是独立令牌：它是列表里唯一一处必须读成
   "分隔"而不是"内部间距"的间隙，且它两侧的列都在增长。
   大多数模板此前**根本没有声明 `ColumnSpacing`**，描述直接顶到控件上，
   两列读起来像一段折行的文字。

**未使用的令牌**：`MpvSpaceL`(16) 与 `MpvSpaceXl`(24) 当前**零消费者**
（202 处资源引用 / 76 个键里，只有这 2 个零引用）。
判定：**保留**（刻度少两档会诱导下一个人重新发明字面量），
但**不得为了"用掉它们"而改变已正确的间距**。

**缺失的令牌**：`MpvOptionErrorStyle` **不存在**，而它的三个兄弟
（`MpvOptionLabelStyle` / `MpvOptionDescriptionStyle` / `MpvOptionWarningStyle`）
都在（`Styles.xaml:900/913/942`）。错误文字是**唯一从未被令牌化**的文本角色 ——
这与 §7 A-15 的 `ErrorText` 失效是同一个缺口的两面。

### 4.2 排版

- 标签：`BodyTextBlockStyle`（14/20），1 行 + 省略号。
- 描述：`CaptionTextBlockStyle`（12/16），次级前景，1 行 + 省略号。
- 分区标题：`MpvSectionHeaderStyle`（派生 `BodyStrongTextBlockStyle` = 14/20 semibold），
  **不要在站点重述字号**（曾经硬编码 16px，不是字阶上的一档）。
- 警告：`CaptionTextBlockStyle` + `SystemFillColorCautionBrush`，可换行。
- **字号下限：14px semibold / 12px regular**（Windows 11 排版规范）。
- **前景一律用语义令牌**（`TextFillColorSecondaryBrush` 等），禁止硬编码颜色。

### 4.3 窗口与缩放下限

- 设置窗口是**独立窗口** `SettingsWindow`，`PreferredMinimumWidth/Height` = **720/560**
  （`SettingsWindow.xaml.cs:49-50`，是**唯一**内容为两列列表的窗口）。
  - **宽度 720**：低于约 640 逻辑像素时，文字列比最长的选项标签还窄，
    每条描述都在几个字符处就截断 —— 列表读起来是噪声而不是截断文本。
  - **高度 560**：480 只够约 6 行，**不到一个分类的量**，滚轮成了看分区的唯一手段。
    560 才是一屏行。
- **不要动主窗口的 `MIN_LOGICAL_WIDTH`** —— 它在 `Shell/MainWindow_Size.cs:19`
  （= 250，ModernX 风格下 400），管的是播放器/PiP 的最小宽度，**不渲染设置行**。
  曾经改错过一次。
- **560 是 `CategoryFloor = 8` 的推导依据（§2.5）。改 560 必须重算 8。**

### 4.4 折叠与下钻的交互语义

- 折叠的分区在列表里**发一张卡片**，位置就是它的分区顺序位置 ——
  **不是全部堆在末尾**。"全部落在末尾"正是让一个分类看起来像
  "常用项 + 一堆垃圾"的原因。
- 下钻后**面包屑的分类段是可点的返回按钮**，末段（分区名）是纯文本。
- **搜索结果是单行**，所以搜索分支必须清空折叠集合
  （否则任何落在折叠分区里的结果会被折叠掉，而那是绝大多数结果）。
- **搜索索引覆盖**：标签、描述、mpv 选项名、分区名、可选值。
  *验证*：`check-search-index.py`。

### 4.5 mpv.conf 双向同步

- **`mpv.conf` 是唯一真源**，用户手改的内容**最高优先级**。
- 写方向：界面改动 → `ManagedMpvConfig` 的托管区块 + `mpv.conf`。
- 读方向：`MpvSettings.LoadFromConfig()`，在 `AppContext.Init()` 里
  **先读后写**（唯一无损的顺序：先读后写收敛到文件已说的值；
  先写后读会用陈旧默认值覆盖手改且没得可读）。
- **运行时外部改动**由 `MpvConfWatcher` 监听，reload 必须同时跑
  **两个 reader**：`LoadFromConfig()`（管显示值）+ `MpvConfOverrides.Refresh()`
  （管启用状态和"已被覆盖"徽章）。**少跑后者，手改新增一行时值对了但行仍可编辑。**
- **防自激环**：写盘**前**（不是后）`NoteOwnWrite()` 戳 generation；
  reload 前后各读一次，相同则说明整个 reload 落在我们自己的写入内部 → 丢弃。
- **归属判定**：mpv.conf 里的一行有**三个所有者** —— 我们、用户、
  **以及我们分发的正文**。第三类缺失曾让正文里约一百个活动 assignment
  全被算成用户手改（症状：104 个选项全部置灰只读）。
  正文归属靠**与打包模板逐行比对**判定。

### 4.6 禁止事项（明确清单）

| 🛑 禁止 | 理由 |
|---|---|
| 新增"高级 / 其他 / 杂项"分类或分区 | §3.4，历史上删过两次 |
| 给同一个概念保留两套变量名 / 两套拼写 | §3.1，曾导致分区双名与重复计数 |
| 手改任何 `.Gen.` 文件 | §1，下一轮生成静默丢失 |
| 在 `DataTemplate` 里重述卡片外壳 | §2.6 |
| 只在站点写字号 / 颜色 / 间距魔数 | §4.1/§4.2 |
| 给这三行内容列加包装层 | §2.1 规则 3 |
| 让内层列表自己滚（或让宿主不滚） | §2.2 |
| 从行读分区级折叠状态 | §2.3 规则 2 |
| 声称"检查器全绿所以功能正确" | §5.4 |
| 启动 `mpvw.exe` 或跑 UIA 探针 | 用户明令，见 §5.5 |

---

## 5. 验证流程（强制）

### 5.1 唯一合法的自动化验证

```powershell
# 1) 构建 —— 必须 0 警告 0 错误
dotnet build mpv-winui/mpv-winui/mpv-winui.csproj -c Release -p:Platform=x64 `
  -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:minimal

# 2) 全部检查器（17 个）
python <governance>/run-baseline.py after      # 或逐个跑 tools\check-*.py
```

两个 `-p:` 开关**不能省**，否则 MSIX 签名报
`SigningCertificateThumbprintNotInStore`，exit 变 1（但 dll 其实编好了）。

**前置**：`mpv-winui/packages` 需按 `mpv-winrt/packages.config` 的 10 个精确版本
从 `~/.nuget/packages` 铺出来。缺包的症状是 79 个全在 `Modules/Player` 下的
"找不到类型"错误 —— **看起来像工具链被拦，实际是依赖缺失。**

### 5.2 检查器矩阵与门槛

**当前状态：17 个检查器，15 通过 / 2 失败。**
两个失败项都是**真实缺陷**，不是工具问题。第二轮修好了 5 台坏仪器，
代价是它们**开始报出真实缺陷** —— 这正是修好它们的意义。

| 检查器 | 门槛 | 可信度 |
|---|---|---|
| `check-sections` | 分区数 / 分类数 / 图标唯一 / 排序键 / caption 译文 / sectionMap 无缺项 / **反向无死条目** | ✅ **已修**：`option_keys()` 改括号配平、读两半；`custom-section:` 与 `Shortcut:` 按前缀豁免。**反向验证过**（注入假死条目 → 精确报出该条目）。仍**不查 `sectionOrder` 完整性**，见 A-14 |
| `check-localization` | **2439** props × 9 语言文件（含 2 个生成 partial） | ✅ **已修**：读全部 `AppLang*.cs`；缺键按家族汇总。**现在正确失败**：9 个文件各缺 1098 键（A-16） |
| `check-empty-sections` | 无空分区、无孤儿分区 | ⚠️ 报 `EMPTY: none` 仍是**陈旧读数**（实际 13 个，全是生成分区）—— A-17 |
| `check-placeholders` | 每个非路径自由文本项在空值时显示示例 | ✅ |
| `check-search-index` | 索引覆盖 label / desc / mpv 名 / 分区 / 值 | ✅ |
| `check-conf-lines` | 将写盘的选项名全部合法；运行时属性被正确排除 | ✅ |
| `check-conflict-rules` | 依赖集合与规则真正读取的一致 | ✅ |
| `check-mpvconf-precedence` | 选项名映射与 `ToCommand` 完全一致 | ✅ |
| `check-section-icons` | 生成 / 手写图标零碰撞 | ✅ |
| `check-xaml` | 所有 XAML 良构，注释内容合法 | ✅ |
| `check-ui-tooltips` | TOTAL=0 | ✅ |
| `check-scroll-maxwidth` | 无"不可居中的 MaxWidth 列" | ✅ |
| `check-visual-state-layout` | 无负 margin、无 VisualState 改对齐 | ✅ |
| `check-settings-drift` | AppSettings ↔ ToCommand ↔ 选项键三方一致 | ✅ **已修**：7 个 App-only 属性补进 `UNMAPPED_OK` 并逐条写明理由；**反向验证过**（移出任一条 → 立刻报 ERROR）。**注意它不是设置树的完整索引** —— 47 个未映射键里 44 个是刻意的 |
| `diagnose-advanced-flag` | 两个下限 + pin + "隐藏行必须有卡片" + 分区连续性 | ✅ 最可信的一个（模拟真实代码路径） |
| `report-fold-cards` | 每个折叠分区卡后 ≥ 4 行 | ✅ 读两半 |
| `report-missing-options` | 未暴露的选项都被排除或编目 | ✅ **已修**：`load_exposed()` 读两半 → **815 已暴露 / 1 未暴露**（原报 266/550）。`docs/missing-options.md` 已重生成 |

**基线 = 15/17。** ⚠️ **不要把"16/17"或"17/17"当成结论** —— 改动前的
"16/17"掩盖了 **6 台坏仪器**，它们全绿是因为一致地算错了对象。
这就是 §5.4 那条规则存在的全部理由。

**仍带 ⚠️ 的 1 个**：`check-empty-sections`（A-17）。

### 5.3 反向验证（新增或修改检查器时强制）

**任何检查器都必须先证明它会红。**

做法：构造一个已知违规的输入，确认检查器报错，再撤销。

**已有的正例**（照这个做）：
- `check-visual-state-layout` 对 HEAD 运行会精确报出被删掉的两行。
- `check-scroll-maxwidth` 对 `162d307` 运行会精确报出那一行。
- `check-sections` 的 caption 规则喂过合成输入。

**反例（教训）**：`report-category-overview.py` 曾报
`OK: every category has at least one row visible` ——
它按**局部变量名**给分类分桶，而选项树分两半、两半拼写不同
（手写 `var network` vs 生成 `var catStreamingCache`），
同一个分类两个桶、各自都"通过"，而真正的问题从来没被问过。
**该检查器已被删除**（与 `diagnose-advanced-flag` 重复但门槛更弱）。

### 5.4 ★ 检查器必须自证"读的是代码真正读的东西"

**本文件最重要的一条工程约束。**

**并且：任何"值在两个表示之间往返"的代码路径，都必须有一条往返断言。**

这条规则的来历是一处**正在造成数据损坏**的缺陷（A-26）：
写方向用 `Q()` 把值转义进 mpv 语法，读方向**没有逆运算**，
于是每次启动都把 `mpv.conf` 里的字符串值**再转义一次** ——
反斜杠 2^n 增长，用户的 `mpv.conf` 已经涨到 **896 MB**。
**三个检查器都没抓到它**，因为它们只看**选项名**、名字映射和键齐否，
**全都在静态源码上工作，没有一条做「写→读→再写」**。

⇒ 补检查器时，除了"读的是代码真正读的东西"，还要问：
**它有没有覆盖值本身的往返？** 形式化要求：

| 不变量 | 断言 |
|---|---|
| 转义对称 | `UnQ(Q(v)) == v`，对含 `\` `"` `=` `#` 与空串的值都要成立 |
| 往返无增长 | `ToConfLine` 的输出被读回后再写一次，**字节不变**（这条会直接抓到 A-26） |
| 幂等 | 反复 apply 同一个设置，托管块的行数与字节数收敛 |

改完检查器要问两个问题：

1. **它读的是"人的意图"，还是"代码真正读的东西"？**
   `report-fold-cards.py` 一直报绿，因为它读**源码里的折叠名单**，
   而运行时**不读那个名单** —— 500 多行全铺在总览上，检查器一无所知。
2. **拆掉一行代码，它会变红吗？** 不会变红的检查器是安慰剂。

**已知的反面实例（第二轮已全部修复，保留在此作为判例）**：
`check-sections.option_keys()`、`report-missing-options.load_exposed()`、
`check-localization.parse_applang_props()` **都只读手写那一半**。
后果分别是 549 条假 stale 警告、一份声称"550 条未暴露"而真实只剩 1 条的文档、
以及 1098 个生成键被反报成"多余键"。
**而且三处都不会让检查器变红** —— 所以"16/17 通过"发现不了它们。

⇒ **规则**：任何枚举「设置项 / 分区 / mpv 选项映射 / 本地化属性」的检查器，
**必须同时读手写与生成两半**（`*.cs` 与 `*.Gen.cs`、`AppLang.cs` 与全部 `AppLang*.cs`）。
**新增生成层文件时，必须回头审计所有已存在的枚举器。**
正确写法可抄：`check-sections.section_map_keys()`、`diagnose-advanced-flag.py`、
`report-fold-cards.py`、以及修好后的 `check-localization.parse_applang_props_all()`
（用 **glob** 而不是列文件名，这样第四个 partial 不会重新引入同一个 bug）。

**配套要求：报错必须可读。** 1098 个键名打印成一行，人和工具都会学会跳过它。
按"家族"汇总（`1098x Schema*`）比逐条列出更有用 —— 这是 `check-localization.py`
的 `_summarise()` 存在的原因。

### 5.5 验收测试的红线（用户明令，最高优先级）

> 「不要任何测试，你的测试毫无价值只是浪费 token，写进文档里，
> 不要自己做打开应用的测试，不要使用探针，就没有起效果过，
> 你只要告诉我路径我自己测试」

**唯一判据：这个动作会不会让 `mpvw.exe` 出现在进程列表里？会 → 不做。**

禁止：启动 `mpvw.exe`、写/跑 UIA 探针、任何「启动 → 断言 → 杀进程」往返。
**验证到「编译 0 error + 检查器全绿」为止，然后把路径交给用户。**

> ⚠️ `AGENTS.md` 目前的「Work loop」第 2、3 步仍要求"运行 exe、通过 IPC 驱动、
> 截图验证" —— **该节与本条冲突，以本条为准**（见 §7 A-3）。

### 5.6 代码卫生收尾检查

提交前对照：

1. **BOM 与行尾与 HEAD 一致。** 本仓库 **BOM 不统一**
   （`MpvSettings.cs` 有、`MpvConfOverrides.cs` 没有），
   判据永远是"与 HEAD 一致"。⚠️ Python 批量改文件会加 BOM，
   而 `git diff --stat` 只显示 `5 -----`、**看不出 BOM 变化**。
2. **`git status --short` 干净**，不 stage `bin/` `obj/` `dist/` `libs/`。
3. **grep `\[DBG-`** 清理调试插桩。
4. **XAML 注释里禁止 `--`**（XML 注释体内禁双连字符），
   markup 编译器直接拒绝该文件（`WMC9997`）。用括号代替。
5. **改了生成源码就要重新生成**，并确认 `grep GeneratedSections()` 之类
   的旧形态为空（生成器曾发射一个全树没人调用的死方法）。

---

## 6. 常见陷阱速查

### 6.1 测量语义

| 症状 | 根因 | 位置 |
|---|---|---|
| 标题飘在窗口中间、卡片另起一条左缘 | `MaxWidth` 配 `Center`：每行按自身内容定宽再居中，三行内容宽度不同就各居各的 | §2.1 规则 1 |
| 宽窗口下内容贴左、右侧留白 | 预期行为：`MaxWidth` 配 `Stretch`，钳出来的盒子停在起始边 | §2.1 规则 1 |
| 属性完全正确但 `HorizontalAlignment` 不生效 | `ScrollViewer` 横向默认 `Enabled`，用无限宽度测量 | §2.2 |
| 没有滚动条、页脚盖住内容 | 内层 `ListView` 留着内部 `ScrollViewer`，报告视口高度 | §2.2 |
| 面包屑飘在窗口中间 | 给它加了铺满全宽的包装 `Grid` | §2.1 规则 3 |
| 三行等宽但起点不同 | `MaxWidth` 与水平内缩不在同一元素上 | §2.1 规则 2 |
| 行高被描述撑高、控件偏离居中 | 描述折了第二行 | §2.6 |

### 6.2 选项 key 的三种合法拼法

写检查器时**三种都要认**：

```csharp
Key = nameof(AppContext.AppSetting.Xxx)   // 生成行 + 部分手写
Key = nameof(AppSettings.Xxx)             // 部分手写
Key = SomeStringConstant                  // 常量可能声明在别的文件
```

字符串常量**可能声明在别的文件**（`FileAssociationCheckListKey` 在
`SettingsPage.Actions.cs`），所以扫描常量要扫整个 `Modules/Settings/`。
`Shortcut:N` 键是运行时从 `input.conf` 生成的，**没有静态行**，
`sectionMap` **不该也不可以**覆盖它们。

### 6.3 崩溃诊断的路径

设置页相关崩溃报 `0xc000027b` + `Microsoft.UI.Xaml.dll` 时：

1. **先看 WER 报告的 `P7`。** 是 `0x8013xxxx` 家族
   （`0x80131509` = `COR_E_INVALIDOPERATION`）→ 去代码里 `grep "throw new"`，
   **不要翻 XAML**。`SettingsPage.Options.cs` 的缺 `sectionMap` 守卫就是这类。
2. `STATUS_STOWED_EXCEPTION` = 异常从 click handler 逃逸到 WinUI 自己的分发，
   框架捕获 → **无对话框、无托管栈、无日志**。
3. **最强的单一信号是「兄弟方法防护不对等」** ——
   `OptionLayoutControl` 的 `ToggleEdit` 包了 try/catch 而 `Select` 没有，
   且 `panel.Children[0]` 无守卫（邻近的 `[1]` 用的是 `Count > 1`，
   **这个检查不覆盖索引 0**，0 需要 `Count > 0`）。
   逐个 diff 同类方法比通读文件快得多。
4. 日志目录：`%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\`
   （**不是** `...\mpv-winui\logs\`，那是陈旧的）。
   唯一绕过 NLog 的实时文件是 `display-info.log`。

### 6.4 进程存活判据（仅在用户自己测试时有意义）

**判据只能是「按可执行名过滤的进程」+「窗口类名 `WinUIDesktopWin32WindowClass`」。**

三种"应用早退"的假象，**都不是应用问题**：
1. `MainWindowTitle` 解析到 **Windows 系统「设置」应用**的窗口
   （pid 不同、类名也不是 WinUI 的）；
2. 进程计数被无关进程污染；
3. **单实例锁未释放** —— `Program.Main` 注册 AppInstance 键 `"main"`，
   第二次启动会把激活重定向给第一个进程，所以"杀旧→立刻启新"会让
   新进程的激活交给**仍在拆除中**的实例，新进程随即退出。
   **同一二进制寿命 15s/22s/24s/40s/53.8s 的随机变化几乎可以断定是这个问题，
   不是崩溃。** 正解：轮询确认进程真的消失，不要固定 `sleep`。

> 注意：这一节是**知识留存**，不是行动许可。按 §5.5，本项目的 agent
> **不启动应用**，这些判据只在用户自己排查时有参考价值。

---

## 7. 已知例外登记（技术债台账）

**本表是"已知且暂时接受"的不一致清单。** 与 §1–§6 的规则冲突时，
**必须在本表登记**，否则视为违规。

> **第二轮（2026-09-21）修掉了 A-1 / A-2 / A-3 / A-4 / A-5 / A-8 / A-9 / A-10 / A-14**，
> 全部带**反向验证**（确认修好的检查器对合成违规会变红）。
> 下表保留原条目与结论，状态列标明结果 —— 删掉它们会丢掉"为什么有这些规则"。

| # | 项 | 性质 | 判定 | 状态 |
|---|---|---|---|---|
| A-1 | `check-sections.option_keys()` 只读手写半边（正则只匹配 `},`，生成块是 `});`） | 🔴 检查器缺陷 | 改括号配平读两半；`custom-section:`/`Shortcut:` 按前缀豁免 | ✅ **已修**，反向验证过 |
| A-2 | `report-missing-options.load_exposed()` 只读 `MpvSettings.cs` → 报 266/550 | 🔴 文档错误 | 读两半 → **815/1**；`docs/missing-options.md` 重生成 | ✅ **已修** |
| A-3 | `AGENTS.md` 的 Work loop 要求运行 exe + IPC + 截图 | 🔴 与 §5.5 冲突 | 改为 `Build → Check → Hand over`，新增「Do not launch the app」小节并写明判据 | ✅ **已修** |
| A-4 | `AGENTS.md` 的产物名写 `mpv-winui.exe` | 🔴 错误 | 改为 `mpvw.dll`／`mpvw.exe`，注明是 `b777f30` 的 `<AssemblyName>mpvw</AssemblyName>` 改的。**原记录漏了一半责任方**：当时只改了 `AGENTS.md`，`installer/product.wxs`（开始菜单快捷方式 `Target`、协议 `DefaultIcon`、协议 `shell\open\command` 共三处）与 `installer/build-installer.ps1`（publish 输出的存在性检查）**仍写 `mpv-winui.exe`，从未修**。后者使 MSI 构建在 publish 输出完全正常时也必然报错退出，所以该脚本此前从未成功产出过 MSI；前者让装出来的包装了开始菜单快捷方式指向不存在的文件、空白图标、`mpvw://` 协议启动不存在的程序，且构建期完全不报错。**本次已一并修复安装器两处** | ✅ 文档已修 / 安装器本次补齐 |
| A-5 | `check-settings-drift` 的 7 条 ERROR | 🔴 假阳性 | 7 项均为 App 内部状态；补进 `UNMAPPED_OK` 并逐条写理由，**不补假映射** | ✅ **已修**，反向验证过 |
| A-6 | `docs/compare-upstream.md` 的"上游"指 `ikas-mc` 而非本仓库 `origin`（`saillill`） | 🔴 基线错误 | 已在文件头加基线声明 + 表格说明哪些章节还能用；§3.1 加了就地作废标记 | ✅ **已修** |
| A-7 | `SettingsPage.Options.Advanced.cs` 文件名与内容不符（`advanced` 分类已删，文件仍是 102 项 Video/GPU/Program 主题项） | 🔵 命名遗留 | 改名会让 26 个提交的 diff 难读，**需权衡**；至少改文件头注释 | 待定 |
| A-8 | `sectionOrder` 有 4 条重复条目（连续两份相同的 `// osd` 块） | 🟡 冗余 | 删除 | ✅ **已修** |
| A-9 | `ControlBarHiddenIcons` 的 `sectionMap` + `optionOrder` 死条目 | 🟡 死代码 | 删两条；`AppSettings` 属性**保留**（旧配置迁移仍读它） | ✅ **已修** |
| A-10 | `Option.cs` 的孤儿 XML 注释（"Display tier"贴在 `OptionCheckItem` 上） | 🟡 冗余 | 删除 | ✅ **已修** |
| A-11 | `MpvSpaceL` / `MpvSpaceXl` 零消费者 | 🟡 未用令牌 | **保留**（§4.1），不得为用掉它而改间距 | 接受 |
| A-12 | 描述/警告文字有 3 套并行实现；11 个控件各写一份文字栈 | 🔵 一致性问题 | **部分修复 + 剩余部分决定不抽**。① 新增缺失的 `MpvOptionErrorStyle`（Styles.xaml:960），3 个会校验的控件（Double/Integer/String）从 4 行内联改成 1 行引用 —— **样式词汇现在统一了**（L/D/W/E 四个角色都有令牌）。② **剩余的"每控件 6 行"决定不抽**：实测每控件净省只有 6 行（11 个控件共约 66 行），而代价是要让共享块同时支持「有/无 WarningText」「有/无 ErrorText」「Action 的 StatusText」三种变体 —— 6 个控件是 L+D+W、3 个是 L+D+W+E、1 个是 L+D+W+S、1 个是 L+D。**为 66 行引入一个可配置共享块，是拿可读换行数，不划算。** 判据记录在此，免得下一轮又把它当"未完成的清理" | ✅ 样式已统一 / 抽取已否决（有据） |
| A-13 | 手写行与生成行的缩进风格不同 | 🟡 外观 | 生成器负责 | 待修 |
| A-14 | 3 个分区在 `sectionMap` 里但**不在 `sectionOrder`**：`sVideoGeometry`(5)、`sOsdText`(9)、`sWindowBehavior`(4) → 落 `int.MaxValue` | 🔴 排序静默失效 | 补 3 条（按语义定位：OsdText 紧跟 Appearance、WindowBehavior 在 Window 与 PiP 之间、VideoGeometry 收 Video 尾）；**另发现 `sProgramTesting`/`sProgramAssociations` 物理地漂在 `// osd` 注释之后并与 Config 撞号**，一并归位。**继承自上游** | ✅ **已修**（46/46 覆盖、0 重复） |
| A-15 | `OptionMultiListControl` 声明了 `ErrorText` 但**永不填充**；同时**缺 `WarningText`** | 🔴 缺一个坏一个 | **已核实并订正原判定**：该控件 4 个选项全是 `AllowEmpty = true` 的自由文本列表，**没有任何校验规则**，所以 `ErrorText` 是不可达的**死标记**（不是"绑定失效"）。而 `WarningText` 缺失是**真缺陷** —— `ComputeWarning()` 会对**任何**被手改 mpv.conf 接管的选项返回 `WarningOverriddenByMpvConf`，这 4 行原本**什么都不显示**。修法：删 `ErrorText`、补 `WarningText`、在 `OnOptionStateChanged` 里驱动它（顺带把 `IsEnabled` 应用到各行的输入框与按钮，与 `OptionShaderListControl` 同构） | ✅ **已修**，构建 0 警告 |
| A-16 | 549 条生成行**从未被本地化**：9 个语言文件**各缺 1096 个** `Schema*` 键 | 🔴 用户可见 | 生成行的标签/描述一律显示英文。`check-localization` 修好后**正确报 ERROR** | **待修**（大工程量；取决于 A-24 的决策） |
| A-17 | `check-empty-sections` 只读手写 `sectionMap`，把 13 个生成分区全报成 EMPTY | 🔴 检查器缺陷 | 从 `Sections.Gen.cs` 折算生成分区。修后 `59/59`、`EMPTY: none`，并暴露出 `SectionGenVideoRendering` 有 **207 行**（与记忆文档记的 205-207 吻合） | ✅ **已修**，反向验证过 |
| A-18 | `Speed` 行**静默失效**，且与生成行 `Mpv_speed` 构成**同一 mpv 选项的两个设置项** | 🔴 真·失效绑定 + 重复项 | `ToCommand` **无 `Speed` 分支**（271 个 arm 里没有）→ 兜底 `GeneratedCommand` 不认非 `Mpv_` 键 → 返回 null → 什么都不发。修法四处协同：① 加 `ToCommand` arm ② 加 `MpvOptionNames` 条目 ③ 新增 `SessionStateKeys` 让 `ApplyAll` 跳过（保住"重置不打断正在播放的会话"）④ 删页面里的补偿性特例。生成器**自动**丢掉 `Mpv_speed`（净 −27 行） | ✅ **已修**；`Mpv_speed` 全库 0 次 |
| A-19 | 8 个手写键无 `optionOrder` → 落 `int.MaxValue` | 🔵 排序 | 沿用「前两位=分区」约定：画面控件 2001–2006（`sVideoImage`，按阅读顺序）、`AdLavcDownmix` 2101、`AutoWindowResize` 2601。三个分区都是**折叠**的，所以影响只在卡片内部 —— 但原状态是"未指定"这个偶然，现在写下来了 | ✅ **已修**；排序键 299 → 307 |
| A-20 | 零引用 `x:Name`：审计报 11 个 | 🟡 死代码 | **已核实并分类**：① **2 个真死**（`OptionColorControl` 的 `HeaderGrid`、`SettingsPage` 的 `CustomizeHost`）→ 已删；② `OptionListControl` 的 `SectionDragSurface`（`DataTemplate` 内）→ 保留并写明它标识可拖动表面；③ `SettingsWindow.xaml` 的 4 个模板部件名（`LayoutRoot`/`SuggestionsPopup`/`SuggestionsContainer`/`SuggestionsList`）→ **不动**，模板部件可能被框架按名查找；④ `SettingsWindow` 的 `RootGrid` → **保留**：它是全仓窗口根元素的**惯例**（`PiPWindow`、`PlayerControl` 都按名引用自己的 `RootGrid`），为一个统计数字破坏惯例不划算 | ✅ 删 2 / 留 7 并注明理由 |
| A-21 | `CustomizeStrings` 的表键（49 个）没有 AppLang 属性 | 🟢 **设计如此** | 其类文档说明：AppLang 属性靠反射读取，无法同时承载译文。`check-localization` 仍报为 warning，已注明"expected" | 接受（文档化） |
| A-22 | 行尾 / BOM **在本仓库内不统一**，且 `edit` 类工具会静默剥掉 BOM | 🟡 工具陷阱 | 判据是"与索引/HEAD 一致"，**不是**"与邻居一致"。`core.autocrlf=true` ⇒ 工作区应为 **LF**、仓库内为 LF。BOM 只能靠字节检查（`git diff` 第一行会显示 `-﻿using`，而 `--stat` 只报 1 个字符） | 规则已登记（§5.6） |
| A-23 | `check-sections` **不查 `sectionOrder` 完整性** | 🔴 检查器缺口 | 已补判据：`sectionMap` 用到的每个分区都必须在 `sectionOrder` 里有条目，否则**静默排到分类末尾**（A-14 那 3 个分区就是因此长期错位）。生成分区按其自身的注册文件折算 | ✅ **已补**，当前 46/46 通过 |
| A-24 | **要不要保留 549 条 schema 生成选项？** | 🔴 **产品决策** | 保留 → 必须补 A-16 的 1096 键 × 9 语言；不保留 → 删生成器 + 产物，A-16 随之消失，回到上游的 50 分区 / 11 分类。**这不是缺陷，是需要用户拍板的产品范围问题** | **待用户决策** |
| A-25 | **要不要保留 `MpvConfWatcher`（运行时重读 `mpv.conf`）？** | 🔴 **产品决策** | 242 行并发（`FileSystemWatcher` + 400ms 防抖 + generation 计数器）。它修掉了一个真实的 UI 谎言（开着应用改 conf 不同步），但上游无对应物，且它介入的正是整个设置模型依赖的那份文件。**反对理由是风险，不是质量** | **待用户决策** |
| A-26 | 🔴🔴 **`mpv.conf` 因指数级转义损坏至 896 MB** —— 字符串型 mpv 选项在「读→写」往返中反斜杠每轮翻倍 | 🔴 **数据损坏（现场正在发生）** | 写方向 `MpvSettings.cs:465` 用 `Q()`（`:21-22`）转义；**读方向没有逆运算** —— `AssignmentValue()`（`:1102-1106`）只 `Trim()`，`TryWriteSetting()`（`:1154`）把原样文本直接存进 AppSettings。于是每轮启动：`"auto"` → `"\"auto\""` → `"\"\\\"auto\\\"\""` → … **2^n 增长**。实测 `%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf` = **896 MB / 仅 884 个换行**，爆炸起始点正是 `audio-device=` 那一行 | ✅ **已修**：① 补 `Unquote` 让读方向与 `Quote` 对称（抽成 `MpvConfValueCodec`，纯 .NET、可测）② 39 个真实 NUnit 测试 ③ **用户的 `mpv.conf` 已恢复**：两个 896 MB 文件移入 `_corrupt-20260920-150845\` 隔离（未删），从 19:14 备份还原为 24,615 B / 447 行 / 257 活动行，`audio-device="auto"` 完好 ④ 写进构建闸门。**附带发现**：残留的部分转义在新算法下**会自行收敛**（实测 7 次启动长度恒定），所以即使备份里也有旧损坏也能自愈 |
| A-27 | 检查器套件**没有任何「写→读→再写」往返断言** | 🔴 检查器缺口（A-26 的成因） | `check-conf-lines` 只查选项**名**；`check-mpvconf-precedence` 只查名字映射；`check-settings-drift` 只查三方键齐否；**三个都只在静态源码上工作**。`mpv-conf-test` 里也**没有**一条 `ToConfLine` 相关的往返测试（已 grep 确认）。⇒ A-26 这类缺陷对整套检查器**不可见** | 部分修复：① 转义对已抽成 `MpvConfValueCodec`（纯 .NET，无 WinUI 依赖）② 写了 `MpvConfValueCodecTests.cs` ③ 但见 A-28 —— 测试项目**根本编译不过** |
| A-28 | 🔴 **`mpv-conf-test` 测试项目无法编译**（不是本轮引入） | 🔴 测试基建失效 | `MpvConfManager.cs:65,67` 调用 `ManagedMpvConfig.MpvConfPath` 与 `MpvConfWatcher.NoteOwnWrite()`，而测试项目只 link `MpvConf/`、`Menu/MpvMenu/`、`FileSystem/FileService` —— **`Modules/Settings` 没有 link**。**已在 HEAD 用临时 worktree 复现：同样 2 个 error**，所以不是治理轮次引入的。根因是 `de4496c`（`MpvConfWatcher`）给一个「纯配置」类引入了对 Settings 的**编译期依赖**。测试项目是 `net10.0`、应用是 `net10.0-windows10.0.26100.0` + `UseWinUI`，**ProjectReference 走不通**（TFM 不兼容） | ✅ **已修**：`MpvConfManager` 构造函数改为接受 `Action? onBeforeSave`，路径判断移到组合根（`MpvConfEditorPage`）。**整份测试套件因此复活：297 个测试首次跑通**（此前编译不过 = 一个测试都跑不了）。临时闸门 `tools/run-codec-tests.ps1` 已删除，`build.ps1` 改跑真实的 `dotnet test mpv-conf-test` |
| A-29 | 🔴 **6 个设置的值读不回来**（60 次 WARN） | 🔴 读写不对称（已修） | `ToCommand` 会**编码**值：`AbLoopA/B` 的 0 → `no`、`AbLoopCount` 的 -1 → `inf`、`LoopFile` 的 true → `inf`、demuxer 限额追加 `MiB` 后缀。而读方向用**朴素的 `double.TryParse`/`int.TryParse`**，一个都读不回来。日志实测：`mpv.conf value not convertible for AbLoopA: 'no'` 等 **60 次**，每次都在把用户设过的值静默退回 UI 里的旧值 | ✅ **已修**：新增 `MpvConfValueCodec.ParseMpvValue(type, text)`，按**属性类型**决定 `no`/`inf` 的含义（同一个 `no` 对 double 是 0、对 bool 是 false），并处理 `+` 相对前缀与 `MiB` 后缀。**39 个真实 NUnit 测试通过**（含 6 个日志原值） |
| A-30 | **没有人在跑测试**：`mpv-conf-test` 编译不过（A-28），所以任何测试都跑不了 | 🔴 过程缺陷（与 A-28 同因） | A-29 与 A-26 两个缺陷都本该被一条往返测试拦住。**这就是「测试套件死了」的实际代价** | ✅ **已修**：A-28 解开后整套 297 个测试可跑，临时闸门已删；`build.ps1` 现在直接跑 `dotnet test mpv-conf-test` 作为打包前的闸门 |
| A-31 | 🔴 **`tools/deploy-test.sh` 在本机是静默空操作** | 🔴 部署工具失效 | 它是 bash，调 Unix `find`/`cp`；本机 PATH 把 `find` 解析成 **Windows find.exe**，它报 `FIND: Parameter format not correct` 且**不输出文件名** → 复制循环读到**空列表** → **一个文件都没拷**，却仍然打印"src dll / dst dll"、以 `xbf count: 0` 收尾、**exit 0**。实测：dst dll 时间戳毫无变化。让人以为部署过了 | ✅ **已修**：① 新 `tools/deploy-test.ps1`（原生 Copy-Item overlay + `-Verify` 复核 + xbf 断言）② 新 `tools/compare-deploy.ps1`（逐文件 size+SHA256 比对，报告差集）③ bash 脚本开头加探测，**检测到非 GNU find 直接 exit 1 并指向 PS 版本** |
| A-33 | **没有检查器验证 `{StaticResource Mpv*}` 是否解析** | 🟡 静默陷阱 | `StaticResource` 指向不存在的键**不是构建错误** —— 标记编译器接受它，XAML 解析器在运行时才抛；对设置行就是"这一行永远不出现"或窗口加载失败。`check-xaml.py` 只查良构，所以这个拼写错误对现有**全部**闸门不可见（加 `MpvOptionErrorStyle` 时发现的） | ✅ 已加检查器（仓库外 `audit/check-resource-keys.py`）：当前 25 个引用键**全部解析**，6 个定义了未引用（`MpvSpaceL`/`MpvSpaceXl` 是 §4.1 决定保留的；另 3 个从 C# 消费，是审计已纠正过的假阳性） |
| A-34 | **语言范围收窄为 zh-CN + en-US**（2026-10-19 用户裁定） | 🔵 范围决策 | 九种语言里，zh-CN 完整（0 缺）、en-US 只缺 746 个生成 `Schema*`，其余七种各缺 746 个 `Schema*` + **71 个本轮新增的手写键**（`Library*` 36 / `Shell*` 14 / `Screenshot*` 9 / `Drop*` 6 / `Music*` 5 / `Tab*` 1）。裁定：本轮只保证这两门语言，其余七种回落英文字面量。**检查器相应改动**：`check-localization.py` 新增 `--languages=zh-CN,en-US` 窄口径（绿）；**默认仍是九语言全量**，因此默认运行依然红——这道闸门没有被关掉，只是有了一个可核对的验收口径。生成 `Schema*` 缺口降级为 WARNING（登记即例外，例外不该让闸门永久失败），手写键缺口仍是 ERROR | 接受（默认红／窄口径绿，两者都是真的） |
| A-35 | 🔴 **三个写入者同时写 `hdr-reference-white` / `hdr-peak-percentile`，且 SDR 回落命令必然失败** | 🔴 真缺陷（画面级） | ①`mpv.conf` 写 `"auto"`（带引号，被当字符串）与 `0.0`；②`profiles.conf [HDR_generic]` 写 `100` 与 `99.99`；③应用 `MpvPlayerPage_Display.cs` 写 `displayPeak`（本机 1000）与 `99.5`。`profile-restore=copy` 使先写者跨片源残留，所以谁最后写谁赢，且没人知道赢家该是多少。更硬的证据：SDR 分支发的是 `no-osd set target-peak 0`，**本条命令实测必失败**（属性接口 `-9 unsupported format`，命令接口 `-12 error running command`），于是同进程内一旦走过 HDR，`target-peak` 永久停在 1000。修法：回落改 `auto`（实测接受）；`hdr-reference-white` 改取 `SdrWhiteLevelInNits`（192-200，实测日志值）；删掉 `[HDR_generic]` 的两行（已授权），配置层不再猜显示器真值；`hdr-peak-percentile` 统一 99.5 | ✅ **已修** |

| A-32 | **`mpvw-TEST.lnk` 桌面快捷方式不存在** | 🟡 文档过时 | 记忆文档称「桌面快捷方式 `mpvw-TEST.lnk` 已指向该目录」，实测 `%USERPROFILE%\Desktop` 下**没有**该文件。交付路径要以**文本路径**为准，不要依赖快捷方式 | 待修（文档已在本轮加横幅） |

---

### 7.1 两项产品决策的裁定（2026-09-21，由治理方拍板，非用户）

台账里 A-24 / A-25 是**产品范围**问题而不是缺陷，因此留给决策；用户已表示由治
理方自行裁定。裁定如下，两者都**保留**，各自附带一个必须做的收尾动作。

### A-24 裁定：**保留 549→548 条 schema 生成选项**

理由（按权重）：

1. **它就是产品的定位。** README 的第一句差异说明就是「设置窗口被重新组织」，
   而"把 mpv 的全部选项做成界面"是本项目相对原版的核心主张。删掉生成层等于
   取消这个主张 —— 那不是清理，是功能倒退。
2. **架构不是本分支发明的，删不掉的成本已经付了。** `Layout/SettingsSections.cs`
   与四张映射表的类文档注释**与上游逐字节相同**（上游 `93bf0b4`）。生成层只是
   往既有机制里**填数据**，不是新机制。
3. **生成器确定性已实测**：`--check` 幂等、7/7 SHA-256 与独立档案副本一致、
   4 个 `PYTHONHASHSEED` 下位相同。可复现，所以可以长期维护。
4. **删掉会丢掉已经工作的东西**：`diagnose-advanced-flag` 的 767 行卡后行、
   13 个分区的图标与描述、以及"每个分类 ≥ 8 行"的下限判定，全都建立在
   这 548 行存在的前提上。删掉要重算全部折叠策略。
5. **唯一的真实缺口是翻译，而翻译是可增量补的**，不需要一次性完成。

**必须做的收尾动作**：A-16 的翻译缺口要**显式化**，不能继续以"看起来像 bug"
的形式存在。当前生成行的标签/描述渲染的是英文 schema 文本
（`SchemaLabel_Mpv_*` / `SchemaDesc_Mpv_*` 在 9 个语言文件里一个都没有）。
建议的最小闭环：**让 `check-localization` 的这条 ERROR 保持失败，直到补完**，
而不是加豁免把它压绿 —— 一个持续变红的检查器比一条注释更能防止它被遗忘。

**一并订正的数字**：`549` → **`548`**（`Speed` 那一轮删掉了 `Mpv_speed`，
连带 2 个 `Schema*` 键）。这个数字散落在生成器与多个检查器的注释里，
**不订正的话它会自己长回来** —— 见 `gen-schema-options.py`、`check-sections.py`、
`check-localization.py`、`MpvSettings.cs`。**已登记为待办。**

### A-25 裁定：**保留 `MpvConfWatcher`，但必须解开它的编译期耦合**

理由：

1. **它修的是一个真实的谎。** 在它之前，"设置页的显示值"与"mpv 实际用的值"
   在用户开着应用改 `mpv.conf` 时会**静默分叉** —— 界面显示旧值、mpv 跑新值，
   且没有任何提示。这比没有这个功能更糟，因为用户会以为界面是真的。
2. **它的工程质量是这套代码里最高的一档**：监听目录而非文件（文件可能还不存在）、
   事件后等文件安静 400ms（编辑器常是 truncate-then-write）、
   reload 跑**两个** reader、**写盘前**戳 generation 防自激环、
   失败静默降级但必打日志。这些细节都不是随手写的。
3. 反对它的理由是**风险**，而风险可以用结构消除，不必靠删功能来消除。

**必须做的收尾动作（就是 A-28）**：`MpvConfManager.cs:65,67` 现在**编译期依赖**
`ManagedMpvConfig` 与 `MpvConfWatcher`，而它是被 `mpv-conf-test` link 进去的
"纯配置"类 —— 结果是**整个测试项目编译不过**（已在 HEAD 复现，非本轮引入）。
正确接缝：`MpvConfManager` 不该知道"谁在监听这个文件"，把那个调用改成
**注入的回调**（`Action? onOwnWrite`）或一个极小接口。
**解开之后，A-26 的往返测试就能真正跑起来** —— 这也是裁定保留 watcher 的前提：
它当前的耦合状态正在让测试基建失效。
## 8. 变更类型 → 必须做的验证（对照表）

| 你要改的东西 | 必须做 |
|---|---|
| 新增 / 删除设置行 | 构建；`check-sections`（sectionMap 守卫）；`check-localization`；`check-settings-drift`；`check-empty-sections` |
| 新增 / 删除分区 | 上面全部 + `check-section-icons` + `diagnose-advanced-flag` + `report-fold-cards`；确认四张表都改了（§3.2 规则 4） |
| 调整折叠名单 / 两个下限 | `diagnose-advanced-flag`（必须报「每个分类 ≥ 8 行、每个折叠分区 ≥ 4 行、无行被隐藏而无卡片」）+ `report-fold-cards`；**改 560 窗口高度则重算 CategoryFloor** |
| 改内容列 / 滚动 | `check-scroll-maxwidth` + `check-xaml`；目视三行左缘对齐（§2.1） |
| 改间距 / 排版令牌 | `check-xaml`；确认没有新增字面量魔数（§4.1） |
| 改图标 | `check-sections`（59 个互不相同）+ `check-section-icons`；**先渲染成图看过** |
| 改 mpv 选项映射 | `check-mpvconf-precedence`（映射必须与 `ToCommand` 完全一致）+ `check-conf-lines` + `check-settings-drift` |
| 改生成器 | 重新生成；`grep "list.Add(new Option"` == 项数；`grep -cP '^\s*new Option\s*$'` == 0；确认生成文件只差预期内容 |
| 改 / 新增检查器 | **反向验证**（§5.3）+ **自证它读的是代码真正读的东西**（§5.4） |
| 改文档 | 对照本文件 §7 登记台账；**不要复述已作废的结论** |
| 任何改动 | `git status --short` 干净；BOM/行尾与 HEAD 一致；构建 0 警告 0 错误；**不启动应用** |

---

## 附：本文件的依据

- 实测数据：`_work/mpv-settings-governance/` 下的 `findings.md`、
  `defect-inventory.md`、`audit/count-exposed-options.py`、`run-baseline.py` 及
  `audit/baseline/before--*.txt`。
- 代码库内既有的权威注释：`SettingsPage.xaml:63-82,146-191`、
  `SettingsPage.Options.cs:22-33,108-135,237-313,1263-1398`、
  `SettingsSections.cs:7-26,37-63,123-166`、`Styles.xaml:880-1021`、
  `OptionListControl.xaml.cs:300-357`、`check-sections.py:131-227`。
- 外部规范：Windows 11 排版与间距规范（字号下限、8/12/16 节奏）、
  微软设置页指导（可滚动、最大宽度约 1000–1100px、分区标题 BodyStrong、
  展开器深度上限 1 层）。
- 用户明令：§5.5（禁止验收测试与探针）。