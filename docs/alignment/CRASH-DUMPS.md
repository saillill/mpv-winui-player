# 让 mpvw 崩溃时自动留下可分析的转储（已验证闭环）

> 2026-10-27。这份记录的目的是让下一次崩溃**可归因**，而不是像
> `AGENTS.md` 描述的 `0xc000027b` 那样"无对话框、无托管栈、无日志行"。

---

## 1. 先说结论：WER 是错的路，CLR 的 createdump 才对

mpvw 是 .NET 应用，崩溃转储应该由 **CLR 自己**产出，而不是 Windows Error Reporting。

实测三条路：

| 方案 | 结果 |
|---|---|
| WER `LocalDumps`（HKLM 注册表） | 配置成功（`DumpType=2`），但**跑崩溃程序不产出任何转储** |
| 修 WER 服务 | `WerSvc` 原本是 **Stopped**，提权启动并设为 Automatic 后仍是**不产转储** |
| **CLR `DOTNET_DbgEnableMiniDump`** | **成功**：98.2 MB full dump，166 ms 写完 |

也就是说，之前 `%LOCALAPPDATA%\CrashDumps` 里那两个 `mpvw.exe.*.dmp`（67/64 MB，
2026-10-25）是 **mini dump，缺堆**——`dotnet-dump` 对它们跑 `dumpheap -stat` 返回空
（只有一行 `Loading core dump:`），只能读到 14 个线程名和主线程栈，
**读不到崩溃异常**。

---

## 2. 闭环验证（每一个环节都实测过）

用一个故意的崩溃程序（`InvalidOperationException`）验证整条链路：

```
$env:DOTNET_DbgEnableMiniDump  = 1
$env:DOTNET_DbgMiniDumpType    = 4          # 4 = full
$env:DOTNET_DbgMiniDumpName    = <dir>\crash.%p.dmp

$ crash.exe
[createdump] Writing full dump for process 3436 to ...\crash.3436.dmp
[createdump] Dump successfully written in 166ms      ← 98.2 MB
Unhandled exception. System.InvalidOperationException: deliberate test crash...
```

然后在 dump 上：

```
$ dotnet-dump analyze crash.3436.dmp -c eeversion -c clrthreads -c "dumpheap -type Exception -stat" -c exit

SOS Version: 10.0.15.501 retail build
ThreadCount:      3

  0  2  ...  Ukn  System.InvalidOperationException 0000023cdf49d7c8    ← 崩溃线程
  7ffdc9d47658  1  120  System.InvalidOperationException              ← 堆里有 1 个实例
  Total 158 objects, 4,176 bytes
```

**`clrthreads` 直接指出了异常类型和地址，`dumpheap -type Exception` 给出了实例地址。**
这正是 `windbg` 插件做不了的事（它面向 native，且本机连二进制都装不上）。

---

## 3. 怎么给 mpvw 用

### 3.1 临时启用（一次会话）

```powershell
$env:DOTNET_DbgEnableMiniDump = '1'
$env:DOTNET_DbgMiniDumpType   = '4'
$env:DOTNET_DbgMiniDumpName   = "$env:LOCALAPPDATA\CrashDumps\mpvw.%p.dmp"
& C:\Users\z2637\Documents\mpv-project\mpv-deploy\mpvw.exe
```

环境变量会被子进程继承，所以从同一个 shell 启动的 mpvw 崩溃时自动落盘。

### 3.2 永久启用（推荐，按进程配置）

写进 `runtimeconfig` 或用一个启动脚本。更简单的是在 `.csproj` 里加：

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="System.Runtime.DbgEnableMiniDump" Value="1" />
  <RuntimeHostConfigurationOption Include="System.Runtime.DbgMiniDumpType" Value="4" />
</ItemGroup>
```

**但这是改产品代码，我没有动** —— 它会影响每一个使用者的磁盘占用（每次崩溃 100+ MB）。
要不要加由你决定，这属于产品决策不是工具配置。

---

## 4. 分析崩溃的标准动作

```powershell
# 1. 找最新的转储
Get-ChildItem "$env:LOCALAPPDATA\CrashDumps" -Filter 'mpvw*.dmp' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# 2. 一次拿到全部关键信息
$d = "<上面的路径>"
dotnet-dump analyze $d `
    -c eeversion `
    -c clrthreads `
    -c "clrstack" `
    -c "dumpheap -type Exception -stat" `
    -c exit

# 3. 拿到异常地址后看它的内容与栈
dotnet-dump analyze $d -c "printexception <addr>" -c exit
```

