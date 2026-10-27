# 设置界面契约

约束 `Domains/Settings`、`Domains/SettingsModel`、`Domains/MpvConf` 三者的配合。
数字全部可重算（`§7`）；每条规则的依据都指向代码。

---

## 1. 三层分工

| 层 | 目录 | 拥有什么 | 入口类型 |
|---|---|---|---|
| 界面 | `Domains/Settings/` | 设置树、行渲染、布局定制、依赖门控 | `SettingsPage : Page`、`SettingsWindow : BaseWindow`、`Option`、`OptionControlBase`、`OptionTemplateSelector` |
| 存储 | `Domains/SettingsModel/` | 设置值本身及其持久化 | `AppSettings`（12 个 partial）、`IDataSetting` + 3 个实现、`SettingValueCodec` |
| 配置 | `Domains/MpvConf/` | `mpv.conf` 的读写与归属判定 | `MpvSettings`、`ManagedMpvConfig`、`MpvConfOverrides`、`MpvConfWatcher`、`MpvConfValueCodec`、`MpvConfManager` |

### 1.1 边界（实测）

- `Settings → MpvConf` 与 `Settings → SettingsModel` 都是**单向**的。
- `MpvConf → SettingsModel` 单向；`MpvConf → Settings` **没有类型引用**。
  两个文件仍保留 `using mpv_winui.Domains.Settings;`（`MpvConfEditorPage.xaml.cs:9`、
  `MpvConfTextWindow.xaml.cs:9`），但文件内不引用该命名空间的任何类型——是残留 using，
  不是依赖。
- **例外**：`SettingsModel/AppSettings.cs:379` 直接读 `AppContext.AppSetting.CurrentLanguage`，
  这是存储层对组合根的反向引用。存储层除此之外不依赖任何域。
- `SettingsModel` 下**没有任何 XAML**：它不认识 UI。

### 1.2 谁写 `mpv.conf`

设置页**不直接写**。唯一入口是 `AppContext.WriteManagedMpvConfig()`
（`Shell/AppContext.cs:75-76`），设置页只调用（`SettingsPage.Actions.cs:233`）。
启动顺序固定为「先读文件 → 再写 → 再重算归属」：
`AppContext.cs:136`（`LoadFromConfig`）→ `:142`（`ManagedMpvConfig.WriteAsync`）→
`:146-148`（`MpvConfOverrides.Refresh`）。

---

## 2. 身份与分组

### 2.1 唯一身份表

`Domains/Settings/Layout/SettingsSections.cs` 是 category 与 section 的唯一来源。

| 结构 | 位置 | 条数 |
|---|---|---:|
| `Categories` | `SettingsSections.cs:56-64` | **6**：`program` / `video` / `audio` / `subtitles` / `playback` / `interface` |
| `All`（section 表） | `SettingsSections.cs:76-213` | **69** |
| `Section(Id, Icon, Caption, Description)` | `:31-35` | — |

section 的 `Id` **就是** `AppLang` 的属性名（类文档 `:10-25`）。例：
`new("SectionVideoGeometry", "\uE7C2", l => l.SectionVideoGeometry, l => l.SectionDescVideoGeometry)`。
标题与描述**显式成对写**，不做命名推导。

`Id` 不生成、不推导，是表里手写的第一个构造参数。查表方法：
`ForId`（`:279-295`）、`IdFor(caption)`（`:298`）、`CaptionFor(id)`（`:301`）、
`ForCaption`（`:228-248`）。缺省图标 `FallbackIcon = "\uE8B7"`（`:216`）。

### 2.2 四张表（都在 `SettingsPage.Options.cs`）

| 表 | 行号 | 条数 | 作用 |
|---|---|---:|---|
| `categoryOrder` | `:335` | 6 | 分类顺序 |
| `optionOrder` | `:345` | **323** | 行排序键 |
| `sectionOrder` | `:711` | **47** | 分区顺序 |
| `sectionMap` | `:812` | **325**（手写）+ 356（生成注册） | 每行属于哪个分区 |

**`sectionMap` 是分区的唯一来源。** 一行没有条目会让 `BuildSettings()` 抛
`InvalidOperationException`（`:1218-1223`），这是故意的 fail-loud。

