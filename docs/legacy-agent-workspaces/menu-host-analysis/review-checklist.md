# MpvMenuHost（WinUI 3 菜单宿主）审查清单

> 用途：对本插件的独立性、兼容性、功能可用性、稳定性、可配置性、代码质量与生态对照做逐项审查。
> 用法：每项按“检查点”执行，“证据”写具体文件/命令/实测结果，“结论”填 ✅ 通过 / ⚠️ 部分通过 / ❌ 不通过 / ➖ 不适用，P0 表示必须修，P1 建议修，P2 可延后。
> 涉及目录：
> - 宿主源码 `C:\Users\z2637\Documents\Codex\2026-08-06\c-users-z2637-documents-codex-2026\work\mpv-menu-host`
> - Lua/配置 `C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\mpv-config`（repo）
> - 安装目录 `C:\Program Files\mpv-lazy\portable_config`

## 原则 0：原生优先与依赖最小化（最高优先级）

> 总原则：**尽量依赖 mpv 本体功能（内建菜单/Lua/属性/IPC）与 Windows 系统运行库，尽量不要新增依赖、运行库或自绘组件。** 以下每一条的结论都会影响发布判定（P0）。

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| 0.1 | 新增依赖台账：逐项列出 .NET 10、Windows App SDK 2.3.1、WinUI 3、自包含运行时、NuGet 包，每项写清“用途 / 体积 / 升级路径 / 替代方案” | 查 `csproj`、publish 目录、包引用 | 每项有书面理由；无“顺手引入”的包 |
| 0.2 | 是否必须自包含运行时：能否使用系统已有运行库（.NET Framework 4.8 / 系统级 WAS / 纯 Win32 / 无托管运行时） | 评估 C# WinUI 之外的最小实现路径 | 自包含 266MB×2 有明确必要性结论，否则回退 |
| 0.3 | mpv 本体能力优先：context-menu / menu-data / Lua 脚本能否满足需求；自绘菜单相对原生菜单的增量收益清单 | 对照 b1（原生菜单路径）与 b2（WinUI 宿主）实测 | 增量收益（图标/动画/稳定性）值得额外依赖，否则默认走原生 |
| 0.4 | 系统组件优先：能用 User32/GDI/DWM/系统字体等已存在组件的，不引入 NuGet 或新运行时 | 审所有 P/Invoke 与包引用 | 无“系统已有能力却引入新库”的用例 |
| 0.5 | 默认路径零新增依赖：host_menu=no 的原生回退是否完整可用、能否作为默认配置 | 以 menu.dll 与纯 mpv 内建菜单分别跑全功能 | 原生路径无功能缺口时，默认配置应选原生 |
| 0.6 | 新增依赖的维护面：WAS/.NET 生命周期、安全补丁、体积、升级对 mpv-lazy 分发的影响 | 查版本发布日期与已知问题 | 维护成本有承担者；风险有记录 |
| 0.7 | 验收线：任何新增依赖/自绘组件必须有“原生/系统方案不满足”的具体证据，否则标记 P0 返工 | 审查时逐条对照 0.1~0.6 | 依赖增量与功能增量成正比 |

