# 交接文档 — mpvw UI 修复任务

> 写给接手的人（或下一个会话的 agent）。只写"现在是什么状态、坑在哪、下一步怎么走"，
> 不复述历史。权威记录是 `docs/ui-repair/QUEUE.md`，本文是它的入口摘要。

**日期**：2026-10-26（原 2026-10-25 的状态已作废，见 §1）
**HEAD**：`9aaae43` 之后
**仓库**：`C:/Users/z2637/Documents/DeepSeek/mpv`（WinUI 3 + 内嵌 libmpv 的播放器）

---

## 1. 一句话状态

**这份交接的状态已经作废，以 `PLAYBACK-FAULT-2026-10-26.md` 为准。**

下面原本写着"`gate.py` 8/8 全绿"——那句话在写下时是真的，但它**什么也没证明**：
当时应用连视频都放不了。gate 之所以全绿，是因为它的九条判据里**没有任何一条**
用过应用自己的打开路径，判据本身也只是"最后一行输出里含 OK 这个子串"。

2026-10-26 已重建 gate（见 §2），现在它会因为播放坏了而**变红**。

---

## 2. 当前基线（2026-10-26 重建后）

| 项目 | 值 |
|---|---|
| `python tools/ui-repair/gate.py` | **会停在 `verify_playback`，退出 1** —— 因为视频确实放不了 |
| gate 判据 | 每个 verifier 必须打印 `VERDICT: PASS\|FAIL`（且与退出码一致）**并且** `--self-test` 通过 |
| 检查器 | 23 个 `tools/check-*.py`，全部退出 0 |
| 单元测试 | `mpv-conf-test` **517** + `mpv-winrt-test` **26**，全通过 |
| 已确认未修的缺陷 | **视频无法播放**（根因未找到，20+ 候选已排除）；设置页键盘导航 |


跑之前先 `python tools/check-xaml.py`（约 1 秒）。XAML 注释里不能有 `--`（WMC9997），
本仓库的编辑工具不拦这个，已经赔过 5 个完整构建周期。

---

## 3. 本会话交付

### 3.1 修掉的缺陷

| 提交 | 缺陷 | 根因 |
|---|---|---|
| `7b61658` | 7 个 Pivot 页签无无障碍名 | 文件自己的注释写明了诊断，但只应用到 4 个里的 1 个 |
| `836bb39` | 从库页面进入全窗口会把窗口卡死 | `FullWindowButton` 在库页面仍可点；`CanEnterFullWindow` 守卫加在翻转视觉状态**之前** |
| `071d5d9` | 高对比度主题下界面不可读 | `ThemeDictionaries` 缺 `HighContrast` 分支 + `ResolveTheme()` 把 Auto 钉死成 Light/Dark（只有 `Default` 才允许选到高对比度） |
| `7ac7487` | 「紧凑」皮肤设置无任何可见效果 | WinUI 从不给 `UserControl` 应用 `Style.Template`（实测 `styleTplNull=1`）；改为实现该设置唯一可检验的承诺——紧凑皮肤隐藏音量滑块 |
| `da44e16` | **验证一启动就抢前台**（用户报"占用控制器"） | `App.OnLaunched` 里的 `Window.Activate()`。手柄侧已排除：`mpv-2.dll`/`mpvw.dll`/`mpv_winrt.dll` 里 `gamepad`/`XInput`/`SDL2`/`hid.dll` 出现次数**全为 0** |
| `1b345fb` | 4 个错字形 + 3 处硬编码字体 + 2 处未 token 化尺寸 | 见 §3.3 |
| `9398df1` | 76 处字号/间距字面量与 token 等值 | 见 §3.3 |

### 3.2 新建的工具（均已纳入 gate）

| 文件 | 作用 | 现状 |
|---|---|---|
| `tools/mvpw-mcp/background_capture.py` | 用 `PrintWindow(PW_RENDERFULLCONTENT)` 渲染到自建 bitmap | 窗口停在 `-32000` 也能截到完整界面 |
| `tools/mvpw-mcp/background_window.py` | `OnScreen()` 非激活移动 + `foreground_guard()` | 守卫已双向验证 |
| `tools/check-no-focus-steal.py` | 禁止探针把窗口拖到屏内 | 278 文件扫描通过；3 种注入全红 |
| `tools/check-no-launch-activate.py` | 自动启动不得抢前台 | 4 条断言 + `--measure`；3 种注入全红 |
| `tools/check-icon-standard.py` | 图标规范 | **51 known** |
| `tools/check-typography-standard.py` | 排版规范 | **17 known** |

---

## 4. 接手前必须知道的事（按踩坑代价排序）

