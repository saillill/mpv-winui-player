# MpvMenuHost（WinUI 3 菜单宿主）审查结果

> 依据 `mpv-winui-menu-host-handoff-20260807/work/review-checklist.md` 逐项审查。
> 审查日期：2026-08-07；结论图例：✅ 通过 ／ ⚠️ 部分通过 ／ ❌ 不通过 ／ ➖ 不适用；优先级 P0=必须修、P1=建议修、P2=可延后。

## 审查环境与证据范围

- 系统：Windows 11 专业版（10.0.26200），1920×1080 单屏，100% DPI（本机无 Win10/双屏/高 DPI 实机）。
- mpv：`C:\Program Files\mpv-lazy\mpv.exe`，v0.41.0-615-g7b057f66f（2026-05-09 自定义构建）。
- 宿主源码：`...\2026-08-06\c-users-z2637-documents-codex-2026\work\mpv-menu-host`；Lua/配置 repo：`...\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\mpv-config`；安装目录：`C:\Program Files\mpv-lazy\portable_config`。
- 本次实测：
  - `test\run_all.ps1` 两次：首次在第 7 项 M3 E2E 处因测试脚本清理竞态中止（`Stop-Process` 目标进程 41208 已退出）；清理后二次运行 **10/10 PASS**（11:23，含 verify 样例×3、40 项长菜单、M2 mock、M2 真 mpv、M3 E2E、M4 级联、孤儿预热退出、缺失宿主回退）。
  - M3/M4/孤儿/回退单独复跑全部 PASS（11:18）。
  - 真实右键 E2E 证据（当日 10:04 `rc-e2e-evidence.txt`）：mpv 启动→预热就绪 836ms；触发→菜单可见 129ms；窗口 300×806 在光标处，`rendered=True`。
  - `%TEMP%` 实测：**heartbeat=60、state=29、items=2、trigger=2 残留**（9:15–11:23 多轮测试累积）。
  - 图标覆盖脚本（本次新增）：input.conf 154 个静态叶项 **全部命中** IconFor 映射，未命中 0。
  - 编码扫描：repo 与宿主全部 Lua/JSON/conf 均为合法 UTF-8；test\*.ps1 均无 BOM，其中 6 个脚本含非 ASCII 字节。
  - 哈希比对：repo `tools\winui-menu-host` 与安装目录 518 个文件 **0 差异**；与 `publish-r2r\win-x64` 差异仅 `MpvMenuHost.pdb`、`test\m3-host-menu.png` 及 repo 内部嵌套的三份 publish 目录。

## 原则 0：原生优先与依赖最小化（最高优先级，P0）

| 编号 | 结论 | 证据（本机/静态） | 遗留/建议 |
|---|---|---|---|
| 0.1 | ⚠️ | csproj 仅 3 个包引用：Microsoft.Windows.SDK.BuildTools 10.0.28000.2526、Microsoft.WindowsAppSDK 2.3.1、BuildTools.WinApp 0.5.0；TargetFramework net10.0-windows10.0.26100，自包含 WAS。注释给出 WAS 未打包/自包含理由 | 无 DEPENDENCIES.md 台账（见 K11，P1） |
| 0.2 | ⚠️ | README 记录：WAS 2.3.1 未打包需自包含；Release 用 ReadyToRun 非裁剪（裁剪产物无法启动）。未评估 .NET Framework 4.8/纯 Win32 替代路线，无“266MB×2 必要性”书面结论 | P0：补书面决策（接受并说明 / 改 framework 依赖 / 回退） |
| 0.3 | ⚠️ | README/dyn_menu.conf 注释：menu.dll 首次宽度缓存 bug、无图标/动画/Win11 风格；b2 增量收益有记录 | 收益未量化（见 L2） |
| 0.4 | ✅ | P/Invoke 全部走 user32/dwmapi/shcore/advapi32/kernel32 + 系统字体（Segoe Fluent/MDL2）；无“系统已有能力却引库”用例 | — |
| 0.5 | ⚠️ | 原生回退路径完整：Lua 默认 host_menu=false；缺失 exe 回退实测 PASS（`fb-evidence.txt`：警告+mpv 存活）；当前发货配置为 host_menu=yes | 默认值决策未落文档（见 L3） |
| 0.6 | ⚠️ | README 记录 WAS/.NET 裁剪失败、R2R 提速、未打包限制；无版本发布日/已知问题链接 | 补充维护面条目（P2） |
| 0.7 | ⚠️ | 新依赖均有代码注释/README 理由，但 0.1/0.2 的正式台账与必要性结论缺失，按清单口径不能算完全达标 | P0：0.1+0.2 补文档 |

