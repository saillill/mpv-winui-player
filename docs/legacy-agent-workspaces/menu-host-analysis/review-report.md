# MpvMenuHost 审查报告（按 review-checklist.md 执行）

> 状态：进行中。按用户指示从**原则 0（原生优先与依赖最小化）**开始，**暂停全部动态测试**；已完成原则 0 + L（b1 vs b2 架构复核），其余 A–K 维度按序续写。
> 审查基线：2026-08-07；repo main 领先 origin 39 提交（v3.10.5 已提交，v3.10.6+ 未提交）；宿主源码 `MainWindow.xaml.cs` ~1900 行；mpv 0.41.0-615-g7b057f66f（mpv-lazy 包装器）。

## 原则 0：原生优先与依赖最小化

> 结论先行：**当前 b2（WinUI 3 自绘宿主）与原则 0 直接冲突，P0 成立。** 默认配置走 b2（自包含 .NET 10 + Windows App SDK 2.3.1，266.5MB×2 副本，每进程 ~125MB），而项目本身已带 b1 原生路径（menu.dll，99KB，系统 Win32 菜单），且系统/OS 已提供用户要求的大部分观感（Win11 圆角、亚克力、动画、级联重叠）。没有任何书面证据说明这些收益值得 266MB + 每会话 3×125MB 常驻的代价。

### 0.1 新增依赖台账

结论：❌ 不通过。无台账文档；实际引入（证据：`MpvMenuHost.csproj`）：

| 依赖 | 版本/形态 | 体积/代价 |
|---|---|---|
| .NET 10 | net10.0-windows10.0.26100.0 | 自包含 R2R 运行时 |
| Windows App SDK | 2.3.1（WindowsAppSDKSelfContained=true，WindowsPackageType=None） | 随宿主分发 |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2526 | 构建期 |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.5.0 | 构建期 |
| PublishReadyToRun | Release 开启 | 发布体积增大 |

产物证据：`publish-r2r` 267MB、`publish-release` 245MB、`publish` 221MB、`bin` 442MB、`obj` 104MB；部署到 repo 与安装目录各 266.5MB（518 文件）×2。运行证据（审查期间实测）：单宿主进程工作集 124–125MB，三级预热链 ≈374MB 常驻。

无 README/DEPENDENCIES 说明“为什么引入、替代方案、升级路径”。README 自认“分发方式（自包含 WAS 目录 ~340 文件，或改 framework 依赖）”“发布前需定”。

### 0.2 是否必须自包含运行时

结论：❌ 不通过。功能上不必须：

- 项目已具备 b1 原生路径：`menu.dll`（tsl0922/mpv-menu-plugin，C 插件，99,328B，系统 Win32 菜单），零新增运行时。
- 即使保留自绘菜单，也可改为框架依赖系统 WAS 运行时，或 .NET Framework 4.8（Win10/11 系统自带）+ WPF/WinForms，均无需 266MB 自包含。
- 自包含必要性无书面论证；README 的“已知限制”也未解释为何放弃 framework 依赖。

### 0.3 mpv 本体能力优先

结论：❌ 不通过（主路径反转）。

- 默认配置 `dyn_menu.conf`：`host_menu=yes` → 右键/按钮菜单默认走 b2 自绘宿主。
- b1 路径存在且完整：`use_mpv_impl`（mpv 内建 menu-data/context-menu，运行时检测）与 `menu.dll`（Win32 原生弹窗，含本地重编译修复宽度缓存 bug）。
- 用户点名的“重叠 + 展开动画”是系统 Win32 弹出菜单（TrackPopupMenu）在 Win11 上自带的行为；b2 是在重复实现系统已有能力，并为此付出 266MB + 自包含运行时 + 手写 DWM/置顶链/低层钩子的代价。
- b2 的历史 bug（白框闪、置顶闪烁、悬停转圈、三级方向、图层覆盖）在 b1 原生路径中不存在——这些问题是自绘窗口引入的，不是 mpv 原生菜单的问题。
- 当前可确认的 b1 缺口只有“条目图标”（menu.dll 不渲染图标），且 Win32 菜单本身支持 bitmap/owner-draw，不是不可逾越的原生限制。

### 0.4 系统组件优先

