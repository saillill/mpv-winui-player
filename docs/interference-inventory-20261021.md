# mpvw 干扰项清单（带证据）

**产出日期**：2026-10-21（编号按任务约定）
**基线**：`d2330fd`（本次扫描开始时 HEAD 为 `7b42fd5`，扫描中途仓库被其他并发会话推进到 `d2330fd`，全部结论已在 `d2330fd` 上重跑复核）
**已跟踪文件**：604（任务描述给的是 581，指的是旧基线 `aab1a9f`/`7b42fd5` 时期的数字）
**性质**：只读审计。**没有改动任何代码，没有删除任何文件，没有执行任何 git 写操作，没有启动 `mpvw.exe`。**
**产物**：全部扫描脚本在 `_work/scan/`（该目录已被 `.gitignore:386` 忽略，未提交），输出在 `_work/scan/out/`。

> **本文档的自我限制**：`## 4 删除` 里每一条都给到「零引用」的可复现命令。凡我拿不出零引用证据的，一律放进 `## 3 待确认`，不放进 `## 4`。

---

## 0 方法与命令

### 0.1 环境口径（先对齐，因为任务描述里的三个数字与当前 HEAD 不一致）

| 指标 | 任务描述 | 当前实测 | 命令 |
|---|---:|---:|---|
| 已跟踪文件 | 581 | **605** | `git ls-files` |
| `.cs`（应用工程，排除 `bin`/`obj`） | 329 | **241** | 见下 |
| `.xaml`（应用工程，排除 `bin`/`obj`） | 80 | **40** | 见下 |
| `tools/` 下文件 | 35（19 个 `check-*.py`） | **37**（**17** 个 `check-*.py`） | 见下 |

329 / 80 是**包含 `bin`、`obj`、`AppPackages` 的全量**，是实测可复现的另一个数：

```powershell
(Get-ChildItem mpv-winui\mpv-winui -Recurse -File -Filter *.cs).Count      # 329  ← 含 obj 生成的 88 个
(Get-ChildItem mpv-winui\mpv-winui -Recurse -File -Filter *.xaml).Count    # 80   ← 含 obj 下 .xbf 同名的 40 个副本
git ls-files 'mpv-winui/mpv-winui/*.cs'   | Measure-Object | % Count       # 241  ← 真正的源文件
git ls-files 'mpv-winui/mpv-winui/*.xaml' | Measure-Object | % Count       # 40
```

