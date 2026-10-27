# 工具与技能盘点（TOOLING）

> **证据等级**：【已运行】本轮实际执行并给出输出 ｜【代码】源码/配置静态事实（附 `path:line`）｜【推断】推理未验证 ｜【未知】需补充
>
> 盘点对象：本机已装的技能、插件、MCP 服务器与 CLI，以及它们对 `mpv-clean`（`mpvw.exe`，程序集名 `mpvw`）这一轮"构建 → 部署 → 验证"的实际作用。
>
> 约束说明：本轮**没有**启动 `mpvw.exe`，也没有读写 `mpv-deploy`。UIA 可达性引用 Lead 的既有测量（构建产物启动、窗口标题 `MPV`、72 个 UIA 元素、导航读作 主页/音乐库/视频库/播放队列、传输控件读作 播放/静音/全屏、进程已确认退出）。凡标【已运行】的 UIA/截图结论，都是在 **Notepad** 上做的等效验证。

---

## 1. 清单

### 1.1 WinUI 官方插件 `winui@win-dev-skills` 0.7.1

安装事实【代码】`C:\Users\z2637\.claude\plugins\installed_plugins.json`：唯一已装插件，`installPath` 指向 `...\plugins\cache\win-dev-skills\winui\0.7.1`，`gitCommitSha` 为 `5a064a4`。技能目录由插件清单声明【代码】`.claude-plugin/plugin.json`：`"skills": "./agent-plugin/skills/"`。

| 名称 | 类型 | 用途 | 状态 | 本轮是否使用 | 结论 |
|---|---|---|---|---|---|
| `winui-dev` | **agent**（`agents/winui-dev.agent.md`，26 行） | 以"WinUI 开发者"身份走 需求→设计→脚手架→编码→构建 | 存在，但不是 skill：插件里**没有任何名为 `winui-dev` 的 SKILL.md**；经 `dsh-claude-compat` 的 agent→委派 shim 机制以同名 skill 形式出现在目录 | 已调用（`skill` 工具，返回的 base directory 是 `...\0.7.1\agents`） | 【已运行】能调，但内容是过程性指令（"先加载 winui-dev-workflow 和 winui-design"），本轮已有等价流程，不采用 |
| `winui-dev-workflow` | skill（96 行） | 建项目/加分析器/构建运行/Native AOT/崩溃诊断 | 存在 | **部分读**（读完全文） | 【代码】"新建项目"与"`dotnet add package` 加分析器"两步对本仓库是死重（`mpv-winui.csproj:81` 已引 0.7.1 分析器）；"崩溃诊断"一节是本插件对**本仓库唯一补空白的部分** |
| `winui-design` | skill（189 行 + 3 份 references） | 控件选型、Fluent 布局、主题、无障碍、绑定 | 存在 | 未调用（读了前 60 行） | 【代码】核心价值是"写 XAML 前先 `winapp find-ui` / `winapp find-api`"这一条纪律；App-shape 表把"媒体/画布"类应用判为**不要 NavigationView**，与本应用 4 段 NavigationView 冲突，只能当参考不能当规则 |
| `winui-code-review` | skill（94 行 + `references/quality-rules.md` 12 KB） | 8 组清单：MVVM / x:Bind / Native AOT / 无障碍 / 主题 / 安全 / 性能 / 全局化 | 存在 | **已调用**（`skill` 工具加载成功） | 【已运行】可用，但 8 组里有 2 组（MVVM、全局化）与本仓库架构相悖、1 组（无障碍）前提失效，必须改写后才用（见 4.4、4.5） |
| `winui-ui-testing` | skill（341 行，全插件最大） | `winapp ui` 驱动的 UI 自动化脚本模板 | 存在 | **已调用**（读完全文） | 【已运行】方法骨架可用，模板断言**不能照抄**：与本应用的三处硬落差见 4.4 |
| `winui-setup` | skill（170 行） | 装并验证 .NET SDK / WinApp CLI / 开发者模式 | 存在 | 未调用 | 【代码】本轮环境已就绪（`winapp 0.7.1`【已运行】），此技能只负责"装"，无活可干；开发者模式状态【未知】，本轮不需要 |
| `winui-packaging` | skill（102 行 + `references/sourcegen-patterns.md`） | MSIX / 签名 / 证书 / CI | 存在 | 未调用 | 【代码】本轮**明确不产出 MSIX**，部署是 `mpv-deploy` overlay；其"Self-Contained 不等于单文件"一节对 `PublishAot + WindowsAppSDKSelfContained` 的解释仍有参考价值 |
| `winui-wpf-migration` | skill（111 行） | WPF → WinUI 3 迁移 | 存在 | 未调用 | 【已运行】不适用：`git grep -l 'System\.Windows\.' -- '*.cs'` 命中 **0** 个文件，本仓库没有 WPF 源 |

**结论一句话**：期望的"7 个 WinUI 技能"实际是 **7 个 skill + 1 个 agent**，共 8 个目录项；`winui-dev` 是 agent 经 compat 层转成的 shim skill，不是插件里写着的同名 skill【已运行：`skill` 工具对两个名字都返回了内容，且两句 resource base 分别是 `agent-plugin\skills\…` 与 `agents`】。

### 1.2 dsh 侧技能（`C:\Users\z2637\.dsh\skills\`）