解析链：`sectionMap` 的值是**本地化标题**变量（`AppContext.AppLang.SectionX`）→
`option.Section = section`（`:1229`）→ `option.SectionId = SettingsSections.IdFor(option.Section)`
（`:1235`）。所以改 `AppLang` 里某个 `Section*` 的**属性名**是改变分区 id，
改它的**中文字面量**不是。

### 2.3 设置 key 的三种合法拼法

| 拼法 | 实测数量 | 例 |
|---|---:|---|
| `Key = nameof(AppContext.AppSetting.X)` | **676** | `SettingsPage.Options.Gen.cs:34`、`SettingsPage.Options.Playback.cs:37` |
| `Key = nameof(AppSettings.X)` | **0** | 不要用这种 |
| 字符串常量 / 字面量 | **13** | `SettingsPage.Options.Program.cs:239`（常量）、`:264,277,290,314`（字面量）、`SettingsPage.Options.cs:1486`、`SettingsPage.Options.Layout.cs:489` |

常量可以声明在别的文件：`SettingsPage.Actions.cs:402`
`private const string FileAssociationCheckListKey = "FileAssociationCheckList";`

### 2.4 两种运行时键，`sectionMap` 刻意不覆盖

| 前缀 | 生成处 | 守卫 |
|---|---|---|
| `Shortcut:` | `SettingsPage.Options.cs:693-696`（`shortcutOrder = 1000`） | `SettingsPage.Options.cs:1216` 排除 |
| `custom-section:` | `SettingsPage.Options.Layout.cs:489` | 同上 |

### 2.5 生成的属性名与 mpv 选项名

- 生成的 `AppSettings` 属性是 `Mpv_<下划线化>`（`AppSettings.Gen.cs:13` 等），**356 个**。
- `MpvSettings.ToMpvOptionName(key)`（`MpvSettings.cs:601-608`）先查手写
  `MpvOptionNames`，再查生成 `GeneratedOptionNames`。
- 选项名必须匹配 `^[a-z0-9][a-z0-9-]*$`（`MpvSettings.cs:618-621`）。因此
  `user-data/mpvw/*` 这类运行时属性被拒于 `mpv.conf` 之外（`:611-616`）——它们
  不是 mpv 选项，写进配置会让 mpv 解析失败。

### 2.6 生成分区

由 `tools/gen-schema-options.py` 产出 `SettingsPage.Sections.Gen.cs`：

- id：`_subtheme_key(sub)` = `"SectionGen"` + 逐段 `title()` 拼接（`:879-891`），
  如 `video-output` → `SectionGenVideoOutput`。
- 归属：`folder_for`（`:894-917`）按 `SECTION_OVERRIDE` → 主题表 → `GROUP_TO_SECTION`
  三级路由；分类映射在 `GROUP_TO_CATEGORY`（`:94-104`）。
- 注册：`sectionOrder[lang.X] = 1000 + index`（22 条）、`advancedSections.Add(...)`、
  `sectionMap[nameof(AppContext.AppSetting.Mpv_…)]`（356 条）。

---

## 3. 界面的强制规则

### 3.1 两半树都要读

手写行在 `Options.<域>.cs`，生成行在 `Options.Gen.cs`（356 条）。任何枚举选项、
分区或 mpv 名的代码与检查器**必须读两半**。历史上 6 个检查器只读一半，报出数万条
幻影发现。

### 3.2 加一种 `OptionType` 要改三处

`OptionType`（11 个成员）→ `OptionTemplateSelector` 的 switch（`:39`）→
`OptionListControl` 的 `DataTemplate`。1:1:1，缺一处该类型的行不渲染。

### 3.3 折叠分区的三处同步

| 处 | 位置 |
|---|---|
| 行标志 `Option.AdvancedSection` | `Controls/Option.cs` |
| 页面的 `FoldedSections` | `SettingsPage.Options.cs:35`，赋值 `:1298` |
| `OptionListControl.CollapsedSectionIds` | `OptionListControl.xaml.cs:226-234`（DP），消费 `:512` |

最后一个是 **sticky** 依赖属性，所以每个给 `OptionList` 赋值的分支都必须清空它。
实测 **4 个分支**都做了：`SettingsPage.xaml.cs:1540`（搜索）、`:1588`（总览）、
`:1616`（下钻）、`:1637`（单分区兜底）。