结论：⚠️ 部分通过。b2 大量使用系统组件（User32/DWM/系统字体/低层钩子），但核心“菜单渲染/交互”绕开了系统已有 TrackPopupMenu 自绘实现；H4 判定为“系统已有能力却自行实现”的高成本用例。

### 0.5 默认路径零新增依赖

结论：❌ 不通过。发布默认 `host_menu=yes`（b2）；b1 仅作为宿主缺失时的回退。按原则 0 应反转：默认 `host_menu=no`（原生），b2 作为用户显式选择的实验增强。

### 0.6 新增依赖的维护面

结论：❌ 不通过。无维护方案：

- Windows App SDK 2.3.1 / .NET 10 的升级与安全补丁跟踪无记录；
- 自包含产物需在 repo tools 与安装目录双份同步（各 266.5MB）；
- 宿主无版本号管理、无 LICENSE（宿主源码目录无 LICENSE/COPYING）；
- `crash.log` 追加到运行目录且无轮转（安装目录为 Program Files 时可能写失败，静默）；
- README“M4 待办/剩余（发布前）”明确承认分发方式未定。

### 0.7 验收线

结论：❌ 不通过。没有“原生/系统方案不满足”的证据记录；L1/L2 的缺口清单与收益量化均缺失。唯一明确增量（图标）未量化收益 vs 266MB+374MB 成本。

## L. 架构决策复核（b1 原生菜单 vs b2 自绘宿主）

| 编号 | 检查点 | 结论 | 证据/说明 |
|---|---|---|---|
| L1 | b1 原生菜单能力缺口清单 | ⚠️ 未完成 | 可确认缺口：menu.dll 不渲染条目图标。系统菜单在 Win11 自带圆角/亚克力/动画/级联重叠；宽度缓存 bug 已由本地重编译修复。其余缺口无记录。需实机核验（暂停测试中） |
| L2 | b2 相对 b1 增量收益量化 | ❌ 未做 | 无功能矩阵对比；成本明确：266.5MB×2 + 每会话 3×125MB 常驻 + 全部 b2 渲染 bug 修复投入 |
| L3 | 可否“原生优先、自绘可选” | ✅ 可行（建议） | `host_menu` 已是现成开关；改默认 `no` 即可，b2 保留为可选增强，成本与风险由用户显式承担 |
| L4 | 回退路径保真度 | ⚠️ 未验证 | 需两路径跑同一菜单树对比（暂停测试中） |
| L5 | 决策记录 | ❌ 缺失 | README 只有版本历史，无“为什么 b2 而非 b1”的决策文档，无法追溯采纳/拒绝理由 |

## P0 / P1 行动项（按原则 0）

P0：
1. 建立 `DEPENDENCIES.md` 台账：每个依赖/运行时的用途、体积、升级路径、替代方案（0.1/K11）。
2. 发布默认改回原生：`dyn_menu.conf` 默认 `host_menu=no`，b2 降级为可选增强（0.3/0.5/L3）。
3. 写 b1 vs b2 决策记录（L5）：明确缺口清单与收益/成本结论。
4. 对“是否保留自包含 b2”给出明确结论：保留需补必要性论证，否则移除（0.2/K7）。

P1：
5. 宿主源码补 LICENSE 与版本号管理（0.6/K8）。
6. 临时文件残留修复：heartbeat/state/trigger/items 文件退出时清理（审查实测：%TEMP% 累积 heartbeat 79、state 33、trigger 3、items 3，README“零残留”声明不成立）——该项属 E8/K1，此处仅登记，修复放在对应维度。

## 测试状态（按用户指示暂停）

- 已完成（暂停前）：`run_all.ps1` 10/10 PASS（verify 系统/深/浅/长菜单、M2 mock、M2 真 mpv、M3 e2e、M4 级联、孤儿预热退出、宿主缺失回退）。
- 未定论：`run_input_test.ps1` 键盘/ESC/点外关闭失败，但探针显示自动化环境本身 `SetCursorPos(123,321)` 未生效（光标仅从 1157,835 变到 1154,838），判定为环境受限，不能据此下“宿主输入坏”结论；K4/E 相关项留待实机人工验证。
- 其余动态项（真实右键 E2E、Win10 实机、双屏、206 项逐项执行）全部挂起，待用户解除测试禁令后按 L1/L4 计划执行。

