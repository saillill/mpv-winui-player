# 07 · 排版 / 图标 / 间距：工具化检测线索

> **现状（实测）**
> - 已有 **30 个自建检查器**（`tools/check-*.py`），但 `build.ps1` 只跑 4 个、CI 跑 8 个
> - 已引用 **`Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`**（`mpv-winui.csproj:81`），
>   内置 **18 条规则**（DLL 内实测提取：`WUI0001-0004`、`WUI1001-1010`、`WUI2001-2020`、
>   `WUI3001`、`WUI4001-4102`）
> - **WinUIAnalyzer 已验证在跑，规则会红**（注入真实违规实测，见 §2.2）；两条实测盲区也记在那里
> - **没有 XamlStyler 配置**（`src/` 下无 `*.xamlstyler`）
> - 用户的约束：**先搜现成方案，不要自研脚本**

---

## 一、能自动检测 vs 不能（先说结论）

| 问题类别 | 能否自动检测 | 用什么 | 现状 |
|---|---|---|---|
| **`x:Bind` 未写 `Mode`** | **能** | WinUIAnalyzer `WUI2011` | **已验证会红**（注入真实违规）。全仓 156 条 `{x:Bind}`：139 条带路径、**0 条缺 `Mode`**，17 条是裸 `{x:Bind}` 被规则豁免 |
| 嵌套 `x:Bind` 缺 `FallbackValue` | 能 | `WUI2010` | **已验证会红**，但门槛是**三段及以上**路径。本仓库 26 条嵌套绑定全是两段或函数绑定，0 条够得着门槛 |
| `Converter={x:Null}` | 能 | `WUI2012` | 运行时崩溃；**已验证会红**，本仓库 0 处 |
| **XAML 格式（属性顺序、缩进、换行）** | **能** | **XamlStyler** | **未引入** |
| **C# 格式** | 能 | `dotnet format` | 未知是否在用 |
| **间距/字号令牌违规** | 能（已有） | `check-typography-standard.py` | 已绿，13 条已知豁免 |
| **图标 codepoint 与字体不匹配** | 能（已有） | `check-icon-standard.py` | 已绿 |
| **三点菜单 glyph 正确性** | 能（已有） | `check-more-glyph.py` | 已绿，2 处 |
| **无障碍名与工具提示** | 能（已有） | `check-ui-tooltips.py` | 已绿，TOTAL=0 |
| **VisualState 负 margin / 改对齐** | 能（已有） | `check-visual-state-layout.py` | 已绿，147 个 setter |
| **动画缺失** | **不能** | — | 需人工或录制对比 |
| **视觉层次、配色协调、间距"节奏"** | **不能** | — | 需人工 |
| **图标语义是否恰当** | **不能**（只能查字形存在） | — | 需人工 |

**关键判断**：本仓库的自建检查器已经覆盖了相当多"能自动检测"的类别。
真正缺的是 **XAML 格式统一**（XamlStyler）。WinUIAnalyzer 已经在跑且会红（§2.2），
不再是缺口；它的两条盲区见同一节。

---

## 二、线索清单

### 2.1 XamlStyler（格式统一）