## A. 插件独立性

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| A1 | ✅ | `verify_host_window.ps1` 反复拉起 `MpvMenuHost.exe --no-connect`：窗口 300×190、frame-diff=0、UIA 节点正常，无崩溃（run_all 4 场景 PASS） | — |
| A2 | ✅ | `rg`：dyn_menu.lua 主路径（launch_host_menu）仅用 Lua 数据序列化+subprocess；`menu-data`/`context-menu`/`user-data/menu` 只在回退分支 | — |
| A3 | ✅ | M4/孤儿测试：强杀 mpv/父进程后子预热自行退出（`child exited on own within 5s: True`）；mpv 全程无异常 | — |
| A4 | ✅ | 全 scripts 扫描：无同名 `playlist-menu`/`audio-menu`/`get`/`update`/`menu-ready` 冲突；`menu-init` 由 dialog.lua 与 dyn_menu.lua 同时注册（广播语义，互不干扰）；MBTN_RIGHT 仅 dyn_menu 绑定 | — |
| A5 | ✅ | `run_fallback_test.ps1` PASS：host_menu_path 指向不存在文件时宿主不启动、mpv 日志警告“回退原生菜单”、mpv 存活 | — |
| A6 | ⚠️ | 自包含 R2R 266.5MB/515 文件；repo 与安装目录可脱离系统 WAS 运行（本机实测） | 未在全新 Win10/11 机器验证（P1） |
| A7 | ⚠️ | repo=安装目录哈希 0 差异；deploy_host.ps1 只覆盖不删除（残留 pdb、嵌套 publish、旧文件） | 补卸载/清理脚本；deploy 加“删除多余文件”步骤（P1） |
| A8 | ✅ | 全部临时文件按 `mpv pid / prewarm-id` 命名（heartbeat-<pid>.pid、state-<id>、items-<pid>-<time>-<seq>、trigger-<id>），实测多实例互不覆盖 | — |
| A9 | ❌ | 子进程清理 OK（M4/孤儿 PASS）；被消费的 items JSON 即时删除（日志 `ITEMS deleted`）；但 **heartbeat/state 从不删除，mpv 退出前未消费的 items/trigger 也残留**：实测 60+29+2+2 个文件 | P1：退出/启动时清理本会话 heartbeat/state，并清扫旧 pid 的 items/trigger |

## B. mpv 多版本兼容性（含 MCP 澄清）

> MCP 澄清：全仓（Lua+C#+测试）`rg -i mcp` 无命中；本插件不存在 Model Context Protocol 面，结论“当前不适用”，无需新增该能力。

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| B1 | ⚠️ | 实测 mpv-lazy v0.41.0-615（2026-05-09 构建）全链路通过 | 无官方 mpv / <0.35 旧版矩阵与文档声明（P1） |
| B2 | ⚠️ | 用到的 Lua API：`utils.format_json`、`add_key_binding('MBTN_RIGHT', nil, fn)`、`register_script_message`、`observe_property`、`utils.subprocess{detach=true}`，当前 mpv 全部可用 | `detach=true` 需 mpv≥0.33，未声明最低版本（P2） |
| B3 | ✅ | MpvIpc.cs：`NormalizePipeName` 同时接受裸名与 `\\.\pipe\name`；request_id、异步响应、空响应/超时均处理；M2 mock+真 mpv E2E PASS（`pause=true` 经 IPC 回查） | — |
| B4 | ✅ | dyn_menu.lua `use_mpv_impl` 分支：有 menu-data 走 `context-menu`，否则走 `user-data/menu/temp-items`+menu.dll；本 repo 回退用本地重编译 menu.dll（`use_mpv_impl=no`） | — |
| B5 | ✅ | 未知/缺失键：Lua 默认 `host_menu=false`；实测带未知键加载不崩溃；缺失 exe 回退 PASS | — |
| B6 | ✅ | mpv 退出→宿主链清理：孤儿预热退出测试 PASS（5s 内子级自退）；WatchPeerExit 覆盖优雅退出与强杀 | — |
| B7 | ⚠️ | 仅 Win11 26200 实测（DWM 圆角/Thin 亚克力/图标字体正常）；Win10 21H2 无实机 | P1：Win10 实机验证；代码已有 MDL2 回退与 DWM 属性忽略逻辑 |
| B8 | ⚠️ | net10.0-windows10.0.26100 + WAS 2.3.1 自包含在本机可跑 | 未在最低目标 OS（17763）验证（P1） |