## A. 插件独立性

| 编号 | 结论 | 证据 |
|---|---|---|
| A1 | ✅ | 宿主可独立运行（示例菜单 + `--no-connect`）；verify_host_window 系列全部 PASS（历史测试） |
| A2 | ✅ | 主路径不依赖 menu-data/context-menu；Lua 仅在选择原生回退（use_mpv_impl）时探测 `menu-data` |
| A3 | ⚠️ | 独立进程 + Lua `detach=true` + 父死子清（WatchPeerExit/30min 看门狗），静态成立；“杀宿主不影响 mpv”需实机确认（暂停） |
| A4 | ✅ | 键位/消息为脚本命名空间隔离；`dynamic_menu.lua` 已核实为协作脚本（不绑 MBTN_RIGHT，只向 dyn_menu 推送 vs/shader 动态菜单），非重复实现；其余脚本依赖面已识别 |
| A5 | ✅ | run_fallback_test PASS：宿主缺失 → 警告 + 回退原生菜单（历史测试） |
| A6 | ✅ | 自包含，不依赖系统运行时（代价见原则 0） |
| A7 | ⚠️ | deploy_host.ps1 三处同步，dll/lua/conf 哈希一致；input.conf、dyn_menu.lua、dynamic_menu.lua、recentmenu.lua、dyn_menu.conf 五个关键文件 repo/安装目录全部一致；无卸载脚本、无版本号管理 |
| A8 | ⚠️ | 文件按 pid/prewarm-id 隔离，多实例不互踩；但残留文件累积（见 E8/K1） |
| A9 | ⚠️ | 五类退出路径代码齐全、孤儿预热测试 PASS；items 偶发残留（%TEMP% 现存 3 个），heartbeat/state 从不删除 |

## B. mpv 多版本兼容性（含 MCP 澄清）

| 编号 | 结论 | 证据 |
|---|---|---|
| B1 | ❌ | 无版本矩阵文档；本机 mpv 0.41.0-615；menu.dll 上游要求 ≥0.37；旧版 mpv 未验证 |
| B2 | ⚠️ | Lua 同时兼容 5.1（setfenv）与 5.2（load）；`utils.subprocess{detach}` 等 API 的最低版本要求未验证（需实机，暂停） |
| B3 | ✅ | MpvIpc 同时支持 `name` 与 `\\.\pipe\name`；M2 真 mpv / M3 e2e 实测 IPC 成功（历史测试） |
| B4 | ⚠️ | `use_mpv_impl` 运行时探测 menu-data；本机构建未验证内建菜单路径（需实机，暂停） |
| B5 | ✅ | host_menu_path 缺失/不存在 → host_available 警告并回退；未知配置键由 mp.options 忽略 |
| B6 | ✅ | 父死子清 + 孤儿预热退出测试 PASS（历史测试） |
| B7 | ⚠️ | Win11 本机通过；Win10 的 DWM 圆角/亚克力/字体回退均为 best-effort，未实机验证 |
| B8 | ✅ | 自包含 R2R，OS 最低 17763；无运行时缺失风险（体积代价见原则 0） |

> MCP 澄清：全仓 `rg -i mcp` 无命中，本项目无 Model Context Protocol 面；若指 mpv 客户端协议，见 B3。

## C. 对原生 SDK 的调用兼容性

