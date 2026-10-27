# 工具与插件：主动寻源与评估（V2）

> 这份文件是对 `TOOLING.md` 的**重做**，不是补充。
> 上一版的问题是：只盘点了已经装上的东西，等于把"现状"当成了"结论"。
> 用户的原话是「不要只检查当前已有的工具插件，需要主动寻找并评估更多」。
>
> 日期：2026-10-27。证据等级：【已运行】/【代码】/【推断】/【未知】。

---

## 0. 一句话结论

| 问题 | 答案 |
|---|---|
| 官方市场里有多少可装的？ | **39 个**（`claude-plugins-official`）+ **3 个**（`win-dev-skills`：`windbg`、`winui`、`winappcli`）。当前只装了 `winui` 一个 |
| 长任务规划该用什么？ | **dsh 自己的 `create_goal` + `agent-teams`**。`ralph-loop` 与 `planning-with-files` 的自动化部分在本机**都不工作**，原因见 §2 |
| 资源整理该用什么？ | **`git remote` + `Get-ChildItem` + 一个脚本**，没有哪个 MCP 比它更好。理由见 §3 |
| 还有必要装什么？ | **1 个真正有用**：`csharp-lsp`（已装并验证）。`windbg` 已装但对本项目不可用（它面向 native/kernel，明确排除 WinUI/XAML 与 managed .NET）。其余 37 个与本项目无关 |

---

## 1. 主动寻源：市场里到底有什么

### 1.1 两个已配置的 marketplace

`C:\Users\z2637\.claude\plugins\known_marketplaces.json`【代码】：

| 名称 | 来源 | 提供插件数 |
|---|---|---|
| `claude-plugins-official` | `github: anthropics/claude-plugins-official` | **39** |
| `win-dev-skills` | `github: microsoft/win-dev-skills` | **3**（`windbg` 本地；`winui`、`winappcli` 从 `microsoft/winappCli` 的 git 子目录装）。已装：仅 `winui` 0.7.1 |

### 1.2 `claude-plugins-official` 的 39 个，逐类判定

不是把 39 个都列一遍——**只列与本项目有关的，以及明确排除的**。

**与本项目相关：**

| 插件 | 用途 | 判定 | 理由 |
|---|---|---|---|
| `csharp-lsp` | C# 语言服务 | **装** | 这是 32.8 万行 C# 项目（`mpv-winui/mpv-winui` 328 个 `.cs`）。跳转/引用查找目前靠 `grep`，而这个仓库的文档反复警告"六个检查器在错误的半棵树上通过"——结构性导航能减少这类错误 |
| `code-review` | 多 agent PR 审查 + 置信度打分 | **观望** | 它面向 PR 工作流；本项目是本地单线提交，没有 PR。若以后走 PR 再用 |
| `code-simplifier` | 简化代码、保持功能不变 | **观望** | 与"最小改动"原则冲突的风险高；本项目当前阶段不做重构 |
| `commit-commands` | 简化 commit/push/PR 命令 | **不装** | 无收益：`git commit -F <file>` 已经够用，而且本仓库对 commit message 有具体的风格要求（小写类型前缀），包装反而碍事 |
| `claude-md-management` | 审计并维护 `CLAUDE.md` | **参考，不装** | 它的思路可用——但本项目的对应物是 `AGENTS.md`，且本轮已经手工修正了其中两条与实测冲突的规则。装它不会自动知道本项目的实测结论 |
| `mcp-server-dev` | 指导设计 MCP 服务器 | **不装** | 只在"要写新 MCP"时才需要；`mvpw-mcp` 已判定够用 |
| `skill-creator` | 创建/优化技能 | **观望** | 若要把本项目的 gate 流程固化成技能，这个有用。当前尚未到那一步 |
| `plugin-dev` | 插件开发工具包 | **不装** | 同上，不是现在的需求 |
| 其余 31 个 | `*-lsp`（clangd/gopls/jdtls/kotlin/lua/php/pyright/ruby/rust/swift/typescript）、`frontend-design`、`math-olympiad`、`agent-sdk-dev`、`claude-security`、`hookify`、`session-report`、`receipts`、`playground`、`cwc-makers`、`example-plugin`、`explanatory-output-style`、`learning-output-style`、`feature-dev`、`project-artifact`、`mcp-tunnels`、`pr-review-toolkit`、`code-modernization` | **不装** | 语言不匹配，或面向与本项目无关的工作流 |

