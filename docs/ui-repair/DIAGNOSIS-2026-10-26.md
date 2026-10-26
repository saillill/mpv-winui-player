# 测试流程失效诊断

**日期**：2026-10-26
**HEAD**：`1a78e17`
**结论**：`gate.py` 报 8/8 全绿的同时，应用**无法通过任何用户路径播放视频**。
这不是"漏了一个 bug"，是五个结构性缺陷叠加的必然结果。本文先给测量原始值，
再给根因，最后给修复方案。**修复方案未经确认前不进入 UI 修复。**

---

## 0. 一句话

> 现有测试体系测的是"探针自己能不能把 mpv 喂起来"，不是"用户能不能用这个播放器"。
> 探针代替应用执行了 `loadfile`，因此应用自己的打开路径**从未被执行过一次**。

---

## 1. 直接测量：MPV 到底能不能播放

### 1.1 mpv 内核完全正常（不是 mpv 的问题）

`_work/probe/probe_playback.py`，通过 IPC 直连裸 mpv 实例：

```
loadfile -> {"data": {"playlist_entry_id": 1}, "error": "success"}
 t     pause  idle   time-pos   filename       video-format  w x h
 1.0   False  False  58.433333   long-60s.mp4   'h264'        640x360
 3.0   False  False  2.6         long-60s.mp4   'h264'        640x360
17.0   False  False  14.193339   long-60s.mp4   'h264'        640x360
final: duration=60.0  video-format='h264'
video-out-params: {"pixelformat": "d3d11", "w": 1280, "h": 720, ...}
track-list: video(h264) + audio(aac)，均 selected
```

**结论**：解码、d3d11 输出、音轨选择、时间轴推进全部正常。**问题在应用层。**

### 1.2 应用自己的打开路径：文件加载了，但界面不播

`_work/probe/probe_doubleclick.py`（用户真实动作：把视频路径作为 argv[1] 启动）
的应用日志（`%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\`）：

```
01:24:02.7273|INFO |App|settings loaded from mpv.conf: 229 of 229 option(s) mapped
01:24:03.0119|DEBUG|App|MainWindow.Open enter
01:24:03.0619|DEBUG|App|MainWindow.Open parsed 1 item(s)      <-- 路径解析成功
01:24:03.3646|DEBUG|MpvPlayer|CreateAsync: player initialized at 198ms
01:24:03.5171|DEBUG|MpvPlayer|media info changed: video=960x540   <-- 文件真的加载了
01:24:03.6441|DEBUG|MpvPlayer|media info changed: video=0x0       <-- 随即归零
01:24:03.8066|DEBUG|App|aspect fit: skipped: no active video (extra=NULL)
```

同一时刻的界面（`_work/probe/out/embedded-screen-012443.png`）：
**停在"主页"，播放器进度条显示 `00:00 / 00:00`，中间是媒体库网格。**

**结论（实测部分）**：文件确实进了 mpv（`media info changed: video=960x540`），
但界面停在"主页"，进度条 `00:00 / 00:00`，视频面板尺寸为"无"。

**结论（推断部分，需 P0-4 的日志确认）**：`FileStarted` 是否触发**无法从现有日志判定**
——`MpvPlayerPage_LibraryFileStarted()`（`MpvPlayerPage_Library.cs:2553`）里没有一行日志。
§1.2b 的 `0x0` 链是一条有依据的**假说**，不是结论。

### 1.2b 为什么界面不跟着走：视频尺寸 100 ms 内归零

日志里最关键的两行是这对：

```
01:24:03.5171  media info changed: video=960x540    <-- 有效视频
01:24:03.6441  media info changed: video=0x0        <-- 127 毫秒后无效
01:24:03.8066  aspect fit: skipped: no active video (extra=NULL)
```

`MpvPlayerPage_Info.cs:20-50` 的语义是：`VideoWidth>0` 时置 `MainWindow.HasActiveVideo = true`
并 `ScheduleAspectFit()`；否则置 `false`。`video=0x0` 把它重置回 `false`，250 ms 后定时器
触发时看到的是"没有视频"，于是跳过（`MainWindow_Aspect.cs:180`）。

**`0x0` 是从哪来的，是这条链上唯一还需要定位的一环**。已知：
- 文件加载后先报 `960x540`，127 ms 后报 `0x0`；
- 而"没有画面"正是用户看到的现象。

但 **`0x0` 与"不切页"之间的因果关系尚未证明**：`FileStarted` 的处理函数
（`MpvPlayerPage_Library.cs:2553`）和 `SetLibraryVisible` 都没有日志，
所以现在无法区分下面两种可能：

| 假说 | 需要什么证据 |
|---|---|
| A. 页面切了，但视频面板尺寸是 0，所以看不见 | `SetLibraryVisible(false)` 有没有被调用 |
| B. 页面根本没切，只是因为某个守卫提前返回 | `FileStarted` 有没有进来、在哪一行返回 |

**这正是 P0-4 存在的理由**：五条 INFO 日志能一次性把这五个环节分开，
而不是继续靠推测。**在取得这个证据之前不改产品代码。**

### 1.3 点媒体卡片：处理器根本没有发出加载命令

`_work/probe/probe_card_clean.py`：不带任何文件启动，等媒体库出现，对第一张卡片做
**真实鼠标点击**，然后读应用自己的 DEBUG 日志（绕过不可靠的 IPC，见 §2.3）：

```
pid: 54304
tile: ButtonControl '1' rect=(1068,780,1511,1129)
clicking at (1289,954)
app log since click:
   01:27:30.8149|DEBUG|UpdateChecker|update check skipped ... 403
