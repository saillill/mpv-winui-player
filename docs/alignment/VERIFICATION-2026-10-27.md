# 未知项验证结果（2026-10-27）

> 上一轮 `TOOLING-V2.md` 末尾列了 6 项"未确定"。这份文件逐项给出结果，包括**推翻我自己上一轮结论**的那两条。
> 证据等级：【已运行】/【代码】/【推断】/【未知】。

---

## 结论速查

| # | 上一轮的未知项 | 现在的结论 |
|---|---|---|
| 1 | `winapp` 写侧 11 个动词 | **全部可用**，且发现 3 个使用陷阱 |
| 2 | `find-api` 的 caveat 排除哪些元数据 | 排除了**已安装运行时**的元数据，不影响查询结果；`2.3.12` 是 Foundation 组件包版本 |
| 3 | 开发者模式 | **已开启**（`AllowDevelopmentWithoutDevLicense=1`, `AllowAllTrustedApps=1`） |
| 4 | 进程退出断言 | `winapp` 确无此能力；`Get-Process`/`psutil` 可靠 |
| 5 | `verify_playback` 像素分支 | **根因找到**：GPU 合成视频对 GDI 捕获不可见。这是产品级技术事实，不是探针 bug |
| 6 | `task-board` 为何空置 | **随 bundle 加载但从未使用**（`tasks: []`, `revision: 0`），不是故障 |

以及**我上一轮的 1 个错误推荐**，已纠正：

| 我说过 | 实测真相 |
|---|---|
| 建议装 `windbg` 补"stowed exception 归因空白" | **错了**。它的 11 个技能全部面向 native C/C++ 或 kernel，`windbg-user-exception-triage` 的 description 明确写着 **"Not for managed .NET exceptions, WinUI/XAML app errors"**——正是本项目唯一的崩溃类型 |

---

## 1. `winapp ui` 写侧 11 个动词

用 Notepad 做安全靶子（不打扰用户的 mpvw 窗口），逐个实跑。

| 动词 | 结果 | 证据 |
|---|---|---|
| `send-keys` | **可用** | 键入 21 字符，`Document` 的 `value` 读到内容【已运行】 |
| `set-value` | **可用** | 设 `SET-VALUE-VERIFIED` 后读回一致【已运行】 |
| `get-value` | **可用** | 返回 `SET-VALUE-VERIFIED`【已运行】 |
| `focus` | **可用** | `Focused mnu-edit-3130`，exit 0【已运行】 |
| `get-focused` | **可用** | 回读焦点元素及其边界【已运行】 |
| `invoke` | **可用** | `[collapsed]` → `[expanded]`，状态可验证【已运行】 |
| `click` | **可用** | `click on mnu-edit-3130 at (309, 445)`【已运行】 |
| `scroll` | **可用** | `Scrolled doc-4f57`【已运行】 |
| `hover` | **可用** | `Hovered ... dwelled 800ms`【已运行】 |
| `drag` | **可用** | `left-dragged from (1008,1111) to (211,445)`【已运行】 |
| `touch` | **可用** | `tap at (1008,1111) with 1 finger(s)`【已运行】 |
| `pen` | **可用** | `pen tap with 1 point(s), pressure 0.50`【已运行】 |
| `record` | **可用** | 录 30 帧 1694×1274 h264【已运行】 |
| `yield` | **可用** | 需 `WINAPP_UI_WORKFLOW_ID`，返回 `Nothing to release`【已运行】 |

### 三个必须记住的使用陷阱

**(a) 选择器歧义会静默命中错误的元素。** `get-value 'Edit'` 在 Notepad 上返回了
`编辑`（菜单项），而不是编辑框。必须用 slug（`inspect` 输出的 `doc-4f57` 这类）
或加 `--type`。**这不是无输出，是错误的输出**——比无输出更危险。

**(b) 不是所有动词都支持 `--type`。** `get-value` 支持，`set-value` 不支持
（`Unrecognized command or argument '--type'`）。参数集因动词而异，用前查 `--help`。