25 个目录【已运行】。其中 **18 个**在本会话技能目录里可见，**7 个**不可见 —— 不可见的 7 个恰好全部声明了 `disable-model-invocation: true`（仅用户可 `/` 调用），这不是失踪，是设计【已运行：逐目录 grep 后两集合完全互补】。

| 名称 | 类型 | 用途 | 状态 | 本轮是否使用 | 结论 |
|---|---|---|---|---|---|
| `diagnosing-bugs` | dsh 原生 skill | 复现→最小化→假设→插桩→修复→回归 | 已装、可见（mtime 2026-10-26 20:11） | 未调用 | 【代码】与 `AGENTS.md`「Driving the app」「Reading a crash」高度重叠，而后者带本仓库专属事实（IPC 0.4 s、P 字段、兄弟方法不对称守卫）；通用流程让位给仓库文档 |
| `planning-with-files` | dsh 原生 skill | 写 `task_plan.md`/`findings.md`/`progress.md` 三件套 | 已装、可见 | 未调用（用 `todo_write` 代替） | 【代码】本任务单文件、单会话可完成，三件套的恢复价值为零；其 hook 依赖 `~/.claude` 的 hooks 通道，而 `enableHooks: false`【代码 `cordis.patch.yml:154`】 |
| `research` | dsh 原生 skill | 起后台 agent 查一手源并落 Markdown | 已装、可见 | 未调用 | 【已运行】本轮不可用：它要求"spin up a background agent"，而本会话委派深度已用尽（`subagent` / `subagent_fork` 剩余层级 0） |
| `domain-modeling` | dsh 原生 skill | 建立/打磨领域模型与通用语言 | 已装、可见 | 未调用 | 【代码】`docs/alignment/TERMINOLOGY.md` 已承担通用语言；该技能自己的正文写明"用于**改变**模型，而非只消费词表" |
| 其余 14 个可见技能 | dsh 原生 | 写作、设计、Office、原型、UI/UX、青龙、VHS 著录等 | 已装 | 未使用 | 【代码】与本仓库"构建/部署/UI 验证"无交集，不逐一展开 |
| `batch-grill-me`、`claude-handoff`、`handoff`、`teach`、`ubiquitous-language`、`writing-fragments`、`writing-shape` | dsh 原生 | 追问、交接、教学、写作 | 已装，**模型不可见** | 未使用 | 【已运行】`disable-model-invocation: true`，只能用户显式调用；本轮不需要 |

### 1.3 `.claude` 侧（插件市场、缓存、技能）

| 名称 | 类型 | 用途 | 状态 | 本轮是否使用 | 结论 |
|---|---|---|---|---|---|
| `plugins/installed_plugins.json` | 清单 | 已安装插件登记 | 仅 `winui@win-dev-skills` 0.7.1 | 已读 | 【已运行】插件通路只有一条，判断"WinUI 技能从哪来"时不会有歧义 |
| `plugins/known_marketplaces.json` | 清单 | 市场源 | `claude-plugins-official`、`win-dev-skills` | 已读 | 【代码】两个源均已克隆到本地 |
| `plugins/marketplaces/win-dev-skills` | git 克隆 | `microsoft/win-dev-skills` 的市场清单 | 克隆内**只有 `plugins/windbg`（11 个技能）**；`winui` 在 `marketplace.json` 里被声明为 **git-subdir**（源自 `microsoft/winappCli`，sha `5a064a4`），实体落在 `cache/` 而不在这里 | 只读 | 【已运行】市场里的第二个插件 `windbg` **未安装**，而它正好覆盖本机唯一的工具空白（见 §2.4、§5） |
| `plugins/cache/win-dev-skills/winui/0.7.1` | 缓存 | 插件实体 | 7 skill + 1 agent + `index.js`（空注册）+ `.claude-plugin` / `.codex-plugin` / `agent-plugin` 三套清单 | 已读 | 【代码】`index.js` 只 `register() {}`，本插件是**纯内容插件**，没有运行时工具 |
| `.claude/skills/` | 目录 | 27 个技能 | 与 `.dsh/skills` 差集：多 `docx/pdf/pptx/xlsx`，少 `qinglong/vhs-nfo-sourcing` | 未使用 | 【已运行】本轮**不被扫描**：`cordis.patch.yml:151` 把 `userClaudeDir` 指到不存在的 `~/.dsh/.claude-detached`；且 `docx/pdf/pptx/xlsx` 已由 dsh 原生 `office-*` 覆盖 |

### 1.4 dsh profile 配置（`profiles/desktop/cordis.patch.yml`，156 行，中文重度注释）

| 名称 | 类型 | 用途 | 状态 | 本轮是否使用 | 结论 |
|---|---|---|---|---|---|
| `claude-compat` 段（:144-156） | 插件配置 | 决定 Claude 生态如何进入 dsh | `enableSkills: true`、`userClaudeDir: ~/.dsh/.claude-detached`、`enableRules: false`、`enableHooks: false`、**`enablePlugins: true`** | 已读 | 【代码】WinUI 技能之所以可用，全靠末一行 `enablePlugins: true`；注释明确写着"设成 false 会把它们一起关掉（已实测）" |
| `llm-pi-ai` / `agent-default-model` | 路由配置 | 模型路由 | `workbuddy` + `cn:deepseek-v4.1-flash`，`reasoningEffort: max` | 未涉及 | 【代码】与本交付无关，仅记录：本会话跑在该路由上 |
| `permission` 段 | 权限预设 | 沙箱/审批 | `defaultPreset: danger-full-access` | 未涉及 | 【代码】解释了本会话为何无审批提示 |
| `subagent` / `agent-loop` | 运行配置 | 并发上限 | `maxActiveSubagents: 4`、`maxParallelToolCalls: 100` | 未涉及 | 【代码】与"委派深度已用尽"是两回事：深度是会话级限制，不是这个开关 |

