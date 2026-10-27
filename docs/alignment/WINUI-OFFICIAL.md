# WinUI 官方工具评估（WINUI-OFFICIAL）

> **证据等级**：【已运行】本轮实际执行并给出原始输出 ｜【代码】源码/配置静态事实（附 `path:line`）｜【推断】推理未直接验证 ｜【未知】需补充
>
> **本轮约束的执行情况**：未启动 `mpvw.exe`。接手时 PID **19572** 已在运行（`StartTime 2026/10/27 10:45:53`），全部 mpvw 测量都在该进程上完成；收尾时它仍在运行（`Get-Process -Name mpvw` → count=1, id=19572），**不是**我启动的、也**没有**被我关闭。Notepad 是我为 DPI 对照启动的（PID 22000/33688，10:56:13），已全部 `Stop-Process`，当前 `notepad` count=0。
> 未触碰 `mpv-deploy`、`mpv-deploy.bak-20261026`、`mpv.old-20261026`、`mpv` 的任何内容；未运行 `build.ps1`；未执行 `dotnet test`；未安装任何需要 UAC 的东西。
>
> 一句话结论：**上一轮的 UI 检查几乎全部可以迁到官方 `winapp ui`，且截图能力比自写版更强；但遮挡判定、XAML 良构、部署一致性、进程退出断言这四项 `winapp` 确实没有等价物。**

---

## 1. 官方工具清单与实测能力矩阵

`winapp` 版本【已运行】：`winapp --version` → `0.7.1`。`winapp ui` 下确有 **22 个动词**【代码：`winapp ui --help` 的 Commands 段】。

### 1.1 `winapp ui`（UIA 自动化，本轮实测主力）

| 动词 | 作用 | 是否运行 | 目标 | 实测结果 |
|---|---|---|---|---|
| `status` | 连接目标并显示连接信息 | 【已运行】 | `-a mpvw` 与 `-a 19572` | `Process: mpvw / PID: 19572 / Window: MPV / HWND: 197366 / Connected`，两者 exit=0 |
| `list-windows` | 列可见窗口（HWND/标题/进程/尺寸） | 【已运行】 | 全桌面 + `-a mpvw` | 全桌面 29 个顶层窗口；`-a mpvw` → `hwnd 197366, title "MPV", className "WinUIDesktopWin32WindowClass"` |
| `inspect` | 元素树（类型/名称/bounds） | 【已运行】 | `-a mpvw`，`-d 4` / `-d 6`（`PaneRoot`）/ `-d 12` | 根窗口 `elementCount 46`；`-d 12` 共 **71** 个元素；头部含 `windowDpi:168, scale:1.75, coordinateSpace:"physical-screen-pixels"` |
| `inspect -i` | 只列可交互元素（默认深度 8） | 【已运行】 | `-a mpvw --interactive` | `elementCount 39`，`depth 8` |
| `search` | 按文本/属性搜元素 | 【已运行】 | `-a mpvw "Grid"` | `{"matchCount":0,"hasMore":false,"matches":[]}`，**exit=1** —— Grid 无节点（见 §3.2） |
| `get-property` | 读 UIA 属性（单个或全部） | 【已运行】 | `PlayPauseButton` / `ShellNav_home` | `BoundingRectangle: "277,1710,98,98"`、`Name: "播放"`、`ControlType: Button`、`IsKeyboardFocusable: True`；字体类属性为 `Unavailable` |
| `get-value` | 读值（TextPattern→ValuePattern→Name） | 【已运行】 | `ShellTitleText` | `{"elementId":"lbl-shelltitletext-fcf0","text":"MPV"}` |
| `wait-for` | 等出现/消失/值到目标 | 【已运行】 | 存在元素 / 不存在元素 / `--value` | 命中：`found:true, waitedMs:16`，**exit=0**；不存在：`{"found":false,"timedOut":true}`，**exit=1**；`--value "主页"` → `Element found with value="主页" after 19ms` |
| `invoke` | 触发 Invoke/Toggle/Selection/Expand | 【已运行】 | `VideoSortButton` | `Invoked btn-videosortbutton-fcfd via InvokePattern`，**exit=0**，随后 `list-windows` 多出弹出窗口 hwnd **1313392** |
| `screenshot` | 截窗口/元素为 PNG | 【已运行】 | `-a mpvw` / `-w 197366` / `-w ... --capture-screen` | 见 §1.2（这是本轮最有价值的发现） |
| `get-focused` | 当前键盘焦点元素 | 【已运行】 | `-a mpvw` | `VideoSortButton`（"排序方式: A-Z"） |
| `send-keys` | 合成键盘输入 | 【已运行】 | `-a mpvw "escape"` | `✅ Posted 1 key action(s) via post-message`；同时打印警告：*post-message 对 WinUI 3 无窗口控件可能被静默丢弃，建议 `--via send-input`* |
| `click` `drag` `touch` `pen` `hover` `scroll` `scroll-into-view` `set-value` `focus` `record` `yield` | 交互/录制/让出桌面 | 【未运行】 | — | 本轮无需，未验证。`yield` 额外要求 `WINAPP_UI_WORKFLOW_ID`【代码：`winapp ui --help`】 |