### 3.4 两个下限

`FoldFloor = 4`（`SettingsPage.Options.cs:1325`）、
`CategoryFloor = 8`（`:1385`）。

窗口下限是 `PreferredMinimumWidth = 560`、`PreferredMinimumHeight = 560`
（`SettingsWindow.xaml.cs:81-82`）；宽度**刻意低于** `MpvNarrowLayoutThreshold = 720`，
否则窄布局永远不可达（理由注释 `:64-80`）。

### 3.5 生成物禁止手改

`SettingsPage.Options.Gen.cs`、`SettingsPage.Sections.Gen.cs`、
`AppSettings.Gen.cs`、`MpvSettings.Gen.cs`、`AppLang.Gen.cs`、
`AppLang.Sections.Gen.cs` 都带 `auto-generated` 横幅。改生成器或
`MpvConfOptions/mpv-options.json`，跑 `python tools/gen-schema-options.py --check`。

### 3.6 生成器不发射什么

`Options.Gen.cs` 里 `list.Add(new Option` 356 处、裸 `new Option` **0** 处、
`AdvancedSection` **0** 处。生成器侧对应 `gen-schema-options.py:866-874`。

---

## 4. 已知例外登记

逐条对当前代码核验。**「仍成立」= 旧说法与代码一致；「已失效」= 旧说法不再成立。**

### 4.1 仍然成立

| # | 内容 | 证据 |
|---|---|---|
| A-1 | `check-sections` 按括号配平读两半 | `tools/check-sections.py:179-193` |
| A-3 | 允许驱动应用、禁止遗留进程 | `AGENTS.md:77,81,84` |
| A-4 | 产物名 `mpvw.exe`/`mpvw.dll` | `tools/installer/product.wxs:70,101,106`、`build-installer.ps1:42` |
| A-5 | `check-settings-drift` 的 `UNMAPPED_OK` | `tools/check-settings-drift.py:87`；实测 exit 0 |
| A-6 | `compare-upstream.md` 的基线声明 | `docs/compare-upstream.md:3-11` |
| A-8 | `sectionOrder` 无重复条目 | 实测 47 条、重复 0 |
| A-9 | `ControlBarHiddenIcons` 无 `sectionMap`/`optionOrder` 条目，属性保留 | `SettingsPage.Options.cs:1101-1102`、`AppSettings.cs:569-572` |
| A-10 | `Option.cs` 无孤儿 XML 注释 | 全文 0 次 "Display tier" |
| A-12 | 样式词汇四角色统一（`MpvOptionLabel/Desc/Warning/ErrorStyle`） | 定义 `Styles.xaml:1102,1115,1144,1162`；3 个校验控件各 1 处引用 |
| A-15 | `OptionMultiListControl` 有 `WarningText`、无 `ErrorText` | `Controls/OptionMultiListControl.xaml:21-35`、`Controls/OptionMultiListControl.xaml.cs:66` |
| A-17 | `check-empty-sections` 读生成分区 | `tools/check-empty-sections.py:36,51,101-104`；实测 `EMPTY: none` |
| A-18 | `Speed` 有 `ToCommand` 分支、`Mpv_speed` 已消失 | `MpvSettings.cs:358,872,1095-1110` |
| A-19 | 8 个手写键已补 `optionOrder`（2001–2601） | `SettingsPage.Options.cs:444-449,476,689` |
| A-21 | `CustomizeStrings` 的键无 `AppLang` 属性，设计如此 | `CustomizeStrings.cs:7-23`；检查器按 `Customize*`+`Apply*` 归 expected |
| A-23 | `check-sections` 查 `sectionOrder` 完整性 | `tools/check-sections.py:526-552` |
| A-25 | `MpvConfWatcher` 保留（242 行），耦合已解 | `MpvConfWatcher.cs`；`MpvConfManager.cs:44-47` 接 `onBeforeSave` |
| A-26 | 转义损坏 896 MB 已修 | `MpvConfValueCodec.cs:46`(`Quote`)/`:71`(`Unquote`)；隔离文件 `_corrupt-20260920-150845\mpv.conf` = 939559174 B |
| A-28 | `mpv-conf-test` 能编译（`MpvConfManager` 不再依赖 Settings） | `MpvConfManager.cs:44-47,89`；`tests/conf/mpv-conf-test.csproj:3` |
| A-29 | 6 个设置能读回（`no`/`inf`/`MiB`） | `MpvConfValueCodec.cs:149,123-131,162-169,185-187` |
| A-30 | `build.ps1` 跑真测试 | `build.ps1:117-120,134` |
| A-31 | `deploy-test.sh` 有 GNU find 探测 | `tools/deploy-test.sh:36-50` |
| A-32 | 桌面 `mpvw-TEST.lnk` 仍不存在 | `Test-Path` = False |
| A-33 | `check-resources.py` 在仓库内且通过 | 实测 `OK: 182 resource key(s)` |
| EX-1 | `OptionCardStyle` 仍是 `OptionListControl.xaml` 局部样式 | `OptionListControl.xaml:39`，11 个模板引用 |
| EX-2 | 三处刻意保留：搜索胶囊、`Generic.xaml` 仅两条隐式样式、`LeftContentToggleSwitchStyle` | `Styles.xaml:1318-1347`、`Themes/Generic.xaml`（13 行）、`Styles.xaml:225` |
| EX-8 | `Shortcut:` 无静态行、`sectionMap` 不覆盖 | `SettingsPage.Options.cs:1216`、`:693-696` |
| EX-9 | 生成器不发裸 `new Option`、不发 `AdvancedSection` | 实测 0/0；`gen-schema-options.py:866-874` |
| EX-13 | 4 个分支各自清 `CollapsedSectionIds` | `SettingsPage.xaml.cs:1540,1588,1616,1637` |
| EX-14 | `MpvOptionControlGap = 24` 独立令牌 | `Styles.xaml:1189-1196`，20 处消费 |
| EX-15 | `.Gen.` 文件禁止手改 | 各文件首行 `auto-generated` |

