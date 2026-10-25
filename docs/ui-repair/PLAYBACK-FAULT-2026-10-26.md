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

### 1.3 mpv 自身的 trace 证明：文件从未被打开过

在应用**真正读取**的位置（`%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\mpv.conf`）临时打开
`log-file` + `msg-level=all=v`，然后**正常关闭**应用（`CloseMainWindow`，不是
`Stop-Process`，否则缓冲区不落盘），trace 共 2277 行。

```
[0.401][d][cplayer] Run command: loadfile, flags=65,
    args=[url="...probe-clip.mp4", flags="play+replace", index="-1", options=""]
    <- 第 2271 行，之后 9 秒内没有任何一行 mpv 输出
[9.047][d][cplayer] Run command: keyup, ...          <- 我的探针在 9 秒后关窗口
[9.078][d][main] Destroying client handle...
[9.078][d][cplayer] Run command: quit, ...           <- 正常退出
```

两条硬结论：

1. **argv 被 mpv 正确解析**：`url` 正确、`flags` 规范成了 `play+replace`。
2. **mpv 没有尝试打开这个文件**。正常加载会立刻出现一串
   `[file] Opening ...` / `[demux]` / `[cplayer] Playing:`，这里一条都没有。

**mpv 收到 loadfile 之后，它的主循环就停了。** 连它自己的 IPC 服务器也不再有响应
（后续连接返回 `read 109` = `ERROR_BROKEN_PIPE`），这从另一侧证实"卡住"而不是"慢"。

### 1.4 已排除的原因（每一条都实测过）

| 候选 | 怎么试的 | 结果 |
|---|---|---|
| `start="none"` 拦截播放 | 查 mpv 手册：`start` 的默认值就是 `none` | 不是原因 |
| watch-later 残留触发恢复 | 清空 136 个条目后重跑 | 不变 |
| 内建预览器抢占 / 第二实例 | `EnableVideoBuiltInPreview=False` 后重跑 | 不变，已还原 |
| 悬空指针（argv 里存 `c_str()`） | 改成两段式转换后重跑 | 不变（但确是一个真 bug，已修） |
| 未加锁的 `mpv_command` 与事件线程并发 | 加 `recursive_mutex` 包住 `Command` 后重跑 | 不变，**已回滚** |
| 管道残留 | 关应用后探测 | 管道正确消失 |
| mpv 拒绝命令 | 检查返回值 | 返回 0 |
| Lua 脚本阻塞 hook | 把 `mpv/scripts/` 整个移走后重跑 | 不变，已还原 |
| `input-ipc-server` 自身的死锁 | 注释掉 `input-ipc-server` 后重跑 | 不变，已还原 |
| `d3d11-output-mode=window` | 改**原生代码**（改 mpv.conf 无效，见 §1.7）后重跑 | 不变，已回滚 |
| HDR auto profile（`d3d11-output-csp=pq` 等四项） | 经 IPC 先设这四项再 loadfile | 播放正常 |
| `d3d11-composition-size=1x1` | 组件以 1x1 和 960x540 各跑一次 | 两者都 `file-loaded` |
| **配置层整体** | 组件用空 `mpv.conf` 跑 | 能 `file-loaded` |
| **真实 app `mpv.conf`（20930 字节）** | 把应用的 `mpv.conf` + `profiles.conf` 拷给组件跑 | **能 `file-loaded`** |
| **vo / composition 输出** | 组件分别用空配置与 `vo=gpu-next`+`composition` 跑 | **两者都能 `file-loaded`** |
| **`Command(...)` vs `LoadFile(...)`** | 组件同一文件两种调用各跑一次 | **两者都能 `file-loaded`** |
| **attach `SwapChainPanel`** | 临时注释掉应用的 `AttachSwapChain` 后重跑 | 仍失败，已还原 |
| **两个预览开关全关** | `EnableVideoPreview` + `EnableVideoBuiltInPreview` 都设 False 后重跑 | 仍失败，已还原 |
| **loadfile 前那批 `user-data` 命令** | 组件先发这五行（真实 `mpv.conf` + profiles），再 loadfile | 能 `file-loaded` |

> 注意 `mpv 未打开文件` 与 `mpv 主循环停止` 是同一件事的两面：`loadfile` 之后再无
> 任何 trace，说明它在**处理这条命令的过程中**卡住，而不是拒绝后继续运行。