| 编号 | 结论 | 证据 |
|---|---|---|
| C1 | ✅ | P/Invoke 全部用 IntPtr/正确签名，x64 实测通过；x86 未构建验证（项目 Platforms 含 x86） |
| C2 | ⚠️ | 钩子安装/卸载成对、句柄入日志；真实输入验证受环境限制未定论（run_input_test 失败，SetCursorPos 本身未生效） |
| C3 | ⚠️ | DWM 调用不检查返回值；Win10 上圆角/边框色属性会静默失败（不崩溃），无能力检测 |
| C4 | ✅ | WM_STYLECHANGING 子类化剥 caption/thickframe；verify 断言 frame-diff=0,0,0,0 且无 WS_CAPTION/THICKFRAME（历史测试） |
| C5 | ✅ | 未打包 WAS：自定义 Main + XBF/PRI 拷贝目标；本机全部测试通过；全新机器未验证 |
| C6 | ✅ | DesktopAcrylicController.IsSupported 检测 + 回落 solid + 日志 |
| C7 | ⚠️ | Program.Main 强制 PerMonitorV2 + GetDpiForWindow；仅 100% 缩放验证过，多 DPI/双屏未验证 |
| C8 | ✅ | WM_SETTINGCHANGE 热切换；verify dark/light 断言 THEME/tint PASS（历史测试） |
| C9 | ⚠️ | UIA 文本节点可枚举（verify ≥5 节点）；无显式 AutomationProperties.Name，读屏体验未验证 |
| C10 | ✅ | 无 Application.Exit 调用；崩溃路径统一 Environment.Exit；AppDomain 异常写 crash.log |
| C11 | ❌ | 运行库依赖面违反原则 0（见 0.1/0.2） |

## D. 每个选项的功能可用性

| 编号 | 结论 | 证据 |
|---|---|---|
| D1 | ⚠️ | 206 条 `#menu:` 静态走查完成（全部有命令）；逐项执行验证暂停中 |
| D2 | ⚠️ | Lua 状态表达式编译/求值逻辑在（compile_expr + observe_property 脏标记）；状态矩阵实机验证暂停中 |
| D3 | ⚠️ | 动态源代码路径齐全（playlist/track/chapters/editions/profiles/shader/vs/recent/audio-devices）；动态场景实测暂停中 |
| D4 | ⚠️ | 快捷键与菜单命令同源（input.conf）；抽查走查未发现漂移；全量对照待机 |
| D5 | ⚠️ | 特殊命令族代码路径识别完毕（dialog/input_plus/stats/select/recentmenu、vf、ab-loop 等）；执行验证暂停中 |
| D6 | ✅ | disabled/checked/hidden/separator 四态在宿主渲染与 JSON 模型均有实现（MenuData/MakeItem） |
| D7 | ✅ | M2 真 mpv + M3 e2e 验证点击→IPC→mpv 执行→关闭链路（历史测试） |
| D8 | ✅ | M4 级联测试验证三级菜单叶子命令（历史测试）；方向/重叠/动画观感需实机确认 |
| D9 | ⚠️ | 空播放列表有 show-text 提示；纯子菜单/空动态菜单场景未实机验证（暂停） |

## E. 窗口渲染稳定性

| 编号 | 结论 | 证据 |
|---|---|---|
| E1 | ⚠️ | layered alpha=0 + 2 帧机制 + 动画兜底；白帧需实机录像确认（暂停） |
| E2 | ⚠️ | 链式置顶（KeepOnTopLoop 100ms）+ 父插子下逻辑；快速移动闪烁需实机（暂停） |
| E3 | ⚠️ | 切换子菜单有 KillChildIfAlive/ResetChildPrewarmToIdle；残留需实机（暂停） |
| E4 | ⚠️ | 边缘钳制/翻转代码齐全（pass1/pass2）；四角与双屏实机未验证（暂停） |
| E5 | ✅ | 60s 瞬态看门狗 + 30min 预热看门狗 + 心跳 1s/3s 判定，代码齐全 |
| E6 | ✅ | AnimateIn/FadeInWindowAsync 异常兜底均恢复可见（代码审查） |
| E7 | ✅ | 长菜单 300x1032 高度钳制 PASS（verify long menu，历史测试）；多 DPI 未验证 |
| E8 | ❌ | 临时文件泄漏：heartbeat 79、state 33、trigger 3、items 3（实测）；README“零残留”声明不成立 |
| E9 | ⚠️ | 崩溃后冷启动由 Lua host_available/launch 路径保障；实机未验证（暂停） |
| E10 | ⚠️ | README 已知限制有台账（Acrylic/DWMSBT）；但多项描述与现状漂移（见 F5/H10） |

## F. 用户可自定义的配置文件