### 1.5 MCP 与 CLI

| 名称 | 类型 | 用途 | 状态 | 本轮是否使用 | 结论 |
|---|---|---|---|---|---|
| `tools/mvpw-mcp/mcpw_server.py` | 自建 MCP 服务器（218 行） | stdio JSON-RPC 2024-11-05，暴露 8 个工具 | **可运行但未注册进 dsh** | **已运行**（手工 stdio 会话，未启动 app） | 【已运行】`initialize` 返回 `mvpw-mcp 0.1.0`；`tools/list` 返回 **8** 个工具（7 个 + `close_app`）；未知方法回 `-32601`；无 app 时 `read_playback_state` / `get_ui_snapshot` 均 `isError: true`、正文 `ToolError: mpvw.exe is not running`；进程正常退出码 0 |
| `tools/mvpw-mcp/mpvw_tools.py` | 工具实现（793 行） | UIA / 截图 / IPC / 进程 | 与服务器配套 | 已读全文 + **已运行**其中 `background_capture` 一条路径 | 【代码】`DEPLOY_DIR = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"`（`mpvw_tools.py:31`）硬编码；`_push_to_back` 把窗口停到 `(-32000,-32000)`；`_ipc` 的 `CreateFileW.restype=HANDLE` 修正写在注释里 |
| `tools/mvpw-mcp/` 脚本族 | 63 个 `.py` | 验证/探针/审计/诊断 | 与文档宣称的"60 个验证器"**不符** | 统计 + 运行 2 个自检 | 【已运行】真实构成：`verify_*` **23**、`probe_*` **5**、`audit_*` **7**、`diag_*` **20**、支撑模块 **8**（`mcpw_server`/`mpvw_tools`/`background_capture`/`background_window`/`capture_domains`/`explain_wui_findings`/`ui_measure`/`ui_measure_selftest`）= 63。"60" 既不是文件数也不是验证器数 |
| `winapp` CLI | 原生 CLI（非 MCP） | 打包/签名/运行/API 查询/UI 自动化 | **0.7.1**，`C:\Users\z2637\AppData\Local\Microsoft\WindowsApps\winapp.exe` | **已运行**（6 组 `--help` + 5 次真实调用） | 【已运行】21 个顶层命令；`ui` 22 个动词；`target` 6 个；`find-api` 6 个子命令（另有裸搜索）；`find-ui` 搜索/`--id`/`--list`/`--source` |

**`winapp` 与 UI 相关的动词**（`winapp ui --help`【已运行】）：`status`、`inspect`、`search`、`get-property`、`get-value`、`screenshot`、`record`、`invoke`、`click`、`drag`、`touch`、`pen`、`hover`、`send-keys`、`set-value`、`focus`、`scroll-into-view`、`scroll`、`wait-for`、`list-windows`、`get-focused`、`yield`。

**`find-api` 动词**：`members`（带 `--filter`）、`check-property`、`enums`、`packages`、`stats`、`refresh`，加裸搜索。【已运行】`--help` 全文读过，`members --filter` 确认存在（`winui-code-review` 里那句 `--filter selected` 不是编的）。

---

## 2. 能力差距分析（截图 / UI 自动化 / 进程窗口检查 / 日志捕获）

### 2.1 截图

| 问题 | 回答 |
|---|---|
| 覆盖了吗 | **覆盖，三条独立通路**：(a) `PIL.ImageGrab`（等价 `CopyFromScreen`）；(b) 仓库 `background_capture.capture_window()` 的 `PrintWindow(PW_RENDERFULLCONTENT)`；(c) `winapp ui screenshot` |
| 证据 | 【已运行】Notepad 980×840 窗口：ImageGrab → 1 146 130 B、92 972 种颜色；同窗口 `background_capture` → `{'printWindowReturned': True, 'blank': False, 980×840}`、265 种颜色；`winapp ui screenshot -a notepad --json -o …` → `width 3402 / height 1489`、50 385 B 文件落地 |
| 缺口 | `PrintWindow` 抓 **WinUI 浮出层**确实不行，而且是可量化的：【已运行】对 Notepad 的 Flyout 弹出窗（`Microsoft.UI.Content.PopupWindowSiteBridge`，253×463）`PrintWindow` 出图 35 色、**单一底色占 82.8%**；同矩形 ImageGrab 出图 11 350 色、底色仅占 2.4%。这与 `AGENTS.md:142` 的判断一致，但把"一张空白图"变成了数字 |
| 结论 | 无需安装任何东西。规则应是：**窗口级用 PrintWindow（可在屏外，不抢焦点）；浮出层/视频合成内容必须 ImageGrab，且必须先确保窗口可见** |

### 2.2 UI 自动化