## A. 插件独立性

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| A1 | 无 mpv 时宿主能否独立运行（示例菜单 + `--no-connect`） | 直接跑 `MpvMenuHost.exe --no-connect`，看窗口是否正常 | 能弹出示例菜单，无崩溃 |
| A2 | 是否依赖 mpv 内建菜单属性（menu-data/context-menu）才能工作 | `rg -n "menu-data\|context-menu\|user-data/menu" scripts/dyn_menu.lua` | 主路径不依赖；仅回退路径使用 |
| A3 | 宿主崩溃/被杀是否影响 mpv 主进程 | 播放中强杀 MpvMenuHost 所有进程，观察 mpv 与视频 | mpv 无异常退出，菜单下次可重新呼出 |
| A4 | 与同目录其他脚本（console、select、stats、dialog、input_plus、recentmenu、modernz、uosc 等）的键位/消息/属性冲突 | `rg -n "register_script_message\|script-message-to\|add_key_binding"` 全 config 脚本 | 无同名消息/键位抢占；host 消息命名带前缀 |
| A5 | 宿主不可用时是否自动回退原生菜单 | 临时改名 `tools\winui-menu-host` 后右键/播放列表按钮 | 回退 menu.dll 弹窗，有日志/无静默失效 |
| A6 | 文件自包含：不依赖系统级 WAS/.NET 运行时 | 全新 Win10/11 机器拷目录直接运行 | 直接可跑（当前自包含 266MB，确认目标机无依赖） |
| A7 | 安装/更新/卸载是否干净 | 对比 repo `tools\winui-menu-host` 与安装目录；删除后无残留进程/临时文件 | 三处哈希一致；无残留 `MpvMenuHost.exe`、`mpv-menu-host-*` 文件 |
| A8 | 临时文件/管道/状态文件名是否按 pid 隔离，多实例不互踩 | 枚举 `%TEMP%\mpv-menu-host-*`、`mpv-menu-host-state-*`、`heartbeat-*`、`trigger-*` | 命名含 pid/id；双 mpv 实例同时用不串台 |
| A9 | 退出清理：正常关闭、点外、Esc、IPC 失败、父死 5 类路径都清子进程和文件 | 读 `Close/ExitOrClose/ResetToPrewarm/WatchPeerExit` + 实测 | 无孤儿进程；临时 JSON 删除；日志有对应记录 |

## B. mpv 多版本兼容性（含 MCP 澄清）

> 前置说明：全仓（Lua + C# + 测试）`rg -i mcp` 无任何命中，本项目目前不存在 Model Context Protocol（MCP）面。若“MCP”指 mpv 客户端协议/多版本，按下表审查；若确指 MCP 服务器，则结论为“当前不适用”，另需确认用户是否要求新增该能力。

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| B1 | mpv 版本矩阵是否明确（mpv-lazy 自定义构建 / 官方 mpv / 0.35 以下旧版） | README/发布说明有无版本声明；实测官方 mpv | 有文档化矩阵；关键 API 有版本降级 |
| B2 | Lua API 兼容：`utils.format_json`、`mp.add_key_binding('MBTN_RIGHT', nil, fn)`、`register_script_message`、`observe_property`、`utils.subprocess{detach=true}` | 对照 mpv 文档与旧版 mpv 实测 | 最低支持版本能加载、不报错 |
| B3 | JSON IPC 兼容：管道名（`name` 与 `\\.\pipe\name` 两种形式）、request_id、异步响应、空响应/超时 | 读 `MpvIpc.cs`；用官方 mpv 起 `--input-ipc-server` 实测 | 两种命名都能连；超时重试正常 |
| B4 | `menu` 属性/`context-menu` 命令在宿主回退路径下不同构建的差异 | 读 `dyn_menu.lua` 的 `use_mpv_impl` 分支 | 回退路径在目标构建可用（本 repo 用本地重编译 menu.dll） |
| B5 | `dyn_menu.conf` 未知/缺失选项的容错（如旧版没有 host_menu） | 删掉/写错选项后启动 | 默认值生效，不报错不白屏 |
| B6 | mpv 退出时宿主链是否被清（父死子清、30 分钟兜底） | 起 mpv→呼菜单→杀 mpv，观察进程 | 1~2 秒内宿主进程全部退出 |
| B7 | Windows 版本兼容：Win10 21H2 / Win11 的 DWM 圆角、亚克力、`SegoeIcons.ttf` 是否存在 | 两台实机或虚拟机验证 | Win10 无崩溃、图标用 MDL2 回退 |
| B8 | .NET/WAS 运行时兼容：net10.0-windows10.0.26100 + WAS 2.3.1 自包含在最低目标 OS 可跑 | 全新机器拷贝运行 | 无运行时缺失错误 |