### 4.2 已失效（旧说法不再成立，按现状记录）

| # | 旧说法 | 现在的实际 |
|---|---|---|
| A-2 | 已暴露 815 / 未暴露 1，`missing-options.md` 已重生成 | **`tools/report-missing-options.py` 现在 exit 1**：`:375` 报 `curated names that are not mpv options: ['cuda-decode-device']`（该名在 `:219`），而 `mpv-options.json` 里该选项 0 次。`docs/missing-options.md` 已删除。脚本本身不可用，它产出的任何数字都不成立 |
| A-7 | 至少改 `Options.Advanced.cs` 文件头注释 | 文件头**仍未加**（第 1 行是 `using Microsoft.UI.Xaml;`）。文件里 102 条行 |
| A-11 | `MpvSpaceL`/`MpvSpaceXl` 零消费者但保留 | `MpvSpaceXl` **已不存在**（全仓 0 次）；`MpvSpaceL` 有 **5 处**消费者（`MediaInfoUserControl.xaml:19`、`MpvPlayerPage.xaml:1116,1417,1994,2049`） |
| A-13 | 手写行与生成行缩进风格不同 | 缩进**已一致**（生成 `SettingsPage.Options.Gen.cs:32,34` 与手写 `SettingsPage.Options.Playback.cs:35` 同为 12/16 空格）。仍不同的是**语法形态**：生成用 `list.Add(new Option { … });`，手写是集合表达式元素 `new Option { … },` |
| A-16 | 549 生成行未本地化，9 文件各缺 1096 键，待修 | 生成行 **356**、`Schema*` 属性 **712**。`Schema*` 键数：**zh-CN 712**，其余 8 个语言文件 **0**。检查器已把这条降为 WARNING（`check-localization.py:209-219`），exit 0 |
| A-20 | `SectionDragSurface` 保留 | 该名**已不存在**；`HeaderGrid`/`CustomizeHost` 也已删。零引用 `x:Name` 的总数**无法确认**（无对应检查器） |
| A-22 | BOM 不统一，`MpvSettings.cs` 有 BOM | 实测两者**都无 BOM**（`git ls-files --eol` 均为 `i/lf w/crlf`）。旧规则说"工作区应为 LF"与实测**相反**：`core.autocrlf=true` 下工作区是 CRLF、索引是 LF |
| A-24 | 548 条 schema 生成选项，数字待订正 | 裁定成立（生成分区保留），但**陈旧数字仍在代码里**：`gen-schema-options.py:1014`、`SettingsSections.cs:134`、`MpvSettings.cs:360,361,486` 都写 548；`gen-schema-options.py:851` 写 549。实际 **356 行 / 712 键** |
| A-27 | 检查器无「写→读→再写」往返断言 | `Quote`/`Unquote` 往返**已有**（`MpvConfValueCodecTests.cs:59-61,93-122`）。仍缺的是 `ToConfLine` 的字节级往返：`ToConfLine` 在 `tests/conf/*.cs` 中 **0 次**命中 |
| A-33 附带 | 审计脚本 `audit-resource-keys.py` 可用 | 它硬编码旧根 `mpv-winui/mpv-winui`（`:26`），实测输出 `defined x:Key resources: 0`——静默测了个空目录，结果不可用 |
| A-34 | 缺口 746，默认口径红 | 缺口 **712**；默认九文件口径现在**绿**（降级为 WARNING），窄口径 `--languages=zh-CN,en-US` 也绿 |
| A-35 | 三个写入者写 `hdr-reference-white`/`hdr-peak-percentile` | 配置层（`profiles.conf`）**已移除**这两个键。仍是**两个**写入者：`MpvPlayerPage_Display.cs:443-449` 与设置行 `MpvSettings.Gen.cs:131-132`（经 `SettingsPage.Options.Gen.cs:47`、`:60`）。落盘证据 `%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf` 第 421-422 行（部署后的文件，不是仓库内 `data/mpv.conf`） |
| EX-3 | 间距令牌化率 74/109 = 67.9%，25 个非零字面量 | 口径不同，不可直接比较。本次统计 `Domains/Settings/**/*.xaml` 的 120 处间距值：`{StaticResource}` **83**、字面 `0` **8**、其它字面量 **35**。令牌化率约 **69%** |
| EX-4 | `ColumnSpacing="18"`、`Margin="0,10,0,0"` 待决 | 前者已升格为令牌 `MpvSectionCardColumnSpacing`（`Styles.xaml:1268`）；后者全仓 **0** 次。剩下的是重复字面量 `Margin="0,12,0,0"` × 3（`OptionCheckListControl.xaml:54`、`OptionColorControl.xaml:60`、`OptionLayoutControl.xaml:26`） |
| EX-5 | `MpvOptionErrorStyle` 不存在 | 已定义（`Styles.xaml:1162`），3 处消费 |
| EX-6 | `Padding="16,16,16,16"` 未接令牌 | 已用 `Padding="{StaticResource MpvCardPadding}"`（`OptionListControl.xaml:366`） |
| EX-7 | 17 个检查器（15 通过 / 2 失败） | 现在 **25** 个。本轮逐条实跑 25 个，**全部 exit 0** |
| EX-10 | 59 分区 = 59 个互不相同图标 | **69** 分区、**68** 个不同图标、**1 处共享**（`\uE8B7`：`SettingsSections.cs:87` 与 `:91`）。检查器报 WARNING 而非失败 |
| EX-11 | 顶层 9 个分类 | **6** 个（见 §2.1） |
| EX-12 | 窗口下限 720×560 | `PreferredMinimumWidth = 560`、`PreferredMinimumHeight = 560`（`SettingsWindow.xaml.cs:81-82`）。`CategoryFloor = 8` 未随之改 |
| EX-16 | 四张表条数为 299/46/307 等旧值 | `optionOrder` **323**、`sectionOrder` **47**、`sectionMap` 手写 **325** |