## C. 对原生 SDK 的调用兼容性

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| C1 | ✅ | GetWindowLongPtrW/SetWindowLongPtrW/GetClassLongPtrW/SetClassLongPtrW 均用 IntPtr，x86/x64 位宽正确；x64 实测通过 | x86 发布包存在 pubxml 但未实测（P2） |
| C2 | ✅ | InstallHooks/UnhookHooks 成对；重建菜单前先卸钩（ShowTriggeredMenu）、Close 统一卸钩；日志 `HOOKS mouse=... kb=...` | 钩子回调异常注入未测（P2） |
| C3 | ✅ | DWM 调用忽略失败返回值不崩溃（Win10 不支持时静默无效）；corner/border/ncrendering 均有设置 | 未在 Win10 确认忽略路径（P1） |
| C4 | ✅ | verify_host_window 四次断言：frame-diff=0,0,0,0、无 WS_CAPTION/WS_THICKFRAME、宽≥300 全部 PASS；ThemeWndProc 拦截 WM_STYLECHANGING 防 WinUI 加回边框 | — |
| C5 | ✅ | Program.cs 自定义入口（先 PerMonitorV2）、OverlappedPresenter 无边框、csproj CopyXbfToPublish 补 XBF/PRI；发布产物含 App.xbf/MainWindow.xbf/MpvMenuHost.pri | — |
| C6 | ✅ | TryInitAcrylic：IsSupported 检查→日志 `ACRYLIC unsupported` 回退 solid；异常时 Disposable+回退 XAML backdrop；本机日志 `ACRYLIC controller ok tint=...` | 不支持环境未实测（P2） |
| C7 | ⚠️ | Program.cs 强制 PerMonitorV2、GetDpiForWindow 按窗口取 DPI；本机仅 100% 单屏 | README 自述“多显示器 DPI 仅 100% 缩放验证过”；150%/200%/混屏未测（P1） |
| C8 | ✅ | IsLightTheme 读 AppsUseLightTheme；ThemeWndProc 监听 WM_SETTINGCHANGE 热切换；`--theme` 仅测试覆盖且运行中不跟随（代码明确） | — |
| C9 | ⚠️ | verify 断言 UIA 文本节点≥5（实测 7/41） | NVDA/读屏未实测；无 Tab 焦点（P1） |
| C10 | ✅ | 全仓 `rg "Application.Exit"` 仅命中注释；实际全部走 `Environment.Exit(code)`（5 处） | — |
| C11 | ⚠️ | 自包含 .NET+WAS 266MB×2；README 记录 R2R 提速与裁剪失败 | 系统运行时替代方案无书面结论（P0，同 0.2） |