| 问题 | 回答 |
|---|---|
| 覆盖了吗 | **覆盖，两套**：Python 侧 `uiautomation` 2.0.29（仓库 63 个脚本全用它）；CLI 侧 `winapp ui`（22 动词，含 `touch`/`pen`/`record`/`wait-for`） |
| 证据 | 【已运行】Notepad 树 50 个元素、深度 4；`DocumentControl` 的 `ValuePattern` 与 `TextPattern` 均可用；`winapp ui inspect -a notepad --interactive --json` 返回 `windowDpi: 168`、`scale: 1.75`、`coordinateSpace: physical-screen-pixels`、21 个交互元素；`winapp ui invoke` 成功点到 `mnu-file-5d42` |
| 缺口 | 无能力缺失。真正的坑是两套栈的**坐标语义不同**（见 2.5），以及 `winapp ui screenshot --capture-screen` **强制前台**：【已运行】非前台时直接返回 `{"code":"foreground_not_target"}` 且不落文件 |
| 结论 | 无需安装。`mvpw.exe` 的 UIA 可达性已由 Lead 测过（72 元素、导航 4 项、传输 3 项），本轮不重跑 |

### 2.3 进程 / 窗口检查

| 问题 | 回答 |
|---|---|
| 覆盖了吗 | **覆盖，三层**：`psutil` 7.2.2（进程表）；`ctypes` + `user32`（`EnumWindows`/`GetWindowRect`/`GetClassNameW`/`IsWindowVisible`）；`winapp ui list-windows`（带 owner 关系与 `window`/`popup` 标签） |
| 证据 | 【已运行】`psutil.process_iter` 拿到 Notepad pid；`EnumWindows` 枚举出 5 个顶层窗口，其中 4 个是 `GDI+ Hook Window Class` / `MSCTFIME UI` / `IME`（均不可见）—— 这正是 `mpvw_tools._main_window` 不用"第一个窗口"而按类名精确匹配的理由；`winapp ui list-windows -a notepad` 明确标出 `popup, owner: HWND 200096` |
| 缺口 | 无。`mpvw_tools` 里 `_pid()`、`_pid_alive()`（`GetExitCodeProcess == 259`）、`close_app()`（`GetWindowPattern().Close()` 再等 pid 真死）已经比通用库更贴合本应用 |
| 结论 | 无需安装 `pywinauto`：它的价值是窗口定位与控件包装，而这两件事 `uiautomation` + 30 行 ctypes 已经做完，且仓库代码里写满了本机实测过的坑（HANDLE 截断、`psutil` 进程表缓存滞后）。再叠一层 UIA 栈只会引入第二套选择器与 DPI 语义 |

### 2.4 日志捕获

| 问题 | 回答 |
|---|---|
| 覆盖了吗 | **部分覆盖**。应用日志可读、可增量；**崩溃报告这一层完全空白** |
| 证据（日志） | 【已运行】活目录是 `%LOCALAPPDATA%\mpv-winui\mpv-winui\logs`：`mpv-winui.2026-10-26.log.txt` 4.27 MB、末行时间 23:27:46，本轮以"记字节偏移 → 再取长度"的方式证明可做**只读增量**捕获（delta 0）。仓库侧有 `tools/ui-repair/scan-app-log.py`（`--baseline` / `--since`，写入 `_work/nightly/app-log.md`）与 `verify_playback.py:59` 的 `LOGDIR`，两者都指对了双层目录 |
| 证据（修正） | 【已运行】`AGENTS.md:185-186` 把 `%LOCALAPPDATA%\mpv-winui\logs` 称为"decoy，最新文件 2026-09-25"，实测该目录下的 `display-info.log` **在 2026-10-26 23:24:53 仍被写入**（209.7 KB）。结论应改为"该目录只承接 `display-info.log`，NLog 正文不在那儿"，而不是"过期目录" |
| 缺口（真缺口） | 【已运行】崩溃侧：本机 `C:\ProgramData\Microsoft\Windows\WER` 下有 **19 份 mpvw 的 `Report.wer`**（2026-10-24 十二份、2026-10-25 七份；异常码 `c0000005` ×12、`80004003` ×7），而**全部 27 份报告里 `P<n>=` 字段数为 0**；`Application` 日志最近 200 条里没有任何 mpvw 相关记录。也就是说 `AGENTS.md:168` 让人"先看 P7"，在这台机器上**这条路走不通**。仓库里也没有任何脚本读 WER 或事件日志（`git grep` 全空） |
| 缺口（工具） | 【已运行】调试器：`cdb.exe` / `windbg` / `procdump` 均不在 PATH；`C:\Program Files (x86)\Windows Kits\10\Debuggers\x64` 只有 `dbghelp/dbgcore/symsrv/srcsrv` 四个 DLL，**没有可执行调试器**。已有 `dotnet-dump 9.0.661903`（`~\.dotnet\tools`） |
| 结论 | 日志捕获**不缺工具**，缺的是一条"日志之外的崩溃证据"路径。`dotnet-dump` 已在，但对 `0xc000027b` + `Microsoft.UI.Xaml.dll` 这类 stowed exception，缺的是 **cdb + 符号**，装它需要 UAC → 本轮**明确说需要、不装**（见 §5） |

### 2.5 三处会咬人的环境事实（每条都实测过）