### 1.2 `winapp ui screenshot` 三种模式实测（关键）

`VideoSortButton` 的下拉已打开（弹出窗口 hwnd 1313392，`className "Microsoft.UI.Content.PopupWindowSiteBridge"`，`ownerHwnd 197366`，`label "popup"`）时分别截三张：

| 命令 | 输出 JSON | 结果 |
|---|---|---|
| `ui screenshot -a mpvw -o ...` | `3108x1588`，`windows` 列两项：主窗 `captured:true`、**弹出窗 `captured:true`** | 【已运行】合成图右侧单独贴上弹窗内容，A-Z / Z-A / 最新在前 / 最早在前 **四个菜单项清晰可见** |
| `ui screenshot -w 197366 -o ...` | `3108x1588`，同样两项 `captured:true` | 【已运行】字节数与上一张完全相同（541603），说明 `-w` 也含其 owned 窗口 |
| `ui screenshot -w 197366 --capture-screen -o ...` | `2880x1570` | 【已运行】屏幕 DC 原位截图，弹窗**在原本位置**可见（未被裁切） |

**这一条直接推翻了"截图必须用 CopyFromScreen 因为 PrintWindow 漏掉 WinUI 弹窗"的既有规矩**：`winapp` 根本不用 `PrintWindow`，它把 WinUI 弹窗当作独立的 owned HWND 一并合成/原位截取。详见 §5.3。

### 1.3 `find-api` / `find-ui` / `target`

| 命令 | 作用 | 是否运行 | 目标 | 实测结果 |
|---|---|---|---|---|
| `find-api stats` | 项目 API 索引统计 | 【已运行】 | `--project-dir ...\mpv-winui\mpv-winui` | `packages 19, namespaces 873, types 52876, members 332961, winMdFiles 38`，**并带 `caveats`**（见 §5.4） |
| `find-api packages` | 列出已索引包与版本 | 【已运行】 | 同上 | 19 个包；`Microsoft.WindowsAppSDK.Foundation 2.3.12`、`.WinUI 2.3.9`、`.InteractiveExperiences 2.1.9`、`WindowsSDK 10.0.26100.0`，全部 `status: ok` |
| `find-api members` | 列类型成员 | 【已运行】 | `NavigationView --filter selected` | `fullName Microsoft.UI.Xaml.Controls.NavigationView`，`totalProperties 342`，过滤后返回 `SelectedItemProperty` / `SelectedItem` |
| `find-api check-property` | 校验属性是否存在（缺失非零退出） | 【已运行】 | `TitleBar IsBackButtonVisible Subtitle IconSource` | `found:true ×3, missingCount 0`，**exit=0**；故意查 `NoSuchPropXYZ` → `found:false`，**exit=1** |
| `find-ui` | 搜 WinUI Gallery/Toolkit 样例 | 【已运行】 | `"navigation"` | `matchCount 2`，返回 `gallery-navigationview-1` 等 scenario id；`--list` 共 **398** 条 |
| `target snapshot sandbox` | 只读探测沙箱就绪 | 【已运行】 | `sandbox` | `{"running": false, "attached": false, "desktop": {"unavailable": "sandbox_target_stale"}, "windowCount": 0}` —— **本机走不了 sandbox，只能 local** |
| `new` `init` `restore` `update` `package` `sign` `cert` `manifest` `run` `store` `tool` `unregister` `create-debug-identity` `embed-identity` `create-external-catalog` `get-winapp-path` `az-sign` | 脚手架/打包/签名/运行 | 【未运行】 | — | 本轮任务不需要；且 `package`/`run` 会触发构建，与"不跑 build.ps1 / 不部署"的约束冲突 |

---

## 2. 用官方工具重做上一轮的检查 —— 对照表