### 4.1 一个必须知道的参数陷阱

`-c "clrstack -all"` 会被参数解析器拆开，报
`Unrecognized command or argument '-all'`。**整条命令必须作为一个字符串传**，
用 `Start-Process` 时尤其要注意引号：

```powershell
# 错：-all 被当成独立参数
dotnet-dump analyze $d -c clrstack -all -c exit

# 对
Start-Process dotnet-dump -ArgumentList @('analyze',$d,'-c','"clrstack -all"','-c','exit') -Wait -NoNewWindow
```

### 4.2 输出捕获也要注意

`dotnet-dump` 的交互式 shell 直接绑定控制台句柄，以下四种写法都**拿不到**命令输出：

```
... | dotnet-dump ... | Out-String
... | dotnet-dump ... *>&1 | Out-File
$commands | & dotnet-dump ... 2>&1 | Out-File
cmd /c 'type rc | dotnet-dump ... > out'
```

**能拿到**的两种：
- `-c` 参数（输出走正常 stdout）
- `Start-Process ... -RedirectStandardOutput <file>`

---

## 5. 系统级改动：做了，验证无效，已全部撤回

排查过程中我提权改过两处系统设置，**发现对 mpvw 无效后已经改回原值**。

| 项 | 原值 | 中间改过 | 现在 |
|---|---|---|---|
| `WerSvc` 启动类型 | `Manual` | `Automatic` | **`Manual`**（已还原） |
| `WerSvc` 状态 | `Stopped` | `Running` | **`Stopped`**（已还原） |
| `HKLM\...\LocalDumps\mpvw.exe` | 不存在 | 创建了 | **已删除** |
| `HKLM\...\LocalDumps\crash.exe` | 不存在 | 创建了 | **已删除** |

**为什么撤回**：WER LocalDumps 在这台机器上对 .NET 应用根本不产转储（§1 实测），
所以那些改动没有任何收益。留着无效的系统改动只会让环境偏离原状、让下一个人
误以为崩溃捕获已经配好了。**没有收益的改动就应该撤回**，这比"留着显式一点"更重要。

当前系统状态已确认回到：`WerSvc = Stopped/Manual`，`LocalDumps` 键不存在。

---

## 6. 未验证的

- **mpvw 真实崩溃时 CLR 是否会写转储**：我只在自建的 `crash.exe` 上验证过。
  mpvw 的崩溃如果是 stowed exception（`0xc000027b`），它是**通过
  `RoFailFastWithErrorContext` 走的 fail-fast**，CLR 的 createdump 是否介入、
  介入后栈是否还有意义，**都需要一次真实崩溃才能确认**。这是下一步。
- **mpvw 是 self-contained WinUI**，`DOTNET_DbgEnableMiniDump` 依赖 CLR 的
  `createdump` 工具存在。**已查：有。**
  `C:\Users\z2637\Documents\mpv-project\mpv-deploy\createdump.exe`（71,512 B）与
  `coreclr.dll`（4.40 MB）都在【已运行】。所以这条路对 mpvw **不缺组件**。

  但 `createdump.exe` **不能从外部对任意 PID 抓**：`-p` 已废弃
  （`The pid argument is no longer supported`），它只接受
  `-f/-n/-h/-t/-u/-d/-v/-l`，设计上是由 CLR 从进程内部调起的。所以两种工具分工不同：

  | 场景 | 工具 |
  |---|---|
  | 进程还在，现在就要一份 | `dotnet-dump collect -p <pid>` |
  | 进程崩溃，希望它自己留下证据 | `DOTNET_DbgEnableMiniDump=1`（CLR 调 `createdump`） |


### 6.1 下一步该怎么做

真复现一次崩溃，然后：

```powershell
$env:DOTNET_DbgEnableMiniDump='1'; $env:DOTNET_DbgMiniDumpType='4'
$env:DOTNET_DbgMiniDumpName="$env:LOCALAPPDATA\CrashDumps\mpvw.%p.dmp"
& ...\mpvw.exe
# 复现崩溃 → 检查 %LOCALAPPDATA%\CrashDumps\mpvw.*.dmp 是否出现
```

如果 CLR 没产出，说明 fail-fast 绕过了它，那时才需要 WER 或 procdump 作为兜底。