| 事实 | 测量 | 对工具的影响 |
|---|---|---|
| UIA 矩形不是 DIP | 【已运行】同一 Notepad 窗口：DPI-unaware 进程 `GetWindowRect` = `980×840 @ (220,220)`；导入 `uiautomation`（它在 `uiautomation.py:2958` 把进程设为 PerMonitorV2）后 `BoundingRectangle` = `1715×1470 @ (385,385)`；比值 **1.75**。`GetScaleFactorForMonitor` = 175、`GetDpiForWindow` = 168 | 所有几何断言必须除以实测 scale。**`AGENTS.md:202` 写的"×1.5"已过期**；仓库自己 `tools/ui-repair/scan.py:19` 记的是 168 / 1.75，与本次测量一致 |
| 进程 DPI 感知**取决于是否 import** `uiautomation` | 【已运行】纯 `ctypes` 脚本 `GetProcessDpiAwareness = 0`（unaware）；只要 `import uiautomation`，进程被改成 PerMonitorV2 | 同一个仓库里，混用两种脚本量同一控件会得到 1.75 倍的差；截图前必须知道当前进程属于哪类 |
| WinUI 浮出层是**独立顶层窗口** | 【已运行】Notepad 打开"文件"菜单后，`winapp ui list-windows` 多出 `popup, 443×811, owner: HWND 200096, [Microsoft.UI.Content.PopupWindowSiteBridge]` | "截主窗口"永远拍不到 Flyout；`-a` 的合成截图会把 4 个窗口**并列**贴出来（6009×1489），不是把浮层叠在窗口上 |

---

## 3. 对仓库自带 `tools/mvpw-mcp/` 的判断

### **结论：够用。不改写、不替换；但必须把它接上电。**

不是"看起来还行"，有四条正面事实：

1. **协议层是真的**，不是自称。【已运行】手工 stdio 跑通 `initialize` → `tools/list`（8 个工具）→ 失败路径（无 app 时 `isError`）→ `-32601` → 干净退出 0。`mcpw_server.py:145-197` 的 `handle()` 会区分 `TypeError`（参数错，回 `isError`）与其它异常（打 traceback 到 stderr 再回 `isError`），日志一律走 stderr 以免污染流。
2. **工具层里存的是本机实测知识，不是通用封装**。`mpvw_tools.py:78-97` 声明了 `CreateFileW` 的 `restype=HANDLE`（否则句柄被截成 32 位，症状是"管道只服务一次请求"）；`:35-76` 记录了五种"不抢焦点地启动"尝试各自失败的原因，最终落到 `MVPW_NO_ACTIVATE=1` + `SetWindowPos(-32000,-32000)`；`:357-397` 解释了为什么必须按 `WinUIDesktopWin32WindowClass` 精确匹配而不能取"第一个窗口"。这些是换任何现成库都买不到的东西。
3. **验证器有"能变红"的证明**。【已运行】`verify_e2e.py --self-test` 与 `verify_screenshot.py --self-test` 都输出 `SELF-TEST PASSED: verdict plumbing reaches a FAIL`、退出 0。机制上：23 个 `verify_*` 里 **12 个**同时具备 `--self-test` 与 `VERDICT:` 契约行，而 `tools/ui-repair/gate.py:234-250` 接的正好是这 12 个，`:197-220` 会同时检查"VERDICT 与退出码一致"和"self-test 必须 PASSED"。
4. **失败模式是显式的**。`read_playback_state` 在拿不到启动快照时**抛错**而不是返回一串 null（`mpvw_tools.py:748-758`），注释直接写明"上一版返回全 null，`verify_e2e` 把它当 PASS"。

但它有三处**结构性缺陷**，且都不是"能力不足"，是"接线不足"：

| # | 缺陷 | 证据 | 影响 | 修法（配置级，不是重写） |
|---|---|---|---|---|
| D1 | 部署目录硬编码 | `mpvw_tools.py:31` | 换机器/换部署目录即全盘失效；本轮 Lead 正在重建 `mpv-deploy`，任何并发运行都可能踩到半套二进制 | 改读环境变量，缺省才回落到现路径 |
| D2 | 未注册进 dsh | 【已运行】仓库无 `.mcp.json`；`profiles/desktop/cordis.patch.yml` 无 `dsh-mcp-client` 条目；运行时确实**自带** `@deepseek-ai/dsh-mcp-client 0.2.1-rc.3`（asar 内） | 63 个脚本只能靠人手 `python …` 调用；`AGENTS.md` 里给的注册写法是 **Claude Desktop 的 `mcpServers`** 语法，dsh 不吃这一套 | 二选一：(a) 项目根放 `.mcp.json` —— 但 compat 的项目根靠 `.git` 标记向上查找，而**本会话 cwd 是 `Documents\DeepSeek`，其本身与各级祖先都没有 `.git`**【已运行】，必须把 `mpv-clean` 作为 DSH 工区打开才生效，且 `.mcp.json` 只在 DSH 启动时读一次；(b) 在 profile 里加一条 `name: '@deepseek-ai/dsh-mcp-client'`，与本会话 cwd 无关，更稳 |
| D3 | 没有任何自动化入口 | 【已运行】`.github/workflows/build.yml` 只有 **6 个** Python 检查步骤（`check-localization`、`check-settings-drift`、`check-xaml`、`gen-schema-options --check`、`check-winrt-events`、`check-script-opts`）+ 2 个测试工程，**不含** `mvpw-mcp`、也不含 `gate.py`；`Get-ScheduledTask` 231 条里无一条引用 `mvpw-mcp` / `ui-repair` / `mpv-clean` | `gate.py` 是个好门禁，但门禁不开；"检查器从没红过"这条仓库自己的红线，对 63 个脚本中的 51 个仍然成立 | 最低成本：CI 增加一步 `python tools/ui-repair/gate.py`（它自己会做 build/deploy，本轮不碰） |

另有两处**文档与事实不符**，值得顺手记下：