---

## 5. 一处改动的验证流程

改设置项的最小闭环。逐条说明见 [`verification.md`](verification.md)，**不要只跑这些**：

```powershell
python tools/gen-schema-options.py --check     # 改了生成层才需要
python tools/check-settings-drift.py           # AppSettings ↔ ToCommand ↔ 选项 key
python tools/check-conf-lines.py               # mpv 选项名合法
python tools/check-mpvconf-precedence.py       # 映射双向一致
python tools/check-sections.py                 # 分区表 ↔ AppLang
python tools/check-empty-sections.py           # 空分区
python tools/check-localization.py             # 文案 × 9 语言
python tools/check-xaml.py                     # XAML 良构
python tools/check-resources.py                # {StaticResource} 都能解析
python tools/check-scroll-maxwidth.py          # 滚动内容列
.\build.ps1 -Configuration Release -Platform x64   # 大范围改动后
```

**其中 4 个不能作为证据**：`check-icon-standard.py` 与 `check-typography-standard.py`
的扫描根仍写 `("Modules","Shell")`（`Modules` 已不存在），39 个 XAML 只看 1 个；
`check-empty-sections.py:152` 与 `check-placeholders.py:94` 恒 `return 0`。
另 `check-baseline-lines.py` 现在就有 1 条 stale（`icon-standard-baseline.txt:43`）却 exit 0。
详见 `verification.md` §4。

