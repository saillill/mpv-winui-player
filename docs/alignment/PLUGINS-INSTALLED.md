# 插件安装与生效状态（2026-10-27）

> 承接 `VERIFICATION-2026-10-27.md`。那次验证推出的结论是：`csharp-lsp` 有用、`windbg` 对本项目不可用。
> 这份记录实际安装的结果，以及**两个插件生效条件不同**这个容易踩的事实。

---

## 1. 安装结果

```
$ claude plugin list
  > csharp-lsp@claude-plugins-official   Version: 1.0.0   Status: √ enabled
  > windbg@win-dev-skills                Version: 1.0.0   Status: √ enabled
  > winui@win-dev-skills                 Version: 0.7.1   Status: √ enabled
```

安装前后备份：`~/.dsh/.backups/plugins-before-windbg-csharp-20261027/`
（含 `installed_plugins.json` 与 `cordis.patch.yml`）。

安装位置【已运行】：
- `~/.claude/plugins/cache/claude-plugins-official/csharp-lsp/1.0.0/`
- `~/.claude/plugins/cache/win-dev-skills/windbg/1.0.0/`（11 个技能目录）
- `installed_plugins.json` 三者齐全

---

## 2. `csharp-lsp`：装完还要装一个二进制，然后才真的能用

这个插件**本身不含语言服务**，只有 `LICENSE` + `README.md`。README 要求：

```bash
dotnet tool install --global csharp-ls
```

所以"装完插件"不等于"能用"。补装后【已运行】：

```
$ dotnet tool install --global csharp-ls
已成功安装工具"csharp-ls"(版本"0.28.0")。

$ ~/.dotnet/tools/csharp-ls.exe --version
csharp-ls, 0.28.0 (Klebiškis)+72671fc7fa810c158864e72485db68e9b25c3d
```

`csharp-ls` 已在 PATH【已运行：`Get-Command csharp-ls` → `C:\Users\z2637\.dotnet\tools\csharp-ls.exe`】。

### 2.1 实测：能不能真的索引这个项目

光启动不算，要和它讲 LSP。用 `_work/tooling-20261027/verify_csharp_ls_deep.py`
发真实的 `initialize` → `didOpen` → `hover`/`definition`/`workspace/symbol`：

```
file   : mpv-winui\mpv-winui\Modules\Settings\MpvSettings.cs  (87065 chars)
server : C:\Users\z2637\.dotnet\tools\csharp-ls.exe

PASS  initialize -> csharp-ls
      waiting 20 s for project load...
  PASS  hover ToCommand  ```csharp string? MpvSettings.ToCommand(string key, object value)```
  PASS  definition ToCommand -> MpvSettings.cs
  FAIL  hover class MpvSettings
  FAIL  definition class MpvSettings -> (none)
  PASS  hover BuildApplyAllCommands  ```csharp List<string> MpvSettings.BuildApplyAllCommands()```
  PASS  definition BuildApplyAllCommands -> MpvSettings.cs
  PASS  workspace/symbol 'MpvSettings' -> 2 hit(s)

RESULT: 5/7 deep queries answered
VERDICT: PASS -- symbol resolution works on this project
```

**5/7 的解释**：两个 FAIL 的探测点落在 `class MpvSettings` 的 `class` 关键字上——
那个位置本来就没有符号可解析，`hover`/`definition` 返回空是**正确行为**，不是工具缺陷。
其余五个位置（`ToCommand` 调用点、`BuildApplyAllCommands` 调用点）全部返回了**准确签名**
和文件定位。

**结论：`csharp-lsp` 在这个 328 个 `.cs` 的项目上真实可用。** 返回的签名
`string? MpvSettings.ToCommand(string key, object value)` 与源码一致，不是占位。

### 2.2 一个必须知道的前置条件