- `PROJECT_BASELINE.md:27` 与 `S5`（`:136`）都说"60 个验证器"。真实是 **63 个 `.py`，其中 `verify_*` 23 个**（§1.5）。
- `gate.py:11` 的注释说"the 17 check-*.py"，`tools/` 根目录实际是 **25 个**（`ls tools/check-*.py`）。

---

## 4. WinUI 官方插件（`win-dev-skills/winui` 0.7.1）结论

### 4.1 能被调用吗？怎么调？

**能，走 `dsh-claude-compat` 的插件通道，按名字用 `skill` 工具调。**

机制链条（每一环都有证据）：

1. `cordis.patch.yml:156` 的 `enablePlugins: true` ——这是唯一开关，注释写明"设成 false 会把它们一起关掉（已实测）"【代码】。
2. compat 从 `~/.claude/plugins/installed_plugins.json` 读安装项，再读插件清单的 `skills` 字段（`"./agent-plugin/skills/"`），把 7 个 `SKILL.md` 注册成 dsh skill；`pluginSkillRank: 750` 是"长尾"，任何同名冲突都不会被插件覆盖【代码 `dsh-claude-compat/src/index.js:96` 与 README 配置表】。
3. `agents/` 目录下的 `winui-dev.agent.md` 被 `src/agents.js` 转成"委派 shim skill"，所以它会以 `winui-dev` 这个名字出现在目录里【代码 `src/plugins.js:99-101`】。
4. **本轮实测**：`skill(winui-code-review)` 返回正文，resource base 为 `...\0.7.1\agent-plugin\skills\winui-code-review`；`skill(winui-ui-testing)` 同样成功；`skill(winui-dev)` 返回正文，resource base 为 `...\0.7.1\agents` —— 两条发现路径都被验证【已运行】。

**注意**：技能目录在**会话创建时**快照（compat README「Catalog 快照时机」）。所以"装一个新技能"在本会话内不会出现，必须 `/reload-cc-plugins` 或新开会话。

### 4.2 对本项目的 UI 工作具体帮上什么

按"可检查"的标准列，全部在本轮实跑或实读：

| 具体能力 | 在本仓库的落点 | 证据 |
|---|---|---|
| `winapp find-api --project-dir mpv-winui\mpv-winui` 定 API 事实 | 直接把"分析器说的"和"SDK 真有"分开 | 【已运行】`find-api stats` → 19 包 / 52 876 类型 / 332 961 成员 / 38 winmd；`find-api members Microsoft.UI.Xaml.Controls.Pivot` → `totalProperties: 308`，与 `docs/ui-repair/QUEUE.md:82` 记录一致；`find-api check-property NavigationViewItem AutomationId` → **❌ 不存在**，退出码 1，并给出替代 `AutomationProperties.AutomationIdProperty` —— 这正是 `AGENTS.md:121-123`"`NavigationViewItem.AutomationId` 编译不过"的机器证明 |
| `winapp find-ui` 查真实控件用法 | 写新 XAML 前先拿官方 Gallery 片段，而不是凭记忆 | 【已运行】`winapp find-ui "navigation view"` → `gallery-navigationview-1`；corpus 本地有缓存，断网可用 |
| 分析器规则族（WUI0xxx–WUI4xxx）与既有告警对照 | 本仓库**已经**引了同一个包 0.7.1 | 【代码】`mpv-winui.csproj:81` + `:67-80` 的注释：首次运行 214 条、且"它发现的第一件事是自己的一条规则是错的（WUI2003 说 Pivot 是 UWP-only，而 find-api 报 308 成员）"。也就是说插件文档里的"加分析器"一步，本仓库**已完成并已收获过收益**。旁证（本轮实测）：`<Pivot` 出现在 1 个文件、9 处（`PlayerTrackSelectorControl.xaml:20` 起的 1 个 Pivot + 8 个 PivotItem），而 WUI0xxx 的目标（`using Windows.UI.Xaml` / `CoreDispatcher` / `GetForCurrentView` / 真正的 `Window.Current`）在 `*.cs` 里命中 **0**——这套规则在本仓库的可行区间已经收窄到 WUI2xxx 的绑定类 |
| `winapp ui` 的动词映射到本仓库的 UIA 需求 | 导航/传输控件的断言可用 `wait-for --value`、`invoke`、`-p IsEnabled` 表达，比手写轮询稳 | 【已运行】`invoke` / `list-windows` / `inspect` / `screenshot` 四个动词实跑成功 |
| `winui-dev-workflow` 的崩溃诊断段 | `--debug-output` 会对 WinUI 崩溃做 stowed-exception 归因，正是本仓库 `0xc000027b` 那一类 | 【代码】SKILL.md:53-55。但需要调试器，本机没有（§2.4） |

### 4.3 哪些用不上、为什么（按技能拆）

