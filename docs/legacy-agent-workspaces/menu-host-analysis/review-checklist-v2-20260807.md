# MpvMenuHost 审查清单 v2（2026-08-07，目标重设）

> 用途：按用户要求重设审查目标，围绕四个维度审查现有插件
> （v4.1：单进程 WinUI 3 + 官方 MenuFlyout + mpv IPC）。
> 结论标记：✅ 通过 / ⚠️ 部分通过 / ❌ 不通过 / ➖ 不适用；P0 必改，P1 建议，P2 可延后。
> 涉及目录：宿主源码 `work\mpv-menu-host`；Lua/配置 `mpv-config`（repo 与安装目录）。

## F. 官方组件还原度（mpv 官方菜单组件 / WinUI 官方控件）

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| F1 | 数据源同源：host 渲染树与官方菜单路径（menu-data/menu.dll）来自同一 input.conf，无第三份手工菜单 | 走查 dyn_menu.lua 的 `#menu:` 解析、show_button_menu / launch_host_menu | 同一数据源，host 不另造菜单 |
| F2 | 静态条目还原：206 条 `#menu:` 在 host 中一一出现（标题/顺序/分隔线/层级） | 静态走查 input.conf 与 JSON 生成 | 数量、顺序、分隔线一致 |
| F3 | 快捷键还原：行尾文本与 input.conf 原文一致 | 走查 BuildItems + SetAcceleratorText | TextOverride 显示原文，无真实加速键 |
| F4 | 图标差异声明：官方 menu.dll 不渲染图标；host 图标为增量 | 对比 menu.dll 能力与 IconMap.cs（157+47） | 差异有书面记录 |
| F5 | 状态还原：disabled/checked/hidden 正确映射 | 走查 BuildItems 的 IsEnabled/IsChecked/跳过逻辑 | 三态与 mpv 状态一致 |
| F6 | 动态菜单还原：播放列表/轨道/章节/版本/配置/着色器/最近播放/音频设备结构与官方一致 | 走查 Lua 动态源与 JSON 输出 | 层级、标题、截断（max_title_length）、空列表占位一致 |
| F7 | 命令还原：cmd 字符串（引号感知）/数组两种；`#menu:` 注释与状态表达式剥离 | 走查 MenuData.CmdToList | 与官方执行命令一致 |
| F8 | 交互还原：级联方向/重叠/展开动画与 Win11 原生一致；点击叶子→IPC→mpv 执行 | MenuFlyout 平台行为 + 代码走查 | 平台内置，无自绘交互 |
| F9 | 官方控件：仅 WinUI 官方 MenuFlyout 家族，无自绘/自定义模板/低层钩子 | `rg` 全源码 | 无自定义弹出实现 |
| F10 | 回退一致性：host_menu=no 回退官方后功能等价（同源数据） | 走查 use_mpv_impl 分支 + 历史回退测试 | 等价，无功能缺口 |

## C. 与 mpv 的兼容度

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| C1 | 版本矩阵：当前 mpv 版本、最低支持版本声明 | 查 mpv 版本与 README | 有文档化矩阵（含 menu.dll ≥0.37） |
| C2 | Lua API：mp.options、utils.subprocess{detach}、add_key_binding、register_script_message、observe_property | 走查 dyn_menu.lua | 全部为长期稳定 API |
| C3 | IPC：管道 `name` 与 `\\.\pipe\name`、request_id、超时重试、响应解析 | 走查 MpvIpc.cs | 两种命名可用；10×1500ms 重试；超时退出 |
| C4 | 命令透传：特殊命令族（script-message-to/vf/ab-loop/截屏参数）不解析不拦截 | 走查 CmdToList | 原样数组发送，由 mpv 执行 |
| C5 | JSON 数据兼容：字段（title/type/shortcut/cmd/submenu/state）与 Lua 输出一致；深度上限 | 走查 MenuData.Load | 字段齐全；深度限制有文档或递归化 |
| C6 | 状态表达式：`#@state=` 由 Lua 求值后写 state 数组，host 只消费结果 | 走查 Lua 编译/求值 + host 映射 | 与官方一致 |
| C7 | 快捷键归属：mpv 自身处理键位；host 不注册真实加速键（避免 #708 卡死/双触发） | 走查 SetAcceleratorText | 无 KeyboardAccelerator 实例 |
| C8 | 回退容错：host 缺失/损坏/被杀 → 自动回退官方 | 走查 launch_host_menu + host_available | 警告 + 回退，无静默丢失 |
| C9 | 生命周期：点击/点外/Esc 退出；60s 看门狗；mpv 退出后宿主不残留 | 走查 Close/看门狗 | 无孤儿进程（最迟 60s） |
| C10 | 多实例：临时 JSON 按 pid 隔离，双 mpv 同开不串台 | 走查 host_temp_dir/命名 | `mpv-menu-host-items-<pid>-*` |

## L. 多语言本地化支持

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| L1 | 现状盘点：菜单内容语言、Lua 硬编码文案、host 硬编码文案 | `rg` 中文 + 走查 | 每处有归属记录 |
| L2 | 架构定位：语言由数据源决定，host 不翻译（与 mpv 官方一致） | 走查数据链路 | 明确设计结论 |
| L3 | 编码链路：UTF-8 无 BOM Lua→JSON→C#→TextBlock 全链路正确 | 编码检查 | 中文/多语言标题不乱码 |
| L4 | 系统语言自适应：host 无自身 UI 文案；未来新增文案需 i18n | 走查 MainWindow | 无用户可见硬编码文本 |
| L5 | 英文/双语模板与 locale 开关 | 查 repo 模板 | 有则验证；无则记录为已知限制 |
| L6 | 本地化配置项（语言切换） | 查 dyn_menu.conf/MenuOptions | 有则文档化；无则记录 |

## U. 用户自定义的配置文件

| 编号 | 检查点 | 证据 / 执行方式 | 通过标准 |
|---|---|---|---|
| U1 | 配置面盘点：dyn_menu.conf 全部选项均生效（mp.options 读取） | 对照 Lua 默认值与 conf | 9 项选项全部接入 |
| U2 | 优先级：Lua 默认 < dyn_menu.conf < host CLI 参数 | 走查读取顺序 | 有明确且文档化的优先级 |
| U3 | CLI 参数文档：MenuOptions 全部参数与 README 对应 | 对照 Parse 与 README 命令行节 | 无文档外参数、无参数外文档 |
| U4 | 外观配置：主题/字体/图标/动画/圆角是否可自定义 | 走查 v4.1 控件属性 | 有决策记录（暴露或明确平台默认） |
| U5 | 图标自定义：IconMap 是否支持用户映射文件 | 走查 IconMap.cs | 有则文档化；无则记录 P2 |
| U6 | 容错：未知键/坏值/路径缺失 | 走查 mp.options 与回退 | 回退默认，不崩溃 |
| U7 | 文档一致性：README 与 v4.1 现状一致 | 全文对照 | 无 v3.x 自绘描述残留 |
| U8 | 主题：生产跟随 Windows；--theme 仅测试 | 走查 MainWindow/README | 一致 |
| U9 | 菜单内容自定义：input.conf 为官方自定义入口 | 走查数据源 | host 不额外生成菜单 |
| U10 | 日志配置：host_log 可配置且文档化 | 走查 dyn_menu.conf/README | 有文档 |