即：**329 = 241 源 + 88 `obj\` 生成；80 = 40 源 + 40 `obj\` 副本**。本清单全部按**源文件**统计（241 / 40）。

`tools/` 的 35 / 19 与任何基线都对不上，逐基线实测：

```powershell
foreach($r in @('aab1a9f','7b42fd5','d2330fd','5f43522')){
  "{0}: tools={1} check={2}" -f $r,
    (git ls-tree -r --name-only $r -- tools | ? { $_ -match '^tools/[^/]+$' }).Count,
    (git ls-tree -r --name-only $r -- tools | ? { $_ -match '/check-.*\.py$' }).Count
}
```

| 基线 | `tools/` 顶层文件 | `tools/` 递归（含 `mvpw-mcp/`） | `check-*.py` |
|---|---:|---:|---:|
| `aab1a9f`（旧清单的基线） | **35** | 35 | **15** |
| `7b42fd5`（本轮扫描起点） | 36 | 36 | **17** |
| `d2330fd` | **37** | 41 | **17** |
| `5f43522`（当前 HEAD） | **37** | 42 | **17** |

`35` 只对得上 `aab1a9f` 的**顶层文件数**，而那个基线的 check 脚本是 **15** 不是 19。**19 在任何基线上都测不出来**（最多 17）。所以任务描述的「35 个 / 19 个」混合了两个不同基线的口径；本清单以 `5f43522` 的 **37 顶层 / 42 递归 / 17 check** 为准。

> 基线在本次扫描期间被并发会话推进了三次（`7b42fd5` → `d2330fd` → `5f43522`），每次只增不删（`9e6162e` 改 AGENTS 数字、`d2330fd` 加 `tools/mvpw-mcp/` 4 文件、`5f43522` 加 1 文件）。**所有实质结论（§3/§4）均已在最新 HEAD 上重跑确认，未发现结论翻转。** 复现时请以 `git rev-parse --short HEAD` 记录你自己的基线。

### 0.2 扫描脚本（可复现）

四个脚本，全部在 `_work/scan/`，互不依赖：

| 脚本 | 回答 | 输出 |
|---|---|---|
| `_work/scan/refs.py` | 未引用 `.cs`/`.xaml` | `_work/scan/out/refs.md` |
| `_work/scan/resources.py` | 未使用资源键 | `_work/scan/out/resources.md` |
| `_work/scan/docnums.py` | 文档过期数字 | `_work/scan/out/docnums.md` |
| `_work/scan/commented.py` | 注释代码块 | `_work/scan/out/commented.md` |

```powershell
cd C:\Users\z2637\Documents\DeepSeek\mpv
python _work\scan\refs.py        # 零引用类型 7 个
python _work\scan\resources.py   # 119 键 / 12 未引用
python _work\scan\docnums.py     # 48 处可疑数字
python _work\scan\commented.py   # 59 个块
```

`refs.py` 的一个关键点，值得单独说明，因为**第一版是错的**：只查任务列出的 6 种引用时，脚本报出 **69 个**「零引用类型」，其中包含 `MpvCommandQueue`、`FileService`、`UpdateChecker` 等明显在用的类。原因是这些类是 `X.Method(...)` 静态调用或扩展方法，6 种模式一个都不覆盖。加上第 7 种（裸标识符 token）后降到 **7 个**。证据：

```powershell
# 加第 7 种之前：refs.md 的零引用表有 69 行
# 加之后：7 行。手工复核其一：
git grep -c "MpvCommandQueue" -- "*.cs"   # 只有定义文件自己，3 处
```

因此本清单的「零引用」= **6 种（任务要求）+ 裸标识符**，是比任务要求更强的判据。

### 0.3 保留项判定原则

`.Gen.` 文件、`partial` 另一半、`Program.cs`、`App.xaml.cs`、`*Window.xaml`（经 `WindowsManager.Open` 构造）一律**不因「名字没出现」而判死**，见 §1。

---

## 1 保留启用（不是干扰项，防误删）

这一节是为了防止后续清理把它们当成候选。每条都给「它是活的」的证据。

| 路径 | 证据 | 理由 | 动作 |
|---|---|---|---|
| `mpv-winui-lazy\scripts\dyn_menu.lua`（66 KB，GPL-2.0-only） | `dyn_menu.lua:1376` 广播 `menu-ready`；`:1480` `register_script_message('update', …)`；`:1633` `load_dyn_menus()` | mpv-menu-plugin 上游源 + 本地补丁，**不是** `dynamic_menu.lua` 的重复。见 §2.1 的完整核实 | 保留 |
| `mpv-winui-lazy\scripts\dynamic_menu.lua`（6.6 KB） | `:172` `register_script_message('menu-ready', update_menus)`；`:166`/`:169` 反向 `script-message-to dyn_menu update vsr_menu/hdr_menu` | 只做 VSR/HDR 动态子菜单，靠 `menu-ready` 与 `update` 与 dyn_menu 串联 | 保留 |
| `mpv-winui-lazy\scripts\recentmenu.lua` | `:433-436` 同样监听 `menu-ready` 并走 `update` 通道 | 说明 `update` 通道是**共享设计**，不是 dynamic_menu 专用 | 保留 |
| `Themes\Generic.xaml` | `Styles.xaml` 无此键；但它是 `SliderExt`/`CheckBoxExt` 的**隐式样式**载体（`BasedOn="{StaticResource DefaultSliderStyle}"`），按 WinUI 约定由 `Themes\Generic.xaml` 自动加载 | 全文件仅 13 行，0 个 `x:Key`，不需要被 `{StaticResource}` 引用 | 保留，**不能因为「0 个 key 被引用」判死** |
| `Modules\Common\View\FluentIcons.Gen.cs` | `tools/gen-fluent-icons.py:27` 的 `OUT` 就是它；23 个 `public const string` | 生成物，手改会被覆盖。见 §2.2 | 保留 |
| `Modules\Common\View\FluentIcons.cs` | `:62/:69/:80` 三个 helper（`Geometry`/`Create`/`Parse`），`partial class` 的另一半 | 手写半边，生成器明确声明不会碰它 | 保留 |
| `tools\deploy-test.ps1` 与 `tools\deploy-test.sh` | `.sh:46` 有非 GNU find 的**主动 exit 1** 探测；`.ps1` 头部注明了为何另开一份 | 二者不是重复实现，是「Windows 上 bash 版会静默空跑」的防护对。见 §2.3 | 保留 |
| `Modules\MpvConf\MpvConfEditorPage.xaml`（606 行） | `MpvConfEditorWindow.xaml.cs:26` `Frame.Navigate(typeof(MpvConfEditorPage), …)` | 结构化编辑器，主窗口菜单 `mpvconf` 进入 | 保留 |
| `Modules\MpvConf\MpvConfTextWindow.xaml`（688 行） | `SettingsPage.Actions.ImportExport.cs:170` `new MpvConfTextWindow()` | 原始文本编辑器，设置页 action 进入。见 §2.4 | 保留 |
| 全部 `*Window.xaml`（`SettingsWindow`/`PiPWindow`/`MediaInfoWindow`/`MenuEditorWindow`/`MpvConfEditorWindow`/`MpvConfTextWindow`） | 各自有 `new XxxWindow(` 站点（`refs.md` 的 `new *Window` 表） | 由 `WindowsManager.Open(...)` 工厂构造，XAML 里不会出现标签 | 保留 |
| `Modules\Player\PlayerControl.ProgressMarks.cs` 等 21 个 `MpvPlayerPage_*` / `SettingsPage.*` partial | 同类型名以 `partial` 分片，`refs.py` 按**类型**归并 | 分片本身不是独立类型 | 保留 |
| `Styles\Styles.xaml` 中 `Light`/`Dark` 两个 `x:Key` | `Styles.xaml:8` `<ResourceDictionary.ThemeDictionaries>`，`:9`/`:103` 是两个主题字典本身 | 主题字典的键，不被 `{StaticResource}` 引用是**正确的** | 保留 |

---

## 2 保留归档（有价值但未启用）

### 2.1 `dyn_menu.lua` vs `dynamic_menu.lua` —— **核实结论：不是重复**

任务要求核实这一点。结论与任务的警告一致，证据如下。

| 维度 | `dyn_menu.lua` | `dynamic_menu.lua` |
|---|---|---|
| 体积 / 行数 | 65,992 B / 1,644 行 | 6,590 B / 178 行 |
| 许可 | `:1-2` `SPDX-License-Identifier: GPL-2.0-only`，`Copyright (c) 2023-2024 tsl0922` | `THIRD_PARTY_NOTICES.md:18` 列为项目自有 |
| 职责 | 解析 `input.conf`（`:1399 parse_input_conf`）、建 `menu-data`/`user-data/menu/items`（`:844-845`）、翻译表（`:20 BEGIN MENU_I18N`） | 只产出两棵子树：`vsr_menu_data`（`:63-92`）、`hdr_menu_data`（`:97-119`） |
| 串联方式 | `:1376` `mp.commandv('script-message', 'menu-ready', …)` 广播；`:1480` 收 `update` | `:172` 收 `menu-ready` → `:164 update_menus()` → `:166/:169` 发 `update vsr_menu` / `update hdr_menu` |
| 证据链是否闭合 | 是：`keyword_to_menu`（`:849` 定义、`:1333` 填充、`:1481` 消费） | 是：`vsr_menu`/`hdr_menu` 两个 keyword 由 dyn_menu 的菜单项声明 |

**关键佐证**：`recentmenu.lua:433-436` 用**同样的** `menu-ready` + `update` 协议。即这套协议是 mpv-menu-plugin 的通用扩展点，`dynamic_menu.lua` 是它的一个消费者，`recentmenu.lua` 是另一个。删掉 `dynamic_menu.lua` 会让 VSR/HDR 子菜单变成空壳（dyn_menu 里仍有 keyword 条目，但 `update` 永不到来）。

**动作**：保留两者。若将来要合并，正确做法是把 `dynamic_menu.lua` 的两棵数据表并入一个脚本，**不是**删掉其中之一。

### 2.2 `FluentIcons.cs` + `FluentIcons.Gen.cs` —— 生成/手写分片，不是重复

| 文件 | 行数 | 内容 | 证据 |
|---|---:|---|---|
| `FluentIcons.cs` | 254 | `partial class`（`:55`）+ 3 个 helper：`Geometry`（`:62`）、`Create`（`:69`）、`Parse`（`:80`） | 0 个 `public const string` |
| `FluentIcons.Gen.cs` | 107 | `partial class`（`:12`）+ 23 个 `public const string` 路径 | `:1-8` `<auto-generated>` 头，声明「Do not edit by hand」 |

生成器 `tools/gen-fluent-icons.py:10-12` 明确写着「Only the constants are generated… running this can never clobber them」。与 `AppLang.cs`/`AppLang.Gen.cs` 是同一套分片约定。

**动作**：保留两者。**唯一注意点**：23 个常量对 329 个 `.cs` 的调用面偏小，属于「够用即止」，不是冗余。

### 2.3 `deploy-test.ps1` + `deploy-test.sh` —— 有意的双实现，不是重复

- `deploy-test.sh:36-50`：注释说明本机 `find` 会被解析成 Windows `find.exe`，并有**主动探测 + `exit 1`**（`:46-50`），宁可失败也不静默空跑。
- `deploy-test.ps1:5-11`：头部说明「Why this exists alongside deploy-test.sh」，并记录了实测事故（dst dll 时间戳未变却 exit 0）。
- `tools/compare-deploy.ps1:4` 也引用同一事故。

两者共享两条不变量（`.xbf` 必须随 dll；永不删库重建），那是有意的**契约一致**，不是复制粘贴。

**动作**：保留。若一定要减一份，应删 `.sh` 并在文档里说明「本机无 GNU find」，但那会让非 Windows 环境失去入口 —— 不建议。

### 2.4 `MpvConfEditorPage` vs `MpvConfTextWindow` —— 两条编辑路径，服务不同入口

| | `MpvConfEditorPage` | `MpvConfTextWindow` |
|---|---|---|
| 行数 | 606 | 688 |
| 入口 | `Shell\MainWindow.xaml.cs:959` → `WindowsManager.Open("mpvconf", () => new MpvConfEditorWindow(), …)` → `MpvConfEditorWindow.xaml.cs:26` `Frame.Navigate(typeof(MpvConfEditorPage), configPath)` | `SettingsPage.Actions.ImportExport.cs:170` `new MpvConfTextWindow()` |
| 形态 | 结构化：profiles / groups / 搜索 / 逐项 apply（`:123 BuildProfiles`、`:148 BuildGroups`、`:237 ApplyToPlayer`） | 原始文本：RichEditBox + 行号槽 + 语法配色（`:424 Recolor`、`:592 UpdateGutter`、`:38-43` 六色板） |
| 写盘 | `SaveButton_Click`（`:520`）按结构重建 | `Save()`（`:357`）+ `FlushPendingSave`（`:347`）逐次落盘 |

二者不是同一功能的两次实现：一个是「按选项编辑」，一个是「按文本编辑」。**唯一的真实重叠**是「两个窗口都能改同一个 `mpv.conf`」，且 `ImportExport.cs:183` 的 `MpvConfOverrides.Refresh()` 已经在处理这个一致性。

**动作**：保留。**建议**（非删除）：在 `MpvConfTextWindow` 的标题或 `SetStatus` 里标注「原始文本，结构请用 mpv.conf 编辑器」，降低误用。

### 2.5 `tools\mvpw-mcp\`（已跟踪，5 个 `.py`）—— **规则冲突已在本次审计期间消解**

我在扫描期间观察到一次规则冲突，并在同一次会话里观察到它被解决。两个状态都记录，因为**这正是这类审计最容易漏掉的形态**：

| 时间点 | AGENTS.md 的说法 | 与 `verify_settings.py` 的关系 |
|---|---|---|
| 审计开始时（`d2330fd`） | `:69-78`「**Do not start `mpvw.exe`**，UI probes have never once worked」 | **直接冲突** |
| 现在（`5f43522` + 工作区改动） | 新增「Driving the app: MCP, not a screenshot (2026-10-22)」一节，并把工作流改为 4 步（第 3 步 = MCP 验证）；旧节仍在，但被新节限定为「that was true of the probes written before `uiautomation`/UIA3」 | **冲突已消解** |

现有 5 个文件：`mcpw_server.py`、`mpvw_tools.py`、`verify_e2e.py`、`verify_mcp_protocol.py`、`verify_settings.py`。

**动作：保留。** 但 AGENTS.md 现在**同时**存在「Do not launch the app」（`:95-101` 附近）与「Driving the app: MCP」（新节）两节，前者是后者的反例，**没有互相引用**。这会让下一个读者按读到的先后得出相反结论 —— 这是 AGENTS.md 内部一致性问题，不是本清单能修的。建议给旧节加一行指针：「see *Driving the app: MCP* above; this section describes the pre-2026-10-22 probes」。

> 另注：`verify_settings.py:4` 写「688 rows across 69 sections」，与 §6.1 实测的 688 一致。**代码侧已经知道正确数字，文档侧没有** —— 这是 §6 那 20 行过期数字的旁证。

---

## 3 待确认（疑似失效但可能仍有引用 / 证据不足）

这一节是**证据不足以判死**的项。每条都写清「差什么证据」。

| 路径 | 证据 | 差什么 | 动作 |
|---|---|---|---|
| `Modules\Common\View\AboutControl.xaml` + `.xaml.cs` | `refs.py` 零引用；`git grep AboutControl` 只命中自身 5 行；`AboutControl.Create()`（`:13`）无调用者；菜单 `about`（`MpvPlayerPage_MenuBar.cs:91`）走的是 `ShowAboutDialogAsync()`（`:585`），后者**手搭 `StackPanel`**（`:587-629`），完全不复用这个控件 | 它是不是**AppPackages 里已发布版本**的遗留？若曾发布过，删除会改变二进制内容。需确认「是否只用于打包截图」 | 待确认 → 大概率可删（§4 给了命令） |
| `Modules\Player\MpvCommandQueue.cs`（178 行） | **已结案，移入 §4.1.2。** 复核：`git grep -n 'MpvCommandQueue' -- '*.cs'` = 3 命中全在文件内；`git log --all -S 'new MpvCommandQueue'` **零提交**（历史上从未被构造过） | — | 见 §4.1.2 |
| `Modules\Common\Utils\TaskExtensions.cs` | 类型名零引用，但 `FireAndForget` **12 处调用**（`MpvSoftwarePreviewControl.xaml.cs:170,183,193,202,229`、`WatchHistoryControl.xaml.cs:46,110`、`WatchLaterControl.xaml.cs:39,96`、`MpvPlayerPage_Drop.cs:176` 等）。C# 14 `extension` 块语法下**类型名不出现**是正常的 | 无。这是**误报**，列在这里是为了说明 `refs.py` 的局限 | **保留**，不是干扰项 |
| `Modules\Common\View\WindowExtensions.cs` | 同上：`ShowWindow` 5 处、`SetWindowMinSize` 4 处（`WindowsManager.cs:32,35`、`MainWindow_Size.cs:173,187`、`MainWindow_Style.cs:90`、`App.xaml.cs:62`、`MainWindow.xaml.cs:945`） | 无 | **保留** |
| `Modules\Common\Utils\DispatcherExtensions.cs` | 类型名零引用；`RunAsync` 有 **11 处**调用（`WindowStyleManager.cs:132`、`MpvPlayerPage_Info.cs:43`、`MpvPlayerPage_Library.cs:2549,2594`、`MpvPlayerPage_Playlist.cs:410`、`MpvPlayerPage_Window.cs:11`、`MpvPlayerPage.xaml.cs:568` 等） | 但 `MpvCommandQueue.cs:35,127` 的 `RunAsync` 是**私有方法**，不是扩展调用，需逐处区分 | **保留**（扩展形式），仅提示判据要按**成员名**而非类型名 |
| `Modules\Common\View\HighlightTextBlock.xaml` + `.xaml.cs` | 零引用。`:10-11` 类文档自述「used by the playlist search filter」，但 `git grep PlaylistSearch\|SearchQuery` 只命中 `SettingsPage.xaml.cs` 的设置页搜索，**播放列表搜索不存在** | 自述的调用方不存在，但需要确认是否**曾经**存在过（`git log` 该文件能确认） | 待确认 → 与 `AboutControl` 同类 |
| `Modules\Player\Menu\MenuDefinitionSource.cs:14` `MenuJsonContext` | 零引用**是因为** `System.Text.Json` 源生成器在**编译期**由 `[JsonSerializable]` 自动接线；`:58` `JsonSerializer.Deserialize(json, MenuJsonContext.Default.ListMenuDefinition)` 就是它的用处 | 无 | **保留**，误报（源生成器约定） |
| `Styles\Styles.xaml` 的 12 个未引用键（见 §4.3） | 逐个 `git grep` 复核，全部只命中定义行 | 其中 `MpvPlayerPopupShadowDepth` 被 `AGENTS.md:191` 点名为「四处共享 chrome，必须一起改」的一员 | 拆开处理：**判死 11 个，`MpvPlayerPopupShadowDepth` 进待确认** |
| `tools\deploy-test.sh` | `.sh:46-50` 的探测使其在本机**必然 exit 1** | 它在别的机器上是否仍被使用？仓库内无引用（`git grep deploy-test.sh` 只命中文档描述） | 待确认（不建议删） |
| `screenshot\`（6 图，663 KB） | `README.md` 不引用（`git grep "screenshot/"` 仅命中 `docs/project-overview-20261021.md:77,243`，而后者**自己也把它列为待确认问题**） | 「README 用截图」的用处没有被 README 兑现 | 待确认（归档，见 §5） |

---

## 4 删除（已证实无引用 / 重复）

**本节每一条的「零引用」都可以用给出的命令复现。本节不做任何实际删除。**

### 4.1 源文件

| # | 路径 | 证据（可复现） | 理由 | 动作 |
|---|---|---|---|---|
| 4.1.1 | `mpv-winui\mpv-winui\Modules\About\AboutControl.xaml`<br>`…\AboutControl.xaml.cs` | `git grep -n "AboutControl" -- "*.cs" "*.xaml"` → 5 行，全部在自身两个文件内；`AboutControl.Create` 零调用 | 「About」对话框已由 `MpvPlayerPage_MenuBar.cs:585 ShowAboutDialogAsync()` 手搭实现，这个控件从未被实例化 | 删除（与 §3 第一条联动：若确认曾发布则改归档） |
| 4.1.2 | `mpv-winui\mpv-winui\Modules\Player\MpvCommandQueue.cs`（178 行） | ① `git grep -n 'MpvCommandQueue' -- '*.cs'` → **3 命中，全在文件自身**；② `git log --all -S 'new MpvCommandQueue'` → **零提交**（自建库以来从未被构造过）；③ 无字段、无 `EnqueueCommand`/`DrainAsync` 外部调用 | 一个从未被实例化的 FIFO 队列。**审计期间 `AGENTS.md` 的「Command ordering」条目已由他人修正**，现明确写「That class still exists… nothing constructs it… the file is a deletion candidate, not a mechanism」（2026-10-22），并给出与我一致的复核命令 | 删除；`AGENTS.md` 已同步 |

**这两条是本清单唯一「已证实可删」的源文件。** `MpvCommandQueue.cs` 从 §3 移到这里，是因为审计过程中 `AGENTS.md` 的对应条目被修正，且我用 `git log --all -S` 独立验证了「历史上从未构造」这个更强的结论 —— 不只是「当前无引用」。

### 4.2 重复实现核查结论

任务列的四组，**没有一组是重复实现**：

| 组 | 结论 | 关键证据 |
|---|---|---|
| `FluentIcons.cs` + `FluentIcons.Gen.cs` | **不是重复**（生成/手写分片） | §2.2 |
| `deploy-test.ps1` + `deploy-test.sh` | **不是重复**（防护对） | §2.3 |
| `dyn_menu.lua` + `dynamic_menu.lua` | **不是重复**（协议两端） | §2.1 |
| `MpvConfEditorPage` vs `MpvConfTextWindow` | **不是重复**（两种编辑范式） | §2.4 |

### 4.3 未使用的资源键

`Styles\Styles.xaml` 共 **119** 个 `x:Key`；**12 处定义**全库零引用（已排除 `Light`/`Dark` 两个主题字典键）。`Themes\Generic.xaml` 为 **0** 个 `x:Key`（它只有两个隐式 `Style`）。

```powershell
python _work\scan\resources.py     # "keys=119 unreferenced=12"
```

**12 处定义 = 8 个不同键名**，因为有 4 个键在 Light/Dark 两个主题字典里各定义一次：

| # | 键 | 定义位置（行） | 复核结果 | 动作 |
|---|---|---|---|---|
| 1 | `MpvContentGutter` | `Styles.xaml:1260` | 唯一另一处是 `MpvPlayerPage.xaml:1532` 的**注释文本**里提到这个词 | 删除 |
| 2 | `MpvPlaybackToolSurfaceBrush` | `Styles.xaml:80`（Light）、`:158`（Dark） | 两处都只有定义 | 删除 2 处 |
| 3 | `MpvShellBarMicaBrush` | `Styles.xaml:59`、`:145` | 只有定义 | 删除 2 处 |
| 4 | `MpvShellContentMicaBrush` | `Styles.xaml:56`、`:142` | 只有定义 | 删除 2 处 |
| 5 | `MpvShellPaneMicaBrush` | `Styles.xaml:53`、`:139` | 只有定义 | 删除 2 处 |
| 6 | `MpvShellTabStripHeight` | `Styles.xaml:1510` | 另一处 `:1456` 是**注释**解释它为何是固定值 | 删除（注释一并清） |
| 7 | `MpvSpaceXl` | `Styles.xaml:1155` | 只有定义 | 删除 |
| 8 | `MpvPlayerPopupShadowDepth` | `Styles.xaml:1578` | 只有定义 | **待确认，不删**（见下） |

**净可删：7 个不同键名 / 11 处定义**（12 减掉 `MpvPlayerPopupShadowDepth` 那 1 处）。

**两个差点被误删的坑，必须写下来**：

1. **`MpvChapterTickBrush` 是活的，且它根本不在上表里。** 它在 `Styles.xaml:89`/`:163` 有定义，读取方式是 `PlayerControl.ProgressMarks.cs:152` 的 `Resources.TryGetValue("MpvChapterTickBrush", out var brush)` —— 既不是 `{StaticResource}` 也不是 `Resources["…"]`。我第一版扫描器只认这两种，把它报成未引用（当时总数是 14）；补上 `TryGetValue` 模式后降到 12，它自动消失。这一点在仓库里有先例注释：`ProgressMarks.cs:148-151` 专门讲了「Application indexer resolves against the APPLICATION theme, not this element's」。**任何按 `{StaticResource}` 扫孤儿资源键的工具在这里都会误报。**
2. **`MpvPlayerPopupShadowDepth` 不能凭「零引用」删。** `AGENTS.md:191` 把它与 `MpvPlayerPopupPresenterStyle`、`MpvPlayerPopupCornerRadius` 并列为「四处 popup 共享 chrome，**Change those together**」。实测三者命运不同：

   | 键 | 使用处 |
   |---|---|
   | `MpvPlayerPopupPresenterStyle` | 2 处（`PlayerControl.xaml.cs:1884`、`:1893`） |
   | `MpvPlayerPopupCornerRadius` | 2 处（`PlayerControl.xaml:940`、`:963`） |
   | `MpvPlayerPopupShadowDepth` | **0 处** |

   这说明**文档描述的契约本身已经破了**：要么某个 popup 没走共享 chrome（自己硬编码了阴影深度），要么这个键是为「计划中但未接线」准备的。删键只会让问题消失，不会让它解决 —— 所以进 §3。

### 4.4 注释掉的代码块

规则**照任务原文**：连续 ≥5 行 `//`，且至少一行含 `;` 或 `{`。`///`（XML 文档注释）不算 `//` 行；vendored 的 `packages/`、`libs/` 已排除（否则第三方头文件会淹没列表）。

```powershell
python _work\scan\commented.py    # "blocks found: 59"
```

**关键发现：59 个块里，没有一个是真正的被注释掉的代码。** 全部是**说明性散文注释**，只是恰好含 `;` 或 `{`。下表前 20 大，`看起来像代码` 列 = 去掉 `//` 后匹配源码形态的行数：

| # | 位置 | 行数 | 看起来像代码 | 判读 |
|---:|---|---:|---:|---|
| 1 | `Modules\Settings\Layout\SettingsSections.cs:181` | 27 | 0 | 散文 |
| 2 | `Modules\Settings\SettingsWindow.xaml.cs:55` | 25 | 1 | 散文（讲最小宽高为何是 560，含 `{` 因行内有花括号示例） |
| 3 | `Modules\Language\AppLang.cs:474` | 24 | 0 | 散文 |
| 4 | `Modules\Settings\SettingsPage.Options.Layout.cs:91` | 22 | 0 | 散文 |
| 5 | `Modules\Settings\SettingsPage.Options.cs:116` | 20 | 0 | 散文 |
| 6 | `Modules\Settings\SettingsPage.Options.cs:316` | 18 | 0 | 散文 |
| 7 | `Modules\Player\MpvPlayerPage_Library.cs:2342` | 16 | 1 | 散文，**内含 1 行确实被注释掉的签名**（`:2344` `// private void CardOverlayButton_PointerPressed(...) => e.Handled = true;`），是「此处曾有代码」的墓志铭 |
| 8 | `Modules\Player\MpvPlayerPage.xaml.cs:153` | 14 | 1 | 散文（解释为何不再需要 `set speed`） |
| 9 | `Modules\Player\MpvPlayerPage.xaml.cs:464` | 14 | 1 | 散文 |
| 10 | `Modules\Player\MpvPlayerPage_Library.cs:2481` | 14 | 1 | 散文 |
| 11 | `Modules\Player\PlayerControl.Animation.cs:217` | 14 | 1 | 散文 |
| 12 | `Modules\Settings\SettingsPage.Options.cs:300` | 14 | 0 | 散文 |
| 13 | `Modules\Player\PlayerControl.xaml.cs:2091` | 12 | 0 | 散文 |
| 14 | `Modules\Settings\SettingsPage.Options.cs:947` | 12 | 0 | 散文 |
| 15 | `mpv-winrt\MpvPlayer.idl:160` | 11 | 0 | 散文 |
| 16 | `Modules\Settings\Controls\OptionListControl.xaml.cs:331` | 11 | 0 | 散文 |
| 17 | `Modules\Settings\Layout\SettingsSections.cs:160` | 11 | 0 | 散文 |
| 18 | `Modules\Settings\SettingsWindow.xaml.cs:119` | 11 | 0 | 散文 |
| 19 | `Modules\MpvConf\MpvConfEditorPage.xaml.cs:52` | 10 | 0 | 散文 |
| 20 | `Modules\MpvConf\MpvConfTextWindow.xaml.cs:60` | 10 | 0 | 散文（`---- Gutter geometry ----` 分隔线） |

**动作**：**一条都不删**。这个仓库的注释密度是刻意的（AGENTS.md 反复强调「记录原因」）。唯一可议的是 `MpvPlayerPage_Library.cs:2344` 那一行签名 —— 但它作为「为什么删」的论据存在，删掉会让 `:2346-2357` 的解释失去指代对象。

> 结论写在这里以免被误解：任务要求的「列出前 20 个最大的」已完成，但**「干扰项」在本项上为 0**。这是一个诚实的负结果。

---

## 5 构建产物与 .gitignore 建议

**没有删除任何文件。** 下表是体积实测与建议动作。

| 路径 | 跟踪状态 | 体积 | `.gitignore` 现状 | 建议 |
|---|---|---:|---|---|
| `dist\`（`.msi` 73.5 MB + `.zip` 87.1 MB + `.wixpdb` 0.1 MB） | **未跟踪** | **160.6 MB** | 已忽略（`.gitignore:381 /dist/`） | **无需动作**，已正确忽略。可随时清盘 |
| `_cloud\`（云端克隆，541 文件） | **未跟踪** | **85.1 MB** | 已忽略（`.gitignore:391 _cloud/`） | 已正确忽略。**按要求未触碰** |
| `_work\`（含本次扫描脚本） | **未跟踪** | 2,443 KB / 40 文件 | 已忽略（`.gitignore:386 _work/`） | 已正确忽略 |
| `tools\__pycache__\`（2 个 `.pyc`） | **未跟踪** | 68 KB | 已忽略（`.gitignore:326 __pycache__/`） | 已正确忽略 |
| `tools\mvpw-mcp\__pycache__\`（1 个 `.pyc`） | **未跟踪** | 20 KB | 已忽略（同上） | 已正确忽略 |
| `screenshot\`（6 图） | **已跟踪**（`git ls-files` 命中 6 条） | **663 KB** | **未忽略** | 见下 |
| `_eb00.png`（任务描述的根目录残留） | — | — | — | **该文件在 `d2330fd` 上不存在**（`Get-ChildItem -Recurse -Filter '_eb00*'` 零命中；`git ls-files \| grep eb00` 零命中） |
| `@AutomationLog.txt` | **未跟踪** | 145 B | 已忽略（`.gitignore:394`） | 已正确忽略 |

### 5.1 `screenshot\` 的处理建议

它是**唯一「已跟踪但不该跟踪」的一组**。判断依据：

- `README.md` 全文**不引用**任何 `screenshot/...` 路径（`git grep "screenshot/" -- README.md` 零命中）。
- 唯一提到它的是 `docs\project-overview-20261021.md:77`（分类为「文档资源」）与 `:243`（**同一份文档自己把它列为待确认问题**：「`screenshot/` 目录 6 张图是 README 引用还是历史产物？`README.md` 未引用它们」）。

| 动作 | 说明 |
|---|---|
| **建议 A（推荐）**：`git mv screenshot\ docs\archive\screenshot\` | 6 图共 663 KB，作为历史截图有价值（记录了 2026-09 前后的 UI 状态），但不应占根目录。**注意**：`git mv` 是写操作，本次**未执行** |
| 建议 B：删除 + 在 `README.md` 补一句「截图见 releases」 | 若确认无历史价值 |
| 建议 C：保留原地，在 `README.md` 里真正引用它们 | 若这 6 张就是给 README 用的，那缺的是 README 的引用，不是图片 |

### 5.2 `.gitignore` 建议增补（本次**未修改**）

现有 394 行已覆盖 `dist`/`_work`/`_cloud`/`__pycache__`/`@AutomationLog.txt` —— 覆盖得**很好**，几乎没有缺口。唯一可议：

```gitignore
# 建议（当前不需要，因为文件已不存在）：
# _eb00.png        ← 任务提到的根目录残留，实测已不在
```

**结论：`.gitignore` 不需要改动。** 真正的问题不是「该忽略的没忽略」，而是「`screenshot/` 已经进去了」—— 那是 `git rm --cached` 的范畴，不是 ignore 的范畴。

---

## 6 文档过期点

### 6.1 实测真值（与任务给的一致，已独立复核）

```powershell
python tools\report-doc-numbers.py
```

```json
{
 "generated_rows": 362,
 "handwritten_rows": 326,
 "total_rows": 688,
 "schema_label_props": 724,
 "schema_keys_in_zh_CN": 724,
 "schema_keys_in_en_US": 0,
 "languages": 9,
 "checker_scripts": 17,
 "tools_files": 37
}
```

与任务给的 362 / 326 / 688 / 724 完全一致。**一处与任务描述不符，按实测记录**：`tools/` 口径，见 §0.1 的四基线对照表（任务写 35 / 19，实测 `5f43522` 为 37 顶层 / 17 check；其中 `35` 能对上 `aab1a9f` 的顶层文件数，但那个基线的 check 数是 15）。

`report-doc-numbers.py` 的 `rows()` 用 `\bnew Option\b`，我一度怀疑它会误吃 `new OptionChoice(`。**实测否定了这个怀疑**：`\bnew Option\b` 得 **362**，与真实行数一致（`OptionChoice` 的 `C` 是大写，`\b` 在 `n` 与 `C` 之间不成立… 实际是 `\b` 要求词边界，`OptionC` 里 `Option` 后直接跟 `C` 不构成边界，故不匹配）。我先前得到的 592 是用了 `new Option` **不带尾 `\b`** 的错误模式。这个检查器**没有缺陷**，此前的怀疑作废。

反过来的坑仍成立：统计生成行**不能**用 `grep -c 'new Option'`（无 `\b` 尾界），那会得 592（362 + 230 个 `new OptionChoice`）。

### 6.2 过期点清单（`文件:行` / 写的 / 实测）

共 **48 处**命中，分布在 6 个文件。`python _work\scan\docnums.py` 可复现。拆分：**需改 20 行**（下表上半）+ **引用旧值但正确、不改 3 行**（下半）。行内出现的可疑数字共 25 个（20 行里有 5 行各含 2 个）。

**需修改（20 行）**：

| 文件:行 | 写的 | 实测 | 需改成 |
| `docs\settings-design-contract.md:24` | 设置行总数 **874** 字面量 = **871** 唯一（322 手写 + **549** 生成） | 688 = 326 + 362 | `688 = 326 + 362` |
| `docs\settings-design-contract.md:27` | 9 个语言文件**各缺 1098 个** | 724（362+362） | `724` |
| `docs\settings-design-contract.md:36` | 检查器 **17 个：15 通过 / 2 失败**；`check-localization` **1098**×9 缺键 | 17 个 check-*.py；缺键 724 | 检查器数巧合正确，缺键数改 724 |
| `docs\settings-design-contract.md:50` | schema 生成层（**549** 行、757 KB、6 个文件） | 362 行 | `362` |
| `docs\settings-design-contract.md:61` | `SettingsPage.Options.Gen.cs` ⚙ 生成：**549** 条 schema 行 | 362 | `362` |
| `docs\settings-design-contract.md:440` | 编译 0 error 却 **549** 个对象全被丢弃 | 362 | `362` |
| `docs\settings-design-contract.md:592` | `check-localization` **2439** props × 9；各缺 **1098** 键 | AppLang 实测 2208 props；缺 724 | `2208` / `724` |
| `docs\settings-design-contract.md:665` | **549** 条假 stale 警告 | 362 | `362` |
| `docs\settings-design-contract.md:666` | **1098** 个生成键被反报成「多余键」 | 724 | `724` |
| `docs\settings-design-contract.md:676` | **1098** 个键名打印成一行 | 724 | `724` |
| `docs\settings-design-contract.md:803` | A-16 **549** 条生成行；各缺 **1096** 个 | 362 行；724 键 | `362` / `724`（`1096` = 548×2，双重过期） |
| `docs\settings-design-contract.md:811` | A-24 **549** 条 schema 生成选项；**1096** 键 × 9 | 362 条；724 键 | `362` / `724` |
| `docs\settings-design-contract.md:820` | A-34 en-US 只缺 **746** 个 `Schema*`，七种各缺 **746** | 724 | `724` |
| `docs\settings-design-contract.md:832` | A-24 裁定：保留 **549→548** 条 | 362 | `362` |
| `docs\settings-design-contract.md:846` | 这 **548** 行存在的前提上 | 362 | `362` |
| `docs\settings-design-contract.md:855` | 一并订正：**549** → **548** | 362 | `362` |
| `docs\compare-upstream.md:37` | schema 生成层（**548** 行 / 757 KB / 6 文件） | 362 行 | `362` |
| `docs\handoff-20261012.md:32` | 8 份文件各缺 **746** 个 `Schema*` 键 | 724 | `724` |
| `docs\wmp-study-and-handoff.md:178` | 生成的 **373** 行全部有人工撰写文案 | 362 | `362` |
| `docs\wmp-study-and-handoff.md:297` | 生成的 **373** 行 | 362 | `362` |

**不改（3 行，引用旧值是正确的用法）**：

| 文件:行 | 写的 | 为什么不改 |
|---|---|---|
| `docs\cloud-diff-audit-20261021.md:338` | 引旧值 548/549/874/1098（作为对照列） | 它是审计报告，「旧值 vs 实测」对照是它的内容 |
| `docs\cloud-diff-audit-20261021.md:410-413, 504-512, 568, 585` | 同上，全部是「旧值 → 实测」对照 | 同上 |
| `docs\localization.md:23-24` | 「此前本表写的是 1315 / 1096 / 548」「548 与实际生成的 362 行不符」 | 它是在**解释**旧值为何错，属于正确用法 |

### 6.3 已自行修正、无需再改的两处

| 文件:行 | 状态 |
|---|---|
| `AGENTS.md:206` | **已正确**：`362 schema-generated rows`（本次扫描中途由 `9e6162e` 修正，此前是 548） |
| `docs\localization.md:20` | **已正确**：`**724**` 条 / `**362**` 行 |

### 6.4 未复核的数字（诚实标注）

`docs\cloud-diff-audit-20261021.md:585` 自己声明有两项没复核：`settings-design-contract.md:25` 的「815/820」与 `:28` 的「35 折叠卡片」。我**没有**复核它们，不在本清单表态。

另：`report-doc-numbers.py` 输出的 `schema_entries: 820` 与 `:25` 的「815/820」可能是同一来源，但 820 是 `MpvConfOptions\mpv-options.json` 的**选项条目数**，与「362 条生成行」不是同一口径（某些条目被合并/跳过，如 `Mpv_speed`）。**这两个数不矛盾，不应互相订正**——这是我建议保留 `820` 的依据。

---

## 7 不做什么（风险与拒绝项）

### 7.1 本次明确拒绝执行的操作

| 拒绝项 | 原因 |
|---|---|
| 删除任何文件 | 任务硬性要求「不改仓库、不删文件」 |
| `git rm` / `git mv` / `git add` / `git commit` | 任务硬性要求「不跑 git 写操作」 |
| 启动 `mpvw.exe` | AGENTS.md 的「Do not launch the app」/ 2026-09-21 两节；且本任务就明确要求「不启动 mpvw.exe」。本次全部结论来自静态阅读，**没有运行 `tools/mvpw-mcp/` 下任何脚本** |
| 修改 `.gitignore` | 实测无需改动（§5.2） |
| 触碰 `_cloud\` | 任务明确「云端克隆，勿动」 |
| 修改 `docs\audit-cleanup-list.md` | 它是线索，不是待改产物；且已有新文档取代它的作用 |

### 7.2 执行清理时的风险提示

| 风险 | 说明 |
|---|---|
| **`.Gen.` 文件不可手改** | 7 个 `.Gen.cs` 全部由 `tools/gen-*.py` 生成。删除 `FluentIcons.Gen.cs` 的 23 个常量会让 `FluentIcons.Create` 的调用方编译失败，且下次生成会复原 |
| **`screenshot/` 用 `git rm` 会改历史可见性** | 6 图在 git 历史里留存，`git rm --cached` 只停止跟踪。若要真正减小仓库体积需 `filter-repo`，那是**改写历史**，本清单不建议 |
| **删资源键必须先跑 `check-resources.py`** | 仓库**已有** `tools\check-resources.py`（`gh` 未见运行记录，但存在）。删键前应跑它，而不是信本清单 —— 本清单是一次性快照 |
| **`MpvPlayerPopupShadowDepth` 的删除会掩盖真实缺陷** | `AGENTS.md:191` 说四处 popup 共享 chrome；实测只有 2 个键在用、这个键 0 处。删键 = 承认契约作废，但**没有解决**「某个 popup 可能没用共享 chrome」。应先查 `PlayerTrackSelectorControl` / `QuickControlPanel` 的 `Border` 是否硬编码阴影 |
| **`MpvCommandQueue` 已结案** | 零引用且 `git log --all -S 'new MpvCommandQueue'` 零提交，`AGENTS.md` 也已同步修正该条目（2026-10-22）。可直接删，不存在「文档说在用」的阻力了 —— **这条留在这里是因为它一度是本清单里唯一需要裁决的项，现已消解** |
| **`extensions` 语法让类型名不出现** | `TaskExtensions`/`WindowExtensions`/`DispatcherExtensions` 三个「零引用」全是 C# 14 `extension` 块的误报。**任何按类型名扫死代码的工具在这里都会骗人** —— 这是本仓库特有的陷阱，值得写进 `AGENTS.md` |

### 7.3 顺带发现、但**超出本任务范围**（只报告，不处理）

| 发现 | 证据 | 为什么值得单独跟进 |
|---|---|---|
| `AGENTS.md` 内部前后矛盾 | 它同时含「Do not launch the app」（旧节，未删）与「Driving the app: MCP, not a screenshot」（2026-10-22 新节），两节没有互相引用 | agent 入口文件出现两个相反结论，读到哪个取决于阅读顺序。建议给旧节加一行指向新节的指针 |
| `MpvPlayerPopupShadowDepth` 的契约断裂 | §4.3：`AGENTS.md:191` 说三键共用，实测前两键各 2 处使用、第三键 **0 处** | 不是文档过期，是**可能有一个 popup 没走共享 chrome**（硬编码了阴影）。删键会掩盖它 |
| `HighlightTextBlock` 的自述失效 | `.xaml.cs:10-11` 说「used by the playlist search filter」，`git grep PlaylistSearch\|SearchQuery` 只命中设置页搜索 | 与 `AboutControl` 同型：**类文档描述了一个不存在的调用方** |

---

## 附：一页速览

| 分类 | 数量 | 条目 |
|---|---:|---|
| §1 保留启用 | 12 行 | Lua 双脚本+recentmenu、Generic.xaml、FluentIcons 两半、deploy-test 两半、两套 MpvConf 编辑器、全部 `*Window`、全部 partial、`Light`/`Dark` |
| §2 保留归档 | 5 组 | §2.1–2.5（含 `tools\mvpw-mcp\` 的规则冲突提醒） |
| §3 待确认 | **9 条** | `AboutControl`、`HighlightTextBlock`、`MpvPlayerPopupShadowDepth`、`screenshot\`、`deploy-test.sh`、3 个扩展类误报、`MenuJsonContext` 误报（`MpvCommandQueue` 已移入 §4） |
| §4 删除 | **2 个文件对 + 7 个资源键 / 11 处定义** | `AboutControl.xaml(.cs)`、`MpvCommandQueue.cs`；7 个零引用资源键 |
| §5 构建产物 | 6 项 | 5 项已正确忽略；`screenshot\` 建议归档 |
| §6 文档过期 | **20 行需改** + 3 行引用旧值但正确 | 集中在 `settings-design-contract.md`（16 行），余下 `compare-upstream.md`/`handoff-20261012.md`/`wmp-study-and-handoff.md` |
| §7 拒绝项 | 6 项 | 未删、未写、未启、未改 |

**一句话总结**：这个仓库的「干扰项」比预期**少得多**（真正可删的只有 2 个从未被实例化的类 + 7 个孤儿资源键），因为它的注释、生成物分片和双实现几乎都是有意的。真正的问题不在垃圾文件，而在 **文档侧**：`docs\settings-design-contract.md` 有 16 行数字停在 874/871/549/1098，`AGENTS.md` 内部有两节互相矛盾（「不要启动」vs「用 MCP 驱动」）。代码比文档新。

### 附-1 本次作废的判断（留痕，避免被引用）

诚实起见，记录我中途推翻的两个结论：

| 曾认为 | 实测推翻它的命令 | 现在 |
|---|---|---|
| 69 个「零引用类型」 | 加裸标识符扫描后 `python _work\scan\refs.py` 输出 7 | 7 个，其中 4 个是扩展方法/源生成器误报 → 真正可疑 3 个 |
| `MpvChapterTickBrush` 等 14 个资源键未引用 | `resources.py` 补 `TryGetValue("K")` 模式后降到 12 | 12 个；且 `MpvChapterTickBrush` 被证明是活的 |
| `report-doc-numbers.py` 的行数正则过宽 | `python -c` 实测 `\bnew Option\b` = 362 | 检查器无缺陷，我的怀疑作废 |