| 项 | 内容 |
|---|---|
| 来源 | [Xavalon/XamlStyler](https://github.com/Xavalon/XamlStyler) · [VS 扩展页](https://marketplace.visualstudio.com/items?itemName=PhoenixW.UI.XamlStyler) |
| 技术栈 | .NET 工具；支持 WPF / UWP / **WinUI 3** / MAUI / Avalonia |
| CLI | `dotnet tool install -g XamlStyler.Console`，命令 `dotnet xstyler -d <dir> -r` |
| 可复用 | 属性排序、缩进、换行、空行规则；配置文件 `Settings.XamlStyler` |
| 现成配置可抄 | [Uno 的 `src/SamplesApp/Settings.XamlStyler`](https://github.com/unoplatform/uno/blob/master/doc/articles/contributing/guidelines/code-style.md)；[CommunityToolkit 的 `settings.xamlstyler`](https://github.com/CommunityToolkit/Windows)；[Rhythm 的 `Settings.XamlStyler`](https://github.com/FallenDeity/Rhythm) |
| **CI 集成范例** | [OpenConsole 的 #9589](https://github.com/zlaski/OpenConsole/commit/3323dc572419c3ab64feba903d723dcb78225d06)：加了 `xstyler` + `Verify-XamlFormat`（**只校验不改文件**），并**特意不把 XAML formatter 放进代码格式步骤**（理由：即使没改动也会 touch 所有 .xaml） |

**对本项目的风险**：本仓库刚因"工具改 BOM/CRLF"踩过坑（`tools/fix-file-encoding.ps1` 就是为此写的）。
XamlStyler 首次格式化会重写**全部 39 个 .xaml**，必须先在独立分支验证 BOM 与换行不被破坏。

### 2.2 WinUI Analyzer（绑定与陷阱）

| 项 | 内容 |
|---|---|
| 已装 | `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`（`mpv-winui.csproj:81`） |
| 规则集 | `WUI0xxx` UWP→WinUI3 API、`WUI1xxx` 迁移表、**`WUI2xxx` 运行时/布局/XAML 陷阱**、`WUI3xxx` MVVM、`WUI4xxx` 互操作 |
| `WUI2xxx` 明细 | `WUI2001` TabView 原始内容、`WUI2002` TabView 代码后置内容、`WUI2010` 嵌套 x:Bind 缺 Fallback、**`WUI2011` x:Bind 无 Mode**、`WUI2012` `Converter={x:Null}`、`WUI2020`（未提取到描述） |
| **自停用契约** | `.targets` 里有 `WindowsAppSDKAnalyzersStandDownIfSuperseded`：当 `WindowsAppSDKProvidesWinUIAnalyzer=true` 时**自动移除自身**，让 WASDK 自带的副本接管 |
| **实测结论** | **在跑，未被 stand-down。** 构建命令行含 88 个 `/analyzer:`，其中一个是 `...\winuianalyzer\0.7.1\analyzers\dotnet\cs\Microsoft.WindowsAppSDK.Analyzers.dll`；39 个源 `.xaml` 全部作为 `/additionalfile:` 注入；构建日志里没有 `AD0001`（分析器崩溃）也没有 `CS8032`（加载失败） |
| **契约为何没触发** | WASDK 2.5.1 的四个 `.targets` 副本里**没有一处**设置 `WindowsAppSDKProvidesWinUIAnalyzer`；`dotnet msbuild -getProperty:WindowsAppSDKProvidesWinUIAnalyzer` 读出空串。stand-down 目标的条件是 `=='true'`，不成立 |
| **判定方法（可重跑）** | 注入而不是推断：把 `MediaInfoUserControl.xaml` 里一处 `Mode=OneTime` 删掉，构建立刻报 `warning WUI2011`；还原即消失。`WUI2012` 用 `Converter={x:Null}` 同样复现 |
| **盲区一：裸 `{x:Bind}`** | 不触发 `WUI2011`。本仓库 17 处都是这种写法（绑定数据项本身、交给代码后置），`OneTime` 是正确语义，豁免也对 |
| **盲区二：带 `Converter=`** | 同样不触发 `WUI2011`。实测 `Opacity="{x:Bind O, Converter=...}"` 不报，去掉 `Converter=` 就报。本仓库 0 处命中，但这是规则真实的空洞 |
| **报的是元素起始行** | 不是属性所在行。第 28 行的属性报成 `(24,18)`——第 24 行是那个 `<TextBlock`。排查时按行号跳转会落到别的控件上 |

### 2.3 现成"WinUI 打磨"清单（非工具，但可直接当检查项）

| 线索 | 来源 | 内容 |
|---|---|---|
| **winui-polish** | [skills.rest](https://skills.rest/skill/winui-polish) | 系统化审计 **materials / theming / spacing / typography / iconography** 五类视觉缺陷的清单 |
| **dotnet-winui-maintenance** | [skills.rest](https://skills.rest/skill/dotnet-winui-maintenance) | 用 `dotnet format` + 包健康检查维护 WinUI 代码库 |
| **winui-design 的 4px 网格判据** | [SkillsMP](https://skillsmp.com/creators/jorgeasaurus/agent-skills/winui-design) | 「margin / padding / size 必须是 4 的倍数」——**这是可以机械检查的规则**，本仓库目前没有对应检查器 |

### 2.4 现有 30 个检查器里与本项直接相关的

| 检查器 | 管什么 |
|---|---|
| `check-typography-standard.py` | 间距/字号字面量 ↔ `Styles.xaml` 令牌（13 条已知豁免） |
| `check-icon-standard.py` | `FontIcon` codepoint ↔ 声明字体 |
| `check-more-glyph.py` | `MoreButton` 的 Glyph ↔ `&#xE712;` |
| `check-ui-tooltips.py` | 交互控件可访问名 + 工具提示 |
| `check-visual-state-layout.py` | VisualState 负 margin / 改对齐 |
| `check-scroll-maxwidth.py` | 可横向滚动容器的子元素 MaxWidth |
| `check-resources.py` | `{StaticResource}` key 是否存在 |
| `check-placeholders.py` / `check-empty-sections.py` / `check-sections.py` | 设置页结构一致性 |

---

## 三、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| **XamlStyler** | **高**（仅被动校验） | **低**（`xstyler -p` 不写文件） | **已实测**：`-p` 模式 78 个文件里 4 个合规、0 文件被改动；主动格式化会**给 32 个文件加 BOM**并重写约 4400 行。见 §3.1 |
| **WinUIAnalyzer** | **高** | **已完成**（不需要改动） | 已在跑、会红。别为了让它"更灵"去改 `WindowsAppSDKProvidesWinUIAnalyzer`：现在它没被设置，动它只会引入第二个分析器副本或把它关掉 |
| **4px 网格规则** | **不推荐** | 低（但会红一片） | **已实测**：仓库的间距刻度是 **2px 基准**（GCD=2），`MpvSpaceCompact`=6 是正式令牌。4px 规则会判定设计系统本身违规。见 §3.2 |
| **winui-polish 清单** | 中 | 低（人工清单） | 非工具，是审计表；适合作为人工走查的 checklist |
| `dotnet format` | 中 | 低 | 只管 C#，与 XAML 无关 |
| 动画缺失检测 | **不可行** | — | 没有成熟工具；只能录制对比或人工 |

### 3.1 XamlStyler：只做校验，不要做格式化（实测）

装了 `XamlStyler.Console 3.2501.8` 在 39 个源 `.xaml` 上实测：

| 项 | 被动校验 `-p` | 主动格式化 |
|---|---|---|
| 通过的文件 | **4 / 78** | — |
| 改动的文件 | **0**（逐字节 sha256 比对） | **35 / 39** |
| BOM | 不变 | **给 32 个文件加上 BOM**（原来只有 3 个有） |
| 换行 | 不变 | 保持 CRLF，无混合 |
| 代码行变化 | 0 | **+4420 / −4677** |
| 工具幂等性 | — | **不幂等**：第二遍又改 1 个文件，第三遍才稳定 |

格式化后 `build.ps1` 全套通过（527 + 26 测试，警告仍是 2 条 `WMC1510`），
所以**功能上安全**。但代价是 32 个文件凭空多出 BOM——本仓库 `AGENTS.md`
专门警告过 BOM 方向的两个坑，而 `git diff` 不显示它。

**最要命的一条**：`tools/check-typography-standard.py` 的基线是**按行号**匹配的
（`path:line:kind`）。格式化把 13 条基线全部作废，检查器立刻报 "12 NEW violations"——
但独立复算（同一条规则，不查基线）证明 HEAD 与格式化后**都是 12 条**，缺陷数没变。
行号一移，整个基线文件失效。

> 结论：用 `xstyler -p` 做 CI 校验可以，但**首次接入前必须先统一格式**，
> 否则第一天的 CI 就是红的，而红的原因不是新缺陷。

### 3.2 4px 网格：这条规则会跟设计系统打架（实测）

`Styles.xaml` 里 63 个数值令牌（39 `x:Double` + 24 `Thickness`），展开共 123 个数：

| 分类 | 不在 4 倍数上的值 |
|---|---|
| layout（间距/尺寸） | **13 / 106** |
| icon（图标尺寸） | 1 / 10（`MpvInlineIconSize`=14） |
| type（字号） | 2 / 7（14、11，WinUI 字号阶梯本就不是 4 的倍数） |

**根因不是"有些值没对齐"，是刻度基准不同**：`MpvSpaceXxs 2 / Xs 4 / Compact 6 /
S 8 / M 12 / L 16`，最大公约数是 **2**。`6` 是正式令牌（`MpvSpaceCompact`），
在 XAML 里出现 19 次，是最高频的"违规"值。

内联字面量侧：39 个文件、788 个数值，**67 个（9%）** 不在 4 的倍数上。
最高频同样是 6（19 次）、10（7 次）、7（7 次）。

> 结论：**不要加 4px 规则**。要加就加 **2px** 规则（令牌刻度实际就是它），
> 或者先把 `MpvSpaceCompact` 从设计系统里拿掉——但那是产品决策，不是 lint 决策。

### 3.3 XamlStyler 的三个可选接入点

按接入成本从低到高，每档都写清了代价：

| 方案 | 做什么 | 代价 |
|---|---|---|
| **只加被动校验到 CI** | `xstyler -d src/mpvw-app -r -p` | **接入即红**（74/78 不合规）。要么先允许失败，要么先格式化 |
| **先格式化一次，再守** | 格式化 → 提交 → CI 加 `-p` | +4400 行 diff、32 个 BOM、13 条 typography 基线作废需重生成 |
| **只格式化新文件** | `-f` 指定文件 | 手工维护清单，且和"统一格式"的目标相悖 |

`-p` 已经是纯只读（逐字节验证过 0 改动），CI 用它比用格式化安全。
但**BOM 问题只有主动格式化才有**，所以纯校验路线避开了这个坑。

### 已识别的取舍点

1. **先加还是先验证**：`WUI2xxx` 为何没触发已经查清（§2.2）。前置条件解除，可以谈新增工具
2. **XamlStyler 加"格式化"还是只加"校验"**：都已实测（§3.1）。校验是只读的、安全的，
   但今天就是红的；格式化功能安全却带 32 个 BOM 和 4400 行 diff
3. **4px 网格**：已实测（§3.2）。**不建议加**，刻度基准是 2px
4. **30 个检查器只有 8 个在链路上**：**已实测**——22 个未接入的全部 exit 0，
   合计 72.4 秒，其中 `check-build-warnings.py` 占 54.9 秒。见 §3.4

### 3.4 把未接入的检查器接进链路（实测成本与信号）

22 个未接入的检查器，在干净树上全部 exit 0。成本分布：

| 检查器 | 耗时 | 备注 |
|---|---|---|
| `check-build-warnings.py` | **54.9 s** | 唯一昂贵的一个：它会**重新构建**；`WUI2003 = 0` 全靠它守 |
| `check-doc-ranges.py` | 10.0 s | 扫全部 `docs/*.md` + 源码块结构 |
| `check-doc-references.py` | 3.9 s | 166 条引用 |
| 其余 19 个 | **合计 < 3 s** | 最慢 0.7 s（`check-no-pivot`） |

按 AGENTS.md「先看它红」的规矩，逐个注入**真实**违规验证：

| 检查器 | 注入的违规 | 结果 |
|---|---|---|
| `check-no-pivot` | 在 SelectorBar 前加回一个 `<Pivot>` | **变红**（2 条，exit 1） |
| `check-typography-standard` | `Spacing="{StaticResource MpvSpaceS}"` → `Spacing="8"` | **变红** |
| `check-resources` | `{StaticResource NoSuchResourceKey}` | **变红** |
| `check-scroll-maxwidth` | `ScrollViewer > Grid MaxWidth="1060"` | **变红**（缺 `HorizontalScrollMode="Disabled"` 才报，符合设计） |
| `check-more-glyph` | 真实 `MoreButton` 的 `Glyph` 改成 `&#xE10C;` | **变红** |
| `check-sections` | 改名一个 `AppLang.Sections.Gen.cs` 的属性 | **变红**（exit 1） |

> 结论：**19 个总耗时不到 3 秒、已验证能变红的检查器，没有理由不进 `build.ps1`。**
> `check-build-warnings.py` 单独处理——它重复构建一次，应该由 CI 跑而不是本地每次 build。
> `check-doc-ranges` / `check-doc-references` 属文档类，放 CI 更合适。
>
> 特别注意：`check-no-pivot.py` 目前唯一的防线是它自己。`WUI2003` 只扫 XAML，
> `PlayerControl.ControlPanel.cs` 里那个运行时 `Pivot` 一条分析器警告都不产生
> （`docs/verification.md` §4.7 已记录）。它耗时 0.7 秒。

---

## 四、检索提示词

### 4.1 搜索式

```
XamlStyler Console dotnet tool WinUI 3 configuration
"Settings.XamlStyler" example properties order
XamlStyler CI verify format without modifying
WinUI 3 analyzer WUI2011 x:Bind without Mode
WindowsAppSDKProvidesWinUIAnalyzer stand down
WinUI 3 4px grid spacing guideline
dotnet format XAML Uno WinUI
```

### 4.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`），有 **30 个自建检查器**，
> 但 `build.ps1` 只跑 4 个、CI 跑 8 个。项目已引用
> `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`，内置 18 条规则，
> 其中 `WUI2011`（x:Bind 无 Mode）、`WUI2010`（嵌套 x:Bind 缺 Fallback）、
> `WUI2012`（Converter={x:Null}）属于"布局/XAML 陷阱"类别。
>
> **用户明确要求：先搜现成方案，不要自研脚本。**
>
> `WUI2xxx` **已经实测确认在跑**，不要再查一遍：分析器在 `/analyzer:` 里，XAML 在
> `/additionalfile:` 里，注入违规会报；WASDK 2.5.1 不设 `WindowsAppSDKProvidesWinUIAnalyzer`，
> stand-down 不触发。细节见本文 §2.2。
>
> 请先回答，不要写代码：
> 1. 引入 XamlStyler 时，应只做"校验"（`Verify-XamlFormat`）还是也做"格式化"？
>    本仓库有 `* text=auto eol=crlf` 的 .gitattributes，且刚因工具改 BOM/CRLF 踩过坑。
> 2. "margin/padding/size 是 4 的倍数"这条规则，现有 `Styles.xaml` 令牌满足吗？
>    请逐个核对，给出违例清单。
> 3. 30 个检查器只有 8 个在链路上。新增检查器若仍不进 CI，意义何在？应该先补链路还是先加检查？
>
> 参考现成配置：CommunityToolkit 的 `settings.xamlstyler`、Uno 的 `Settings.XamlStyler`、
> OpenConsole 的 CI 集成（#9589）。参考清单：skills.rest 的 `winui-polish` 审计项。

### 4.3 深挖方向

- `WUI2011` 对带 `Converter=` 的绑定失明（§2.2 盲区二）：这算规则的 bug 还是设计取舍，值得查一次上游
- 把 `Styles.xaml` 全部 `x:Double` / `Thickness` 令牌导出，按 4 的倍数核对
- 查 `docs/verification.md` 的"两边都不跑"清单，评估补链路的成本
