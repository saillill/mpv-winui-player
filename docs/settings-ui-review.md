# 设置界面调研与改造计划

> 调研日期：2026-09-18（含第二轮深挖）。
> **改造部分仅为计划，尚未执行**；批次 A 的清理已完成并推送（见 §3.2）。
> 除注明「据项目文档」的条目外，本项目与 mpv 的数据均为本次实测。

---

# 第一部分：现状

## 1.1 规模

| 项 | 数量 | 位置 |
|---|---|---|
| 顶层分类 | 12 声明 / 10 实际使用 | `AppLang.SettingsCategory*` |
| 分区 | 51 | `AppLang.Section*` |
| 分区描述 | 51（与分区一一对应） | `AppLang.SectionDesc*` |
| 已注册设置项 | 约 272 | `SettingsPage.Options*.cs` 的 `Key = nameof(...)` |
| 选项控件类型 | 14 | `Modules/Settings/Controls/` |
| 设置页代码量 | 6,069 行（仅 Options 部分） | `SettingsPage.Options*.cs` |
| 自定义布局系统 | 2,614 行 | `SettingsPage.Customize*.cs`（1,776）+ `Layout/`（838） |

> 统计口径注意：属性名含数字（如 `SectionGpuD3d11`），用 `Section[A-Za-z]+` 这类正则
> 会在数字处截断而漏数 —— 本文初版就因此把分区报成 50、把「缺 `SectionMeta` 条目」
> 的分区报成 1 个。以下数据均用 `[A-Za-z0-9]+` 重算：**102 个 `Section*` 属性
> = 51 个分区 + 51 个描述**，1:1 完全配对。

设置项分布（按文件）：

```
Advanced    84   ← 占 31%
Playback    60
Subtitles   43
Video       31
Audio       24
Screenshot  16
Program     12
PathFolders  2
```

## 1.2 界面结构

`TreeView`（左侧导航）+ 右侧详情；详情里是**分区标题行 + 选项卡片**。之上叠了一套自定义系统：拖动排序、跨文件夹移动、重命名、隐藏/显示、新建文件夹。

## 1.3 与 mpv 的联动方式（关键）

本项目走**「设置存自己的、运行时下发给 mpv」**：

- **持久化**：`ApplicationData.Current.LocalSettings`（未打包模式下即注册表），**不写 mpv.conf**（`AppDataSetting.cs`）。
- **下发**：`MpvSettings.ToCommand(key, value)` 生成 `set <option> <value>`，通过 IPC 即时生效；播放器初始化时批量应用。
- **例外**：少数**只能在启动前生效**的选项（如 ytdl_hook 的 script-opts）由带标记的区块写进 `mpv.conf`：

  ```
  # === mpv-winui managed options (do not edit) ===
  ...
  # === end mpv-winui managed options ===
  ```
  （`ManagedMpvConfig.cs`）

- **一致性检查**：`tools/check-settings-drift.py` 校验三方对齐 —— AppSettings 属性 / MpvSettings 映射 / 设置页条目。

固有张力：`mpv.conf` 用户可手改，App 设置是另一份事实来源，两者可能互相覆盖。项目用「标记区块」+「漂移检查」局部缓解，但没有统一优先级模型。**mpv.net 的解法值得注意：它在每个选项上打 `file = mpv | mpvnet | libplacebo` 标签，明确标注这个选项归哪个配置文件管**（见 §3.3）。

当前 `check-settings-drift.py` 的报告（HEAD 与本次改动后一致）：

- **7 条 ERROR**：设置项存了但**从不下发给 mpv**（无 `MpvSettings.ToCommand` 映射）——
  `BuiltInPreviewAliveTimeout`、`EnableMouseInput`、`EnableMouseInputDiscNavOnly`、
  `EnableSaveBackup`、`EnableVideoBuiltInPreview`、`KeepVideoBuiltInPreviewAlive`、`PlayerStyle`
- 21 条 WARNING：设置项未出现在设置窗口（部分属 App 内部状态，合理）

那 7 条需要逐个判断：是「本该下发但漏了」还是「App 内部状态、工具规则过严」。

## 1.4 mpv.conf 编辑器

选项定义来自 AppData 的 `mpv-conf-editor/` JSON：

```json
{ "name": "alang", "group": "Track Selection",
  "desc": "Specify a prioritized list of audio languages...",
  "link": "https://mpv.io/manual/master/#options-alang",
  "values": [ { "type": "array" } ] }
```

**实测：仓库里只有 9 条定义**（6 + 3），且按该目录 README，App **不内置** schema —— 目录不存在时加载为空。**默认安装下，这个编辑器打开后没有任何选项。**

---

# 第二部分：外部调研

## 2.1 mpv.net（Windows / C# / WPF，最可比）

配置编辑器结构：**搜索栏 + 导航树 + 设置面板**。控件类型：字符串 / 选项（单选或下拉）/ 布尔 / 数值，另有颜色选择器和目录选择器。保存时**保留注释、章节结构和用户笔记**，只就地更新值。