## D. 每个选项的功能可用性

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| D1 | ⚠️ | input.conf 共 206 行 `#menu:`、154 个静态叶项（清单“106 条”已过时）；命令与参数静态逐一核对（seek -5 exact、add contrast、multiply speed 等均在）；自动化仅覆盖样例点击 | 154 项全量点击未做；建议脚本化抽样或人工过树（P2） |
| D2 | ✅ | 20 处 `#@state=` 全部覆盖清单所列属性（idle_active/pause/tonumber(vid)/sub_visibility/fullscreen/icc_profile_auto/checked/disabled/hidden）；Lua `update_menu_state` 解析 + host 端 state 数组渲染 | — |
| D3 | ✅ | 动态源齐全：播放列表/轨道（视频/音频/字幕/次字幕）/章节/版本/配置/着色器/增强脚本/最近播放/音频设备；`max_title_length=50` 截断生效 | — |
| D4 | ✅ | 抽样 10+ 项快捷键（Ctrl+1..0、F8/F9、PGUP/PGDN、j/v/z、UP/DOWN、m、[]、Alt+0..3、Ctrl+t/h、l、L、Alt+i 等）与菜单命令一致 | — |
| D5 | ⚠️ | 特殊命令族静态齐全：dialog/input_plus/stats/select/recentmenu/save_global_props/commands、vf set、ab-loop、playlist-shuffle、quit-watch-later、screenshot 参数 | 未逐条真实触发（P2） |
| D6 | ⚠️ | 分隔线/禁用/勾选由 sample-menu.json（disabled+checked+separator）与 verify 截图覆盖；hidden 无专门用例 | 补 hidden 视觉用例（P2） |
| D7 | ✅ | M2 mock：收到 `{"command":["playlist-play-index","0"],"request_id":1}`；M2 真 mpv：`set pause yes`→`pause=true`；M3：点击→IPC→`playlist-pos=1`→宿主退出；均无重复执行 | — |
| D8 | ⚠️ | 三级结构（视频>调色>对比度）存在；MenuData.cs 硬编码解析 3 层（孙级不再含 submenu）；M4 实测两级级联 PASS | 4 层以上静默截断；建议递归解析（P2） |
| D9 | ⚠️ | 空播放列表：Lua `show-text 播放列表为空` 并返回（不弹窗）；空子菜单：宿主弹最小高 40px 空窗（不崩溃） | 空子菜单加“无内容”禁用/提示更友好（P2） |

## E. 窗口渲染稳定性

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| E1 | ✅ | v3.10.5 起窗口级 alpha=0→合成 2 帧→淡入；verify 首帧即为最终尺寸（300×190/300×1032，无默认尺寸跳变）；README 逐帧验证 0 白帧 | 未做慢动作录屏复核（P2） |
| E2 | ⚠️ | v3.10.1~3.10.5 链式置顶修复记录完整（最末级最顶、父插子下、心跳句柄防 cloaked）；rc-e2e 实测菜单在光标处显示 | 100 次快速移动/三级链未自动化（P1） |
| E3 | ⚠️ | 切换二级菜单先 `KillChildIfAlive`（v3.8/v3.9.1）；M4 日志 `CHILD PREWARM closed state -> close parent` | A→B 快速切换未压测（P2） |
| E4 | ⚠️ | README v3.7 实测右缘 3790→翻转、底缘 2000→翻转；verify 长菜单 y=120 被钳到 y=0 完整可见 | 双屏边界/任务栏遮挡未测（P1） |
| E5 | ⚠️ | 代码有 60s 瞬态看门狗 + 30min 预热兜底 | 1min/30min 长显未实测（P1） |
| E6 | ✅ | AnimateIn/FadeInWindowAsync 全异常路径恢复可见（Root.Opacity=1、alpha=255、150ms 兜底 RestoreVisible） | 人为故障注入未做（P2） |
| E7 | ✅ | 40 项长菜单实测 300×1032（高度钳制+滚动）；30% 屏宽上限、pass1/pass2 两遍布局、GetDpiForWindow 缩放 | 150% DPI 混屏未测（P1，同 C7） |
| E8 | ❌ | 句柄未见增长迹象，但 **临时文件明确累积**（heartbeat=60/state=29/items=2/trigger=2），与 README“全量回归后 %TEMP% 零残留”矛盾 | P1：退出清理+旧会话清扫；200 次循环压测未做 |
| E9 | ⚠️ | 缺失宿主/被杀后下次右键：回退测试 PASS、孤儿测试 PASS；Lua 每次重新拉起 | 崩溃注入→自动拉起未专门测（P2） |
| E10 | ✅ | README 已知限制（亚克力不模糊视频、DWMSBT_TRANSIENTWINDOW 弃用）与代码一致（无 DWMSBT 调用；DesktopAcrylicController 采样窗口内容） | — |