### 1.5 组件与配置已被完全排除

在 `mpv-winrt-test` 里用**真实组件**逐项跑过，全部能拿到 `FileLoaded`：

- 空 `mpv.conf`
- 应用真实的 `mpv.conf`（20930 字节）+ `profiles.conf`
- `vo=gpu-next` + `d3d11-output-mode=composition`
- `d3d11-composition-size` 为 1x1 和 960x540
- `Command(["osd-auto","loadfile",path,"replace+play"])` 与 `LoadFile(path, 0)`

在应用里临时去掉 `AttachSwapChain(PlayerView)` 后重跑，**故障不变**，
所以面板 attach 也不是原因。

`TimePosObservationRaisesPositionChanged` 至今通过：同一个 `MpvPlayer`、同一个
`LoadFile`，在单元测试里能真正播放并推进 `time-pos`。

**所以：mpv 内核、配置层、vo/composition、原生组件的 `Command`/`LoadFile`、
swap chain attach，全部排除。** 故障在应用里剩下没被覆盖的那些环节。

### 1.6 三处测量盲区（已修，有测试）

| 盲区 | 根因 | 验证 |
|---|---|---|
| `EnableDebugLog` 形同虚设 | `REG_DWORD` 被 `ApplicationDataContainer` 交成 `uint`，读取代码只认 `int` | 注入原缺陷后 3 个测试变红，恢复后 17 绿 |
| 命令被拒时零证据 | `MpvPlayer::Command` 丢弃 `mpv_command` 返回值 | 现在 accepted/rejected + argv + 即时状态都进日志 |
| 播放链无 INFO 日志 | 全链路静默 | 五个环节各有 INFO，且不依赖调试开关 |

### 1.7 一个让多轮排查白做的坑：改 mpv.conf 对某些选项无效

`MpvPlayer::Initialize` 在 `mpv_initialize` **之前**用 `SetOption` 写入
`d3d11-output-mode=composition`、`force-window=yes`、`d3d11-composition-size`、
`gpu-api`。这些 `SetOption` 会覆盖 `mpv.conf` 里的同名值，所以**改配置文件来测这些
选项完全无效**。

我第一次据此得出"vo 不是原因"的结论是错的；本轮改成直接改原生代码才测准。

另外 `ConfigDeployer` 每次启动会重新铺 `mpv/scripts/`、字体等，但**不动**
`mpv.conf` / `input.conf` / `menus.json` / `script-opts`（`UserOwnedFiles`）。
所以要排除脚本，移走 `mpv/scripts/` 目录才有效，改 `load-scripts` 不够。

### 1.8 一个独立的真实缺陷（**已修**，commit `466f11e`）

`FileLoaded` 的订阅发生得太晚：

```
修前：mpv raised file-loaded 02:15:15.0375 / handlers attached 02:15:16.7265（晚 1.7 秒）
修后：subscribed 03:03:11.7279 / loadfile sent 03:03:12.1123（早 384 毫秒）
```

已把 `FileStarted` / `FileLoaded`（play chain）/ `FileLoaded`（HDR once）移进
`SubscribePerLoadEvents()`，由 `CreateAsync` 在 `InitializeAsync` 返回后立刻调用，
并用 `_perLoadEventsSubscribed` 闩住，避免 `Loaded` 里二次订阅导致每个处理器跑两遍。

**但这个修复没有让视频播起来**：订阅确实早于加载了，`file-loaded` 依然不到。
记在这里是为了下一个读的人不会把这个 commit 误当成播放修复。

---
## 2. 现状：根因**仍未找到**，但范围已收得很窄

诚实地说：这一轮排掉了 20 多个候选，**一个都没能复现故障**。每个受控实验里组件都正常，
只有真正的应用不正常。所以现在不能指着任何一行代码说"就是这里"。

§1.5 已把 mpv 内核、配置层、vo/composition、原生组件的 `Command`/`LoadFile`、
swap chain attach、两个预览开关、loadfile 前的 `user-data` 命令批次全部排除。

**受控实验与真实应用之间，还剩的差别只有"时序与并发"这一类** —— 也就是说，
单独每件事都无害，它们叠在一起才出问题。这也解释了为什么逐项排除一直不收敛。

### 下一步最该做的：把时间轴对齐，而不是再猜一个候选

在一个**真实应用**里抓一条精确到毫秒的时间轴，把下面这些事件的相对顺序记下来：

