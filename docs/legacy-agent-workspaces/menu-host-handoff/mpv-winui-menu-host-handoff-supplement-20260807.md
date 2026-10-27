# mpv WinUI 3 菜单宿主交接补充（2026-08-07 第二会话）

> 配套阅读 `mpv-winui-menu-host-handoff-20260807.md`。本文件记录 v3.10.3 部署后的
> 实机问题修复（孤儿级联预热进程泄漏 + 三级菜单白框闪/方向），当前部署 = 修复后构建。

## 1. 发现的问题

- 实机测试（0:40–0:42）后残留 **21 个 MpvMenuHost 级联预热进程**：
  `--parent-pid` 指向的父进程早已不存在，子进程仍在每秒心跳、等待永远不会来的触发
  （每进程约 129MB，合计约 2.7GB）。
- `%TEMP%` 堆积 440+ 个陈旧 heartbeat/state/trigger/items 文件。
- 现象与交接文档「ESC 后无残留进程 / %TEMP% 零残留」矛盾。

## 2. 根因

`MainWindow.xaml.cs` 的 `WatchPeerExit()`：父进程 PID 消失时
`Process.GetProcessById` 抛 `ArgumentException`，catch 分支调用 `Close(0)`；
而 `Close()` 对 `--prewarm` 宿主走 `ResetToPrewarm()`（隐藏复位、等待下一触发），
**不会退出进程**。正常 `peerGone` 分支有「Prewarm → Environment.Exit(0)」，
异常分支漏掉了这一判断 → 父死子留，直到 30 分钟看门狗兜底。

## 3. 修复（已部署）

- 新增 `ExitOrClose()`：`Prewarm` 时 `Environment.Exit(0)`，否则 `Close(0)`。
- `WatchPeerExit()` 三处统一调用：`peerGone` 分支、`catch (ArgumentException)`、
  外层 `catch`。
- 文件：`...\2026-08-06\c-users-z2637-documents-codex-2026\work\mpv-menu-host\MainWindow.xaml.cs`

## 4. 验证

- 定向复现：新建父预热 → 拉起子级预热 → 硬杀父进程（不写 state=0）→
  子级约 2.5s 内自行退出（修复前会滞留）。
- `test\run_all.ps1`：**9/9 PASS**（verify 系统/深/浅/长菜单 + M2 mock +
  M2 真 mpv + M3 E2E + M4 级联 + 宿主缺失回退）。

## 5. 部署状态（当前）

- `publish-r2r\win-x64` 已重发布；`MpvMenuHost.dll` SHA256 前 16 位
  **`A60C876C8874211E`**（v3.10.3 原为 `0B50DC1B0B9A1405`）；exe 仍为 `A8C4B25C4B1458A7`。
- `deploy_host.ps1` 已同步：repo `tools\winui-menu-host` 与
  `C:\Program Files\mpv-lazy\portable_config\tools\winui-menu-host` 三处哈希一致。
- 清理：21 个孤儿进程已杀；`%TEMP%` 清除 445 个陈旧文件（含 2 个历史测试心跳）。
  当前 0 个 MpvMenuHost 进程、`%TEMP%` 0 个 mpv-menu-host 文件。
- 可复用清理脚本：`work\cleanup_stale_host_temp.ps1`（按心跳内记录的 PID 是否存活判定）。

## 5.1 用户实机反馈处理（v3.10.5，本轮）

反馈：右键菜单在右侧展开时三级菜单往左，且仍有闪烁加载框。

### 白框闪（闪烁加载框）——已修复并逐帧验证

- 根因：`Root.Opacity=0` 只对 XAML 内容生效；DWM 在合成出首帧前会把窗口表面/
  亚克力 fallback 直接亮出来。72fps 屏幕抓帧实测：L3 出现瞬间有且仅有一帧
  **纯白均匀块**（均值 255、标准差 2），下一帧才是菜单内容。
- 修复：显示前临时加 `WS_EX_LAYERED` + `SetLayeredWindowAttributes(alpha=0)`，
  窗口级全透明；等 `CompositionTarget.Rendering` 合成 2 帧后恢复 alpha 并摘除 layered
  （150ms 兜底防永不显示）。
- 验证：同场景重抓 455 帧，**0 个白帧**；触发到显示延迟无回退（L2/L3 12–24ms）。

### 三级菜单方向

- 实测几何（1920×1080@100%，当前显示）：L1@800 → L2@1104 → L3@1408（全程向右）；
  L1@1600（距右缘 ~300px 内）→ L2@1296 → L3@992（按 Windows 式“靠右向左”整体翻左）。
  方向逻辑本身正确；用户看到“三级往左”属于右缘翻转设计。
- 顺带修复：子窗口在 pass2 因实际宽度被翻转/钳制时，`_opts.Side` 现在随实际落点
  更新，避免“父说右、实际落左”导致再下一级方向错乱。

### DPI 感知入口（防御性）

- 实测发现发布的 exe 未嵌入 app.manifest（mpv.exe 同样没有）；真实使用时进程
  DPI 感知取决于启动方。已新增自定义 `Program.Main`，在 `Application.Start` 前
  `SetProcessDpiAwarenessContext(PerMonitorV2)`（失败回退 PerMonitor/SystemAware）。
- 注意：本机当前显示为 1920×1080@100%（交接记录是 4K@168DPI），若用户切回 4K@168，
  该入口保证宿主按物理坐标计算；测试脚本此前靠父进程设 DPI 感知掩盖了此缺口。

## 5.2 本轮回归与产物

- `test\run_all.ps1` 9/9 PASS（verify 系统/深/浅/长菜单 + M2 mock + M2 真 mpv +
  M3 E2E + M4 级联 + 宿主缺失回退）。