```

**点击之后，应用的日志里没有任何播放相关的一行** —— 没有 `media info changed`，
没有 `loadfile`，没有 `FileStarted`。而 argv 路径（§1.2）在同样的日志级别下是有
`media info changed: video=960x540` 的。两者对照说明：

- 日志管用，能记录加载；
- **卡片点击这条路连"发出加载命令"都没走到。**

`probe_input_vs_handler.py` 用同一时刻的导航项点击做对照：点"音乐库"→ 标题立刻变成
"音乐"。**输入确实到达了应用**，所以不是坐标或命中测试问题。

> **修正记录（本文件第一版写错过，保留以示纪律）**：第一版用
> `probe_after_click_state.py` 得出"点击导致 mpv 暂停但不加载"。那一次测量在点击前
> 先用 IPC 加载了一个文件，页面已切到播放视图，而 UIA 仍按旧矩形报告卡片位置 ——
> 点到的其实是视频面，mpv 对左键的默认绑定是 `cycle-pause`。**那次"暂停"不是卡片处理器
> 造成的。** 结论不变（卡片不会打开文件），但证据换成了日志，而不是那个被污染的读数。

### 1.4 五个探针为什么都没发现

| 探针/验证器 | 它实际做了什么 | 为什么测不到 |
|---|---|---|
| `verify_e2e.py` | 只 assert 控件存在性和 AutomationId | 20 个 step 里**没有任何一个断言"文件在播放"** |
| `verify_screenshot.py` | `T._ipc(["loadfile", CLIP, "replace"])` 自己加载 | 绕开应用路径，测的是 mpv |
| `verify_skins.py` | 同上，`T._ipc(["loadfile", ...])` | 同上 |
| `verify_preview_api.py` | 同上 | 同上 |
| `verify_skin_geometry.py` | 同上 | 同上 |

**15 个 `verify_*.py` 里，5 个需要"有媒体在播"，5 个全部自己 `loadfile`。
应用自己的打开路径（`PlayLibraryEntry` → `_mediaPlayer.OpenAsync` →
`MpvPlayerExtensions.Open`）一次都没有被执行过。**

而唯一提到"播放"的断言 `verify_e2e.py:85` 长这样：

```python
st = step("read_playback_state", T.read_playback_state)
if st:
    print("      pause=%r volume=%r idle=%r" % (...))