### 4.1 前台 / 截图
- **别再让窗口上屏**。截图一律用 `screenshot_region()`（默认走 PrintWindow）。旧路径要
  `ImageGrab`，必须把窗口从 `-32000` 拖到屏内——用户会看到窗口跳出来抢他的全屏游戏。
- `WINEVENT_OUTOFCONTEXT` 靠消息循环投递。只 `time.sleep` 的探针收到 **0 事件**，
  会"证明"一切正常。要 `PeekMessage`/`DispatchMessage` pump。
- **先证明钩子活性**：先用旧行为复现问题，再看修复后是否为 0。否则那个 0 什么都不说明。

### 4.2 IPC
- 管道 `\\.\pipe\mpvpipe` **每个进程只服务一个连接**，且只在启动后约 1 秒内接受。
  需要自有会话的工具必须 `T.open_launch_window()` 然后立刻发。
- `_ipc(cmd_list)` 直接传命令数组，**不要**再包一层 `["command", [...]]`。

### 4.3 测量纪律（本会话反复栽跟头）
每一条都是"看起来对、实际错"的实测教训：

- **UIA 句柄会失效**。页面重建后旧元素句柄作废，要重新枚举。
- **零尺寸元素会污染计数**。窗口标题栏的 `最小化/最大化/关闭` 无 `AutomationId` 且带文字，
  曾被我算成"3 张卡片"，导致"过滤后还剩 3 张"的假结论。用 `BoundingRectangle` 排除。
- **`SetScrollPercent` 可以返回成功而 `VerticalScrollPercent` 不变**。
- **`CustomizeToggle` 暴露的是 `TogglePattern` 不是 `InvokePattern`**；且进入自定义模式后
  它自己会从树里消失，由 `CustomizeExitButton` 接管。
- **`FolderTree` 的 UIA `AutomationId` 是 `'ListControl'`**，按 `FolderTree` 找不到。
- **注册表设置必须"关应用 → 等进程消失 → 写 → 启动"**。`AppSettings.Flush()` 在退出时
  用内存值重写注册表，运行中写的会被丢掉。
- **不要用截图判断主题色**。`BackdropType=Mica` 且 shell veil 笔刷带 alpha，取样是叠加结果。
- **悬空的探针会撒谎**：`GetWindowRect` 对已关闭窗口返回 `(0,0,0,0)`，我第一版采样器把
  它算成"窗口在屏内"，415 个采样里 275 个像是违规，而前台计数始终是 0。

### 4.4 编码 / 行尾
- **对 index 检查 BOM 和行尾，不要对邻居文件**。`edit` 类工具会丢 BOM；
  `open(..., newline="")` **读取**保留 CRLF，**写入**输出 LF——我用它跑第一遍间距替换时，
  13 个文件被悄悄转成纯 LF，而 `git diff --stat` 只显示 52 行。
- 判据：`git diff --check` 干净 **且** `--stat` 里没有整文件重写。

---

## 5. 未修的项

### 5.1 设置页键盘导航（唯一确认的功能缺陷）
- **现象**：Tab 进得去某一行（`ListItemControl '紧凑'`），但立刻离开；14 次 Tab 只在 6 个
  外壳控件之间循环。688 行的选项列表**无法用键盘抵达**。
- **已定位根因**：`Modules/Settings/Controls/OptionListControl.xaml:448`
  `<Setter Property="IsTabStop" Value="False" />`（`OptionListViewItemStyle`，
  在 `:560` 作为 `ItemContainerStyle` 应用）。
- **已排除**：`IsTabStop=True`（Tab 停在容器但不进子控件）、
  `TabFocusNavigation=Once/Local/Cycle` 全部无效。所有实验已回滚，文件与 HEAD 一致。
- **为什么没修**：两个候选设计都需要先定交互——「整行作为一个 Tab 停靠点，内部用方向键」
  还是「行内每个控件各自是停靠点」。影响 688 行的可用性，不适合我替你拍板。

### 5.2 产品侧仍会在打开子窗口时抢前台
- **已定位到确切阶段**：只在 `new SettingsWindow()` 构造时抢，`ShowWindow()` 里插桩后
  日志一行都没有——说明它没被调用，抢发生在构造。前台的 HWND 从外部应用变成设置窗口。
- **三种修法全部证伪并回滚**：`SetForegroundWindow` 加 `OwnsForeground` 守卫；把
  `AppWindow.Show()` 换成 `SW_SHOWNOACTIVATE`；用 `SetWindowLongPtr(GWLP_HWNDPARENT)`
  设 owner（owner 确实设上了、`GetWindow(child,GW_OWNER)` 与主窗口一致，**仍抢**，
  而且**把设置窗口搞成打不开**，回滚后恢复）。
