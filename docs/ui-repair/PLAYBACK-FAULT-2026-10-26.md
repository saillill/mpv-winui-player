# 播放故障：已确认事实与下一步

**日期**：2026-10-26
**状态**：根因**未修**。已把范围从"mpv 放不了"缩小到一条可复现的边界。
**纪律**：本文只写实测到的；推测单独标出。

---

## 1. 已确认

### 1.1 mpv 内核本身完全正常

`_work/probe/probe_argv_vs_plain.py`，四种 argv 全部真正播放
（`idle=False`、`time-pos` 推进、`video-format='h264'`）：

| argv | 结果 |
|---|---|
| `["osd-auto","loadfile",path,"replace+play"]` | 播放 |
| `["loadfile",path,"replace"]` | 播放 |
| `["loadfile",path]` | 播放 |
| 重复第一种 | 播放 |

这些全部通过 `\\.\pipe\mpvpipe` 发送。

### 1.2 应用自己的路径：命令被接受，mpv 随后静默

`_work/probe/probe_play_chain_log.py`：

```
open requested count=1 mode=Replace type=File first=...probe-clip.mp4
loadfile accepted (0) argv=[osd-auto | loadfile | ...probe-clip.mp4 | replace+play]
   immediately: path=<null> playlist-count=1 idle=0
（此后没有任何 start-file / file-loaded / mpv 日志）
```

- argv **逐项正确**（已打印）
- `mpv_command` 返回 **0**
- 命令后立刻读回：`idle=0`（mpv 已离开 idle）、`playlist-count=1`
- 但 `path=<null>`，且**没有 START_FILE**

### 1.3 mpv 自己的 trace 在 loadfile 之后就没有下一行

在正确的位置（`%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\mpv.conf`）临时打开
`log-file` + `msg-level=all=v`，抓到的最后一行是：

```
[0.402][d][cplayer] Run command: loadfile, flags=65, args=[url="...probe-clip.mp4", flags="play+replace", index="-1", options=""]
```

**这是整个 2188 行 trace 的最后一行。** mpv 收到 loadfile 之后不再产生任何 trace。

> 注意：最后一行如此，也可能有"强制结束进程时缓冲未落盘"的成分，但 trace 里此前的
> 每条命令都成对出现（Run command + Set property / VO 输出），此后连一条都没有，
> 更符合"事件流停止"而不是"缓冲丢失"。

### 1.4 三处测量盲区（已修，有测试）

| 盲区 | 根因 | 验证 |
|---|---|---|
| `EnableDebugLog` 形同虚设 | `REG_DWORD` 被 `ApplicationDataContainer` 交成 `uint`，读取代码只认 `int` | 注入原缺陷后 3 个测试变红，恢复后 17 绿 |
| 命令被拒时零证据 | `MpvPlayer::Command` 丢弃 `mpv_command` 返回值 | 现在 accepted/rejected + argv + 即时状态都进日志 |
| 播放链无 INFO 日志 | 全链路静默 | 五个环节各有 INFO，且不依赖调试开关 |

### 1.5 一个独立的真实缺陷（已定位，未修）

`FileLoaded` 的订阅发生得太晚：

```
02:15:15.0375  mpv raised file-loaded
02:15:16.7265  FileLoaded handlers attached     <- 晚 1.7 秒
```

`MpvPlayerPage_Loaded` 里才挂 `FileLoaded`，而启动时的首个文件在此之前就加载完了。
`FileStarted` 侥幸逃过（它在加载前触发两次）。**修复方向明确：把 `FileLoaded`/`FileStarted`
的订阅移到 `CreateAsync` 内部、`InitializeAsync` 之后立即执行，而不是等到 `Loaded`。**

---

## 2. 唯一剩下的假说（**未经证实**）

`loadfile` 是在 **UI 线程**上调用的（`MpvPlayerExtensions.Open` 是同步方法，
`OpenAsync` 只是 `Task.Run` 包裹，但 `Command` 本身没有任何线程约束检查），
而 IPC 路径来自 mpv 自己的客户端线程。

`MpvPlayer::Command` 之后事件流停止，最像的解释是：
**在 UI 线程调用 `mpv_command` 与事件线程同时操作 mpv 句柄，导致事件线程卡住**。

mpv 的 API 文档明确要求：一个 `mpv_handle` 的所有调用必须串行，事件线程调用
`mpv_wait_event` 期间其他线程不得调用 `mpv_command`。本仓库的 `Command`、`CommandString`、
`LoadFile`、`SetLogLevel`、以及事件线程里的 `GetHStringProperty` **都没有加锁**。

**但这仍是假说。** 反证也现成：`AppContext.SendMpvCommand` 走的 `RunCommandAsync`
用了 `Task.Run`，`CommandArgsAsync` 也用了；只有 `Open()` 没有。若假说成立，
应该只有 `Open()` 那条路坏 —— 而观察到的正是如此。

---

## 3. 下一步（按顺序，每步都有明确判定）

### 步骤 1：给 mpv 调用加锁（最小改动，可独立验证）

在 `MpvPlayer` 里加一把 `std::recursive_mutex m_mpvMutex`，
**所有** `mpv_*` 调用（包括事件线程的 `mpv_wait_event` 之外的那些属性读取）都在锁内。

判定：改完重跑 `probe_play_chain_log.py`，`file-loaded` 出现 → 假说成立。
不出现 → 立刻回滚，进入步骤 2。

### 步骤 2：把 `Open` 移到工作线程

若步骤 1 无效，说明问题不在并发而在线程身份。把 `MpvPlayerExtensions.Open` 的调用
点（`PlayLibraryEntry`、`OpenPendingPath`）改为 `await Task.Run(...)`。

判定同步骤 1。

### 步骤 3：若两者都无效

那么"mpv 收到 loadfile 却完全静默"需要一个更底层的解释。
下一步是抓**不强制结束进程**的 trace：让应用正常关闭（`CloseMainWindow`），
确认 trace 在 `loadfile` 之后是否真的没有更多行，再决定。

---

## 4. 重跑命令

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv
pwsh -NoProfile -File tools/deploy-test.ps1 -CloseRunning -Deploy

python _work/probe/probe_argv_vs_plain.py       # mpv 能播（基线）
python _work/probe/probe_play_chain_log.py "C:/Users/z2637/AppData/Local/Temp/probe-clip.mp4"
                                                # 应用侧静默（复现）
python _work/probe/probe_filestarted_event.py   # FileLoaded 投影缺陷
```

抓 mpv 自身 trace（临时，用完务必还原）：

```powershell
$c = "$env:LOCALAPPDATA\mpv-winui\mpv-winui\mpv\mpv.conf"
Copy-Item $c "$env:TEMP\mpv.conf.winui.bak" -Force
# 把 log-file 那行改成 log-file = "~~/mpv-trace.log"，msg-level 改成 all=v
# 跑一次，读 $env:LOCALAPPDATA\mpv-winui\mpv-winui\mpv\mpv-trace.log
Copy-Item "$env:TEMP\mpv.conf.winui.bak" $c -Force
```

**注意**：应用真正读取的配置目录是 `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\`
（两层），不是 `%LOCALAPPDATA%\mpv-winui\mpv\`。AGENTS.md 写错了，改错了地方会
查出"没有 trace 文件"，我今晚就先踩了这个坑。