```

**只打印，不断言。** `verify_e2e` 的 docstring 说"19 steps"，实测 `step(` 出现 20 次，
但这个数字的意义是"存在性断言的数量"，不是"功能的覆盖度"。

---

## 2. 根因：五个结构性缺陷

### 2.1 【致命】验证器代替被测对象执行动作

这是**唯一足以解释"全绿但放不了视频"**的缺陷。

```
应用的路径:  用户点击 → PlayLibraryEntry(entry) → _mediaPlayer.OpenAsync(FileItem) 
                      → MpvPlayer::Command(["osd-auto","loadfile",path,"replace+play"])

探针的路径:  T._ipc(["loadfile", CLIP, "replace"])  → 直连 mpv，跳过整个 C# 层
```

两条路径**没有任何共享代码**。探针证明的是"mpv 会 loadfile"，这在 1.1 已经证明过了，
不需要再证 15 遍。**被测应用的第一段代码从未运行。**

同一个模式在本仓库至少有三处变体：
- `verify_skins.py` 的 docstring 自己写着"measured the default skin three times, then passed"
  （`6bc68fa` 修过一次，但没推广）
- `verify_settings_rows.py` 只数 UI 元素，不断言行为
- `verify_e2e.py` 的 20 步里 18 步是 `assert_element_state`（存在性）

### 2.2 【严重】`gate.py` 的通过判据是"最后一个词里有 PASS"

```python
def verifier(script):
    ok = code == 0 and ("OK" in tail or "PASS" in tail or "passed -> OK" in tail)
    return ok, tail
```

`tail` 是输出的**最后一行**。任何脚本只要最后打印一行含 `PASS` 就通过，
包括"什么都没做"的脚本。判据不是"断言成立"，是"打印了某个词"。

### 2.3 【严重】IPC 前提是错的，且错误前提被写进了文档

`AGENTS.md:172-175`、`PLAYBOOK.md:52-58`、`mpvw_tools.py` 多处断言：

> "管道每个进程只服务一个连接，且只在启动后约 1 秒内接受"

实测（`probe_ipc_lifetime.py`、`probe_ipc_reconnect.py`、`probe_argv_live.py`）：

```
+1.25s  ok          <-- 第一次请求成功
+1.45s  write 232   <-- 停 200ms 就断
+2.7s   write 232   <-- 连上但没立刻写，死
+4.7s   read 109
+12.7s  write 232
"connection made 6s after launch": connect OK
```

**真相（三条，都与文档不符）**：
1. 连接**随时可以建立**，6 秒后也行 —— 不是"只在启动后一秒内"。
2. **建立后必须立刻有请求，不能空闲**。那个 232 是空闲导致的，不是"窗口关闭"。
3. 同一时刻只服务**一个**客户端；反复新建连接会交替拿到 `232`/`109`
   ——`probe_card_clean.py` 每读一个属性就新建一次连接，`idle-active` 在
   `write 232` 和 `read 109` 之间抖动。**一个探针必须持有一条连接并保持忙碌。**

因为前提错了，所有探针都被迫写成"启动后立刻 loadfile"这种形态。**正是这个被迫的形态
让探针替应用做了本该被测的事情** —— 错误前提直接制造了 §2.1 的缺陷。

`PLAYBOOK.md` 的"想读两个东西，就开两次进程"是错的：真实约束是"不能空闲、不能并发两个"，
不是"不能重连"。本次几乎每个探针都先在这上面摔了一跤（`probe_open_command.py` 加一个
0.3 s 就全套 232；`probe_vo_surface.py` 睡 4 s 再 loadfile 直接失败）。

### 2.4 【严重】日志路径、日志级别、文档三处不一致

| 声称 | 实测 |
|---|---|
| `AGENTS.md:171` 日志在 `%LOCALAPPDATA%\mpv-winui\logs\` | 真实路径是 `%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\`（多一层） |
| `PLAYBOOK.md:41` 同上 | 同上 |
| `AppData.cs` 写 `ResolveLocalData("logs\\...")` | `mpv-winui\logs\` 里只有 9 月的陈旧文件（最后 2026-09-25） |
| 默认日志级别 | Warn（`LoggerHelper.cs`），所以 `_logger.Debug(...)` **全部不落盘** |

后者代价最大：`MainWindow.Open parsed N item(s)`、`CreateAsync: player initialized`、
`media info changed` 这些**唯一能定位播放链路的行全是 DEBUG**。
2026-10-25 和 2026-10-26 两天的日志里 `|DEBUG|` 出现 **0 次**，`|INFO|` **0 次**。
并且注册表里 `EnableDebugLog` 被存成 `REG_DWORD=1`，而应用只认 `"true"` 字符串
（`UnpackageAppDataSetting.SetValue` 对 bool 写字符串），所以**那个开关看起来是开的，实际是关的**。

### 2.5 【中】静态检查器与被检对象无因果关系

23 个 `check-*.py` 全绿，其中最相关的两个：
- `check-icon-standard.py` 数"图标尺寸字面量" → 51 known
- `check-typography-standard.py` 数"字号/间距字面量" → 17 known

这两个检查的是**源码文本**。用户报的"图标不对""按钮位置太高"**都不可能由它们发现**，
因为它们对"渲染结果"和"控件位置"一无所知。它们出现在 gate 里制造了"覆盖面很广"的
错觉，实际覆盖面是 0。

同理 `check-no-focus-steal.py` 扫 278 个文件的源码，而焦点问题只能在运行时测。

---

## 3. 为什么"退化检查过于肤浅"这个判断是对的

用户的原话。把它翻译成可检验的表述：

| 用户报的问题 | 现有工具能不能发现 | 为什么 |
|---|---|---|
| 视频播放不了 | ❌ | 探针代替应用 loadfile（§2.1） |
| 打开文件按钮位置太靠上 | ❌ | 没有任何工具比较**两个按钮的 Y 坐标** |
| 悬浮按钮图标不对 | ❌ | 图标检查器只数源码里的尺寸字面量 |
| 悬浮按钮被遮挡 | ❌ | 没有 Z 序 / 命中测试检查 |
| 封面显示为灰条 | ❌ | 截图看了，但没量化"是否有真实图像" |
| 字母排序栏按钮重叠 | ⚠️ | `audit_sort_overflow.py` 存在，但只测溢出不测重叠 |
| 专辑/艺术家图片不显示 | ❌ | 同封面 |
| 视频库应显示大路径+分栏 | ❌ | 这是**需求**，不是缺陷，没有基线 |
| 播放队列缺添加按钮 | ❌ | 没有任何"按钮清单"基线 |

**9 项里 0 项能被现有工具发现。** 这不是工具不够多，是工具的**语义层级**停在了
"字符出现次数"和"控件存在性"。要测用户报的东西，必须上升到**几何、像素、和状态机**。

---

## 4. 修复方案

按依赖顺序。P0 必须先做完，否则后面每一项的"已验证"都不可信。

### P0-1 测量层：让探针能观察应用，而不是代替应用

**目标**：探针不再 `loadfile`。它启动应用、驱动 UI、然后**只看不碰**。

技术前提（已实测）：管道在应用有文件在播时是稳定的。所以：
1. 连接时机改为"发射后轮询"，不再假设一秒窗口；
2. 连接后**立即发一个 `observe_property`**，之后靠事件流而不是轮询维持；
3. 需要读状态时用 `get_property`，间隔 < 1s。

交付：`tools/mvpw-mcp/mpvw_tools.py` 增加 `observe_playback(seconds)`，
返回事件流（`start-file` / `file-loaded` / `playback-restart` / `property-change`）。
**它不发任何 `loadfile`。**

**验收**：拿 §1.3 那个场景重跑，必须能报出"点击后 0 个 load 事件"。

### P0-2 一条真实路径的端到端验证器（新的 gate 第一关）

**目标**：`verify_playback.py` —— 唯一一个**不允许自己 loadfile** 的验证器。

步骤（全部通过才算 PASS）：
1. 启动应用（不带参数）
2. 等媒体库网格出现（等元素，不 sleep）
3. 真实鼠标点击第一张卡片
4. **等待** `file-loaded` 事件（不是 sleep，不是 poll 一个猜测的时长）
5. 断言：`video-format != null` 且 `time-pos` 在 1 秒内增长 且 `idle-active == false`
6. 截图，断言画面区域**不是**媒体库（用像素差与点击前对比）
7. 再走一遍 argv 路径（`mpvw.exe <file>`），同样断言

**这是本次唯一的"能不能播放"判据。** 先让它红，再修产品。

### P0-3 让 gate 的判据变成证据，不是措辞

- 每个验证器输出机器可读的 `--json` 结果，gate 读 `ok` 字段；
- 禁止用"最后一行含 PASS"判定；
- **每个验证器必须在 gate 里有一条"故意注入失败"的自测**（`--self-test`），
  证明它在应该红的时候会红。没通过自测的验证器，gate 直接判失败。

这直接实现 README §4 已经写下但从未执行的规则："从没红过的检查器不是证据"。

### P0-4 日志：让下一次排查不必再猜

- 修 `AppData` 的路径不一致（或修文档；以**代码实际行为**为准，然后统一）；
- `EnableDebugLog` 的读取同时接受 `REG_DWORD` 和字符串（现状是只认字符串，
  而注册表里是 DWORD，所以开关名存实亡）；
- **播放链路关键节点加 INFO 级日志**：`open requested <path>` / `loadfile sent` /
  `file-loaded received` / `page switched to player` / `video panel sized WxH`。
  这五条能让"放不了"在 3 秒内定位到具体环节。

### P1 UI 测量能力（为后续每一项 UI 修复提供判据）

| 需要测的 | 工具 | 判据 |
|---|---|---|
| 按钮位置对齐 | `assert_alignment(id_a, id_b, axis)` | Y 坐标差 ≤ 2 DIP |
| 元素被遮挡 | `assert_not_obscured(id)` | 命中测试：`ElementFromPoint` 返回该元素或其后代 |
| 图标字形正确 | 现有 `check-icon-standard.py` 扩展 | 对照 Fluent 官方码点表，**且**运行时读到 `FontIcon.Glyph` |
| 封面/图片存在 | `assert_image_content(id, rect)` | 该区域 stddev > 阈值（灰底 ≈ 0） |
| 分栏布局 | `assert_columns(id, n)` | 卡片矩形的 X 坐标聚类数 |

### P2 文档可信度

逐条核对，**由代码或实测结果决定谁对**，不沿用旧文档：
1. `AGENTS.md:171` 日志路径
2. `AGENTS.md:172-175` / `PLAYBOOK.md:52-58` IPC 约束（§2.3 全部重写）
3. `HANDOFF.md` "gate 8/8 全绿" 需要附带"并播放不了视频"的说明
4. `README.md:10` 的工具清单
5. `verify_screenshot.py` 里的 `D:\Workspace\WorkBuddy\...` 硬编码路径
   （文件现在确实存在，但这是它**能 PASS** 的原因，不是设计）

---

## 5. 我需要你确认的一件事

方案 P0-1 到 P0-3 会**推翻现有 15 个验证器的工作方式**（它们全部依赖"探针自己 loadfile"）。
工作量不小，但这是唯一能让"已验证"重新有意义的路。

有两个选项：

**A. 彻底重建（推荐）**：按 P0-1→P0-4 全部做完，5 个 loadfile 型验证器改写为
"驱动 UI 打开"，其余 10 个保留但降级为诊断脚本、移出 gate。

**B. 最小修补**：只加 `verify_playback.py`（P0-2）+ 修日志（P0-4），
P0-1/P0-3 延后。能在今天内让"能不能播放"可测，但"图标/位置/遮挡"这批 UI 问题
仍然没有判据。

我的建议是 **A**，理由：用户列出的 9 个 UI 问题里 0 个能被现有工具发现（§3），
选 B 等于修完播放后继续用"看截图 + 读代码"去判断 UI，也就是用户明确说"查不出来"
的那条路。

---

## 6. 附录：本次测量清单

所有探针在 `_work/probe/`，全部可重跑：

| 探针 | 回答的问题 |
|---|---|
| `probe_playback.py` | mpv 内核能不能解码/输出？→ 能 |
| `probe_open_command.py` | 应用的 `loadfile` 命令形式 mpv 接受吗？→ 全部接受 |
| `probe_doubleclick.py` | argv 启动的完整日志链路 |
| `probe_embedded_or_not.py` | 播放时是嵌入还是独立窗口？+ 屏幕截图 |
| `probe_after_click_state.py` | 点卡片后 mpv 的完整状态向量 |
| `probe_input_vs_handler.py` | 输入到达 ≠ 处理器执行 |
| `probe_ipc_lifetime.py` | 管道的真实生命周期 |
| `probe_ipc_reconnect.py` | 断开后能否重连 → 能 |
| `probe_embed.py` | 用 PrintWindow 确认嵌入（反例：截图会漏掉浮动窗口） |
| `probe_click_traced.py` | `EnableDebugLog` 是否生效 → DWORD 形式不生效 |

截图证据在 `_work/probe/out/`。