选项定义外置在资源文件 `editor_conf.txt`（约 640 行），**不在代码里**。关键格式（据其源码）：

- **没有 section 标题行**，分组靠每个选项的 `directory` 字段用斜杠表达嵌套：

  ```
  directory = General
  directory = Video
  directory = Video/Render Options/Scaling
  directory = Video/libplacebo/Color Mapping
  ```

- 每项可带字段：`name`、`file`、`directory`、`type`、`width`、`help`、`default`、`option`（可重复）、`url`

  ```
  name = process-instance
  file = mpvnet
  directory = General
  help = Defines if more then one mpv.net process is allowed.\nMulti can alternatively be enabled by pressing the SHIFT key. (mpv.net option)
  default = single
  option = multi   Create a new process everytime the shell starts mpv.net
  option = single  Force a single process everytime the shell starts mpv.net
  option = queue   Force a single process and add files to playlist
  ```

- **类型没有统一声明**：布尔靠 `default = yes/no` 推；枚举靠多条 `option =` 行；
  只有颜色和目录有显式 `type = color` / `type = folder`；**数值范围写在 help 文本里**：

  ```
  help = <0-100> Set the JPEG quality level. ... The default is 90.
  help = <0.0..1.0> Antiringing strength to use for all filters. ...
  ```

- 共 **11 个顶层分组**（General / Video / Audio / Subtitle / Window / Playback /
  Program Behavior / Input / Appearance / Cache / Demuxer），含嵌套共 21 条路径，
  **最深 3 层**（`Video/libplacebo/Color Mapping`）。

**评价**：`directory` 路径式分组和 `url` 链接是好设计；但把范围塞进 help 文本是权宜之计。**本项目的 schema（`values[].type` / `values[].enum` / `link`）结构化程度反而更高** —— 只是内容近乎为空。

## 2.2 IINA（macOS / Swift）

侧栏 + 详情分栏，每个分区是独立 ViewController。分区：General / UI / Key Bindings / Subtitles / Network / Codec / Advanced / Utilities。

**最值得借鉴的是高级项的处理**：Advanced 页先有一个「Enable advanced settings」**总开关**，打开后才出现一张 **name / value 两列表格**，可填任意 mpv 选项。这是干净的「精选为主 + 全量逃生口」。搜索为全局索引，带自动补全。

## 2.3 Haruna（KDE / Qt/QML）

分区：General / Playback / Video / Audio / Subtitles / Playlist / Mouse / **Custom commands** / Keyboard shortcuts。**刻意只精选少量 mpv 选项**（评测明确抱怨它暴露的选项偏少）。手册**只解释复杂设置**，其余直接链接 mpv 官方手册。`Custom commands` 页给自定义命令绑快捷键，作为逃生口。

## 2.4 配置包一派（mpv-lazy / uosc / ModernX）

**没有设置界面**：设置 = `script-opts/<脚本>.conf`，脚本里的 `user_opts` 表兼作文档并做类型/范围校验；运行时开关走右键菜单（即本项目已采用的 `dynamic_menu.lua` 模式）。

## 2.5 归纳