| 编号 | 结论 | 证据 |
|---|---|---|
| F1 | ⚠️ | 全部选项真实生效（含未写入 conf 的 uosc_syntax/max_playlist_items/escape_title，均被引用）；host_menu_path 已用 `~~` 相对路径 |
| F2 | ❌ | 宿主外观全部硬编码：MinWidth 300、CornerRadius 8、重叠 6、动画 160ms/14ms、悬停 300ms、行距/字号、60s/30min 看门狗；无用户配置 |
| F3 | ✅ | 路径缺失/未知键均有容错（host_available 警告回退；mp.options 忽略未知键） |
| F4 | ✅ | 优先级明确：Lua 默认值 < script-opts/dyn_menu.conf < 宿主 CLI 参数 |
| F5 | ❌ | README 与代码多处漂移：动画描述为“Composition API”（实为 Storyboard+layered）；图标描述为“保守映射/23 码点”（实为 157+47）；v3.6 称“删除入场动画”（已重新加入）；“%TEMP% 零残留”不成立；“剩余（发布前）”与现状矛盾 |

## G. 中英文与原生 mpv 配置默认值

| 编号 | 结论 | 证据 |
|---|---|---|
| G1 | ✅ | 默认菜单树全中文（206 条，含 URL/MediaInfo/FAQ 等术语）；符合 mpv-lazy 中文定位，需在发布说明中声明 |
| G2 | ❌ | Lua 43 行硬编码中文（“播放列表为空”“音轨”“视频/字幕”“关闭/自动”等），无本地化机制 |
| G3 | ✅ | 宿主与原生回退共用同一份 Lua 菜单数据，标题一致（宿主已去除顶部标题） |
| G4 | ❌ | 无英文模板/语言切换；记录为已知限制 |
| G5 | ⚠️ | Lua/JSON 均 UTF-8；测试脚本 BOM 不一致：6 个含非 ASCII 的 ps1 中仅 3 个带 BOM（verify_host_window.ps1 无 BOM 却含中文，与 README“必须 BOM”声明矛盾） |

## H. 代码逻辑审查

| 编号 | 结论 | 证据 |
|---|---|---|
| H1 | ❌ | 三套菜单构建（Lua append_menu、宿主 BuildMenu、原生 menu-data）重复，无单一数据源；b2 修复不影响 b1，同步成本真实存在 |
| H2 | ❌ | 发布产物冗余：publish 221MB + publish-r2r 267MB + publish-release 245MB + bin 442MB + obj 104MB，另 repo/安装目录 266.5MB×2 |
| H3 | ⚠️ | 文件 IPC 五通道（JSON/trigger/heartbeat/state/RESET）各有职责但复杂度高、文件锁竞态已多次修（git log）；建议评审是否可用父子 stdio/命名管道替代 |
| H4 | ❌ | 重复造轮子多项且无决策记录：自绘 WinUI 菜单 vs TrackPopupMenu；LL 钩子 vs 焦点窗口；跨进程手写置顶链 vs 单进程 owned 窗口；自包含运行时 vs 系统运行库；动画 vs 系统菜单动画（原则 0/L） |
| H5 | ⚠️ | 测试钩子（--no-connect/--test-select/--theme/SampleMenu）均有 README 标注用途；但存在死代码字段 `_openLeft`（行 73/1152/1385 只写不读） |
| H6 | ⚠️ | 魔法数字散布代码/XAML（300/0.3/6px/300ms/100ms/60s/30min/160ms/14ms/12,7 等），无常量类 |
| H7 | ✅ | 竞态防护到位：trigger 独占写重试、_session 防串台、双监听按 pid/id 刷新；git log 显示逐轮修复 |
| H8 | ⚠️ | state 0/1/2 与 RESET 语义只在宿主注释中，无设计文档 |
| H9 | ⚠️ | 日志带时间戳/关键路径；无轮转；crash.log 无限追加且安装在 Program Files 时可能写失败；测试中出现 Application.Start 退出期异常栈噪音（未影响断言） |
| H10 | ❌ | 缺安装/卸载脚本、版本号管理、CI、发布校验清单（deploy_host.ps1 不含自动哈希校验） |

## I. 外部生态对照