## C. 对原生 SDK 的调用兼容性

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| C1 | P/Invoke 位宽正确性：`GetWindowLongPtrW/SetWindowLongPtrW/GetClassLongPtrW` 的 x86/x64 签名 | 审 `MainWindow.xaml.cs` DllImport 区；分别打 x64/x86 验证 | 无不匹配；x64 实测通过 |
| C2 | 低层钩子（WH_MOUSE/WH_KEYBOARD）的安装/卸载/崩溃恢复 | 读 `InstallHooks/UnhookHooks`；测试钩子回调异常 | 每次显示/复位都成对；卸载不残留 |
| C3 | DWM API 可用性：`DWMWA_WINDOW_CORNER_PREFERENCE/BORDER_COLOR/NCRENDERING_POLICY` 在 Win10 的返回 | 查文档 + Win10 实测日志 | 不支持时忽略而非崩溃；有能力检测 |
| C4 | 窗口样式：WS_EX_TOOLWINDOW/NOACTIVATE/TOPMOST、WS_CAPTION/THICKFRAME 剥离在 WinUI 二次显示后是否保持 | `verify_host_window.ps1` 检查 style 位 | 显示后仍无 caption/thickframe |
| C5 | Windows App SDK 未打包模式：Bootstrap 初始化、AppWindow/OverlappedPresenter、XBF/PRI 资源补齐 | 读 `Program.cs`/`csproj` CopyXbfToPublish；全新目录运行 | 资源加载无 ms-appx 报错 |
| C6 | 亚克力：DesktopAcrylicController 在未打包/无 MSIX 下的支持与降级 | 读 `TryInitAcrylic`；关闭支持时跑 | 不支持时回落 solid，不白屏 |
| C7 | 高 DPI：PerMonitorV2、`GetDpiForWindow`、多显示器混合缩放、跨屏移动 | 150%/200% 缩放 + 双屏实测 | 菜单尺寸/位置正确，无缩放毛刺 |
| C8 | 深浅色：AppsUseLightTheme 读取、WM_SETTINGCHANGE 热切换、`--theme` 测试覆盖 | 运行中切换系统主题 | 实时跟随；强制主题只在测试模式生效 |
| C9 | UIA/辅助功能：窗口 Role/Name、菜单项可被读屏识别 | `verify_host_window.ps1` UIA 节点检查；NVDA 抽查 | 文本节点可枚举；无 “Unnamed” 关键控件 |
| C10 | 崩溃路径：`Application.Exit` 已知 AV，是否全部改走 `Environment.Exit` | `rg -n "Application.Current.Exit\|Environment.Exit"` | 无遗留 Application.Exit 调用路径 |
| C11 | 运行库依赖面（原则 0）：能否用系统已有运行时/API 替代自包含 .NET+WAS；手写 P/Invoke 面是否比原生 Win32 菜单更复杂 | 对照 0.2/0.4；实测系统级 WAS 或 Win32 方案 | 运行库增量有书面必要性结论 |