| 技能/部分 | 死重 | 仍有用的部分 |
|---|---|---|
| `winui-wpf-migration`（111 行） | **整份**：`git grep -l 'System\.Windows\.' -- '*.cs'` = 0 个文件，已 100% 是 WinUI 3 | 无。它的 Step 1 是"盘点 WPF API"，盘点结果是零 |
| `winui-setup`（170 行） | **安装部分整份**：`winapp 0.7.1` 已在、.NET SDK 已能构建 `net10.0-windows10.0.26100.0`【已运行 `winapp --version`】 | "Detect everything"的探测思路可留作环境自检；但本仓库的构建前置（`libs\lib\<Platform>`、`CheckNativeLibraries`）它完全不知道，真缺库时它帮不上 |
| `winui-packaging`（102 行） | **MSIX / 签名 / 证书 / Store 部分整份**：本轮不产 MSIX，部署是 `mpv-deploy` overlay | `references/sourcegen-patterns.md`（AOT/裁剪下 JSON 与运行时绑定的源生成）与"Self-Contained 不等于单文件"——本仓库是 `PublishAot + PublishTrimmed + SelfContained + WindowsAppSDKSelfContained`，这两点仍然对 |
| `winui-dev-workflow`（96 行） | **"Create or Open a Project"的新建路径**（`winapp new --template winui-mvvm`）与"`dotnet add package WinUIAnalyzer`"：项目已在，分析器已引 | 崩溃诊断段；`winapp run . --detach --json` 的 JSON 字段（`ProcessId`/`ProcessScope`）在需要"启动并交回 pid"时有用；常见错误表里 `0x80073CF9` 一条对深路径项目有直接价值 |
| `winui-dev`（agent，26 行） | 作为**常驻角色**用不上：它的第一件事是"加载 workflow 和 design"，而这两份的可用部分本轮已经读完了 | 它的 Best Practices 有一句是对的且本仓库已满足：`AutomationProperties.SetAutomationId` 是唯一能设 id 的 API（`MainWindow.xaml.cs:237` 就是这么写的） |
| `winui-design`（189 行） | App-shape 表把"媒体/画布/hero"判为**不要 NavigationView**，与本应用 4 段导航冲突；窗口尺寸经验法则（无 `SizeToContent`、默认约 1024×768）对本仓库不适用（窗口尺寸在 `MainWindow` 里自己算） | `find-api` / `find-ui` 的"写之前先查"纪律；控件选型表（WinUI 无 `DataGrid` → 用 `ListView` + `Grid` 模板）对媒体库这类表格化列表有直接指导 |

### 4.4 已知落差（必须处理，不能绕）

| 技能里的前提 | 本应用的实际 | 该怎么用 |
|---|---|---|
| `winui-ui-testing` 的模板假设"每个交互控件都有 `AutomationId`"，其 `App controls have AutomationIds` 用例会对缺 id 的控件直接 `throw` | 【已运行】XAML 里 `AutomationProperties.AutomationId` = **0** 处、`x:Name` = **716** 处、C# 里 `SetAutomationId` = **1** 处（`MainWindow.xaml.cs:237`，`ShellNav_<key>`）。仓库 `audit_accessibility.py` 的记录解释了原因：**WinUI 在未设时用 `x:Name` 派生 AutomationId**，所以运行时可见 56/36 个 id，而 XAML 里声明的是 0。另外 `AGENTS.md:116`：**`Grid` 不是 UIA 节点**，看得见的容器可能没有元素 | 把该用例改成两条：(1) 断言**子元素**而非容器（`wait-for` 打子控件 id）；(2) 无障碍审计改成"有 id 的元素中有多少带 `Name`"（仓库 `scan-a11y-names.py` 已在做，且 `AutomationProperties.Name` 只有 35 处）。模板原样的"缺 id 就 FAIL"会产出 ~119 个 Button 规模的假阳性 |
| 模板用 `winapp ui screenshot`，浮层靠 `--capture-screen` | 【已运行】`--capture-screen` **必须前台**，非前台直接 `foreground_not_target` 且不落文件；而本仓库的第一条纪律是"不得抢前台"（`mvpw_tools` 用 `MVPW_NO_ACTIVATE=1` + 把窗口停到 `-32000,-32000`）。`PrintWindow` 抓 Flyout 会得到 82.8% 单一底色 | 截图分两类：**窗口级**用 PrintWindow（可在屏外、不抢焦点）；**浮出层/视频合成**必须把窗口临时请回屏幕并 `CopyFromScreen`，且必须在 `finally` 里复位。`winapp` 的 `-a` 合成图只适合"记录有哪些窗口"，不适合当浮层像素证据 |
| 模板不提 DPI | 【已运行】UIA 矩形是**设备像素**，本机 scale = **1.75**（`GetDpiForWindow` 168、`GetScaleFactorForMonitor` 175）；`AGENTS.md:202` 的"×1.5"是旧值 | 所有几何断言先除 scale；`winapp ui inspect --json` 已经把 `windowDpi`/`scale`/`coordinateSpace` 打在输出里，应当直接读它而不是自己猜 |
| 模板默认 `--on sandbox` | 【已运行】`winapp target snapshot sandbox --json` → `running: false`、`desktop.unavailable: "sandbox_target_stale"`；而且本项目的验证对象是**宿主上的 `mpv-deploy` + `\\.\pipe\mpvpipe`**，沙箱里没有这条管道 | 固定 `-Target local`（模板参数本来就支持），不要为了"更隔离"把验证搬到沙箱——那会连被测对象一起搬走 |

### 4.5 结论：**部分用**