`csharp-ls` 需要**项目加载时间**。本脚本等了 20 秒才稳稳拿到符号；3 秒时
`documentSymbol` 只返回文件节点本身（1 个 symbol）——那是**假阴性**，容易被误判成
"装了没用"。判断依据必须是 `hover`/`definition` 这类需要符号表的能力，不是
`documentSymbol` 的条目数。

---

## 3. `windbg`：装了，但本会话调不到

### 3.1 技能确实装进来了

`~/.claude/plugins/cache/win-dev-skills/windbg/1.0.0/skills/` 下 11 个目录齐全【已运行】：
`windbg-diagnostic-method`、5 个 kernel、5 个 user-mode native。

### 3.2 但本会话调不到

```
skill("windbg-user-exception-triage")
→ Error: skill "windbg-user-exception-triage" is unknown or no longer available
```

作为对照，同一次会话里 `skill("winui-code-review")` **成功返回了完整内容**【已运行】。

**原因**：dsh 的 skill catalog 在**会话启动时**构建。`winui` 是装好之后才启动会话的，
`windbg` 是会话进行中装的——目录已冻结，新插件要等下次会话（或 dsh 重启）reconcile。

**不是配置错**：`claude-compat.enablePlugins = true` 仍为 true，`installed_plugins.json`
三者都在，cache 目录也在。纯粹是生效时机问题。

### 3.3 就算生效了，对本项目也没用

这一点在 `VERIFICATION-2026-10-27.md` §7 已用证据说明，这里只重复结论：

- 11 个技能全部面向 **native C/C++ 或 kernel**
- `windbg-user-exception-triage` 的 description 原文：**"Not for managed .NET exceptions,
  WinUI/XAML app errors, or kernel bugchecks"**
- 本项目唯一的崩溃类型（`0xc000027b` + `Microsoft.UI.Xaml.dll`）恰好落在排除项里
- 且硬依赖 `.exr` / `!analyze -v`，而本机 `windbg`/`cdb`/`kd`/`dumpchk`/`procdump`
  **全部不存在**，`Windows Kits\10\Debuggers` 目录也不存在

**留着无害**（不自动触发，不占上下文，除非被显式调用），但不要把它当成
"崩溃归因已经补上了"。**那个缺口仍然开着。**

---

## 4. 两个插件的生效条件不同（这张表值得记）

| 插件 | 需要重启会话吗 | 还缺什么 | 现在能用吗 |
|---|---|---|---|
| `csharp-lsp` | **不需要** | 无（`csharp-ls 0.28.0` 已装且在 PATH） | **能** |
| `windbg` | **需要**（skill catalog 冻结） | 调试器二进制（`.exr`/`!analyze` 的前提） | **不能**，且本项目用不上 |

`csharp-lsp` 不需要重启，是因为它不含 skill——它是纯 LSP 配置，宿主进程按需拉起
`csharp-ls` 子进程，不经过 dsh 的 skill catalog【代码：该插件目录下只有 LICENSE 与 README】。

---

## 5. 如果你想让 `windbg` 真的可用

三步，缺一不可：

1. **重启 dsh** —— 让 skill catalog 重新 reconcile，否则 `skill()` 永远找不到它
2. **装调试器** —— Windows SDK 的 Debugging Tools（`debuggers` 组件），提供
   `cdb.exe` / `.exr` / `!analyze -v`。这一步需要管理员权限
3. **确认崩溃类型匹配** —— 即使前两步做完，本项目是 managed/WinUI 崩溃，
   `windbg-user-*` 那几个技能仍然不适用。真正对口的应该是 .NET 侧的
   `dotnet-dump` / `dotnet-sos`，而不是 WinDbg 插件

**我的建议：第 3 步决定了前两步不值得做。** 想要崩溃归因，方向是
`dotnet-dump` + `dotnet-sos`（`dotnet tool install --global dotnet-dump`），
它能读 managed 堆栈和 stowed exception，正好对上 `AGENTS.md` 描述的那类崩溃。
这一项我**没有安装**——它不在你给的"装你说的这些插件"范围内，且需要你确认。