---

## 6. 本次实测发现的不一致

| 问题 | 事实 |
|---|---|
| 两个检查器对同一属性集给出不同的数 | `check-settings-drift.py` 报 695、`check-mpvconf-precedence.py` 报 696。同一目录、正则不同，差的那个属性未查明 |
| 代码注释里的陈旧数字 | `548`/`549` 出现在 5 处，实际 **356** |
| `report-missing-options.py` 不可用 | exit 1，`cuda-decode-device` 不在 schema 里 |
| 文档引用已删文件 | `SettingsSections.cs:73` 引用 `docs/settings-ui-review.md`；`AGENTS.md` 引用 `docs/prompt-v2/05-MODULE-DESIGN.md` |

## 7. 重算本文每个数字

```powershell
python tools/check-sections.py                 # 69 sections, 6 categories, 68 distinct icons, 1 shared

python -c "
import re
t=open('src/mpvw-app/Domains/Settings/SettingsPage.Options.cs',encoding='utf-8-sig').read()
for name in ['optionOrder','sectionOrder','sectionMap']:
    m=re.search(r'var '+name+r' = new Dictionary<[^>]+>\(StringComparer\.Ordinal\)\s*\{(.*?)\n\s*\};', t, re.S)
    print(name, len(re.findall(r'\[', m.group(1))))
"                                              # optionOrder 323 / sectionOrder 47 / sectionMap 325

$d = 'src/mpvw-app/Domains/Settings'
"nameof(AppContext.AppSetting) : " + ((Select-String -Path (Get-ChildItem $d -Recurse -Filter *.cs).FullName -Pattern 'Key = nameof\(AppContext\.AppSetting\.' -AllMatches | % { $_.Matches.Count } | Measure-Object -Sum).Sum)   # 676
"nameof(AppSettings)           : " + ((Select-String -Path (Get-ChildItem $d -Recurse -Filter *.cs).FullName -Pattern 'Key = nameof\(AppSettings\.' -AllMatches | % { $_.Matches.Count } | Measure-Object -Sum).Sum)   # 0

(Select-String -Path src/mpvw-app/Domains/Settings/SettingsPage.Options.Gen.cs -Pattern 'list\.Add\(new Option').Count   # 356

python tools/check-settings-drift.py      | Select-String 'AppSettings props'        # 695
python tools/check-mpvconf-precedence.py  | Select-String 'AppSettings properties'   # 696

python -c "import json,glob,os; [print(os.path.basename(f), sum(1 for k in json.load(open(f,encoding='utf-8-sig')) if k.startswith('Schema'))) for f in sorted(glob.glob('src/mpvw-app/Languages/*.json'))]"
# zh-CN 712，其余 8 个 0

Select-String -Path tools/gen-schema-options.py,src/mpvw-app/Domains/Settings/Layout/SettingsSections.cs,`
  src/mpvw-app/Domains/MpvConf/MpvSettings.cs -Pattern '\b(548|549)\b'   # 5 处
```

## 8. 原因不明

- **695 vs 696**：两个检查器对同一目录给不同的属性数，差的那一个属性是哪个、
  谁的口径正确，读了两个正则仍不能确定（`check-mpvconf-precedence.py` 的
  `PROP_BLOCK`/`PROP_INLINE` 定义未逐条比对过全部 696 个名字）。
- **`CategoryFloor = 8` 与窗口宽度**：旧文档说改高度 560 必须重算这个下限，
  但代码里两者没有可见的推导关系，也未找到记录该推导的注释。