| 编号 | 结论 | 证据 |
|---|---|---|
| I1 | ⚠️ | 使用官方 API 并记录 WAS #9978 白边 workaround；无“官方替代 vs 手写”对照文档 |
| I2 | ➖ | CommunityToolkit/WinUIEx 未引入（符合原则 0），也未评估记录 |
| I3 | ✅ | 手写 P/Invoke 面约 25 项且已审（C1）；小面自持可接受，无需 Vanara |
| I4 | ✅ | b1 = tsl0922/mpv-menu-plugin（C 插件 + dynamic_menu.lua）；本仓 menu.dll 为本地重编译版，宽度缓存 bug 修复已记录 |
| I5 | ✅ | README 台账覆盖 #9978、Acrylic 未打包限制、Application.Exit AV、DPI 虚拟化、menu.dll 宽度 bug；缺 issue 链接汇总 |

## J. 图标体系

| 编号 | 结论 | 证据 |
|---|---|---|
| J1 | ✅ | 图标为系统预设字体（Segoe MDL2/Fluent 官方码点），fontTools 双字体校验通过；非自绘 |
| J2 | ✅ | 无自绘/资源图标；全部走 IconFor 码点映射 |
| J3 | ✅ | 覆盖脚本实测：157 精确 + 47 前缀，158 个静态标题全部命中（仅分隔线 `-` 无图标） |
| J4 | ⚠️ | 动态项（播放列表/轨道/章节/配置文件/着色器/设备名）多数无图标；“轨道”前缀可覆盖部分动态项；无图标时列对齐正常 |
| J5 | ⚠️ | Win11→Fluent、Win10→MDL2 回退已实现；两字体都缺失时无守卫（会豆腐块） |
| J6 | ⚠️ | 映射表语义人工复核过（版本→Movies、轨道→Switch 等）；无自动化语义校验 |
| J7 | ❌ | 图标开关/替换/大小颜色均不可配置（P2） |
| J8 | ⚠️ | 图标色用 _subBrush；高对比度未验证 |

## K. 补充审查维度

| 编号 | 结论 | 证据 |
|---|---|---|
| K1 | ⚠️ | 临时文件在 %TEMP%（同用户可写）；Lua 用 taskkill 杀心跳文件里的旧 pid，存在同用户进程诱导风险（低危，建议只按前缀+数字校验）；mpv IPC 管道无认证是 mpv 本体设计，需在 README 声明 |
| K2 | ❌ | 常驻成本高：每进程工作集 124–125MB × 3 ≈ 374MB（实测）；冷启动 ~336ms、预热 90–200ms（README）；KeepOnTop 100ms 轮询 |
| K3 | ❌ | 无兼容矩阵文档（OS/DPI/主题/输入法/触屏/读屏） |
| K4 | ⚠️ | 键盘导航/ESC/点外关闭代码齐全；自动化输入环境受限未定论；NVDA/触屏未验证 |
| K5 | ❌ | run_all 10 项覆盖基础链路；真实右键 E2E 未纳入 run_all；input 测试失败未定论；动态菜单/边缘/双屏/崩溃注入/Win10 均缺 |
| K6 | ⚠️ | deploy_host.ps1 存在且三处哈希一致（人工核对）；无自动版本/校验步骤 |
| K7 | ❌ | 自包含 266.5MB×2；trim 被禁用（WAS 未打包裁剪无法启动，README 有记录）；framework 依赖方案未实施（原则 0） |
| K8 | ❌ | dyn_menu.lua 上游 SPDX GPL-2.0-only（本地补丁需合规）；宿主源码无 LICENSE；menu.dll 上游许可证未核对 |
| K9 | ⚠️ | --log 无轮转；crash.log 无限追加且安装目录可能不可写；退出期异常栈噪音 |
| K10 | ⚠️ | git log 与 README 逐版本对应修复项；当前未提交改动（v3.10.6+ 标题/图标/重叠/动画）无回归用例（input 测试失效，真实 E2E 未纳入） |
| K11 | ❌ | 无 DEPENDENCIES.md/README 依赖台账；csproj 三包 + .NET10 + WAS 2.3.1 + R2R 无书面用途/体积/升级/替代方案（即 0.1 的落库缺失） |

## 结论汇总表