## D. 每个选项的功能可用性

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| D1 | input.conf 全部 `#menu:` 条目（206 条）逐项执行验证 | 按菜单树走查：打开/导航/视频/音频/字幕/音量/速度/查看/窗口/工具/截屏/滤镜/关于 | 每项命令真实生效（含参数如 `seek -5 exact`） |
| D2 | 状态条件 `#@state=` 表达式求值正确（idle_active、pause、tonumber(vid)、sub_visibility、fullscreen、icc_profile_auto、checked/disabled/hidden） | 读 Lua 状态解析；构造各状态实测 | 勾选/禁用/隐藏与播放器状态一致 |
| D3 | 动态菜单源可用：播放列表、轨道、章节、版本、配置文件、着色器、增强脚本、最近播放、音频设备 | 各场景实测生成项数与内容 | 动态项完整、超长标题按 `max_title_length` 截断 |
| D4 | 快捷键与菜单命令一致性（如 Ctrl+1/2 调色 vs 菜单项） | 抽查 10 项快捷键 vs 菜单命令 | 无命令漂移 |
| D5 | 特殊命令族：`script-message-to dialog/input_plus/stats/select/recentmenu`、`vf set`、`ab-loop`、`playlist-shuffle`、`quit-watch-later`、截图参数 | 逐族实测 | 都触发正确动作，无“点击无反应” |
| D6 | 分隔线/禁用/勾选/隐藏四态渲染 | 用含四态的 JSON 目测 | 视觉正确，禁用项不可点 |
| D7 | 点击叶子项 → IPC → mpv 执行 → 菜单关闭的完整链路 | M2/M3/M4 测试 + 真机右键 | 命令成功、菜单关闭、无重复执行 |
| D8 | 子菜单深度：三级（如 视频>调色>对比度+1）级联打开与叶子执行 | `cascade-menu.json` + 真机 | 方向正确、不闪烁、叶子生效 |
| D9 | 无命令项/纯子菜单项/动态空菜单的处理（空播放列表等） | 实测空列表、纯子菜单 | 有提示或禁用，不报错不闪退 |

## E. 窗口渲染稳定性

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| E1 | 首帧白屏/白框/缩放跳动 | 快速反复呼出，慢动作录像 | 无白帧；尺寸一次到位 |
| E2 | 多级窗口置顶链：z-order 不互相抢夺、不交替闪烁 | 三级菜单下快速移动鼠标 100 次 | 无闪烁、最末级始终在最上 |
| E3 | 子菜单切换（A→B）时旧窗口残留/旧子级未复位 | 快速来回悬停两个带子菜单项 | 旧子级隐藏待命，无叠窗 |
| E4 | 屏幕边缘/任务栏遮挡：右缘、下缘、左上角、双屏边界 | 各角落呼出 | 完整可见、正确翻转、不超屏 |
| E5 | 长时间显示：60s 看门狗、预热 30min 看门狗、心跳判定 | 挂 1 分钟/30 分钟观察 | 显示中不误杀；空闲后正常复位/退出 |
| E6 | 入场动画异常兜底（Storyboard 属性路径失败、alpha 卡 0） | 读 `AnimateIn/FadeInWindowAsync`；人为制造异常 | 任何异常路径菜单仍可见 |
| E7 | 尺寸计算：pass1/pass2、`ResizeToContent`、超长菜单滚动、30% 屏宽上限、DPI 混合 | 40 项长菜单 + 150% DPI | 高度钳制、可滚动、无横向溢出 |
| E8 | 资源泄漏：钩子、子进程句柄、文件句柄、临时文件、acrylic | 反复呼出/关闭 200 次，观察句柄数与 %TEMP% | 句柄不增长、临时文件不累积 |
| E9 | 崩溃自愈：宿主异常退出后下次右键能重新冷启动 | 杀掉宿主后立刻右键 | 自动拉起新进程，无“死菜单” |
| E10 | 已知限制台账：亚克力不模糊视频、DWMSBT_TRANSIENTWINDOW 弃用记录 | README “已知限制”与实际行为核对 | 记录与现状一致，无已修项仍标注 |

## F. 用户可自定义的配置文件

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| F1 | `dyn_menu.conf` 现有项逐一确认生效：use_mpv_impl、max_title_length、host_menu、host_menu_path、host_test_select、host_log | 改值后实测 | 每项行为与文档一致 |
| F2 | 宿主外观参数是否可配置：最小宽度、圆角、动画开关/时长、子菜单重叠量、图标开关、字体、深浅色覆盖 | 查 `MainWindow.xaml.cs` 常量 | 当前多为硬编码 → 决定是否暴露到配置；若暴露需文档化 |
| F3 | 配置错误容错：路径不存在、yes/no 解析、未知键 | 故意写坏配置 | 回落默认值，日志提示，不崩溃 |
| F4 | 配置优先级：Lua 默认值 < dyn_menu.conf < 命令行参数 | 读加载顺序 | 有明确且文档化的优先级 |
| F5 | 配置文档完整性：README/发布说明与实际选项一一对应 | 对照 README 命令行节 | 无文档外选项、无选项外文档 |

