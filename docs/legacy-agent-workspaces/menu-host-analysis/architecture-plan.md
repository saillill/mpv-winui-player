# 单进程 WinUI 3 原生菜单 + mpv 组件（架构方案 A）

> 状态：待确认。用户指示：优先尽量使用 mpv 组件/SDK 实现原生 WinUI 3；不动美术效果；不做动态测试；大规模重构前先汇报；依赖最小化仅作后备；不采用额外进程/小内存思路。

## 目标

把 b2 从“每级菜单一个 MpvMenuHost.exe 进程 + 文件预热线（trigger/heartbeat/state/RESET）”重构为：

- **单个 WinUI 3 宿主进程**：根菜单与全部级联子菜单在同一进程内渲染；
- **mpv 组件驱动**：菜单定义仍来自 mpv 本体（input.conf `#menu:` + Lua），命令执行仍走 mpv JSON IPC；不再按级写临时 JSON、不再逐级 spawn 进程；
- **美术效果逐像素不变**：圆角/亚克力/深浅色/图标/动画/重叠/行距/字号/宽度/滚动全部沿用现有 XAML 与代码。

## 保留（美术与交互，不动）

- 菜单外观：8px 圆角、Thin 亚克力、深浅色热切换、系统预设字体图标（157+47）、勾选/级联字形、行距/字号/悬停高亮、淡入+缩放动画、子菜单重叠 6px、300px 最小宽、30% 屏宽上限、高度钳制滚动、无顶部标题。
- 交互：右键入口、播放列表/音轨按钮入口、点击叶子发 mpv IPC、外部点击/Esc 关闭、键盘 ↑↓/Enter 导航。

## 删除（进程链与文件协议）

- 每级独立宿主进程：`--depth/--parent-pid/--side/--pos` 级联 spawn。
- 预热链：`--prewarm/--prewarm-id`、心跳、trigger、state、RESET 全套。
- 宿主侧 `WatchPeerExit / WatchChildPrewarmAsync / KillChildPrewarm / KillChildIfAlive / KeepOnTopLoop` 的跨进程部分。
- Lua 侧 `host_trigger_prewarm / host_prewarm_alive / taskkill 旧 pid` 清理逻辑。
- 每级 `mpv-menu-host-items-*` 临时 JSON（改为根菜单一次性数据）。

## 新增（单进程内实现）

- 子菜单改为进程内 Popup/子窗口（同一套 `BuildMenu/MakeItem/IconFor/动画/重叠定位` 复用），`OpenSubmenu` 不再 spawn 进程，改为管理子菜单 Popup 生命周期。
- 置顶问题简化：宿主单窗口保持 TOPMOST，子菜单归宿主进程所有，消除跨进程 z-order 竞争（KeepOnTopLoop 闪烁根因随之消失）。
- 数据通道简化：mpv Lua 一次性传入完整菜单树（现有 JSON 本就含嵌套 submenu，可直接用；去掉按级文件与 trigger）。

## mpv 组件/SDK 利用点

| 能力 | 用途 | 现状 |
|---|---|---|
| input.conf `#menu:` 菜单树 | 菜单定义源 | 已用，保留 |
| mpv JSON IPC（input-ipc-server） | 命令执行/数据通道 | 已用，保留 |
| mpv Lua（dyn_menu.lua/dynamic_menu.lua） | 动态菜单与状态求值 | 保留，仅删预热相关段 |
| menu-data 探测 | 可选 b1a 能力感知 | 保留探测，不影响主路径 |

## 风险与待验证（禁止动态测试，先静态设计）

1. WinUI 3 桌面 Popup 的层级/圆角/亚克力/可超出宿主窗口边界行为需在实施后由用户实机确认（解除测试禁令后补）。
2. Windows App SDK 2.3.1 的 ChildWindow API 是否可用/适合，作为 Popup 的备选；不确定则先用 Popup。
3. 单进程常驻内存仍约 125MB/进程（1 个而非 3 个）；“小内存思路”按用户指示不作为方向。
4. 重构集中在 `MainWindow.xaml.cs`（删进程链约 600–800 行，新增子菜单 Popup 管理）与 `dyn_menu.lua`（删预热约 100 行），属于中等偏大规模重构，按用户要求先汇报确认。

## 分阶段实施（每步汇报，不动美术、不跑动态测试）

| 阶段 | 内容 | 门禁 |
|---|---|---|
| 0 | 本方案确认 + DEPENDENCIES.md 初稿落库 | 用户确认单宿主进程边界 |
| 1 | Lua 侧去 prewarm/trigger/state，单次拉起宿主 | 静态走查 |
| 2 | 宿主 OpenSubmenu 改进程内 Popup，删除进程链代码 | 编译通过 |
| 3 | 关闭/键盘导航/外部点击/置顶适配单进程 | 编译通过 + 代码走查 |
| 4 | 文档更新（README/DEPENDENCIES/决策记录） | 与代码一致 |

## 待确认

1. “不要额外进程”是否指：保留单个 MpvMenuHost.exe 宿主进程，只去掉每级额外进程？（推荐，本方案按此设计）
2. 若要求零额外进程（菜单完全在 mpv 进程内渲染 WinUI），则必须走 libmpv 嵌入 WinUI 3 应用（整机重构 mpv-lazy UI）或放弃 WinUI 外观走系统原生菜单——两者均超出本方案范围，需单独立项。

## 实施进度（2026-08-07）

- 阶段 1（Lua 去预热）✅：dyn_menu.lua 删除 host_prewarm_*/host_trigger_prewarm/spawn_prewarm/taskkill，800 行，repo/安装目录一致。
- 阶段 2（宿主单进程化）✅：OpenSubmenu 改进程内窗口；删除进程链/预热/心跳/状态全套代码；MainWindow.xaml.cs 1900→1310 行；编译 0 警告 0 错误。
- 阶段 3（交互适配+部署）✅：根窗口独占钩子、键盘/外部点击沿链路由、最末级置顶、子窗口 Window.Close；R2R 发布部署（DLL `2BE157F9B993`）；%TEMP% 残留清零。
- 阶段 4（文档/测试）🔄：README/DEPENDENCIES/决策记录已更新；旧测试脚本待同步。

## 2026-08-07 渲染层替换（v4.1）

- 用户确认后，自绘窗口栈整体替换为 WinUI 官方 `MenuFlyout`（图标/级联/动画/
  圆角/亚克力/键盘/Esc/点外关闭全平台内置）；宿主窗口 1×1 透明置顶仅作放置目标。
- 删除：自绘行/悬停/级联窗口/定位/重叠/动画/低层钩子/置顶循环（MainWindow
  1310 → 约 330 行；图标映射独立到 IconMap.cs）。
- z-order 类闪烁问题随多窗口架构一并消失。已编译部署（DLL `E0CE97BDE999`）。
- 遗留：测试脚本断言（窗口尺寸/UIA 节点/子进程数）需按 Flyout 形态改写，待后续。