### 1.3 `ralph-loop` 与 `project-artifact`：看着最对口，实测都不适用

这两个是本轮最像"长任务规划"的候选，所以单独说明。

**`ralph-loop`**（"Ralph Wiggum technique"，循环喂同一个 prompt 直到完成）【已运行：读了 `hooks/stop-hook.sh` 与 `README.md`】：

- 机制是 **bash Stop hook** 拦截退出
- 状态文件在 `.claude/ralph-loop.local.md`
- 本机 `claude-compat.enableHooks = false`，且 `userClaudeDir` 已指向不存在的 `.dsh/.claude-detached`
- **结论：不装。** 它要求 Claude Code 的 hook 机制，dsh 里不会触发。而 dsh 有原生等价物：`create_goal`（就是本会话正在用的机制，`max_goal_rounds` 上限、跨轮持续、`update_goal` 标记完成）

**`project-artifact`**（生成项目状态页）【代码：读 `.claude-plugin/plugin.json`】：

- 需要 **claude.ai 登录 + 内置 Artifact 工具**，发布到 `claude.ai` 页面
- **结论：不装。** 本项目的状态页是 `docs/alignment/` 下受 git 管理的 markdown（`ISSUE_LOG.md`、`HISTORY.md`），发布到外部网页反而脱离版本控制

---

## 2. 长任务规划：装着的两个技能，自动化部分实际不工作

这是本轮最需要你知道的一条。技能**在**、模板**在**，但让它们"自动生效"的那部分被上一轮的配置关掉了。

### 2.1 `planning-with-files`

`C:\Users\z2637\.dsh\skills\planning-with-files\SKILL.md`【代码】声明了 4 类 hook：

| hook | 本该做什么 | 实际 |
|---|---|---|
| `UserPromptSubmit` | 每次提交时把 `task_plan.md` 当前状态注入上下文 | **不执行** |
| `PreToolUse` | 工具调用前读 `task_plan.md` 前 30 行 | **不执行** |
| `PostToolUse` | 写文件后提醒更新 `progress.md` | **不执行** |
| `Stop` | 退出前跑 `check-complete.ps1` 判断是否完成 | **不执行** |

原因：`profiles/desktop/cordis.patch.yml` 里 `claude-compat.enableHooks = false`【代码】。上一轮的注释写明了理由——"规则注入与 hooks 只为 Claude Code 场景服务，关闭"。

**还有第二层断裂**：即便打开 hooks，它的 `Stop` hook 去 `~/.claude/plugins/cache` 找 `check-complete.ps1`，而这个脚本实际在 `~/.dsh/skills/planning-with-files/scripts/` 下【已运行：两处 `Test-Path`，`.claude` 下找不到、`.dsh` 下有】。技能被迁移到了 `.dsh`，hook 里的路径没跟着改。

**剩下能用的**：三份模板（`task_plan.md` 4745 B、`findings.md` 3656 B、`progress.md` 4115 B）和 `session-catchup.py`（15.9 KB，需手动跑）。也就是说——**它退化成了"三个 markdown 模板"**，这仍然有用，但不要指望它自动注入上下文。

### 2.2 `handoff` / `claude-handoff`

`C:\Users\z2637\.dsh\skills\handoff\SKILL.md` 只有 **880 B**【代码】，内容是"把当前对话总结成交接文档，**保存到操作系统的临时目录**"。

- 保存到临时目录 = 重启就没了，跨会话恢复不了
- 它明确要求"不要重复已经在别处的内容，用路径引用"——这一点是对的
- **结论：思路可用，位置不适用。** 本项目的交接文档应该在仓库里（`docs/alignment/ISSUE_LOG.md` 就是），不在 temp

### 2.3 真正在工作的长任务机制

| 机制 | 状态 | 证据 |
|---|---|---|
| dsh `create_goal` / `update_goal` | **在用**，就是本轮会话的驱动机制 | 本会话 `goal-dcc0f1ee...`，`maxGoalRounds: 256`，跨轮持续 |
| dsh `agent-teams` | 可用，本轮用了 4 个 teammate | 本会话 `history-indexer` / `tool-auditor` / `tool-scout` / `winui-official` |
| dsh `task-board` | **可用但空置** | `~/.dsh/task-board/ledger-v2.json`：`"tasks": []`【已运行】。机制在，从未用过 |
| `planning-with-files` 模板 | 可手动用 | 三份模板存在且完整 |

