# 环境与工具：本轮自主安装与实测（2026-10-27）

> 授权范围：可自行安装环境、自己做测试、自行新增插件、进行有风险的修改。
> 这份文件记录**装了什么、哪些真能用、哪些装不上及原因**。证据等级沿用 ISSUE_LOG 定义。

---

## 1. 结论速查

| 项 | 结果 |
|---|---|
| .NET 诊断工具链（4 个） | **全部装上并实测可用** |
| `dotnet-dump` 能否读 mpvw 的托管崩溃 | **能** —— 读到 SOS、12 线程、完整托管栈、97,051 对象 |
| `csharp-lsp` 插件 + `csharp-ls` | **装上并实测可用** |
| `windbg` 插件 | 装上，但**本会话调不到**且**对本项目不适用** |
| WinDbg 二进制 | **装不上**：非管理员 + 微软 CDN 返回 403 |

---

## 2. 装了什么

### 2.1 .NET 诊断工具链（`dotnet tool install --global`）

```
dotnet-dump   10.0.750501   （从 9.0.661903 升级）
dotnet-sos    10.0.750501
dotnet-stack  10.0.750501
dotnet-trace  10.0.750501
```

全部在 `C:\Users\z2637\.dotnet\tools\`，已在 PATH【已运行：`Get-Command` 四个都返回路径】。

### 2.2 `csharp-lsp` 的语言服务二进制

插件本身只有 `LICENSE` + `README.md`，README 要求：

```
dotnet tool install --global csharp-ls
```

装上 `csharp-ls 0.28.0`【已运行】。**验证方式**：`tools/mvpw-mcp/diagnostics/verify_csharp_ls_deep.py`
发真实 LSP 会话，`hover` 返回 `string? MpvSettings.ToCommand(string key, object value)`，
`definition` 定位到文件，`workspace/symbol` 2 个命中。**5/7 PASS**，两个 FAIL 是探测点
落在 `class` 关键字上（那里没有符号，返回空是正确行为）。

---

## 3. 关键成果：`dotnet-dump` 补上了 `windbg` 补不了的缺口

`AGENTS.md` 的"读崩溃"一节描述：

> `0xc000027b` + `Microsoft.UI.Xaml.dll`，无对话框、无托管栈、无日志行 —— stowed exception。

之前这条路走到"看 WER 的 P7 字段"就断了，因为本机没有任何东西能产出**托管栈**。

### 3.1 实测：能做

```
$ dotnet-dump collect -p <mpvw pid>
Writing full to ...\mpvw-managed.dmp
Complete                                    → 1032.7 MB

$ dotnet-dump analyze <dump> -c eeversion
SOS Version: 10.0.15.501 retail build       → CLR 已加载，SOS 可用

$ dotnet-dump analyze <dump> -c clrthreads
ThreadCount:      12
BackgroundThread: 11
Hosted Runtime:   no                        → 12 个托管线程

$ dotnet-dump analyze <dump> -c clrstack
0000003141F7EB30  ABI.Microsoft.UI.Xaml.IApplicationStaticsMethods.Start(...)
0000003141F7EC60  Microsoft.UI.Xaml.Application.Start(...)
0000003141F7ECA0  mpv_winui.Program.Main(System.String[])
                                            → 完整托管调用栈 ← 这是 windbg 给不了的

$ dotnet-dump analyze <dump> -c "dumpheap -stat"
Total 97,051 objects, 9,237,798 bytes       → 对象统计
```

【已运行】全部在上面同一次会话中跑出。

### 3.2 固化成脚本

`tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1`

```
pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1            # 只抓转储
pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1 -Analyze   # 抓 + 分析
```

实测输出：

```
target : mpvw (pid 14740)
dump   : C:\Users\...\mpvw-managed.dmp
         1032.7 MB
--- analysis ---
  PASS  SOS loaded: 10.0.15.501 retail build
  PASS  managed threads: 12
  PASS  managed stack frames: 3
  NOTE  live Exception instances in the heap: (none that are real instances)
