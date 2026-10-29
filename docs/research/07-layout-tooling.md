# 07 · 排版 / 图标 / 间距：工具化检测线索

> **现状（实测）**
> - 已有 **30 个自建检查器**（`tools/check-*.py`），但 `build.ps1` 只跑 4 个、CI 跑 8 个
> - 已引用 **`Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`**（`mpv-winui.csproj:81`），
>   内置 **18 条规则**（DLL 内实测提取：`WUI0001-0004`、`WUI1001-1010`、`WUI2001-2020`、
>   `WUI3001`、`WUI4001-4102`）
> - **没有 XamlStyler 配置**（`src/` 下无 `*.xamlstyler`）
> - 用户的约束：**先搜现成方案，不要自研脚本**

---

## 一、能自动检测 vs 不能（先说结论）

| 问题类别 | 能否自动检测 | 用什么 | 现状 |
|---|---|---|---|
| **`x:Bind` 未写 `Mode`** | **能** | WinUIAnalyzer `WUI2011` | **规则已在项目里，但实测未触发**（全仓 139 条 `x:Bind`，0 条缺 `Mode`，所以无告警是**正确**的） |
| 嵌套 `x:Bind` 缺 `FallbackValue` | 能 | `WUI2010` | 同上，未触发 |
| `Converter={x:Null}` | 能 | `WUI2012` | 运行时崩溃，规则已装 |
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
真正缺的是 **XAML 格式统一**（XamlStyler）和 **WinUIAnalyzer 是否真的在跑**（需验证）。

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
| **需验证** | 本项目是否因该契约而实际未运行？`grep WUI2003` 只在 `build-warning-budget.txt` 出现，说明规则**曾**生效 |

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
| **XamlStyler** | **高** | **中**（首次全量格式化 39 个文件 + CI 接入） | 会重写全部 XAML，**必须先验证 BOM/CRLF**（本仓库 `* text=auto eol=crlf`）；建议先只加"校验"不加"格式化"（照 OpenConsole 的做法） |
| **WinUIAnalyzer（验证是否在跑）** | **高** | **低**（一次构建排查） | 若被 stand-down，需显式设 `WindowsAppSDKProvidesWinUIAnalyzer=false` 或改用 WASDK 自带副本 |
| **4px 网格规则** | **高** | **低**（可作为新检查器，或并入 `check-typography-standard.py`） | 需要先确认现有令牌是否都是 4 的倍数，否则一开就红一片 |
| **winui-polish 清单** | 中 | 低（人工清单） | 非工具，是审计表；适合作为人工走查的 checklist |
| `dotnet format` | 中 | 低 | 只管 C#，与 XAML 无关 |
| 动画缺失检测 | **不可行** | — | 没有成熟工具；只能录制对比或人工 |

### 已识别的取舍点

1. **先加还是先验证**：应先查清 `WUI2xxx` 为何没触发，再谈新增工具
2. **XamlStyler 加"格式化"还是只加"校验"**：前者一次性大 diff，后者可增量
3. **4px 网格**：现有令牌（`MpvSpaceS`=8、`MpvPopupIconSize`=16 等）已知部分是 4 的倍数，
   但 `MpvPlaylistPanelWidth`=320、`MpvPopupRowMinHeight`=36、`MpvPopupContentPadding`=12,4,12,4
   需逐个核对
4. **30 个检查器只有 8 个在链路上**：新增检查器若仍不进 CI，等于没有

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
> 请先回答，不要写代码：
> 1. `WUI2xxx` 那几条规则在本项目**实际触发过吗**？如何验证（不是看配置，是看构建输出）？
>    `.targets` 里有 `WindowsAppSDKProvidesWinUIAnalyzer=true` 时的自动 stand-down 契约，
>    本项目的 WASDK 2.5.1 是否会设置它？
> 2. 引入 XamlStyler 时，应只做"校验"（`Verify-XamlFormat`）还是也做"格式化"？
>    本仓库有 `* text=auto eol=crlf` 的 .gitattributes，且刚因工具改 BOM/CRLF 踩过坑。
> 3. "margin/padding/size 是 4 的倍数"这条规则，现有 `Styles.xaml` 令牌满足吗？
>    请逐个核对，给出违例清单。
> 4. 30 个检查器只有 8 个在链路上。新增检查器若仍不进 CI，意义何在？应该先补链路还是先加检查？
>
> 参考现成配置：CommunityToolkit 的 `settings.xamlstyler`、Uno 的 `Settings.XamlStyler`、
> OpenConsole 的 CI 集成（#9589）。参考清单：skills.rest 的 `winui-polish` 审计项。

### 4.3 深挖方向

- 跑一次带 `-v:n` 的构建，grep 全部 `WUI\d{4}`，确认哪些规则真的活着
- 读 `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer.targets` 的 stand-down 条件
- 把 `Styles.xaml` 全部 `x:Double` / `Thickness` 令牌导出，按 4 的倍数核对
- 查 `docs/verification.md` 的"两边都不跑"清单，评估补链路的成本