## F. 用户可自定义的配置文件

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| F1 | ⚠️ | dyn_menu.conf 6 项均在：use_mpv_impl=no、max_title_length=50、host_menu=yes、host_menu_path=~~\tools\winui-menu-host\MpvMenuHost.exe、host_test_select 注释、host_log 缺省 | 仅 host_menu_path 坏值/回退实测；其余靠代码+缺省逻辑（P2） |
| F2 | ⚠️ | 外观参数（最小宽 300、圆角 8/4、动画 160ms、重叠 6px、字号/图标）全为硬编码；README“剩余”中明确决策未定 | P1：定“保持硬编码”或暴露配置并文档化 |
| F3 | ✅ | 坏路径实测回退默认；未知键不崩溃；mp.options 缺省生效 | — |
| F4 | ✅ | 优先级：Lua 默认 < dyn_menu.conf（mp.options）< 宿主命令行参数；README 命令行节有文档 | 文档需补全参数（见 F5） |
| F5 | ⚠️ | README 命令行节缺 `--anchor/--side/--parent-pid/--prewarm/--prewarm-id/--parent-prewarm-id/--depth/--test-delay-ms`（部分散见行为节） | P2：命令行节与 MenuOptions.Parse 一一对应 |

## G. 中英文与原生 mpv 配置默认值

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| G1 | ✅ | input.conf 菜单树全中文，与 mpv-lazy 中文用户群一致（154 叶项全中文） | — |
| G2 | ⚠️ | dyn_menu.lua 硬编码中文（音轨/字幕/视频/播放列表为空等），无统一文案表 | 补文案表或显式声明“仅中文”（P2） |
| G3 | ⚠️ | 同一 input.conf 数据源；host 与原生回退菜单树同源；回退实测可用 | 双路径 100% 等价未逐项对比（P1） |
| G4 | ⚠️ | 无 `*-en` 模板、无 locale 切换 | 记录为已知限制（P2） |
| G5 | ⚠️ | Lua/JSON/conf 全为合法 UTF-8；但 test\*.ps1 全部无 BOM 且 6 个含非 ASCII（run_fallback 12、run_input 9、run_m2_mpv 96、run_m3 264、run_m4 180、verify 54）；PS 5.1 下中文断言会按 GBK 解码失真（如 run_fallback 的“回退原生”匹配实际失效，靠 ASCII 匹配兜底） | P2：统一 BOM 或改 ASCII-only，与 README 声明一致 |

## H. 代码逻辑审查（冗余/缺失/重复/绕圈子/重复造轮子）

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| H1 | ✅ | 单一数据源：input.conf→Lua 解析→JSON 序列化→宿主 BuildMenu 渲染；动态更新经 script-message（dynamic_menu.lua→dyn_menu update）；原生回退直接 menu-data。同步成本可控且有注释 | — |
| H2 | ❌ | repo `tools\winui-menu-host` 内嵌套 `publish\win-x64`（221MB）、`publish-release\win-x64`（245MB）、`publish-r2r\win-x64`（266MB）三份完整产物 + MpvMenuHost.pdb + test 截图；deploy 只复制不清除 | P1：发布目录只保留一份产物并清理陈旧文件 |
| H3 | ⚠️ | 文件 IPC 各通道职责可区分：items=数据载荷、trigger=信号+参数、heartbeat=存活、state=会话状态、RESET=复位；README/注释有说明 | 通道多、清理缺失（见 A9/E8）；可考虑 trigger 内嵌 JSON 路径改为直接载荷（P1） |
| H4 | ⚠️ | 逐项对照均有出处：(a) 自绘 vs menu.dll（宽度缓存 bug/图标/动画）；(b) 自包含 vs 系统运行时（未决策）；(c) 低层钩子 vs 焦点窗口（NOACTIVATE 需全局钩子）；(d) 手写置顶链 vs owned 子窗口（激活行为差异）；(e) DPI/边框剥离 vs Presenter（#9978）；(f) 动画用 Composition API 非手写 | 缺正式决策表（P1，汇总进 0.2/K7） |
| H5 | ✅ | SampleMenu/--no-connect/--test-select/--theme 均有注释或 README 标注测试用途；正式路径不触发 | — |
| H6 | ⚠️ | 常量分散：300px/6px/160ms/14ms/150ms/30%/100ms/60s/30min/IPC 10 次/触发写 4 次；部分有命名（MaxPrewarmDepth），无集中配置 | P2：集中常量或配置化 |
| H7 | ✅ | 触发写独占+4 次重试；WatchPeerExit 与 WatchChildPrewarmAsync 职责分离；`_session` 代次防串台；ResetChildPrewarmToIdle 失败杀进程兜底；注释完整 | — |
| H8 | ✅ | 状态机 0=关闭/1=显示中/2=复位待命，host 侧 WriteState 与 Lua 侧读取逻辑逐条对应（代码+README v3.10） | — |
| H9 | ⚠️ | `--log` 与 crash.log 均无限追加、无轮转；crash.log 写在 AppContext.BaseDirectory（Program Files 下可能静默失败）；测试脚本中文断言编码坑（见 G5） | P1：日志轮转 + crash.log 改 %LOCALAPPDATA% |
| H10 | ⚠️ | 有 deploy_host.ps1（复制同步）；无宿主安装/卸载脚本、无版本管理（FileVersion=1.0.0.0）、无 CI、无发布清单/哈希校验 | P1：版本化+发布清单（含三处哈希校验）+可选 CI |