**建议**：长任务用 `create_goal`（已在用）+ 把 `task_plan.md` 那套模板**复制进仓库**（而不是留在 `.dsh/skills` 里当摆设），由 `ISSUE_LOG.md` 承担 `progress.md` 的角色。不需要装任何新东西。

---

## 3. 资源整理：没有比自己写脚本更好的

用户这次给的任务本身就是一次资源整理：86 个路径、15 个候选目录、23.3 GB、判断每个"留还是删"。

### 3.1 实测各候选

| 候选 | 能做什么 | 判定 |
|---|---|---|
| 任意 `filesystem` MCP | 读写列目录 | **不装**。`Get-ChildItem` + `git` 已经够，多一层进程和权限 |
| 任意 `memory`/`knowledge-graph` MCP | 存结构化记忆 | **不装**。本项目的"记忆"是 `docs/alignment/` 下受 git 管理的文件，这是优点不是限制 |
| `Everything`（已装，非 MCP） | 全盘文件名索引 | **已在用**。本轮找 `mpv*` 目录就是靠它所在的系统索引能力；但要注意它在 `mpv\docs` 上造成过改名阻塞（上一轮 ISSUE_LOG I-10 记录，本轮已确认该目录可删不可改名） |
| `windbg` 插件 | 崩溃转储分析 | ~~装~~ → **已装但不可用**（见 §7 of VERIFICATION-2026-10-27.md） |

### 3.2 为什么这次的整理没用任何 MCP

因为**判断依据不是文件内容，而是 git 元数据**：

```
git remote -v                      # 这个目录是不是 mpv 仓库
git rev-list --count origin/main..main   # 有没有未推送的提交
git cat-file -e <sha>              # 它的 HEAD 是不是已经在主线里
git log --diff-filter=D            # 那些"独有"文件是不是主线主动删的
```

这四条决定了每一个"留/删"，没有一条是 MCP 能替代的。其中最后一条尤其关键：它把"3.4 GB 克隆里有 18 个独有 `.cs` 文件，所以不能删"这个**看似合理的错误结论**推翻成了"那 18 个都是主线证明过是死代码的东西"。

**结论：资源整理的正确工具是 `git` 的元数据查询，加上一个把判断固化下来的脚本。** 本轮用的脚本在
`_work/mpv-repos-20261027/inventory_repos.py`，判定表在 `docs/alignment/LEGACY-WORKSPACES.md`。

---

## 4. 官方 WinUI 工具：优先使用，但边界要说清

用户要求「优先使用 WinUI 官方提供的插件/工具完成检查与制作，避免依赖自写程序」。实测结果在 `docs/alignment/WINUI-OFFICIAL.md`（251 行），这里只给结论。

### 4.1 已经迁移的（2 项，已落地成脚本）

`tools/mvpw-mcp/verify-official.ps1` —— 用 `winapp ui` 重做上一轮 `verify_exe.py` 的读取断言：

| 断言 | 上一轮 | 现在 |
|---|---|---|
| 窗口标题 | 自写 UIA 遍历 | `winapp ui get-value ShellTitleText -a mpvw` |
| 4 个导航项 + 中文名 | 自写遍历 + `NAV` 字典 | `winapp ui inspect PaneRoot -d 6`（一次全出） |
| 3 个播放条 | 自写遍历 | `winapp ui get-property <id> -p Name` |
| 退出码可用于 CI | — | `wait-for` 在/不在 = 0/1【已运行】 |

**实测 9/9 断言通过，且注入错误期望值时能变红**（第一次失败路径是坏的，见 §4.3）。

### 4.2 必须保留自写的（4 项，各有硬理由）

| 自写物 | 为什么官方工具替代不了 |
|---|---|
| `diag_occlusion.py`（`WindowFromPoint` 判遮挡） | `winapp --cli-schema`（244450 字符）里 `occlusion`/`WindowFromPoint` **完全不出现**；`list-windows` 顺序**不是 z-order**（实测前台窗口排第 20 位）【已运行】 |
| `tools/check-xaml.py` | `winapp` 21 个顶层命令里没有 XAML 校验；`winapp package` 要先构建，做不了"构建前 1 秒拦截 WMC9997" |
| `tools/compare-deploy.ps1` | `target push/pull` 是 Windows Sandbox 专属且语义是"复制"不是"比对" |
| `verify_exe.py` 的"进程已退出"断言 | `wait-for --gone` 等的是**元素**消失，不是进程退出 |

### 4.3 写这个迁移时踩的两个坑（都已修，记在文件里）