## G. 中英文与原生 mpv 配置默认值

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| G1 | 默认菜单语言是否符合发布意图（当前 input.conf 菜单树全中文） | 走查 `#menu:` 标题 | 与 mpv-lazy 用户群一致；有结论记录 |
| G2 | Lua 硬编码文案（“播放列表为空”“音轨”“mpv 菜单”等）是否可本地化 | `rg -n "[一-龥]" scripts/dyn_menu.lua` | 有统一文案表或明确“仅中文”声明 |
| G3 | 宿主/原生回退菜单的文案一致性（Win32 menu.dll vs WinUI） | 切换 host_menu=yes/no 对比 | 功能等价；文案差异有记录 |
| G4 | 是否提供英文配置模板/双语切换机制 | 查 repo 有无 `*-en` 模板、locale 判断 | 有则验证；无则记录为已知限制 |
| G5 | 编码处理：Lua/JSON/PS1 的 UTF-8 与 BOM；非中文系统字体回退 | 全仓扫描编码；英文系统实测 | 中文不乱码；图标字体有回退 |

## H. 代码逻辑审查（冗余 / 缺失 / 重复 / 绕圈子 / 重复造轮子）

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| H1 | 三套菜单构建逻辑（Lua `append_menu`、宿主 `BuildMenu`、原生 menu-data）是否可收敛，同步成本是否可控 | 对比三处结构定义 | 有单一数据源或明确的差异理由 |
| H2 | 多份发布产物冗余：publish / publish-release / publish-r2r 及 repo/安装目录双副本（各 266MB） | 查目录体积与用途 | 保留最少产物；同步流程脚本化 |
| H3 | 文件 IPC 链路是否绕圈：JSON→临时文件→trigger→heartbeat→state→RESET 多通道并存的必要性 | 画调用图，逐个通道找唯一职责 | 每条通道有不可替代职责；否则合并为管道/套接字 |
| H4 | 重复造轮子逐项判定（按原则 0）：自绘 WinUI 菜单 vs mpv 原生 context-menu/menu.dll；自包含 .NET+WAS vs 系统运行库；低层鼠标/键盘钩子 vs 系统焦点窗口；手写置顶链 vs 单进程 owned 子窗口；手写 DPI/边框剥离 vs OverlappedPresenter/WinUI 窗口 API；手写动画 vs Composition/样式 | 每项列出“原生/官方/系统替代方案”并写采纳或拒绝理由 | 有书面决策记录，非默认绕路 |
| H5 | 死代码与测试钩子：SampleMenu、`--no-connect`、`--test-select`、`--theme` 是否标注用途 | `rg -n "SampleMenu\|no-connect\|test-select"` | 有注释或文档；正式路径不触发 |
| H6 | 魔法数字集中：动画时长、重叠 6px、最小宽度 300、30% 屏宽、超时/重试次数 | 扫描数字常量 | 有命名常量或集中配置，便于调参 |
| H7 | 竞态点审计：trigger 文件独占写重试、WatchPeerExit 与 WatchChildPrewarmAsync 并存、`_session` 防串台、ResetChildPrewarmToIdle | 逐函数读状态机 | 竞态有锁/重试/会话号防护；有注释 |
| H8 | Lua 状态机文档化：state 0/1/2、RESET 语义、父死子清 | 读 `dyn_menu.lua` 状态写入点 | 与宿主侧实现逐条对应 |
| H9 | 日志质量：错误码、堆栈、关键路径；避免敏感信息；`--log` 是否轮转 | 跑一遍全测试看日志 | 可定位问题；日志不无限增长 |
| H10 | 缺失项：安装/卸载脚本、版本号管理、发布清单（含三处同步校验）、CI | 查 repo 与 test/ | 按发布频率决定补齐优先级 |