1. `mpv_initialize` 返回
2. `AttachSwapChain` 调用
3. 那批 `user-data` / `BuildApplyAllCommands` 命令
4. `mpv-2.dll` 内部首次 `VIDEO_RECONFIG`
5. `loadfile` 发出

如果 `loadfile` 落在 2 与 4 之间（也就是 swap chain 刚挂上、vo 还没 reconfigure 完），
那就正好是"loadfile 被吞、主循环停住"的形状。验证方式是把 loadfile 人为推迟到
`VIDEO_RECONFIG` 之后再发 —— 这也顺便是一个可用的修复：**等 vo 就绪再加载**。

`_playerReadyTcs` 的注释声称"Callers now await this"，但全文只有声明与赋值两处，
**没有任何调用者 await 它**。把它真的接上，很可能就是缺的那一环。

### 曾经列过的三个嫌疑（都已排除，留档）

1. ~~loadfile 前的 `user-data` 命令批次~~ —— 组件先发这五行再 loadfile，正常。
2. ~~`preview_api_bridge.lua` 等待 mpv 回复~~ —— 两个预览开关全关，仍失败。
3. ~~`CompletePendingSwapChainAttach` 事件线程自锁~~ —— 去掉 `AttachSwapChain`
   后面板不在、该函数提前 return，故障不变。

（下面保留原来的描述，方便复核排查思路。）

---

## 3. 更省时的二分路径

`MpvPreviewer` 用的是一个几乎裸的 mpv 实例（`vo=libmpv`、`config=no`、
`load-scripts=no`、`profile=fast`），**它能正常解码出缩略图**。它和主实例的配置差别
已被 §1.5 逐项排除，但两者**在应用里的装配方式**不同：预览器从不 attach swap chain、
不发那批 `user-data` 命令、也不挂属性观察者。

把主实例临时改成"预览器那样"最小可用，再逐项加回，是下一步最省时的二分。

---
## 4. 重跑命令

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv

# 必跑的三个
python _work/probe/probe_argv_vs_plain.py       # mpv 能播（基线，走 IPC）
python _work/probe/probe_play_chain_log.py "C:/Users/z2637/AppData/Local/Temp/probe-clip.mp4"
                                                # 应用侧静默（复现）
python _work/probe/probe_is_there_a_picture.py  # 屏幕是否真的在动（判定播放）

# 本轮新增的排除用探针，每条对应 §1.4 的一行
python _work/probe/probe_no_scripts.py          # 移走 scripts/ 后重测，并自动还原
python _work/probe/probe_hdr_profile.py         # HDR profile 四项经 IPC 施加
python _work/probe/probe_vo_variant.py          # 组件分别用空配置与 composition 跑
python _work/probe/probe_1x1.py                 # 组件以 1x1 与 960x540 各跑一次
python _work/probe/probe_real_conf.py           # 把应用真实 mpv.conf 喂给组件
python _work/probe/probe_command_vs_loadfile.py # 组件的 Command 与 LoadFile 对比
python _work/probe/probe_command_batch.py       # 组件先发 loadfile 前那批命令
python _work/probe/probe_pipe_lifetime2.py      # 管道真实寿命（+1.42s 后断开）
```

抓 mpv 自身 trace（**必须正常关闭**，否则缓冲区不落盘）：

```powershell
$c = "$env:LOCALAPPDATA\mpv-winui\mpv-winui\mpv\mpv.conf"
Copy-Item $c "$env:TEMP\mpv.conf.clean" -Force
# 把 log-file 那行改成 log-file = "~~/mpv-trace.log"，msg-level 改成 all=v
Start-Process "$env:USERPROFILE\Documents\DeepSeek\mpv-deploy\mpvw.exe" `
              -ArgumentList "$env:TEMP\probe-clip.mp4"
# 等 10 秒，然后【正常关闭】：
Get-Process mpvw | ForEach-Object { $_.CloseMainWindow() | Out-Null }
# 读 $env:LOCALAPPDATA\mpv-winui\mpv-winui\mpv\mpv-trace.log
Copy-Item "$env:TEMP\mpv.conf.clean" $c -Force
```

**路径坑（我先踩过）**：应用真正读取的配置目录是
`%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\`（两层 `mpv-winui`）。
`%LOCALAPPDATA%\mpv-winui\mpv\` 是旧位置，改它不会产生 trace 文件。