**(c) `record --capture-screen` 要求目标在前台**，否则拒绝并说明原因
（"would capture whatever window is actually in front"）。去掉 `--capture-screen`
直接录窗口则可用。这个拒绝是对的，不是缺陷。

---

## 2. `find-api` 的 caveat

```
"caveats": [
  "Installed Windows App Runtime 2.5 does not match the referenced Windows App SDK
   2.3.12; its metadata is excluded from this project's API surface."
]
```

**排除的是什么**：已安装的 Windows App Runtime 2.5 的元数据。保留的是项目**引用**的
那一套。

**`2.3.12` 是什么**：不是元包版本。实测项目引用链【已运行，读 `project.assets.json`】：

```
Microsoft.WindowsAppSDK          2.5.1   ← 元包
Microsoft.WindowsAppSDK.Runtime  2.5.1   ← 运行时
Microsoft.WindowsAppSDK.Foundation 2.3.12 ← 拆分后的组件包
Microsoft.WindowsAppSDK.WinUI     2.3.9
```

而机器上装的运行时有 `Microsoft.WindowsAppRuntime.2 2.5.1.0`【已运行，`Get-AppxPackage`】。
所以"2.5 与 2.3.12 不匹配"是**必然的**——元包与它内部某个组件包的版本号本就不该相等。
另外 csproj 里 `WindowsAppSDKSelfContained=true`，运行时自带，已装运行时本就不该参与判定。

**实际影响：无。** 查询 `NavigationView` 正常返回 342 属性 / 61 事件 / 72 方法【已运行】。

---

## 3. 开发者模式

```
HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock
  AllowDevelopmentWithoutDevLicense = 1
  AllowAllTrustedApps               = 1
```
【已运行】两项都是 1，已开启。

---

## 4. 进程退出断言

**结论：`winapp` 没有这个能力。** 在 `winapp --cli-schema`（244450 字符）里检索：

- `PID` 出现 24 次，全部用于**指定目标窗口**（`--app ... or PID`）
- `exit` 出现 5 次，全部在描述文本里（"exit the command"、"after the application exits"）
- `kill` / `terminate` / `Process`（大写）出现 **0 次**

反向验证断言本身可靠：spawn 一个 `pwsh Start-Sleep 30`，`psutil.pid_exists` 在
terminate 后返回 `False`，`Get-Process` 也返回 `gone`【已运行】。

**所以这一条留在自写侧是对的**，`AGENTS.md` 里也已经写明了。

---

## 5. `verify_playback` 像素分支 —— 根因

这一项花了最多力气，因为结论推翻了"探针写错了"的直觉。

### 现象

`verify_playback.py` 连续 3 次运行，`max frame-to-frame change` 恒为 **0.0%**，而
`mpv time-pos` 正常前进、`pause=false`、`idle=false`，判定走的是"mpv 时钟"这条后备路径。
也就是说**像素分支从未被真正验证过**。

### 排查过程（每一步都排除一个假设）

| 假设 | 做法 | 结果 |
|---|---|---|
| 抓错窗口了 | `FindWindowW` + 进程过滤 | 抓对了 mpvw 主窗【已运行】 |
| 窗口被别的窗口遮挡 | `WindowFromPoint` 采样 25 个点 | 未被遮挡【已运行】 |
| `ImageGrab` 方法本身不行 | 同窗口独立抓两张比对 | **16.1% 变化**，方法可用【已运行】 |
| `OnScreen` 把窗口移出屏幕 | 复现其调用 | 窗口在 (20,20)，正常【已运行】 |
| 片源是静态图 | `ffprobe` + 抽帧比对 | **1800 帧 / 60s**，抽 5 帧互差 10–17%【已运行】 |
| mpv 其实没在播 | IPC 读 `time-pos` 两次 | **8.675 → 11.176**（2.5 秒实走 2.5 秒）【已运行】 |

### 根因

用官方工具同时抓两张（间隔 1.5 秒），做分区比对：

```
overall diff_bbox: (74, 1778, 616, 1817)
  top    (标题/菜单) diff = None
  mid    (视频区)    diff = None        ← 完全静止
  bottom (控制条)    diff = (74, 307, 616, 346)   ← 只有时间码在变
```