## I. 外部生态对照（GitHub 热门库/相关经验/微软官方库）

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| I1 | ⚠️ | 手写点大多有官方对应：#9978 白边→DWM NCRENDERING 禁用、OverlappedPresenter 无边框、DesktopAcrylicController、DWM 圆角属性 | 缺官方文档链接与逐点对照表（P2） |
| I2 | ⚠️ | 未引入 CommunityToolkit.WinUI/WinUIEx；收益评估未记录 | 写“不引入+理由”或评估（P2） |
| I3 | ✅ | P/Invoke 面已审（C1）且稳定；不引入 Vanara/PInvoke.net 合理 | — |
| I4 | ⚠️ | menu.dll 源自 tsl0922/mpv-menu-plugin（repo README 有链接）；宽度缓存 bug 已在 dyn_menu.conf 注释记录并本地重编译规避 | 借鉴经验未成文档（P2） |
| I5 | ⚠️ | #9978 白边、XBF/PRI 复制、Application.Exit AV、Acrylic 未打包限制、Segoe Win10 回退均已在代码注释/README 覆盖 | 每条缺链接与结论（P2） |

## J. 图标体系（自定义/预设/回退）

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| J1 | ✅ | 图标全部来自系统字体：运行期检测 SegoeIcons.ttf→Segoe Fluent Icons，否则 Segoe MDL2 Assets；README 声明“系统预设字形” | — |
| J2 | ⚠️ | 无自定义资源图标能力（无 svg/png/主题图标），仅字形 | 记录为限制（P2） |
| J3 | ✅ | 本次脚本核对：input.conf **154/154 静态叶项全部命中**（精确或前缀）；仅分隔线无图标，与现状声明一致 | — |
| J4 | ⚠️ | 动态项（播放列表/轨道/章节/配置/着色器/设备名）无图标，排版不对齐 | P2：动态项默认图标或对齐方案 |
| J5 | ⚠️ | 代码级双字体校验+README fontTools 声明（23 码点双字体齐全） | Win10/缺字体环境未实机（P1） |
| J6 | ⚠️ | 抽查语义正确（播放 E768、暂停 E769、音量 E767、速度 E945、字幕 E8BA 等） | 全表人工复核未做（P2） |
| J7 | ⚠️ | 无开关/换图标/大小颜色配置 | 记录为 P2 需求 |
| J8 | ⚠️ | `_subBrush` 深浅色有区分；高对比度下 `IsHighContrast=false` 硬编码，无 HC 调色板适配与实测 | P1：HC 主题适配+验证 |