| | mpv.net | IINA | Haruna | 配置包 | 本项目 |
|---|---|---|---|---|---|
| 层级 | ≤3 层，11 顶层 | 8 分区 | 9 页签 | — | 2 层，10 顶层 / 51 分区 |
| 搜索 | 名称/描述/**值** | 全局 + 补全 | — | — | 有，范围待确认 |
| 精选 vs 全量 | 全量 | 精选 + 逃生口 | 精选 + 逃生口 | 无界面 | **全量（272）** |
| 高级项收纳 | 分目录 | 总开关 | 分页签 | — | 无 |
| 手册链接 | `url` 字段 | 有 | 有 | 链接 | **无**（schema 里有 `link` 未用） |
| App 与 conf 的归属 | `file` 字段 | 分离 | 分离 | 只有 conf | **未定义** |

**四条路子里没有一条试图把 mpv 全部选项做成漂亮界面** —— 都是精选 + 留原始出口。而本项目的既定方向是**全量界面**，这是差异点，不是缺点，但它意味着必须解决「全量」带来的组织问题，且**更依赖一个原始出口兜底**。

---

# 第三部分：深挖

## 3. 选项功能是怎么处理的

### 3.1 mpv 提供机器可读的完整元数据

这是「把几百个选项做成界面」的关键使能事实。核心命令：

```bash
mpv --list-options          # 所有选项：类型、范围/可选值、默认值、[file] 标记
mpv --option=help           # 单个选项的合法取值
mpv --list-properties       # 可读写属性（运行时接口）
```

实测本机 mpv v0.41.0：

| 项 | 数量 |
|---|---|
| `--list-options` 输出行数 | **1272** |
| 去掉列表后缀（`-add`/`-set`/`-append`/…）后的**独立选项** | **870** |
| 列表后缀变体 | 393 |
| 枚举型（`Choices:`） | 152 |
| 需要文件路径（`[file]`） | 41 |
| 已废弃/移除 | 16 |

类型分布：

```
Flag 226 | Float 120 | Integer 93 | String 91 | Double 38
String list 31 | Key/value list 16 | ByteSize 12 | Color 11
Object settings list 7 | Window size 4 | ... | 以及 152 个 Choices
```

每行的形态（真实摘录）：

```
 --ab-loop-a                      Time (default: no)
 --ab-loop-count                  Choices: inf (or an integer) (0 to 2147483647) (default: inf)
 --access-references              Flag (default: yes)
 --ad-lavc-ac3drc                 Float (0 to 6) (default: 0)
 --cache-on-disk                  Flag (default: no)
 --gpu-shader-cache-dir           String (default: ) [file]
 --angle-d3d11-feature-level      Choices: 11_0 10_1 10_0 9_3 (default: 11_0)
```

**结论：类型、范围、可选值、默认值、是否文件路径，全部可以自动抽取**，不需要手写。本项目 272 个选项目前是**手写映射**（`MpvSettings.ToCommand` 一个 switch 分支一个选项），这是维护成本的根源，也让「补齐到全量」几乎不可行。

### 3.2 选项名前缀已经天然成组

按名称前缀统计（独立选项）：

```
sub 146 | vo 68 | audio 63 | osd 52 | demuxer 48 | video 38
input 35 | vd 26 | gpu 26 | screenshot 25 | cover 21 | ao 21
glsl 17 | ad 15 | image 14 | hwdec 14 | watch 12 | playlist 12 | tscale 11 | scale 11
```

这直接给出了分组的客观依据：`sub` 一家就 146 项（占 17%），`vo`/`gpu`/`glsl`/`scale`/`tscale` 合起来是渲染栈，`demuxer`+`cache` 是缓存栈。**分组可以按数据来，不必凭感觉。**

### 3.3 选项的归属要显式标注

mpv.net 用 `file = mpv | mpvnet | libplacebo` 标明每个选项属于哪份配置。本项目目前没有这个概念，于是 App 设置与 `mpv.conf` 的关系是含糊的（§1.3）。**建议每个选项元数据加一个 owner 字段**，至少区分三类：

1. 运行时可通过 IPC 改（本项目绝大多数）
2. 只能启动前生效（→ 要走 `ManagedMpvConfig` 的标记区块）
3. App 内部状态，与 mpv 无关（→ 不应出现在「mpv 选项」语境里）

这也正好能解释 §1.3 里那 7 条 ERROR。

## 4. 美术（视觉设计）是怎么做的

本项目是 WinUI 3，所以权威参考是微软自己的设计体系。

### 4.1 微软官方对「设置页」的具体指导

- **版式**：可滚动，**最大宽度约 1000–1100 px**，保证宽屏下可读
- **分区标题**：用 `BodyStrong` 文字样式，相同/相关设置归到一个标题下
- **每节设置数量**：建议**控制在四到五个**
- **控件**：用 Windows Community Toolkit 的 **SettingsCard** / **SettingsExpander**
  - `SettingsCard`：Header + 可选 Description + 可选 HeaderIcon + **动作控件右对齐**
  - `SettingsExpander`：**当一项设置需要按需展示子选项时使用**，展开区里放更多
    SettingsCard。原文说这「既保持页面简洁，又能展示高级选项」
  - **「避免把扩展器嵌套超过一层」**
- **主题设置**：下拉框放在 SettingsCard 内，选项为 浅色 / 深色 / 跟随系统，
  并可加一个指向 `ms-settings:colors` 的链接
- **关于区**：放在设置页底部，用 `SettingsExpander`；折叠时显示 App 名、图标、版本号；展开后可放仓库链接、缺陷反馈、依赖清单（HyperlinkButton）、版权/条款/隐私链接

**注意最后一条**：微软官方就是把「关于」做成可展开项放在底部，且**展开器深度上限为 1 层** —— 这正是「不常用内容收进二级」的官方做法，同时给了明确的深度红线。

### 4.2 字体（Windows 11 排版规范）

- 字体：**Segoe UI Variable**
- 字重：正文 Regular，**标题 Semibold**
- **最小值：14px Semibold / 12px Regular** —— 更小更细在某些语言下不可读
- **全部 UI 文本用句子大小写（sentence case），包括标题**
- 截断优先用省略号，裁剪仅用于罕见情况
- 字号阶梯（有效像素 / 行高）：`12/16`、`14/20`、`14/20 semibold`、`18/24`、`20/28`、`28/36`、`40/52`、`68/92`

本项目实测：选项控件用的是内置样式 `BodyTextBlockStyle`（标签）和
`CaptionTextBlockStyle`（描述），前景用 `TextFillColorSecondaryBrush` —— **字体体系是符合规范的**。

### 4.3 间距（8 / 12 / 16 节奏）

微软的间距语义表：

| 场景 | 值 |
|---|---|
| 按钮之间 | 8 epx |
| 按钮与浮出控件之间 | 8 epx |
| 控件与标题之间 | 8 epx |
| 控件与标签之间 | 12 epx |
| 内容区之间 | 12 epx |
| 表面与边缘文字之间 | 16 epx |
| 多行列表 | Body + Caption，图标 32 epx |
| 分区标题 | Body Strong |
| 扩展器内 | 控件右对齐，与展开按钮间隔 16 epx；内部缩进 48 epx |

本项目实测：设置相关 XAML 里共 **116 处硬编码的 Margin/Padding/Spacing，只有 38 处落在 8/12/16/24 节奏上**。偏离值包括：

```
Spacing="2"   ×12      ← 最常见，且不在节奏上
Spacing="6"   ×6
Spacing="3"   ×3
Spacing="10"  ×3
Padding="8,4" ×4
Padding="10,6" ×2
Padding="6,2"  Padding="6"  Padding="16,5"  Padding="16,10"
```

**没有间距刻度，值是按手感一个个敲的** —— 这是「看起来乱」最直接的技术来源，比分类更影响观感。

### 4.4 卡片版式的重复

`OptionControlBase` 只提供代码后置助手（`UpdateDescription`），**XAML 版式没有共用模板**。实测每个选项控件各写一份 label / description / warning 的 Grid：

```
OptionListControl        32 行样板
OptionStringListControl  16
OptionStringControl      15
OptionShaderListControl  15
OptionActionControl      15
OptionIntegerControl     14
OptionDoubleControl      14
OptionColorControl       13
OptionCheckListControl   12
OptionBooleanControl     12
OptionMultiListControl   11
OptionLayoutControl       5
```

**合计 142 行重复**，且无 `ContentPresenter`/共用模板。后果很具体：想给每行加一条「查看 mpv 手册」链接，要改 12 个文件。

顺带一提，`OptionBooleanControl.xaml` 里有一条注释记录了一个真实踩坑：`ToggleSwitch` 的默认 `MinWidth` 太宽，把开关挤出了卡片右边缘，靠 `MinWidth="0"` 解决；注释还说明他们试过复制 WinUI2 的模板但运行时报错（`Windows App SDK` 里没有那些主题键）。**这类经验值得沉淀成一份卡片版式规范，而不是留在单个文件的注释里。**

### 4.5 图标重复

51 个分区用了 **48 个不同图标** —— 三对分区共用同一个字形：

| 字形 | 共用的分区 |
|---|---|
| `\uE7DE` | `SectionSubtitleAss`、`SectionSubtitleBehavior` |
| `\uE8D2` | `SectionOsdAppearance`、`SectionSubtitleStyle` |
| `\uE787` | `SectionOsdPosition`、`SectionSubtitlePosition` |

分区卡片是**靠图标区分**的（标题 + 一行描述 + 字形），撞图标会直接削弱可扫读性。
B3 把图标收进 `SettingsSections` 表之后，这类重复可以用脚本一眼查出来（见附表命令），
换图标也从「在 51 个分支里找」变成改一行。

## 5. 分类是怎么分的

三种可借鉴的路径：

| 路径 | 做法 | 例子 |
|---|---|---|
| **精选** | 只暴露少数常用项，其余靠逃生口 | Haruna（9 页签）、IINA（8 分区 + 高级表格） |
| **全量 + 分层** | 全部选项进界面，靠目录把不常用的压深 | mpv.net（11 顶层，最深 3 层） |
| **数据驱动** | 按选项自身的命名/元数据自动归组 | mpv 的 `--list-options` 前缀分布（§3.2） |

本项目是 **全量**（272 项）且**只有 2 层**（顶层分类 → 分区），没有第三层，于是所有「不常用」项都只能平铺在分区里 —— `Advanced` 单文件 84 项就是这么堆出来的。

**结论：走全量路线就必须要第三层。** 官方给的形态也正好是现成的：**分区 = SettingsCard 组，不常用项 = 该分区内的 SettingsExpander，深度到 1 层为止**（§4.1）。这与用户提出的「不常用选项作为二级菜单放置」完全一致，且不违反微软的深度限制。

另外按 §3.2 的数据，顶层分类的合理数量应在 9–11 之间（mpv.net 11，IINA 8，Haruna 9），而本项目 10 个 —— 数量本身没问题，**问题是分区数 51 相对 272 项偏多**（平均每区 5.3 项），且分布极不均（Advanced 84 对 PathFolders 2）。

## 6. 描述是怎么写的

mpv.net 的 `help` 文案约定（真实摘录）：

```
help = Defines if more then one mpv.net process is allowed.\nMulti can alternatively be enabled by pressing the SHIFT key. (mpv.net option)
help = <int> Amount of recent files to be remembered. Default: 15 (mpv.net option)
help = <0-100> Set the JPEG quality level. Higher means better quality. The default is 90.
help = Color used to draw parts of the window not covered by video. See the --sub-color option for how colors are defined.
```

规律：开头 `<类型/范围>`、换行用字面 `\n`、默认值写进文本（`Default: 15`）、
来源用括号标注（`(mpv.net option)`）、可交叉引用其他选项、关键项配 `url =` 指向官方手册。

Haruna 更省：**手册只解释复杂设置**，其余直接给 mpv 手册链接。配置包一派干脆把
`mpv.conf` 的精注释当文档。

本项目实测：选项的 `Description` 是短句（如 "Hardware decoding, codec selection"），
**没有类型/范围提示，也没有指向 mpv 手册的链接** —— 尽管自有的 schema 里已经有 `link` 字段。
而 `mpv --list-options` 能自动提供类型和范围，`<0-100>` 这类前缀完全可以自动生成。

---

# 第四部分：改造计划

## 前置：批次 A 已完成 ✅

| # | 动作 | 状态 |
|---|---|---|
| A1 | 删死分类 `SettingsCategoryTesting`、`SettingsCategoryInput` | ✅ `1d7dfbd` |
| A2 | 删死分区 `SectionGpuColor`（AppLang + 9 JSON + 死别名 `sGpuColor`） | ✅ `1d7dfbd` |
| A3 | 给 `SectionGpuD3d11` 补图标与描述（`SectionMeta` 现覆盖全部分区） | ✅ `1d7dfbd` |

图标 `E964` 是**把候选字形用 Segoe Fluent Icons 渲染成图逐个看过**之后选的
（形似硬件卡，对应 `d3d11-adapter` / WARP），不是猜码位。

**仍未做**：`SettingsCategoryProgram` 显示名与属性名不一致 —— 归入 §B 的分类重排一并定，
因为它取决于新的分类命名方案。

## 目标（按用户确认的方向）

保留「**把 mpv 全部选项做成漂亮界面**」这一产品定位 —— 不做 Haruna 式的激进精选。
在此前提下：

1. 全量选项进界面，**不常用的收到二级**
2. 提供**开发者模式**作为总开关
3. 保留**原始选项出口**（name/value 表，填任意 mpv 选项）
4. 保留 **UI 编辑**（现有 Customize 系统）

## 批次 B —— 地基：选项元数据与稳定标识

**这是后面一切的前提，建议先做。**

| # | 动作 | 说明 | 状态 |
|---|---|---|---|
| B1 | 引入选项元数据表，字段含 `owner` / `type` / `range` / `choices` / `default` / `link` | 解决 §3.3 的归属问题，也是 §6 描述的来源 | 一半（生成器已带 `type`/`range`/`choices`/`default`/`group`；`owner` 待定，见 D4） |
| B2 | 元数据**从 `mpv --list-options` 自动生成**并随包分发 | 类型、范围、可选值、默认值全自动；替换手写 switch | **已做**（`1f536f0`，本地未推）—— 820 项，`tools/gen-mpv-options.py` 幂等 |
| B3 | 给分区引入稳定 id，`SectionMeta` 改按 id 查表，删掉 51 个字符串分支 | 见下图；顺带解决 §4.4 的 12 份重复 | **已做**（`93bf0b4`，本地未推） |
| B4 | 卡片外壳抽成共用样式；文字栈抽成共用块 | 见下面的完成度说明 | **一半**（外壳 `e1f40dd` 已做；文字栈因无法编译验证而暂缓） |

> 分区的身份原本是它的译文（`SectionMeta()` 用 51 个 `var l when l == lang.SectionXxx`
> 分支逐字符串比），项目已为此打过三个互不知情的补丁。B3 已把它们收敛成
> `SettingsSections` 一张表（id / 图标 / 标题 / 描述 + 10 行分类表）。

### B3 的验证状态（重要）

**本环境无法编译该项目**：`mpv_winrt` 是 C++/WinRT 组件，需用 VS 构建工具链编译，
而该工具在本机被安全策略拦截，因此产不出它对应的 `.winmd`。缺了这个文件，C# 项目
会报 79 个 `Modules/Player` 下的「找不到类型」错误，且 XAML 标记编译器在分析到设置
模块之前就中止。

已做的最强静态验证：分区表精确覆盖 51 个分区 + 10 个分类（描述访问器一一对应、无重复
id）；7 个改动文件括号配平；`check-localization` 仍为 1223 属性；
`check-settings-drift` / `check-ui-tooltips` 与 HEAD 一致。

**一个反面教训**：中途我曾以「构建后的错误里没有我改的文件」当作证据 —— 这是错的。
我随后往新文件里故意注入一个不存在的成员引用，构建**没有报出它**，证明编译器根本没
分析到设置模块。同一份代码在注入前后产出完全相同的 79 个错误，也印证了这一点。
**「错误列表里没有我」不等于「我的代码通过」。**

→ 信任前请先跑 `.\build.ps1 -Configuration Release -Platform x64`。

### B4 的完成度（两半，只做了一半，原因不同）

B4 其实是两个独立的重复点，风险不一样，所以拆开做：

**已做 —— 卡片外壳**（`e1f40dd`）：`OptionListControl.xaml` 里 11 个选项模板各自
抄了一遍同一个 9 行 `Border`（一共 99 行），改圆角、内边距或背景色要改 11 处，
漏一处就是一处看不见的不一致。现在外壳是一个 `Border` 样式，每个模板 3 行。
这一半能安全落地，因为**元素类型和属性一个没变**，只是搬进 `Style`：
模板→宿主控件的映射逐字节与改动前相同，全仓 42 个 XAML 解析通过。

**暂缓 —— 文字栈**：11 个控件各写一份 label / description / error / warning 的
`StackPanel`（合计 102 行），且 `WarningText` 在所有 9 个控件的代码后置里都是同一句
`UpdateWarning(WarningText)`。这个抽取要动 11 个 code-behind 文件里的 `x:Name`
引用，而**本环境编译不了**（原因见上）。改动本身不难，但没有一次真实构建兜底就
不该合 —— 这正是上一节那条反面教训的直接应用。留待能构建时再做。

## 批次 C —— 信息架构

| # | 动作 | 依据 |
|---|---|---|
| C1 | 顶层分类重整到 9–11 个，命名统一（含 `Program`/`Desktop`） | §5，mpv.net 11 / IINA 8 / Haruna 9 |
| C2 | **引入第三层**：不常用项用 SettingsExpander 收在分区内，**深度严格 1 层** | §4.1 官方形态 + 官方的深度红线 |
| C3 | 重新分配 `Advanced` 的 84 项到有意义的分区，再按 C2 决定各自深度 | §5；`Advanced` 是「乱」的主因 |
| C4 | 分区按 §3.2 的选项名前缀分布重新划界（`sub` 146 项需细分） | 数据驱动，不凭感觉 |
| C5 | 搜索扩展到**值**，并作为主要入口 | mpv.net |
| C6 | 逐节控制项数（官方建议 4–5 项/节） | §4.1 |

## 批次 D —— 美术规范化

| # | 动作 | 依据 |
|---|---|---|
| D1 | 建立间距刻度（8/12/16/24），替换 116 处硬编码里的 78 处偏离值 | §4.3；**这是观感提升最大、风险最低的一项** |
| D2 | 内容最大宽度限制在 1000–1100 px | §4.1 |
| D3 | 分区标题统一 `BodyStrong`；全文句子大小写审查 | §4.1 / §4.2 |
| D4 | 校验字号不低于 14 px Semibold / 12 px Regular | §4.2 |
| D5 | 评估是否改用 Community Toolkit 的 `SettingsCard`/`SettingsExpander` | §4.1；现为手写，需权衡与 Customize 系统的兼容性 |

### D1 / D2 / D3 的实际结论

**D1 已做**（`ad45f8e`，本地未推）。落地的不是原计划的「78 处偏离值」，而是
**全部 67 处**都变成了具名引用，因为实地看下来项目用的是**两套**刻度：布局节奏
（8/12/16，对应微软给「表面之间」的建议）和控件内部紧凑刻度（2/4/6，图标→文字这种
间距本来就该更紧）。原计划的错误在于把后者也当成「偏离」。只有 3 处真的调了值：
控制栏分区间距 3→4、面包屑列间距 10→12、布局控件列表间距 10→12。另有两处
（树内联按钮的 `6,2`、自定义底栏的 `16,10`）是**故意留在刻度外**的，因为它们是按
相邻控件高度手调的，已在代码里写明原因。

**D2 无需改动** —— 内容宽度已经是 `MaxWidth="1060"`，落在 1000–1100 建议区间内。

**D3 无需改动** —— 分区标题在更早的提交里已改为 `MpvSectionHeaderStyle`
（派生自 `BodyStrong`，14px semibold），不再是硬编码 16px。全文硬编码文案只剩 7 条，
逐条核过：其中 3 条（`Browse` / `Open` / `Reset`）是 XAML 占位，运行时由
`AppLang` 覆盖；4 条（颜色选择器的 `Recent colors` 等）同样在 `OnSettingChanged`
里被 `AppLang.ThemeColor*` 覆盖。**都不是未本地化的漏网文案。**

### §4.5 图标重复 —— 已做（`74e62c3`）

三对分区曾共用字形（`E787` 位置、`E7DE` 行为/ASS、`E8D2` 样式/外观）。分区卡片靠
图标扫读，撞图标会削弱辨识度。已为每对里更贴切的一行保留原字形、另一行换新的：
`SectionSubtitlePosition` → `E7C4`、`SectionSubtitleAss` → `E713`、
`SectionOsdAppearance` → `E2B1`。字形是**先用系统图标字体渲染成图看过**才选的，
且三个新码位都先与其余 48 个比过 —— 有两个看起来最合适的候选（`E740`、`E790`）
其实已被占用了。结果：51 个分区、**51 个互不相同的图标、0 重复**。

### 关于 16px 的两处遗留（**不动，等你看**）

`OptionListControl.xaml:147` 是图标（不受文字下限约束），`:393` 的 `PaneTitleText`
是自定义模式的面板标题，`SettingsWindow.xaml:108` 是设置窗口标题。三处都是 16px，
**都不低于 12px 下限**，所以不属于 D4 的范畴；改成 14px 还是 18px 是纯视觉判断，
没有截图确认之前不动。

## 批次 E —— 逃生口与开发者模式

| # | 动作 | 依据 |
|---|---|---|
| E1 | **开发者模式**总开关，默认关闭，控制高级项/原始出口的可见性 | IINA 的「Enable advanced settings」 |
| E2 | **原始选项出口**：name/value 表，可填任意 mpv 选项 | IINA / Haruna；有了它 C3 才能删得大胆 |
| E3 | 选项描述自动带 `<类型/范围>` 前缀，并附 mpv 手册链接 | §6；数据来自 B2 的元数据 |
| E4 | 明确 App 设置与 `mpv.conf` 的优先级模型 | §1.3 / §3.3 的 `owner` 字段 |

## 批次 F —— mpv.conf 编辑器补内容

复用 B2 生成的元数据填充 `mpv-conf-editor` schema（现仅 9 条），解决「默认打开是空的」。
与 E2 有重叠，可合并。

## 建议顺序

```
A ✅ → B（地基） → D1（低风险高收益） → C → E → D2–D5 → F
```

B 必须先行：没有稳定 id 和元数据表，C 的重排会变成大规模字符串搬家。
D1 可以独立插队，因为它不依赖 B 且收益立刻可见。

## 进度

已做：A ✅ · B2 ✅ · B3 ✅ · B4 外壳 ✅ · B1 稳定 id ✅ · C1 ✅ · C4 ✅ · C5 ✅ ·
C6 ✅ · D1 ✅ · D2 无需改 ✅ · D3 无需改 ✅ · §4.5 图标 ✅

未做：B4 文字栈（等能编译）· B1 的 `owner` 字段（属 D4 决策）· C2 机制已存在但未正式化 ·
C3 部分完成 · E 全部 · F 已并入 B2 完成

**推送前需要一次成功构建。** 见「B3 的验证状态」。

### 批次 C 的实际结果（与上表原计划的差异）

原计划写的是「顶层收敛到 9–11 类」「`sub` 146 项需细分」「官方建议 4–5 项/节」。
实测后三处都要修正：

**C1 —— 11 类 → 10 类，不是 9 类。**
`screenshot`（16 项）并入 `video` 是有效的，因为截图的本质是画面功能。但
`Testing` / `Associations` 移到 `advanced` 的那一步，实际只挪动了 5 个选项
（`TestMpvCommandLog` / `TestOsdMessage` / `TestSignal` / `FileAssociationCheckList` /
`ActionUnassociateFiles`），分类数从 11 降到 10。原本估计的「9 类」把
`shortcuts` 也算进了合并，但快捷键有独立控制条，保留更合理。

顺带发现 `categoryOrder` 里**原本没有 `advanced`** —— 这不影响旧行为（旧代码里
`advanced` 只是分区级标记，选项的 Category 仍挂在 video/osd/subtitle 下），
但新代码把 5 个选项的 Category 真正设为 `advanced` 之后，必须补进 `categoryOrder`，
否则它们的排序会落到 `int.MaxValue`。

**C4 —— `sub` 是 67 项，不是 146 项，也不该只按前缀切。**
146 那个数字来自 mpv 手册的条目数，包含 `deprecated` 与不映射到 AppSettings 的项。
实际暴露的字幕选项是 **67 项**，其中 `sub-` 前缀占 52 项，横跨三种不同关切
（外观 / 排布 / 加载），所以只按前缀切会失衡。最终按 **mpv 前缀 + 语义** 划成五区：

| 分区 | 项数 | 可见性 | 依据 |
|---|---|---|---|
| `SubtitleStyle` | 17 | 总览 | `sub-font/color/outline/bold/italic/blur` 等外观项 |
| `SubtitleBehavior` | 12 | 总览 | 加载、轨道选择、延迟、可见性、编码 |
| `SubtitlePosition` | 15 | 卡片 | `pos/align/margin/scale` + ASS 排布 + 次字幕排布 |
| `SubtitleFilter` | 8 | 卡片 | SDH 滤镜、修时、强制事件、整形器、位图尺寸上限 |
| `SubtitleAss` | 14 | 卡片 | ASS 覆盖、嵌入字体、混合、位图字幕 |

删掉了三个过小的分区：`SubtitleImage`(4)、`SubtitleFilter`(3，重建为 8 项)、
`SubtitleSecondary`(5)。后两者的选项并入了语义所属的分区。

期间还清掉一处**长期存在的双别名**：`SettingsPage.Options.cs` 同时声明了
`sSubtitle*` 和 `sSub*` 两套指向同一批 AppLang 字符串的变量。这导致两个后果 ——
分区有两个拼写（改名时容易只改一边），以及 `check-empty-sections.py` 的项数
被重复计数（报 77，实际 67）。现在每分区只有一个 id。

**C6 —— 「4–5 项/节」不适用于这个项目，改用「超 8 项收卡片」。**
项目定位是「mpv 全部选项都做成界面」，删项不可接受；而 4–5 项/节在 266 个选项
面前意味着 50+ 个分区。实际采用的门槛是「分区超过 8 项就整体收进入口卡片」，
并且**必须保证每个分类至少留下一个可见分区** —— 否则分类点开会是一片卡片、
一个能直接调的项都没有，这正是当初修 Network / OSD 时踩过的坑。

字幕这边因此保留了 `Style`(17) 与 `Behavior`(12) 在总览，两者都超 8 项线，
是**刻意的例外**：压进卡片会让 Subtitles 分类打开后无事可做。

### 顺带修掉的一个陷阱

`tools/generate-settings-category-map.py` 自称是 `sectionMap` 的
「single source of truth」，实际**早已与代码脱节**：它的 `MAP` 缺 99 个现有 key、
多 12 个已删除的（`Ytdl*`、`ThemeAccentColor` 等），生成的分类是 15 个
（含 `watchLater` / `demuxer` / `cache` / `input` / `testing`），分区变量名
（`sSubtitleText` / `sTrackLanguage` / `sOsd` / `sGpuColor`）也都不存在了。
它原本只打印 WARNING 就继续输出可覆盖的片段 —— 一旦有人照它说的做，
整个设置树会被静默覆盖回旧形态。现在改成**检测到 drift 直接拒绝执行**，
并把文件头改为说明它已过时、真正维护的是手改 + 两个 `check-*` 脚本。

### 本轮的验证状态

12 个自检脚本全部通过：

```
check-sections        OK: 48 sections, 10 categories, 48 distinct icons, 0 shared
check-empty-sections  EMPTY: none / ORPHAN: none
check-localization    OK: 1320 AppLang props, 9 language files
check-xaml            OK: 42 XAML files well-formed
check-conf-lines      OK: 266 legal option names will be written
check-conflict-rules  OK: dependency set matches what the rules actually read
check-mpvconf-precedence OK: the option-name map matches ToCommand exactly
check-placeholders    OK: every non-path free-text setting shows an example
check-search-index    OK: covers label, description, mpv name, section, values
check-ui-tooltips     TOTAL=0
report-missing-options OK: every unexposed option is excluded or curated
check-settings-drift  7 ERROR  ← 改动前就是 7 条，未受影响
```

那 7 条 `check-settings-drift` 仍挂着，全部是
`AppSettings.Xxx has no MpvSettings.ToCommand mapping`，涉及的项
（`PlayerStyle`、`WindowPositionAndSize`、`Volume`、`EnableSaveBackup` 等）
本来就不该写进 mpv.conf。判定不变：归 B1 的 `owner` 字段解决，而 `owner` 属 D4 决策。

**改动仍未编译。** 本环境缺 VS 工具链，`mpv_winrt` C++/WinRT 组件被安全策略拦截，
所以这一轮 16 个文件的改动只有上述静态检查兜底。删除变量的引用核查是手工做的
（`grep` 全量扫残留 + 逐文件对比声明与使用次数），发现并清掉了 6 处死变量。
**「错误列表里没有我」不等于「我的代码通过」** —— 这次仍然适用。

---

# 附：本次调研的实测命令

```bash
# 分类/分区的引用与覆盖审计（注意 [A-Za-z0-9]+，否则在数字处截断）
grep -cE "public string Section[A-Za-z0-9]+ \{" mpv-winui/mpv-winui/Modules/Language/AppLang.cs
grep -cE "public string SectionDesc[A-Za-z0-9]+ \{" mpv-winui/mpv-winui/Modules/Language/AppLang.cs

# 本地化 / 设置漂移 / 界面文案自检
python tools/check-localization.py
python tools/check-settings-drift.py
python tools/check-ui-tooltips.py

# mpv 选项元数据（全量，机器可读）
mpv --list-options                      # 1272 行 / 870 独立选项
mpv --option=hwdec                      # 单选项的合法取值
mpv --list-properties

# 选项名前缀分布（分组依据）
mpv --list-options | grep -oE "^\s+--[a-z0-9-]+" | sed 's/^ *--//' \
  | grep -v -E -- "-(add|set|append|pre|clr|remove|del|toggle)$" \
  | sed 's/-.*//' | sort | uniq -c | sort -rn

# 间距节奏审计
grep -rhoE "(Margin|Padding|Spacing)=\"[^\"]*\"" \
  mpv-winui/mpv-winui/Modules/Settings/*.xaml \
  mpv-winui/mpv-winui/Modules/Settings/Controls/*.xaml | sort | uniq -c | sort -rn

# 分区表覆盖 + 图标重复审计（B3 之后）
# 见提交 93bf0b4 时使用的临时脚本，等价逻辑：
#   1. 从 AppLang.cs 取所有 Section*（排除 SectionDesc*）与 SectionDesc*
#   2. 从 SettingsSections.cs 的 All 表取 id / 图标 / 标题访问器 / 描述访问器
#   3. 断言两侧集合相等、id 无重复、描述访问器 == "SectionDesc" + id 去掉 "Section"
#   4. 按图标分组，列出被多个分区共用的字形
```