- **结论**：这是 WinUI 3 创建顶层 `Window` 的固有激活，仓库里没有一行开关。
  真修要么把设置改成主窗口内页面/`ContentDialog`，要么 Win32 建窗 + `DesktopChildSiteBridge`。
  都属于结构改造，需要先定方案。
- **注意**：用户主动点"设置"时抢前台是**期望行为**。这条只在自动化场景有害，
  而自动化场景已由 `MVPW_NO_ACTIVATE=1` 解决（见 §3.1 `da44e16`）。

### 5.3 历史 / 稍后看
结构已审（空态逻辑正确、三种文案、列表与空态严格互斥），**运行时未测**。
入口只能从播放页的 File 菜单或 `PlayerView` 右键进，而 `MainMenuBar` 在库页面被
`Collapsed`、`MenuRow` 压成 0，且 IPC `loadfile` **不会**把页面切到播放页
（只有用户自己的打开路径会设 `_forceShowVideo`）。
**要覆盖它需要先造一个用户态打开文件的自动化路径**（拖放注入或 Open 对话框）——
这是测量工具的活，不是改产品。

### 5.4 其余基线
- 图标 51 条：13 个文件里与图标尺寸 token 等值的字面量。
- 排版 17 条：**判定不改**。`Margin`/`Padding` 是 `Thickness`，而 `MpvSpace*` 是
  `x:Double`；替过去是编译错误。且 `CompactPlayerControlStyle.xaml:31` 的注释写明
  该值参与高度预算（"8 vertical, plus 28 + 60"），token 只会藏起数字而非解耦。

---

## 6. 已测量且判定为真 / 为假（不要重查）

**为假（不要再查）**：
- 上游两个 repro 失败是探针 bug（`repro_upstream_fileassoc`、`repro_upstream_displayrequest`）。
- `scan.py` 的 4 条间距告警：DIP/像素混用 + 深度≠兄弟关系。
- MediaInfo「分节折叠」**该功能不存在**（不是坏了）。
- 首页 `QueryButton`（可访问名"搜索"）点了没反应**不是缺陷**：打字时 `TextChanged` 已实时过滤。
- 20 处硬编码颜色**合理**：18 处在 mpv.conf 查看器（自带固定深色配色是设计），
  2 处是 AB 循环的 A/B 标记色 `#FFFF8C00`/`#FF00BFFF`（功能性配色）。

**为真且已修**：见 §3.1。

---

## 7. 开工步骤

```bash
cd /c/Users/z2637/Documents/DeepSeek/mpv
python tools/check-xaml.py                    # 1 秒，先跑
python tools/ui-repair/next.py                # 看 A 区还剩什么
python tools/ui-repair/gate.py                # 全绿才对（现在会停在 verify_playback，见 §2）
```

改完产品代码后：
```bash
pwsh -NoProfile -File build.ps1 -Configuration Release -Platform x64
pwsh -NoProfile -File tools/deploy-test.ps1 -CloseRunning -Deploy
```

**收尾必须做**：应用不能留着跑。用 `pwsh tools/deploy-test.ps1 -CloseRunning`，
**不要** `Stop-Process`（前者先发 `WM_CLOSE`，让 WinUI 正常关闭、mpv 落盘日志和状态）。

---

## 8. 这个仓库的硬规则

1. **没有复现就不改产品代码**。本会话记录的误判已有十几次，探针假阳性率高于真实缺陷率。
2. **不用固定 `sleep`**，等元素。固定时长是本仓库间歇性失败的第一大来源。
3. **一切要有依据**：官方规范、上游实现，或本仓库已有 token。否则先把依据写进 `BASELINE.md`。
4. **从没红过的检查器不是证据**。新增/修改检查器后，注入真缺陷确认它变红。
   本会话有一条规则（"码点是否存在于 Fluent 字体"）写完后发现**永不触发**
   ——打包字体覆盖了私有区全部 6400 个码点——已删除，不是保留。
5. **"修好"的定义**：`gate.py` 全绿 **且** 该复现从 fail 变 pass **且** 翻转在两次独立运行中成立。
   然后 `python tools/ui-repair/done.py <id> --evidence "..."`。
   > 2026-10-26 补充：旧 gate 的"全绿"判据是"最后一行输出含 OK 这个子串"，
   > 因此它在应用完全放不了视频时仍报 8/8。现在的 gate 要求每个 verifier 打印
   > `VERDICT: PASS|FAIL` 并与退出码一致，且 `--self-test` 通过——"全绿"才重新有意义。
6. 一个提交一个缺陷，提交信息写清楚**根因**。