视频区有 113 种颜色（不是黑屏，画面**确实渲染出来了**），但它**逐像素不变**。

**结论：mpv 用 GPU 合成（SwapChainPanel + D3D11）把画面送到屏幕上，而 GDI 系的屏幕捕获
（`ImageGrab`/BitBlt/`PrintWindow`）拿不到这条通路的内容。** 捕获到的是窗口的 GDI 表面，
视频区是"最后合成前"的状态。

**这不是 `verify_playback` 的 bug，也不是本项目的 bug**，是 GDI 捕获对 GPU 合成视频的固有
限制。有意思的是 `verify_playback` 自己的注释里预见了这一点（"composition output + ImageGrab
can produce a static capture"），但把结论写成了"inconclusive, not a failure"——于是它
**永远不会因为画面真的不动而失败**，这个分支等于没有。

### 官方工具也救不了

用 `winapp ui screenshot` 的两种方式各抓两张：

| 方式 | 底层 | 两张之间 diff |
|---|---|---|
| 默认 | `PrintWindow` | `None`（无变化） |
| `--capture-screen` | `BitBlt` | `None`（无变化） |

**两条官方路径同样拿不到视频帧。** 所以"改用官方工具就能测像素"是错的。

### 这意味着什么

要验证"画面真的在动"，GDI 截图这条路走不通。可行的替代只有：
- 用 mpv 自己的 `time-pos` 前进作为判据（**现状就是这么做的**，是正确的）
- 用 Windows Graphics Capture / Desktop Duplication API 抓（需要额外实现，属于新工作）
- 用 `mpv` 的 `screenshot` 命令让它自己导出一帧（走 mpv 内部，绕过 GDI）

**建议**：把 `verify_playback` 里那段"像素不变 → inconclusive"的判据改写成明确说明——
在当前捕获方式下像素判据**不可能**成立，留着它只会让人误以为这条路径被验证过。

---

## 6. `task-board` 为何空置

**不是故障，是从未使用。**

`~/.dsh/task-board/ledger-v2.json`【已运行】：

```json
{ "schemaVersion": 6, "revision": 0, "tasks": [], "recentRequests": [],
  "scheduler": { "ledgerId": "181ba036-...", "lastTickAt": 1793027593219 } }
```

- `revision: 0`、`tasks: []` → 没有创建过任何卡片
- `lastTickAt` = 2026-10-26 23:13:13 → 调度器**运行过**
- 它确实在加载：`profiles/desktop/package.json` 的 `dsh.profile.bundles` 里排第 3，
  `patchReload: "live"`【代码】

所以机制完整可用，只是这个项目一直用 `create_goal` + `agent-teams` 驱动，没走任务板。
没有需要修的东西。

---

## 7. 我上一轮的错误推荐：`windbg`

上一轮我写"装它补崩溃归因空白，`AGENTS.md` 的读崩溃一节全靠读 `P7` 字段但没有工具去取它"。

**这个理由站不住。** 实测 11 个技能的 description【已运行】：

| 类别 | 技能数 | 面向 |
|---|---|---|
| kernel | 5 | bugcheck / IRP / lock / verifier |
| user-mode **native** | 5 | exception / heap / mutex / TTD / VM 耗尽 / wait chain |
| method | 1 | 通用方法论 |

`windbg-user-exception-triage` 的原文：

> Use when a **native C/C++** app, service, or user-mode driver host ... crashes ...
> **Not for managed .NET exceptions, WinUI/XAML app errors, or kernel bugchecks.**

本项目唯一的崩溃类型（`0xc000027b` + `Microsoft.UI.Xaml.dll`）恰好被排除在外。

而且它**硬依赖调试器二进制**：技能正文里的工作流是 `.exr -1`、`.ecxr`、`k`、`!analyze -v`。
本机 `windbg` / `cdb` / `kd` / `dumpchk` / `procdump` **全部不在 PATH**，
`C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\` 目录也不存在【已运行】。

**结论**：插件已装但**对本项目不可用**。留着无害（它不自动触发），但不该被当作
"补上了崩溃归因"。真正的缺口仍然开着。