1. **`{1!r}` 是 Python 语法**，PowerShell 的 `-f` 遇到它会抛异常 → 断言失败时**崩掉而不是打印**。注入错误期望值才发现。
2. **`--timeout-sec` 参数不存在**，正确的是 `-t/--timeout`（毫秒）。第一次测退出码"通过"是假通过——参数解析器拒绝了命令，不是断言成立。

### 4.4 插件评估：**部分用**

| 技能 | 用不用 | 具体 |
|---|---|---|
| `winui-ui-testing` | **用骨架** | 把 `uiautomation` 脚本换成 `winapp ui` 后，它模板里"AutomationId 齐全"的假设要改：本应用 `Grid` 不产生 UIA 节点，断言必须打到子元素（如 `PaneRoot` 下的 `ShellNav_*`） |
| `winui-design` | **用纪律** | `winapp find-api --project-dir` 查 API 形状，而不是凭记忆写 |
| `winui-code-review` | **部分用** | MVVM 与全局化两组不适用（本项目 `ObservableObject` 0 处、`x:Uid` 0 处）；无障碍组的前提失效（XAML 里 AutomationId 0 处，靠 `x:Name` 派生 716 处） |
| `winui-dev-workflow` | **只取崩溃诊断段** | "新建项目"路径无关 |
| `winui-setup` | **不用** | 环境已就绪 |
| `winui-packaging` | **不用** | 本轮不发 MSIX |
| `winui-wpf-migration` | **不用** | 本项目已是 WinUI 3；`System.Windows.` 命中 0 文件 |

---

## 5. 最终建议清单

### 建议安装（2 个）

| 名称 | 类型 | 装它的理由 |
|---|---|---|
| ~~`windbg`~~ | 插件 | **已装，但对本项目不可用** —— 实测 11 个技能全部面向 native C/C++ 或 kernel，`windbg-user-exception-triage` 明确写 "Not for managed .NET exceptions, WinUI/XAML app errors"，正是本项目唯一的崩溃类型；且硬依赖 `.exr`/`!analyze -v`，本机无任何调试器二进制。详见 `VERIFICATION-2026-10-27.md` §7 |
| `csharp-lsp`（官方市场） | 插件 | 328 个 `.cs` 文件，当前靠 `grep` 导航。本仓库有六次"检查器在错误的半棵树上通过"的记录，结构化导航能减少这类错误 |

### 明确不装及理由

- `ralph-loop`、`planning-with-files` 的 hook 部分、`handoff` —— Claude Code 机制，dsh 里不生效（§2）
- `project-artifact` —— 需要 claude.ai 登录，且会脱离 git 版本控制
- 31 个语言 LSP 与前端/数学类插件 —— 不匹配本项目`r`n- `winappcli` 插件 —— 它就是本机已在 PATH 上的 `winapp` CLI（0.7.1）。装插件不会多出能力，只会多一层跟版本走的壳；直接用 CLI 即可
- 任何 `filesystem` / `memory` MCP —— 不比自己写脚本更好（§3）

### 一个不需要装、但值得做的

把 `planning-with-files` 的三份模板复制进仓库的 `docs/`，让"计划/发现/进度"三件套变成受版本控制的文件，而不是躺在 `.dsh/skills` 里当无人调用的模板。

---

## 6. 未确定 / 需要你知道

- **`win-dev-skills` 的 `winappcli` 插件内容未读**：它对应本机已在用的 `winapp` CLI 0.7.1，我按"已具备"处理，没有去装它验证`r`n- **`claude-plugins-official` 里 31 个插件的细节没有逐个读**。判定依据是名称与一句话描述，够用来排除"语言不匹配"，但对 `code-modernization`、`pr-review-toolkit` 这类可能沾边的，只读了描述没读实现。
- **`csharp-lsp` 已装并实测可用**：`csharp-ls 0.28.0` 在本项目上 `hover`/`definition`/`workspace/symbol` 全部有真实返回（如 `string? MpvSettings.ToCommand(string key, object value)`）。`windbg` 已装但不可用，见上。
- **`task-board` 为什么空置**：`ledger-v2.json` 显示 `tasks: []`、`revision: 0`，机制新装未用。是有意不用还是不知道它存在，我无法从文件判断。
- ~~`winapp` 写侧 11 个动词未验证~~ → **已全部验证可用**，并发现三处用法陷阱（选择器歧义、`--type` 支持不一致、`record --capture-screen` 要求前台）。见 `VERIFICATION-2026-10-27.md` §1。