| 维度 | 结论 | 主要证据 | P0/P1/P2 遗留项 |
|---|---|---|---|
| 原则 0 依赖最小化 | ❌ | 自包含 266.5MB×2、每进程 ~125MB×3、menu.dll 99KB 原生路径存在、默认 host_menu=yes | P0：依赖台账、默认回原生、b1/b2 决策记录、自包含去留结论 |
| L 架构决策复核 | ❌ | 无缺口清单/收益量化/决策记录；系统菜单自带重叠+动画 | P0：L1/L2 实机核验 + L5 文档 |
| A 插件独立性 | ⚠️ | 独立进程/回退/自包含成立；残留文件、无卸载/版本 | P1：退出清理 heartbeat/state/items；P2：卸载脚本 |
| B mpv 版本兼容 | ⚠️ | IPC/回退/父死子清有证据；无版本矩阵、旧版未验证 | P1：版本矩阵文档；P2：旧 mpv 实机 |
| C 原生 SDK 调用 | ⚠️ | P/Invoke/无边框/主题/UIA 有证据；DWM 不查返回、多 DPI 未验证 | P1：DWM 能力检测；P2：Win10/多 DPI 实机 |
| D 选项功能可用性 | ⚠️ | 静态走查 206 条 + M2/M3/M4 链路证据 | P1：206 项实机逐项（暂停中） |
| E 窗口渲染稳定性 | ⚠️ | run_all 10/10、长菜单钳制、看门狗/兜底代码齐全；临时文件泄漏 | P1：临时文件清理；P2：白帧/闪烁/边缘实机 |
| F 用户自定义配置 | ❌ | 外观全硬编码；README 与代码多处漂移 | P1：README 修订；P2：外观参数配置化 |
| G 中英文/原生配置 | ⚠️ | 默认中文菜单符合定位；无本地化；BOM 不一致 | P2：本地化机制或显式“仅中文”；ps1 BOM 统一 |
| H 代码逻辑 | ❌ | 三套菜单构建重复、1.3GB 发布冗余、多项重复造轮子无记录 | P0：决策记录；P1：发布产物收敛、魔法数字常量、日志轮转 |
| I 生态对照 | ⚠️ | 已确认 mpv-menu-plugin 来源与 issue 经验；缺官方替代对照文档 | P2：补对照文档与 issue 链接 |
| J 图标体系 | ⚠️ | 157+47 全覆盖、系统预设字体、双字体校验 | P2：字体双缺失守卫、图标可配置 |
| K 补充维度 | ❌ | 内存常驻高、无兼容矩阵、测试缺口、LICENSE 缺失、IPC 无认证未声明 | P1：LICENSE、README 安全声明；P2：兼容矩阵、crash.log 轮转 |

总体结论：**有条件发布**。以 b1 原生路径为默认（host_menu=no）、b2 降级为显式可选，并完成 P0 清单后可发布；维持当前 b2 默认形态则不可发布。

## 覆盖率核对

清单 0.1–0.7、A1–A9、B1–B8、C1–C11、D1–D9、E1–E10、F1–F5、G1–G5、H1–H10、I1–I5、J1–J8、K1–K11、L1–L5 全部编号均在本报告有结论；标 ⚠️/❌ 且注明“实机/暂停”的条目为按用户指示挂起，证据级别已在对应行注明。

落地步骤：
1. 默认回退原生（host_menu=no），b2 降级为显式可选；
2. 补依赖台账、决策记录、LICENSE、版本号与临时文件清理；
3. 动态验证项（D1/D2/D3/E1–E4/K4 等）待解除测试禁令后按 L1/L4 实机补测。

## 已产出草案（对应 P0/P1 行动项）

- 依赖台账草案：[dependencies.md](work/dependencies.md)（P0/0.1/K11）
- b1 vs b2 决策记录草案：[b1-b2-decision.md](work/b1-b2-decision.md)（P0/L5）
- 兼容矩阵草案：[compat-matrix.md](work/compat-matrix.md)（P1/K3）

上述草案定稿条件：L1/L2 实机核验（需解除测试暂停）＋ 用户确认 b1/b2 去留。

## 后续维度进度

| 维度 | 状态 |
|---|---|
| 原则 0 / L | ✅ 已完成 |
| A–C | ✅ 静态完成（实机项已标注） |
| D | ⚠️ 静态走查完成，执行验证挂起 |
| E | ⚠️ 部分完成，实机/观感挂起 |
| F–K | ✅ 静态完成（实机项已标注） |