## I. 外部生态对照（GitHub 热门库 / 相关经验 / 微软官方库）

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| I1 | 微软官方：WinUI 3 Gallery、Windows App SDK 示例、Flyout/MenuFlyout/Popup、DWM 圆角与阴影文档 | 对照官方示例与文档 | 每个手写点都能指出官方替代方案或被拒原因 |
| I2 | WinUI 生态：CommunityToolkit.WinUI（动画/设置存储/窗口）、WinUIEx（AppWindow 封装） | 评估引入成本 vs 手写量 | 有明显收益才引入；否则记录决策 |
| I3 | Win32 封装库：Vanara.PInvoke / PInvoke.net 是否比手写 DllImport 更稳 | 对比当前 P/Invoke 面 | 手写面已审过（C1）且稳定，可不引入 |
| I4 | mpv 生态：mpv.net 上下文菜单、uosc 菜单实现、其他 menu 插件（menu.dll 作者仓库） | 查相关仓库 issue/实现 | 借鉴过的经验有记录（如 menu.dll 宽度缓存 bug） |
| I5 | 已知 issue 台账：WAS 未打包 #9978 白边、XBF/PRI 复制、Application.Exit AV、Acrylic 未打包限制、Segoe 图标 Win10 回退 | 代码注释与 README 是否覆盖 | 每条有链接/结论，避免重复踩坑 |

## J. 图标体系（自定义 / 预设 / 回退）

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| J1 | 图标来源定性：当前用系统预设字体 Segoe MDL2/Fluent（官方码点 E700–EC00），非自绘 | 查 `IconFor` 与字体文件 | 定性准确，文档写明“系统预设字形” |
| J2 | 是否存在自定义/资源图标能力（自绘、svg、png、主题图标） | 查项目资源与 IconFor 结构 | 无则记录为限制；有则验证加载与 DPI |
| J3 | 覆盖策略：156 精确 + 40 前缀；前缀误配风险（“播放”vs“播放列表”等） | 用 input.conf 全量标题跑覆盖脚本 | 无误配；静态项全命中（当前仅分隔线无图标） |
| J4 | 动态项图标策略：播放列表条目/轨道/章节/配置/着色器/设备名当前无图标 | 打开各动态菜单目测 | 无图标时对齐不塌；有默认图标则统一 |
| J5 | 字体回退：Win10 无 SegoeIcons.ttf → MDL2；两者都缺时的表现 | 双字体校验脚本 + 删字体目录模拟 | 无豆腐块/异常 |
| J6 | 语义审查：每个映射与条目语义相符（版本→Movies、轨道→Switch、速度→Lightning 等） | 全表人工复核 | 明显错配项清零 |
| J7 | 图标可配置性：用户能否关图标/换图标/改大小颜色 | 查是否有配置项 | 无则记录为 P2 需求 |
| J8 | 高对比度/缩放：图标颜色 `_subBrush` 在深浅色与高对比度下可读 | 高对比度主题实测 | 可辨识 |