## K. 补充审查维度

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| K1 | ⚠️ | 菜单 JSON/trigger 写入 %TEMP% 无 ACL 限制，任意本地进程可投递伪造菜单（点击后经 mpv IPC 执行命令）；mpv 管道无认证（IPC 固有风险）；README 未记录该风险 | P1：README 安全章节记录；JSON 加校验/目录收紧 |
| K2 | ⚠️ | 实测：预热就绪 836ms、触发→可见 129ms（rc-e2e）；冷启动 README 记录 ~336ms（R2R）；KeepOnTopLoop 100ms 未测 CPU | P2：CPU/内存采样 |
| K3 | ❌ | OS/DPI/主题/输入法/触屏/键盘/读屏兼容矩阵不存在 | P1：建矩阵并逐格实测（Win10、150%/200%、双屏、NVDA） |
| K4 | ⚠️ | 键盘 ↑/↓/Enter/Esc 已实现（低层钩子）；无 Tab；UIA 文本节点可枚举（7/41） | P1：Tab+读屏实测 |
| K5 | ⚠️ | run_all 10 项全自动化；真右键 E2E 脚本存在且当日 PASS；缺口：动态菜单、边缘翻转、双屏、长时间运行、崩溃注入、Win10 实机 | P1：按清单补齐或明确“人工验证” |
| K6 | ⚠️ | deploy_host.ps1 存在；当前三处哈希：repo=install 完全一致，publish-r2r 与 repo 仅差 pdb/嵌套产物；无版本号管理（1.0.0.0）、无发布清单 | P1：发布清单+版本校验（同 H10） |
| K7 | ⚠️ | publish-r2r 266.5MB×2 副本（repo+install）+ repo 内另 3 份 publish 树；裁剪路径已实测不可用（WAS 2.3.1 未打包） | P0：接受/裁剪/framework 依赖三选一并书面化（同 0.2） |
| K8 | ❌ | 发布目录与 tools 目录无任何 license/notice/THIRD-PARTY 文件；dyn_menu.lua 上游头为 GPL-2.0-only（tsl0922），分发整合的许可合规未核对 | P0：补 WAS/.NET/BuildTools 许可声明与上游 GPL 声明，核对 mpv-lazy 分发条款 |
| K9 | ⚠️ | crash.log 会写（异常时）；但无轮转且 Program Files 下可能不可写静默失败；未做崩溃定位实测 | P1：日志轮转+崩溃转储路径（同 H9） |
| K10 | ⚠️ | git log 完整记录实机反馈修复（转圈→v3.10.6 全链预热、三级方向→v3.10.3、白框闪→v3.10.5、级联方向/图标/标题/重叠/动画等）；部分有回归测试（孤儿/回退/run_all），部分无（白框闪逐帧、转圈压测） | P1：为每条反馈补回归用例 |
| K11 | ❌ | 无 DEPENDENCIES.md；依赖理由散落在 csproj 注释与 README | P0：建台账（用途/体积/升级路径/替代方案，与 0.1 同步） |

## L. 架构决策复核（b1 原生菜单 vs b2 自绘宿主）

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| L1 | ⚠️ | b1 缺口有实际依据：menu.dll 首次宽度缓存 bug（dyn_menu.conf 注释）、无图标/动画/Win11 风格、渲染稳定性问题；非主观 | — |
| L2 | ⚠️ | 收益（图标全覆盖 154/154、动画、置顶链、宽度正确）与成本（266MB×2+自包含运行时）均存在，但无量化对照表 | P1：功能矩阵+收益/成本结论 |
| L3 | ⚠️ | Lua 默认 host_menu=false，但发货配置为 yes；原生路径可全功能（回退实测） | P1：决定发布默认（原生优先=no / 自绘=yes）并文档化 |
| L4 | ⚠️ | 回退路径共享同一 input.conf 数据源且实测可用 | 双路径菜单树/状态/命令/动态项 100% 等价未逐项对比（P1） |
| L5 | ⚠️ | README 有完整版本历史（v2.3→v3.10.5）可追溯；无正式“b1/b2 取舍+采纳/拒绝理由”章节 | P1：补决策记录章节 |

## 结论汇总