OK: managed forensics available for mpvw
```

转储约 1 GB/次，默认删除，`-Keep` 保留。

### 3.3 抓输出踩的四个坑（都写进脚本注释了）

| # | 写法 | 结果 |
|---|---|---|
| 1 | `... \| dotnet-dump ... \| Out-String` | 间歇丢行（同一份 ThreadCount 从文件读能匹配、走管道就空） |
| 2 | `... \| dotnet-dump ... *>&1 \| Out-File` | `clrthreads` 表格仍打到控制台 |
| 3 | `$commands \| & dotnet-dump ... 2>&1 \| Out-File` | 同上 |
| 4 | `cmd /c 'type rc \| dotnet-dump ... > out'` | 同上 |

**原因**：`dotnet-dump` 的交互式 shell 直接绑定控制台句柄输出命令结果，任何 PowerShell
流或 cmd 重定向都看不到它。

**正解**：用 `dotnet-dump` 自己的 `-c/--command`，输出走正常进程 stdout，可被捕获。

另有两个我自己的 bug 也记在注释里：
- 数组拼接把 cmdlet 名截断成 `-Content` / `-Item`（parser 抓到的）
- 早先版本 `ThreadCount` 单独测试能匹配、走管道为空

---

## 4. 装不上的：WinDbg 二进制

`windbg` 插件已装（11 个技能），但它硬依赖 `.exr` / `!analyze -v` 这类命令，
而本机没有任何调试器：

```
windbg / cdb / kd / dumpchk / procdump   → 全部 NOT FOUND
C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\   → 目录不存在
Get-AppxPackage *WinDbg*                 → 无
```

尝试安装【已运行】：

```
$ winget install Microsoft.WinDbg --scope user
已找到 WinDbg [Microsoft.WinDbg] 版本 1.2610.1001.0
正在下载 https://windbg.download.prss.microsoft.com/.../windbg.msixbundle
Download request status is not success.
0x80190193 : Forbidden (403).
```

直接 HEAD 该 URL 也是 **403**【已运行】。当前会话非管理员（`isAdmin: False`），
即使换默认 scope 也会卡在 UAC。

**所以我没有继续追它**，理由不只是装不上：

> `windbg` 插件的 11 个技能全部面向 **native C/C++ 或 kernel**。
> `windbg-user-exception-triage` 的 description 原文写着
> **"Not for managed .NET exceptions, WinUI/XAML app errors, or kernel bugchecks"**
> —— 正是本项目唯一的崩溃类型。

**结论：这个插件的目标与本项目不匹配，装不上二进制反而是次要的。** 真正的缺口
已由 §3 的 `dotnet-dump` 补上。

---

## 5. 仍未解决的（如实列出）

| 项 | 状态 | 说明 |
|---|---|---|
| `windbg` 技能在本会话可调用 | **不可以** | skill catalog 在会话启动时构建，插件是会话中途装的。`skill("windbg-user-exception-triage")` 返回 unknown，同会话 `skill("winui-code-review")` 正常。需重启 dsh |
| `windbg` 技能对本项目有用 | **没有** | 见 §4，面向 native/kernel |
| `dotnet-dump` 抓取真实崩溃 | **未验证** | 我只抓了**正常运行中**的进程。真实 stowed exception 发生时能否抓到有效栈，需要复现一次崩溃才能确认。这是下一步 |
| `dotnet-sos` / `dotnet-stack` / `dotnet-trace` 的实际用途 | **未逐一验证** | 装了、在 PATH，但只验证了 `dotnet-dump`。`dotnet-stack` 理论上能给任意进程的托管栈（比 dump 轻量），值得后续试 |

---

## 6. 给后续的实操建议

**要查 mpvw 的托管崩溃**：

```powershell
# 1. 应用崩了之后，进程可能还在（stowed exception 不一定会立即退出）
Get-Process -Name mpvw

# 2. 有进程就直接抓
pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1 -Analyze -Keep

# 3. 没进程就看 WER 转储
Get-ChildItem "$env:LOCALAPPDATA\CrashDumps" -Filter 'mpvw*'
# 然后用 dotnet-dump analyze 读它

# 4. 找异常现场（比 dumpheap -stat 更直接）
dotnet-dump analyze <dump> -c "dumpheap -type Exception -stat" -c "printexception <addr>" -c exit
```

**别再用 `windbg` 插件查本项目** —— 它面向 native，且二进制装不上。