- 源码改动：`Program.cs`（新增，DPI 入口）、`MpvMenuHost.csproj`
  （`DISABLE_XAML_GENERATED_MAIN`）、`MainWindow.xaml.cs`（ExitOrClose 孤儿退出、
  layered 首帧透明、pass2 侧向修正）。

## 5.3 真实右键链路 E2E（补充验证）

- 链路：真实 `mpv.exe`（部署配置，dyn_menu.lua 拉起部署目录宿主）→ 预热心跳就绪 →
  IPC `script-binding dyn_menu/show` → 可见宿主窗口。
- 结果（1920×1080@100% 当前显示）：1 个可见宿主，rect=(900,0) 300x806，
  `GetDpiForWindow=96`（正确）、`state=1`、菜单顶边在光标处、因高度 806 超出
  工作区向上翻转（y 钳制 0，Windows 式）。截图像素统计 mean=242/std=23.4/
  760 深色像素 = 内容已渲染，非白块。
- 坑：mpv-lazy 的 `mpv.exe` 是包装器，会重写命令行并特殊处理 `=` 与引号；
  自动化脚本启动 mpv 必须像 `run_m3_e2e.ps1` 一样用**单串 + `"` 引号包路径**，
  `--input-ipc-server` 传**裸管道名**（不要带 `\\.\pipe\` 前缀、不要用
  Start-Process 数组传含反斜杠的值），否则包装器把参数拆断、IPC 连不上。
- 复用脚本：`work\real_rightclick_e2e.ps1`、`work\debug_rightclick_ipc.ps1`。
- 已固化为宿主测试套件：`test\run_rightclick_e2e.ps1`（独立脚本，未并入 run_all，
  因为它会移动鼠标）。连跑 3 次稳定：菜单 rect=(900,0) 300x806、dpi=96、
  cursorInside=True、state=1；触发到可见 95–160ms；预热就绪 448–836ms。
  README 已补充该脚本说明与 mpv 包装器传参坑。
- 孤儿预热退出修复（v3.10.4）已固化为 `test\run_orphan_exit_test.ps1`
  （父预热硬杀不写 state=0 → 子级 5s 内自行退出），并已并入 `run_all.ps1`：
  **10/10 PASS**（2026-08-07 09:14）。

## 5.4 提交状态

- v3.10.5 已本地提交：`9531c30`（仅 `tools/winui-menu-host/MpvMenuHost.dll`；
  源码在宿主目录，不在 repo 内）。repo 现领先 origin **39** 个提交，未推送。

## 6. v3.10.6（实机反馈源码审查修复，2026-08-07）

用户反馈三项：仍有图层被覆盖、二级向右时三级向左、每次鼠标悬浮有转圈圈加载。
逐项源码审查结论与修复：

1. **转圈圈**：`ResetToPrewarm`/`ShowTriggeredMenu` 每次关闭/切换都
   `KillChildPrewarm()` 杀子级预热进程 → 下次悬停/展开冷启动新 WinUI 进程
   （系统 app-starting 转圈）。修复：mpv 启动时根预热就预拉全链
   （`--depth` 0/1/2 三层常驻，`MaxPrewarmDepth=2`）；`SpawnChildPrewarm`
   复用存活子级；`ResetToPrewarm` 不杀子级（状态 `2`=待命、`0`=用户关闭、
   `1`=显示中）；父级重建时写 `RESET` 触发让子级复位待命；`WatchPeerExit`
   在父进程存活但父菜单复位时改为子级复位待命（仅父进程死亡才退出，保留
   孤儿清理）；`WatchChildPrewarmAsync` 增加 `_shown` 守卫防复位后重复关闭。
2. **三级向左**：`OpenSubmenu` 与 `ResizeToContent(pass2)` 在放不下时把子菜单
   翻转到父菜单另一侧（estW 估算/实际宽度与右缘空间冲突）。修复：级联子菜单
   严格继承上级方向，右侧链放不下只右对齐钳制（`work.Right - width`），左侧链
   贴左钳制；仅根菜单保留 Windows 式「靠右向左」；`childSide` 不再随落点翻转。
3. **图层被覆盖**：`KeepOnTopLoop` 无条件把父级插到子级 hwnd 下方，子级是
   隐藏预热窗口时会带父级掉出置顶带。修复：仅子窗口 `IsWindowVisible` 时才
   插入其下方，否则本级保持 TOPMOST；循环 200ms→100ms。

产物：DLL SHA256 前 16 位 `E99CA08A5659F4AC`，已同步 repo tools 与安装目录。
构建 0 警告 0 错误。因用户 mpv 会话在运行（9:15 启动），未跑会杀 mpv 的
回归套件；待用户重启 mpv 实机验证。

## 6. 注意与待办

- mpv 已不在运行：重启 mpv 后预热使用新构建（本次部署前运行中的 mpv 54616 已退出，
  其旧预热 18532 因文件占用已被结束）。
- repo `mpv-config`：main 领先 origin 38，工作区仅
  `tools/winui-menu-host/MpvMenuHost.dll` 未提交（本次修复产物，未 commit、未 push）。
- 待用户实机确认（原三项）：三级菜单连续操作是否仍被视频盖住、二级白框闪是否消除、
  左侧展开时三级是否跟随往左。另请确认“三级往左”是否发生在距屏幕右缘 300px 内
  （属设计翻转）；若空间充足仍往左，请复测反馈。
- 用户侧待办不变（未授权不动）：commit/push、重打 release（`run_repack_release.ps1`
  → `run_compress_release.ps1`）、select 总菜单迁移决策、多 DPI 实机验证、独立插件发布。