| 维度 | 结论 | 主要证据 | P0/P1/P2 遗留项 |
|---|---|---|---|
| A 插件独立性 | ⚠️ | run_all 10/10；回退/孤儿 PASS | P1：临时文件清理 |
| B mpv 版本兼容 | ⚠️ | 0.41 实测全链路 | P1：Win10/最低 OS/旧版矩阵 |
| C 原生 SDK 调用 | ⚠️ | P/Invoke/钩子/边框/主题全通过 | P1：多 DPI、NVDA |
| D 选项功能可用性 | ⚠️ | 静态全核对+样例 E2E | P2：154 项全量点击 |
| E 窗口渲染稳定性 | ⚠️ | 首帧/长菜单/置顶链实测 | P1：临时文件累积（❌）；双屏/长稳 |
| F 用户自定义配置 | ⚠️ | 选项齐全、坏值回退 | P1：外观配置决策；P2：文档补齐 |
| G 中英文/默认值 | ⚠️ | 全中文一致；编码可读 | P2：PS1 编码声明不一致 |
| H 代码逻辑 | ⚠️ | 单数据源、并发防护完善 | P1：发布产物冗余、日志轮转、版本/CI |
| I 生态对照 | ⚠️ | 已知 issue 均有覆盖 | P2：补链接与决策表 |
| J 图标体系 | ⚠️ | 154/154 命中、双字体回退 | P1：高对比度；P2：动态项对齐 |
| K 补充维度 | ⚠️ | 性能/安全/测试均有证据 | P0：许可、依赖台账、体积决策；P1：矩阵、HC、日志 |
| L 架构决策复核（b1 vs b2） | ⚠️ | 历史完整可追溯 | P1：正式取舍记录与默认值决策 |
| 原则 0 依赖最小化 | ⚠️ | 依赖面小且理由有注释 | P0：0.1/0.2/K7/K8/K11 补书面结论 |

## 总体结论

**有条件发布（建议暂缓正式发布，先完成 P0 项）**。

理由：功能主线扎实——全量回归 10/10 PASS、M2/M3/M4/真右键 E2E/回退/孤儿退出均实测通过，图标静态覆盖 100%，宿主/安装/回退三处部署一致，代码对已知 WAS/DPI/置顶链问题有明确修复与注释。但按清单“原则 0”口径，以下发布阻塞项仍未闭环：

P0（发布前必须处理）：
1. **依赖/体积书面决策**（0.1/0.2/K7）：266MB×2 自包含的“必要性结论”未成文档；需明确“接受并说明 / 改 framework 依赖 / 裁剪”。
2. **依赖台账**（K11）：建 DEPENDENCIES.md，逐项列用途/体积/升级路径/替代方案。
3. **许可合规**（K8）：发布物无 WAS/.NET/BuildTools 许可与声明；dyn_menu.lua 为 GPL-2.0-only，需核对 mpv-lazy 分发整合条款。

P1（建议在发布周期内完成）：
- 临时文件清理（A9/E8/H3）：heartbeat/state 从不删除、未消费的 items/trigger 残留；与 README“零残留”声明矛盾。
- 发布产物收敛（H2/K6）：repo 内三份 publish 树冗余；版本号 1.0.0.0、无发布清单/哈希校验/CI。
- 兼容矩阵与实机验证（B7/B8/C7/K3/K5）：Win10、150%/200% DPI、双屏、NVDA/高对比度、长时间运行。
- 文档决策（F2/L3/L5）：外观配置、host_menu 发布默认值、b1/b2 取舍记录。
- 日志（H9/K9）：--log/crash.log 轮转；crash.log 避免写入 Program Files。
- 安全说明（K1）：README 记录临时文件与 IPC 管道信任边界。

唯一实测到的行为回归是**测试框架竞态**（run_all 首跑在 M3 前清理时失败，二次运行 10/10 PASS），属测试脚本问题而非产品缺陷，建议给 run_all 的跨测试清理加等待/容错。

### 本审查新增/引用的证据文件

- 全量回归输出（两次 run_all 结果，见上文）；M3/M4/孤儿/回退单独 PASS 输出。
- `test\rc-e2e-evidence.txt`（真右键 E2E，当日 10:04）、`test\m3-evidence.txt`、`test\m4-evidence.txt`、`test\fb-evidence.txt`。
- 本次新增脚本（可复跑）：`work\icon-coverage.ps1`（154/154 命中）、`work\test-b5-unknown-option.ps1/2`（未知选项加载不崩溃）。
- 遗留证据：`%TEMP%\mpv-menu-host-heartbeat-*.pid`（60）、`mpv-menu-host-state-*`（29）、`mpv-menu-host-items-*`（2）、`mpv-menu-host-trigger-*`（2）。