## K. 补充审查维度

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| K1 | 安全：临时文件权限（%TEMP% 其他用户可写）、JSON 路径注入、IPC 管道无认证（任意进程可连 mpv 管道发命令） | 审 temp 文件 ACL 与 MpvIpc | 菜单 JSON 仅本地可信来源；管道风险记录到 README |
| K2 | 性能：冷启动耗时、预热链每级进程内存（N 级 ≈ N×进程）、KeepOnTopLoop 100ms 循环 CPU、动画帧率 | 任务管理器/ETW 实测 | 冷启动 < 300ms；空闲 CPU 可忽略 |
| K3 | 兼容矩阵文档化：OS/DPI/主题/输入法/触屏/键盘/读屏 | 建矩阵表并逐格实测 | 未测格显式标注 |
| K4 | 辅助功能：键盘 ↑↓/Enter/Esc（已有）、Tab 焦点、读屏朗读、高对比度 | NVDA + 键盘实测 | 键盘可完整操作；读屏能读项名 |
| K5 | 测试策略缺口：真实右键 E2E（未进 run_all）、动态菜单、边缘翻转、双屏、长时间运行、崩溃注入、Win10 实机 | 对照 `test/run_all.ps1` 10 项清单 | 缺口项有测试计划或明确“人工验证” |
| K6 | 打包/部署/版本：三处同步流程、版本号、DLL 哈希核对、发布说明 | 现有 `deploy_host.ps1` + 哈希比对 | 发布清单脚本化，含版本校验 |
| K7 | 体积优化（按原则 0）：自包含 266MB × 2 副本；优先评估消除自包含（系统已有运行时/原生路径），其次才是裁剪（排除语言包、裁剪失败记录） | 分析发布目录占用 + 运行时替代方案 | 有结论：消除自包含 or 裁剪 or 接受并说明理由 |
| K8 | 许可证与合规：WindowsAppSDK/BuildTools/.NET 许可证、mpv-lazy 分发整合 | 查包许可证 | 无分发冲突 |
| K9 | 诊断能力：`--log` 轮转、crash.log 有效性、崩溃转储 | 制造一次崩溃 | 能定位根因 |
| K10 | 需求追踪：用户已反馈问题（转圈圈/三级方向/图层覆盖/标题/图标/重叠/动画）是否都有对应修复与回归用例 | 对照 git 历史与测试 | 每条有修复点 + 回归验证 |
| K11 | 依赖台账落库（原则 0）：每个包/运行时/自包含组件的用途、体积、升级路径、替代方案形成可维护清单 | 建 `DEPENDENCIES.md` 或 README 节 | 台账与 `csproj`/发布目录一致 |

## L. 架构决策复核（b1 原生菜单 vs b2 自绘宿主）

> 本插件现状是 b2（独立 WinUI 3 宿主进程，自包含 .NET+WAS，266MB×2）。按原则 0，必须先回答“为什么不用 b1（mpv 内建 context-menu / menu.dll / Lua）”，再决定继续自绘、缩减自绘或回退。

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| L1 | b1 原生菜单的能力缺口清单：原生无法满足哪些需求（图标、动画、Win11 风格、渲染稳定性、动态内容） | 用 menu.dll / mpv 内建菜单逐项实测 | 缺口项有真实复现，非主观 |
| L2 | b2 相对 b1 的增量收益量化：每项收益（如图标覆盖、重叠+动画）是否值得 266MB 与自包含运行时 | 对比两方案实测（功能矩阵） | 收益/成本有明确结论 |
| L3 | 可否收缩为“原生优先、自绘仅作可选增强”：默认 host_menu=no，自绘作为用户可选项 | 实测默认原生路径全功能 | 若可行，发布默认应原生 |
| L4 | 回退路径保真度：原生与自绘的菜单树、状态、命令、动态项是否 100% 等价 | 两路径跑同一 input.conf 菜单树 | 无功能漂移 |
| L5 | 决策记录：b1/b2 取舍写进 README/审查结论，含采纳与拒绝理由 | 文档化 | 后人可追溯 |

## 结论汇总模板

| 维度 | 结论 | 主要证据 | P0/P1/P2 遗留项 |
|---|---|---|---|
| A 插件独立性 | | | |
| B mpv 版本兼容 | | | |
| C 原生 SDK 调用 | | | |
| D 选项功能可用性 | | | |
| E 窗口渲染稳定性 | | | |
| F 用户自定义配置 | | | |
| G 中英文/原生配置默认值 | | | |
| H 代码逻辑 | | | |
| I 生态对照 | | | |
| J 图标体系 | | | |
| K 补充维度 | | | |
| L 架构决策复核（b1 vs b2） | | | |
| 原则 0 依赖最小化 | | | |

总体结论：`可发布 / 有条件发布 / 不可发布`，理由与阻塞项列表。