- **用**：`winui-code-review` 的骨架（但删掉 MVVM/全局化两组，见下）、`winui-ui-testing` 的**流程与脚本骨架**（`Invoke-WinAppChecked` / `Test-UI` / `Save-Screenshot` / `yield` 收尾，加上 4.4 的四条改写）、`winui-design` 的 `find-api`/`find-ui` 纪律与控件选型表、`winui-dev-workflow` 的崩溃诊断段与错误表里与本仓库对得上的那几行、`winui-packaging` 的 AOT/源生成那一页。
- **不用**：`winui-wpf-migration`（整份）、`winui-setup` 的安装部分、`winui-packaging` 的 MSIX/签名/Store 部分、`winui-dev-workflow` 的新建项目路径、`winui-dev` 作为常驻角色、`winui-design` 的 App-shape 判定与窗口尺寸法则。
- **用之前必须改写的清单项**：`winui-code-review` 的 MVVM 组（本仓库 **0** 处 `ObservableObject`/`RelayCommand`、**0** 个 `CommunityToolkit` 引用，是刻意的 code-behind 架构）与全局化组（**0** 个 `.resw`、**0** 处 `x:Uid`，本地化走 `AppLang` + `Languages/*.json` 九种语言、`en-US.json` 1 526 键）。这两组照抄会产出成百条与设计相悖的"问题"。

---

## 5. 还应当装什么（只列有真实用途的）

| 优先级 | 项目 | 装/不装 | 为什么（绑到本仓库的真实缺口） |
|---|---|---|---|
| 1 | **把 `mvpw-mcp` 接到 dsh**（配置，不是安装） | **做** | 63 个脚本、23 个验证器、12 个自带 self-test、一个现成的 `gate.py`，现在**没有任何调用入口**（§3 D2/D3）。零新增依赖：运行时已自带 `dsh-mcp-client 0.2.1-rc.3`。落地位置优先选 **profile 里加一条 `dsh-mcp-client`**（与 cwd 无关、不依赖 `.git` 标记，见 §3 D2 的实测约束）；项目根 `.mcp.json` 只在"Dsh 以 `mpv-clean` 为工区打开"时才可用。这是本轮"工具清单"里唯一投入产出比明确为正的一项 |
| 2 | **`windbg` 插件（`win-dev-skills` 市场里的第二个插件，11 个技能）** | **建议装，本轮不装** | 唯一能补上 §2.4 那个空白：本机 19 份 mpvw 的 WER 报告**没有任何 `P<n>=` 字段**，而 `AGENTS.md:168` 的崩溃流程第一步就是"先看 P7"；同时 `cdb.exe` 不存在。`windbg-user-exception-triage` / `windbg-diagnostic-method` 正对 `0xc000027b` + `Microsoft.UI.Xaml.dll` 这一类。**不本轮装的原因**：它是纯内容插件（无 admin、很轻），但技能目录在会话创建时快照，装了本轮也用不上，而且它改的是用户级全局配置，不适合由子代理在别人的会话里动 |
| 3 | **`cdb.exe`（Debugging Tools for Windows 的 user-mode 调试器）** | **需要，但装不了** | 与 2 配套：没有调试器，`--debug-output`、`!analyze`、stowed exception 的托管栈都无从谈起；`dotnet-dump` 已在，但它读不了 `0xc000027b` 的原生转储上下文。安装需要 UAC（Windows SDK 组件）→ **按约束停在这里，报告需求** |

**明确不装，以及理由**：

| 不装 | 理由 |
|---|---|
| `pywinauto` | 【已运行】确认缺失，但不该补：`uiautomation` + `winapp ui` 两套已经覆盖，且 `mpvw_tools.py` 里那些"第一个窗口是错的""句柄被截成 32 位"的坑是踩出来的；再叠一层只会引入第二套选择器/DPI 语义 |
| `pyautogui` / `mss` | 合成输入由 `winapp ui` 的 `send-keys`/`touch`/`pen`/`drag` 覆盖且带目标作用域；抓屏 `ImageGrab`（CopyFromScreen 同族）+ `PrintWindow` 已够，`mss` 只快不新 |
| `pytest` | 这 63 个脚本是**GUI 驱动**而不是单元测试，真正的门禁语义（`VERDICT` 与退出码一致 + `--self-test` 必须能红）是 `gate.py` 自定的，比 pytest 默认的"退出码即结论"更严。引入 pytest 不解决仓库自己写的红线："从没红过的检查器不算证据" |
| Windows Sandbox 启用 | 需要 admin，且会把被测对象搬走：本项目的验证对象是宿主上的 `mpv-deploy` 与 `\\.\pipe\mpvpipe`，沙箱里两者都没有（§4.4 末行实测 `sandbox_target_stale`） |
| Office / 设计 / 写作类技能 | 与本仓库的构建、部署、UI 验证无交集 |

---

## 6. 本轮未确定的事项（如实列出）

1. **开发者模式（Developer Mode）状态未查**：`winui-setup` 会查，但本轮不需要打包/注册，故未测。
2. **`winapp ui screenshot -a` 的合成图里，Flyout 面板像素是否与屏幕一致未逐像素比对**：只测了"合成图把 4 个窗口并列贴出"（6009×1489）与"单窗口 PrintWindow 抓 Flyout 只有 35 色"这两件事。
3. **12 个 `gate.py` 验证器在本轮构建上的实际通过情况未测**：它们都要启动 `mpvw.exe`，本轮明令不启动；Lead 已单独测过 UIA 可达性。
4. **`mvpw-mcp` 经 `dsh-mcp-client` 挂载后能否正常握手未验证**：需要改配置并重启 dsh，超出本轮范围；协议层本身已用 stdio 手工验证（§3）。
5. **`AGENTS.md:202` 的"×1.5"与实测 1.75 的冲突**：只确认了实测值与仓库自己的 `scan.py:19` 一致，没有追溯这条 1.5 是何时、在哪台显示器上测的。