上一轮脚本位于 `C:\Users\z2637\Documents\DeepSeek\_rebuild-20261026\`（与 `mpv.old-20261026\_work\rebuild-20261026\` 同内容）【代码】。

| 上一轮自写程序 | 做了什么 | winapp 官方等价物 | 结论 |
|---|---|---|---|
| `verify_exe.py`（自写 UIA 遍历 + `uiautomation`） | 启动、读窗口标题、读 4 个导航项与 3 个播放条的中文名、断言进程关闭 | `ui list-windows` / `inspect` / `get-property` / `get-value` / `wait-for` | **基本可替换（唯一例外：进程退出）**。标题：`get-value ShellTitleText` → `MPV`【已运行】。4 个导航项：`inspect PaneRoot -d 6` 直接给出 `ShellNav_home=主页`、`ShellNav_music=音乐库`、`ShellNav_videos=视频库`、`ShellNav_queue=播放队列`【已运行】，与 `verify_exe.py` 的 `NAV` 字典逐一吻合。3 个播放条：`get-property PlayPauseButton -p Name` → `播放`【已运行】；`VolumeMuteButton`、`FullScreenButton` 在 `inspect -d 4` 树中同在 `RightCommandBar` 下，名 `静音`/`全屏`【已运行，`-d 4` 输出内】。**"断言进程关闭"没有对应动词** —— `wait-for --gone` 等的是**元素**消失，不是进程退出；这一句仍需 `Get-Process -Name mpvw`。本轮**未执行**该断言（我无权关闭你已开着的 mpvw）。 |
| `uiautomation` 依赖的自动化能力 | 通用 UIA | `winapp ui` 22 个动词 | **可替换（已用子集验证）**。读侧 8 个动词全部命中【已运行】。上一轮 `uiautomation` 做的是"遍历 + 读 Name"，`winapp ui inspect`/`get-property` 一次就给出同样数据，且额外附带 `className`、`isInvokable`、`isOffscreen`、`expandState`、`toggleState`，这些是手写遍历要么没写、要么要自己补的。**注意**：`click`/`drag`/`touch`/`pen`/`set-value`/`focus`/`scroll`/`record` 8 个写侧动词本轮**未运行**，属【未知】。 |
| `diag_occlusion.py`（自写 `WindowFromPoint`） | 判断被测窗口是否被遮挡 | **无** | **不可替换**。`winapp --cli-schema`（244450 字符）里 `occlusion` / `occlud` / `WindowFromPoint` **均不出现**【已运行：schema 关键字检索全 False】。`ui list-windows` 的返回里只有 `isForeground`，且**其顺序不是 z-order**（实测首条是 `explorer`，前台窗口排在第 20 位）【已运行】，拿不到"这个点上最上层是谁"。`diag_occlusion.py` 用的是 `WindowFromPoint` + `GetAncestor(GA_ROOT)` + `GetWindowThreadProcessId`，本轮我复现该逻辑：PMv2 感知下三个探测点全部返回 `root=197366 pid=19572` → mpvw **未被遮挡**【已运行】。这段逻辑必须自己写。 |
| `verify_path_fix.py`（自写静态分析） | 校验路径解析（按 Python 语义顺序求值模块级赋值） | **无（非 UI 问题）** | **不可替换，且不该去找等价物**。`winapp` 是 UI/打包 CLI，没有任何"求值 Python 源码"的能力。该脚本的价值恰恰在于"它自己第一版是错的"这段注释（`verify_path_fix.py:15-22`【代码】），属于领域知识，官方工具无从知晓。**结论：保留。** |
| `check-xaml.py`（仓库自带） | XAML 良构 + `--` 注释（WMC9997 前置拦截） | **无** | **不可替换**。`winapp --help` 的 21 个顶层命令里没有任何 XAML 解析/校验动词；唯一的 XAML 相关命令是 `new`（出模板）【代码】。本轮实跑 `python tools\check-xaml.py` → `OK: 39 XAML files well-formed, no illegal comment content`，exit=0【已运行】。理论上 `winapp package` 会因 XAML 编译失败而报错，但那要**先构建**，与本轮"不跑 build.ps1"的约束冲突，也不是"构建前的 1 秒拦截"。**结论：保留。** |
| `compare-deploy.ps1`（仓库自带） | 部署一致性（双向比对 size/hash） | **无** | **不可替换**。最接近的是 `winapp target push/pull/snapshot`，但那是 **Windows Sandbox 专属**（`target snapshot sandbox` 实测 `running:false`【已运行】），而且 `push/pull` 的语义是"复制"，不是"比对两组目录并报告差异"。`compare-deploy.ps1:9-15` 的注释说明了它存在的真实原因（`deploy-test.sh` 的 `find` 被 Windows `find.exe` 抢走，静默空跑）【代码】——这是一个**测试脚本正确性**问题，不是工具能力问题。**结论：保留。** |

**净结论**：6 项里 **2 项可迁（`verify_exe.py` 的读侧、`uiautomation` 读侧）**，**4 项必须自写**。其中 `verify_exe.py` 的"进程退出"子断言也要留在自写侧。

---

## 3. 官方插件的能力边界（用具体证据）

插件根目录 `C:\Users\z2637\.claude\plugins\cache\win-dev-skills\winui\0.7.1`，7 个 skill 在 `agent-plugin\skills\`，1 个 agent 在 `agents\winui-dev.agent.md`【代码：目录遍历】。

| Skill | 本仓库是否可用 | 具体能干什么 | 死重部分 | 证据（一行） |
|---|---|---|---|---|
| `winui-ui-testing`（341 行） | **可用，且是主力** | `winapp ui` 的断言骨架、picker/flyout/ContentDialog 处理、`WINAPP_UI_WORKFLOW_ID` 协作、截图/录制交付校验 | `--on sandbox` 全部路径（本机 `running:false`）；`touch`/`pen`/`drag` 对播放器无场景 | **已通过 `skill` 工具调用**【已运行】，返回 341 行全文；其模板与本项目三处落差见 §3.2 |
| `winui-code-review`（94 行） | **可用** | 8 组清单；其"报 API 不匹配前先用 `find-api` 校验"的纪律正是本轮 §5.4 用上的 | MVVM 组（`[ObservableProperty]`/`[RelayCommand]`）与全局化组（`x:Uid`/`.resw`）——本仓库是 C++/WinRT + `AppLang`×`Languages/*.json`，不适用【代码：`AGENTS.md:357`】 | **已通过 `skill` 工具调用**【已运行】，返回 94 行全文 |
| `winui-design`（189 行） | 部分可用 | 控件选型表、"写 XAML 前先 `find-ui`"的纪律、`AppWindow.Resize` 取 DPI 的正确写法 | App-shape 表把"媒体/画布"判为**不要 NavigationView**，而本应用是 4 段 NavigationView【已运行：`inspect` 见 `ShellNav_*` 四项】 | 读全文 189 行【代码】；`find-ui "navigation"` 实测可用【已运行】 |
| `winui-dev-workflow`（96 行） | 部分可用 | 常见错误表（XLS0414/XDG0062/0x8000FFFF 等）；崩溃诊断用 `--debug-output` | "新建项目"`winapp new`、"`dotnet add package ... WinUIAnalyzer`"两步是死重——**分析器已引** | 【代码】`mpv-winui.csproj:81` 已有 `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`；NuGet 缓存里该包存在【已运行：目录列举】 |
| `winui-packaging`（102 行） | 低 | "Self-Contained 不等于单文件"一节对 `WindowsAppSDKSelfContained` 的解释 | MSIX/证书/CI 全段——本仓库部署是 overlay 到 `mpv-deploy`，不出 MSIX | 【代码】读全文；`mpv-winui.csproj:22 <WindowsAppSDKSelfContained>true` |
| `winui-setup`（170 行） | 低 | 前置检测脚本（.NET SDK / WinApp CLI / 开发者模式）可复用 | 安装动作——`winapp 0.7.1` 已就位，无活可干 | 【已运行】`winapp --version` → `0.7.1` |
| `winui-wpf-migration`（111 行） | **不适用** | — | 全段 | 本仓库无 WPF 源；`find-api` 的 `alsoMatched` 里出现 `Windows.UI.Xaml.*` 是 SDK 索引，不是源码引用【推断】 |

**`winui-dev` 是 agent 不是 skill**：`agents\winui-dev.agent.md`（26 行）自述"You Are The WinUI Developer"，并要求先加载 `winui-dev-workflow` + `winui-design`【代码】。经 compat 层以同名 skill 形态出现。本轮**未再调用**它——它的内容是过程性指令，本轮目标（评估工具能力）不需要这条流程。

### 3.2 `winui-ui-testing` 模板与本项目三处已知落差的逐条对撞

模板引自该 SKILL.md 第 37–183 行（`ui-tests.ps1`）。三处落差：

**(1) `Grid` 无 UIA 节点** — 模板第 126–159 行的「App controls have AutomationIds」检查：
```powershell
$appElements = @($allElements | Where-Object {
    $_.type -match 'Button|TextBox|ComboBox|CheckBox|ToggleSwitch|TabItem|Edit' -and ...
})
```
这条**永远不会报出 Grid 问题**，因为 `$_.type` 过滤器只放行叶子控件，布局容器被整体排除。也就是说：模板在这台机器上会**绿着通过**，但它对"Grid 拿不到节点"这件事完全无感。
**修正形态**：断言必须落在 **Grid 的子元素**上，绝不落在 Grid 自身。深度要拉到能看见子元素（`-d 12` 时 `ShellNav` 的 4 个 `NavigationViewItem` 才全部出现）；`inspect` 的默认 `-d 4` 只到 `PaneRoot` 一层，**会漏掉 4 个导航项**。即：
```powershell
# 对 Grid 少写一条 wait-for，多写一条对子元素的 wait-for
Invoke-Ui @('inspect', 'PaneRoot', '--json', '-d', '6')   # 不要 '-d', '4'
```

**(2) `PrintWindow` 失明** — 模板第 83–92 行 `Save-Screenshot` 用的是 `screenshot --output <path> -Window $hwnd`，即 `-w <主窗 hwnd>`。按 0.7.1 的 `screenshot --help`，`-w` 的语义是"one window **plus its owned windows**"，所以模板**这一处恰好是对的**——实测 `-w 197366` 与 `-a mpvw` 产出的字节数完全相同（541603），弹窗 `captured:true`【已运行】。
真正冲突的是**本仓库自己的规矩**（`AGENTS.md:201`"PrintWindow misses WinUI popups; use CopyFromScreen"）与 `tools/mvpw-mcp/background_capture.py`、`tools/mvpw-mcp/mpvw_tools.py` 里对 `PrintWindow` 的使用【代码：grep 命中 3 个文件】。
**修正形态**：不再手写 GDI。要"带弹窗、按窗口取"→ `winapp ui screenshot -a mpvw -o x.png`（合成，贴边）；要"弹窗在原位"→ `winapp ui screenshot -w <hwnd> --capture-screen -o x.png`。两者都比 `CopyFromScreen` 稳（不要求前台、不要求窗口可见）。

**(3) DPI 系数** — 模板**通篇没有任何坐标换算**，`hwnd`/slug 直接传，`drag`/`click` 只用元素中心。这正是正确做法。冲突同样来自本仓库：`AGENTS.md:202`「UIA BoundingRectangle has a ×1.5 DPI scale factor」。若有人照此把 UIA 坐标除以 1.5，再喂给 `ui drag`/`ui touch`/`ui pen` 的 x,y，误差是 `1.75/1.5 = +16.7%`。
**修正形态**：**不做任何换算**。`get-property -p BoundingRectangle` 返回的 `277,1710,98,98` 就是 `physical-screen-pixels`（`inspect` 头部自报的 `coordinateSpace` 字段），可直接用于坐标类动词。详见 §5.1。

---

## 4. 结论

### 4.1 应当迁到官方工具的检查

| 检查 | 替换命令 |
|---|---|
| 窗口标题 | `winapp ui get-value ShellTitleText -a mpvw --json` → `.text -eq 'MPV'` |
| 4 个导航项中文名 | `winapp ui inspect PaneRoot -a mpvw --json -d 6`，取 `ShellNav_home/music/videos/queue` 的 `name`；或逐条 `winapp ui wait-for ShellNav_home --value "主页" -t 2000` |
| 播放条中文名 | `winapp ui get-property <aid> -a mpvw -p Name --json`（`PlayPauseButton`/`VolumeMuteButton`/`FullScreenButton`） |
| 元素存在/消失断言 | `winapp ui wait-for <selector> -t <ms>`（命中 exit 0，超时 **exit 1**）→ 可直接当测试判定【已运行】 |
| 元素值断言 | `winapp ui wait-for <selector> --value "<文本>" [--contains] -t <ms>` |
| 截图取证 | `winapp ui screenshot -a mpvw -o <path>`（含弹窗）；需原位用 `--capture-screen` |
| 点按钮 / 开下拉 | `winapp ui invoke <aid>`（`InvokePattern` 优先）；不支持时退 `winapp ui click <aid>` |
| 焦点校验 | `winapp ui get-focused -a mpvw --json` |
| API 是否存在 | `winapp find-api check-property <Type> <Prop> --json --project-dir <dir>`（缺失 exit 1）【已运行】 |
| 控件样例 | `winapp find-ui "<intent>" --json` / `--id <scenario>` |

### 4.2 必须保留自写的检查及理由

1. **遮挡判定**（`diag_occlusion.py`）：`winapp` 无 `WindowFromPoint` 类动词，`list-windows` 不保证 z-order【已运行：schema 检索 + 顺序观察】。保留，但应按 §5.1 补 `SetProcessDpiAwarenessContext(-4)`，否则坐标被虚拟化。
2. **进程退出断言**：`wait-for --gone` 是元素级，不是进程级。保留 `Get-Process -Name mpvw`。
3. **XAML 良构 / WMC9997**（`check-xaml.py`）：无 XAML 校验动词。保留——它是构建前的 1 秒拦截，官方替代（`winapp package`）要先构建。
4. **部署一致性**（`compare-deploy.ps1`）：`target push/pull` 是沙箱专属且语义是"复制"不是"比对"。保留。
5. **路径解析静态验证**（`verify_path_fix.py`）：非 UI 问题。保留。
6. **IPC / mpv 版本 / 原生崩溃诊断**（`diag_pipe*.py`、`diag_mpv_version.py`、`diag_native_crash.py`）：`winapp` 完全无关。保留。

### 4.3 `tools/mvpw-mcp/` 该变成什么

现状【已运行】：63 个 `.py`，其中 **20 个 `import uiautomation`**；按前缀分布 `verify_*` 23 / `diag_*` 20 / `audit_*` 7 / `probe_*` 5 / 其余 8。

建议**三分**，而不是整体保留或整体删除：

- **删掉"UIA 读侧"那 20 个里的绝大多数**：凡是"打开某页 → 读某元素 Name/Rect → 比对"的 `verify_*`/`probe_*`，改为对 `winapp ui` 的一行调用。它们现在的价值是把 UIA 的坑（等待、重试、坐标）封起来了，而这些坑 `winapp ui wait-for` 已经封装得更好，且返回结构化 JSON + 可靠的退出码。
- **保留 `mcpw_server.py` 作为薄适配层**：MCP 协议本身是官方工具**没有**的（`winapp` 是 CLI，不是 MCP server）。把它改成"把 `winapp ui` 的 JSON 暴露为 MCP 工具"，而不是继续自己走 UIA。这才是与官方工具的分工点。
- **保留非 UI 部分**：`diag_pipe*`、`diag_ipc_*`、`diag_mpv_version`、`diag_native_crash`、`diag_settings_applied`、`verify_path_fix` 一类——`winapp` 不碰 mpv IPC、不碰注册表、不碰原生崩溃。这部分是**这个项目真正的护城河**。

一句话：**`mvpw-mcp` 应当从"自己实现 UIA"退化成"官方 CLI 的 MCP 外壳 + 本项目专属的 IPC/原生诊断"**。

---

## 5. 你实测到的事实，包括推翻既有说法的

### 5.1 DPI 系数：`AGENTS.md` 的 ×1.5 是错的，实测 1.75，且"UIA 有缩放系数"这个前提本身也是错的

`AGENTS.md:202` 原文【代码】：`UIA BoundingRectangle has a ×1.5 DPI scale factor on this machine.`

三项独立测量【已运行】：

| 测量 | 值 |
|---|---|
| `winapp ui inspect` 自报 | `windowDpi: 168`，`scale: 1.75`，`coordinateSpace: "physical-screen-pixels"`，`dpiAwareness: "per-monitor-aware"` |
| `GetDpiForWindow(hwnd 197366)` | `168` → `168/96 = 1.75` |
| `GetScaleFactorForMonitor` | `hr=0x0, SCALE_ENUM=175` → `1.75` |

**更关键的是"没有任何系数"这一层**。我做了两组对照：

- **Notepad 对照**（我启动，已清理）：UIA 报 `x=151 y=345 w=1715 h=1470`。以 **Per-Monitor-V2 感知**调用 `GetWindowRect` → `L=151 T=345 W=1715 H=1470`，**逐值完全相同**，比值 1.0。
- **mpvw 自身**：`get-property PlayPauseButton -p BoundingRectangle` → `"277,1710,98,98"`；PMv2 下 `GetWindowRect(197366)` → `L=264 T=264 W=2880 H=1570`，与 UIA 根节点 `x=264 y=264 w=2880 h=1570` 一致。
- **换成 DPI-unaware 的 `python -c "import uiautomation"`**（进程自报 `GetDpiForSystem=96`、虚拟桌面 `2194x1234`）读同一元素：窗口 `(264,264,3144,1834)[2880x1570]`、按钮 `(277,1710,375,1808)[98x98]` —— 与 `winapp` **一模一样**。

所以：**测量调用方的 DPI 感知级别不影响 UIA 返回的数值；UIA 永远给物理像素。** 需要换算时只有一条：`DIP = 物理 ÷ 1.75`。

**"×1.5"很可能是这样产生的**（我复现了这个陷阱，值得记下来）：pwsh 默认非 DPI 感知，我最初直接 `GetWindowRect(Notepad)` 得到 `980x840`，而 UIA 给 `1715x1470`，比值恰为 **1.75** —— 看起来就像"UIA 有 1.75 的缩放系数"，其实是**调用方被 DPI 虚拟化**了。等到我补上 `SetProcessDpiAwarenessContext(-4)` 后，两边立刻一致。`1.5` 对应的是 144 DPI / 150% 缩放，不是本机当前设置（本机 168 DPI / 175%）。

### 5.2 `Grid` 无 UIA 节点：证实

【已运行】`inspect -d 12` 取到全树 **71** 个元素，按 `className` 分组：`TextBlock 14 / AppBarButton 12 / (空) 12 / Button 6 / NavigationViewItem 4 / Image 3 / ScrollViewer 3 / ApplicationBar 3 / RadioButton 2 / 其余各 1`。`className` 含 `"Grid"` 的元素 **count = 0**。`winapp ui search "Grid"` → `matchCount 0`，**exit=1**。

**一个必须说清的边界，否则上面这条会被误读成"有名字就行"**：`ShellNav` 和 `PaneRoot` 确实以 `type: Custom` / `type: Window` 出现在树里且带 `automationId`，但它们**不是 `Grid`**。

- `ShellNav` 是 **`NavigationView`**【代码：`Shell\MainWindow.xaml:160-161` `<NavigationView x:Name="ShellNav" ...>`】，本身的 `className` 在树里是 `SplitViewPane`（它的模板内部 pane）。
- `PaneRoot` 的 `className` 是 **`SplitViewPane`**【已运行：`inspect PaneRoot` 输出】。

也就是说：它们有节点是**因为它们是有 automation peer 的控件**，不是因为"给容器起个名就行"。`Grid` 的问题在于 `Grid` 自身不创建 `AutomationPeer`——这与是否设 `AutomationId` 无关。**`AGENTS.md` 里"`Grid` is not a UIA node, so a container you can see may have no element — assert its children instead"这条是对的，本轮实测支持它**；不要试图靠加 `AutomationId` 把 `Grid` 变成可断言节点，直接断言子元素。

（附带纠正我自己的一个中途错误：我先前按 `AutomationId="ShellNav"` 去 XAML 里 grep，命中 0 就准备下"来源不明"的结论。实际它是 `x:Name`，不是 `AutomationId`——`grep` 一个错的关键字，得到的空结果不是证据。）

### 5.3 `PrintWindow` 失明：机制已被官方工具绕过，且我找到了真实原因

【已运行】`invoke VideoSortButton` 后 `list-windows -a mpvw` 返回**两个**窗口：

```json
{ "hwnd": 1313392, "title": "主机弹出窗口", "label": "popup",
  "width": 240, "height": 272, "ownerHwnd": 197366,
  "className": "Microsoft.UI.Content.PopupWindowSiteBridge" }
{ "hwnd": 197366, "title": "MPV", "label": "window", "width": 2880, "height": 1570 }
```

**WinUI 的 flyout 是一个独立的顶层 HWND（owner = 主窗）**。用 `PrintWindow` 截主窗 HWND 自然截不到另一个窗口的像素——这不是 WinUI 渲染缺陷，是 API 语义。`winapp ui screenshot -a mpvw` 在 JSON 里明确列出两个窗口且 `captured: true`，合成图里四个菜单项（A-Z / Z-A / 最新在前 / 最早在前）清晰可读【已运行，见 §1.2】。

**结论：`AGENTS.md:201` 的"必须用 CopyFromScreen"这条规矩可以退役** —— 不是被更好的自写方案取代，而是被官方工具整体绕开。`CopyFromScreen` 还有两个 `winapp` 没有的硬约束：要求窗口**在前台且未被遮挡**，且截到的是屏幕区域（含别的应用）。

### 5.4 `find-api` 的 "Windows App Runtime 2.5 vs 2.3.12" 告警：确实存在，但它的措辞会误导

【已运行】每一次 `find-api`（`stats` / `packages` / `members` / `check-property`）都带同一条 `caveats`：

```
"Installed Windows App Runtime 2.5 does not match the referenced Windows App SDK 2.3.12;
 its metadata is excluded from this project's API surface."
```

**这条告警的真实成因与它的字面意思不同**：

- 项目引用的是 **元包 2.5.1**【代码：`mpv-winui.csproj:113 <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.5.1" />`】；
- 本机**装的运行时正是 2.5.1.0**【已运行：`Get-AppxPackage Microsoft.WindowsAppRuntime*` → `Microsoft.WindowsAppRuntime.2 2.5.1.0`（以及 2.3.0.0/2.3.1.0/2.4.0.0、`...CBS.2 2.5.1.100`）】；
- 而 `find-api` 拿来比较的 "2.3.12" 是**拆分后的组件包** `Microsoft.WindowsAppSDK.Foundation 2.3.12`【已运行：`find-api packages`】。

也就是说：**元包 2.5.1 与已装运行时 2.5.1 其实是对齐的**；告警是把"元包版本"和"某个组件包的独立版本号"放在一起比，于是永远不相等。另外本仓库是 `WindowsAppSDKSelfContained=true`【代码：`csproj:22`】，"已装运行时"本就不该参与判定。

实际影响有限：`find-api members NavigationView` 仍返回了完整的 342 个属性，说明 WinUI 元数据是索引到的。**具体被排除的是哪个组件的元数据，我没有测出来 ——【未知】**。

### 5.5 其他实测到的事实

- **`winapp` 的退出码可以直接当测试判定**：`wait-for` 命中 0 / 超时 1【已运行】；`find-api check-property` 命中 0 / 缺失 1【已运行】；`ui search` 零命中 1【已运行】。这比 `uiautomation` 需要自己 `assert` 更适合做 CI 门禁。
- **`send-keys` 默认 `post-message` 对 WinUI 3 有已知风险**，CLI 自己会打警告："they are windowless and ignore posted WM_CHAR/WM_KEYDOWN, so keys can be silently dropped even though this command reports success"【已运行：原样输出】。这一条与 `winui-ui-testing` 第 255 行"须用 `--via send-input`"互相印证。
- **沙箱走不通**：`target snapshot sandbox` → `running:false`、`desktop.unavailable = "sandbox_target_stale"`【已运行】。所以 `winui-ui-testing` 的"首选 sandbox"分支在本机不可用，一切须走 `-Target local`。
- **`get-property` 的字体属性对 `AppBarButton` 是 `Unavailable`**（`FontWeight`/`FontName`/`FontSize`/`ForegroundColor` 全为 `Unavailable`）【已运行】。文件头写着"Includes whole-document TextPattern formatting"，那是针对文档类元素；想用 `winapp` 做字体/颜色断言，在按钮上做不到。
- **`ui list-windows` 的顺序不是 z-order**【已运行】；做遮挡判断不能靠它。
- **`winapp ui` 的读侧在窗口非前台时仍可用**：`get-property` 全程不需要前台【已运行，多轮调用均成功】；写侧（`invoke`/`send-keys`）需要前台或 `--focus`【推断，未逐项验证】。
- **官方分析器已在本项目里跑**：`mpv-winui.csproj:81` 引 `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 0.7.1`，NuGet 缓存中存在该包【已运行：目录列举，未跑构建故未看诊断输出】。`winui-dev-workflow` 里"加分析器"那一步对本仓库是死重。
- **`find-ui` 语料可用且离线**：`--list` 返回 **398** 条；`"navigation"` 命中 `gallery-navigationview-1`。`corpus: "cache"` 表示用的本地缓存【已运行】。
- `winapp ui inspect` 对**最小化**窗口仍能读树，但坐标退化：接手时 mpvw 是 `IsIconic=True`，根节点与所有子节点坐标均为 `-32000`/`0x0`【已运行】。做几何断言前必须先让窗口 `IsIconic=False`。

---

## 6. 未能确定的事项

1. **`verify_exe.py` 的"进程关闭"断言未执行** —— 需要关闭 mpvw，而它在接手前已由你启动，我不能关。该能力是否可迁：`wait-for --gone` 只覆盖元素；进程级仍需 `Get-Process`。
2. **`winapp ui` 写侧 11 个动词未验证**（`click`/`drag`/`touch`/`pen`/`hover`/`set-value`/`focus`/`scroll`/`scroll-into-view`/`record`/`yield`）。尤其 `yield` 依赖 `WINAPP_UI_WORKFLOW_ID`，本轮未构造工作流。
3. ~~`ShellNav` 为何能有 UIA 节点~~ —— **已解决**：它是 `NavigationView`（`Shell\MainWindow.xaml:160-161`），不是 Grid，有 automation peer。见 §5.2。
4. **`find-api` caveat 究竟排除了哪些元数据**【未知】。
5. **开发者模式状态**【未知】——本轮不需要，未查。
6. `mpv-deploy` 内的实际文件清单未读取（按约束不触碰）；因此 `compare-deploy.ps1` 的当前输出未复跑。