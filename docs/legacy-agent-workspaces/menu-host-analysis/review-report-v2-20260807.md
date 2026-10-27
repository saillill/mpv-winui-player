# MpvMenuHost 审查报告 v2（2026-08-07，目标重设）

> 按 review-checklist-v2-20260807.md 执行，静态审查（无动态测试）。
> 审查基线：v4.1 MenuFlyout 单进程架构；宿主源码
> `work\mpv-menu-host`（MainWindow ~330 行 + IconMap 157+47）；
> Lua/配置 `mpv-config`（mpv 0.41.0-615，mpv-lazy 构建）。

## 结论汇总

| 维度 | 结论 | 主要证据 | P0/P1/P2 遗留 |
|---|---|---|---|
| F 官方组件还原度 | ✅ 高 | 数据同源；官方控件 100%；206 条静态条目全命中；状态/命令/交互平台内置 | 无 P0；F4 差异仅需保持 README 声明 |
| C mpv 兼容度 | ⚠️ 主线良好 | IPC 双命名/重试、回退、生命周期、pid 隔离均有实现 | P1：深度上限 3 层、版本矩阵 |
| L 多语言本地化 | ⚠️ 架构符合官方 | 语言跟随数据源；host 无硬编码文案 | P2：双语模板/locale（按定位决定） |
| U 用户自定义配置 | ⚠️ 面小且文档滞后 | 9 项 Lua 选项 + 11 项 CLI 均接入 | P1：README 修正、外观/图标可配置决策 |

**P0：无**（无功能性阻塞项）。

## F. 官方组件还原度

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| F1 | ✅ | input.conf → dyn_menu.lua（`#menu:` 解析 + 动态源）→ JSON → host；官方回退走 menu-data/menu.dll，同一数据源 | — |
| F2 | ✅ | 旧审查静态走查：206 条 `#menu:` 全部有命令；host 按 JSON items 顺序渲染（含 separator） | 全量逐项实机核对为 P1（测试暂停中） |
| F3 | ✅ | BuildItems + SetAcceleratorText：`KeyboardAcceleratorTextOverride=原文`；不创建真实 KeyboardAccelerator（规避 #708 卡死） | 已部署 DLL `A8C4B25C` |
| F4 | ✅ | menu.dll 官方不渲染图标；host 用系统 Segoe 157 精确 + 47 前缀，全部静态标题命中；README 已声明“系统预设字体” | 动态项（播放列表/轨道名）无图标，P2 对齐方案 |
| F5 | ✅ | disabled→IsEnabled=false；checked→ToggleMenuFlyoutItem.IsChecked；hidden→跳过渲染 | — |
| F6 | ✅ | Lua 动态源（track/chapter/edition/profile/shader/recent/playlist/audio-device）写 submenu JSON；`max_title_length=50` 截断；空列表占位由 Lua 生成 | 动态源逐场景实机核对为 P1 |
| F7 | ✅ | MenuData.CmdToList：字符串按空格+引号 tokenize、数组直通、剥离 `#menu:` 注释 | — |
| F8 | ✅ | 级联方向/重叠/动画由 MenuFlyout 平台实现（Win11 原生观感）；叶子→IPC→mpv→退出链路代码完整 | 观感实机确认中（用户持续反馈） |
| F9 | ✅ | 全源码仅 MenuFlyout/MenuFlyoutItem/ToggleMenuFlyoutItem/MenuFlyoutSubItem/MenuFlyoutSeparator；无自绘/自定义模板/低层钩子 | — |
| F10 | ✅ | use_mpv_impl 分支 + launch_host_menu 的 host_available 回退；历史回退测试 PASS | 双路径 100% 等价逐项对比 P1 |

## C. 与 mpv 的兼容度

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| C1 | ⚠️ | 本机 mpv 0.41.0-615 全链路（IPC/回退/状态）实测可用；无最低版本矩阵文档 | P1：README 增补矩阵（menu.dll ≥0.37、Lua API、IPC） |
| C2 | ✅ | dyn_menu.lua 仅用 mp.options、utils.subprocess{detach}、add_key_binding、register_script_message、observe_property、format_json 等长期稳定 API | — |
| C3 | ✅ | MpvIpc：`name` 与 `\\.\pipe\name` 均支持；10×1500ms 重试；超时 Close(3)；request_id 递增 | 超时退出码有日志可查 |
| C4 | ✅ | CmdToList 不做语义解析，原样数组发送；script-message-to/vf/ab-loop 等由 mpv 执行 | — |
| C5 | ⚠️ | 字段与 Lua 输出一致；**深度上限 3 层**（root→1→2），gchild.Submenu 被静默丢弃；当前菜单树最大恰好 3 层（查看>缩放>叶子） | P1：递归化解析或文档化上限，避免用户加层级后静默缺项 |
| C6 | ✅ | `#@state=` 由 Lua 编译/求值写 state 数组；host 只消费 disabled/checked/hidden | — |
| C7 | ✅ | SetAcceleratorText 只设文本；`rg KeyboardAccelerator` 仅注释 | — |
| C8 | ✅ | host 缺失 → msg.warn + 回退官方；每次调用探测路径 | — |
| C9 | ✅ | 瞬态 60s 看门狗 Environment.Exit；点击/点外/Esc 均退出；无父进程句柄（detach 冷启动） | mpv 退出后宿主最迟 60s 自清，无孤儿 |
| C10 | ✅ | 临时 JSON：`mpv-menu-host-items-<pid>-<time>-<seq>.json`，pid 隔离；宿主加载后删除自身文件 | — |

## L. 多语言本地化支持

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| L1 | ✅ | 菜单内容全中文（206 条 input.conf）；dyn_menu.lua 硬编码中文 343 字符（空列表占位/轨道类型/“关闭/自动”等）；host 无用户可见硬编码文本（SampleMenu 仅测试用） | — |
| L2 | ✅ | 语言由数据源（input.conf/菜单 JSON）决定，host 不翻译——与 mpv 官方菜单行为一致 | 设计结论写入 README（P2） |
| L3 | ✅ | UTF-8 无 BOM 全链路（Lua→JSON→JsonDocument→TextBlock）；中文实测不乱码；多语言标题渲染依赖系统字体 fallback | — |
| L4 | ✅ | v4.1 host 无自身 UI 文案（无标题、无提示）；未来新增空菜单/错误提示需 i18n 或保持“无文案”设计 | — |
| L5 | ❌（按产品定位） | 无 `*-en` 模板、无 locale 开关；mpv-lazy 定位中文用户 | P2：若需英文支持，提供 input-en.conf 模板或 host_lang |
| L6 | ❌（按产品定位） | 无语言配置项；多语言只能通过更换菜单数据源实现 | 与 L5 同决策 |

## U. 用户自定义的配置文件

| 编号 | 结论 | 证据 | 遗留/建议 |
|---|---|---|---|
| U1 | ✅ | dyn_menu.conf 9 项全部由 mp.options 读取（use_mpv_impl/uosc_syntax/escape_title/max_title_length/max_playlist_items/host_menu/host_menu_path/host_test_select/host_log） | — |
| U2 | ✅ | 优先级：Lua 默认值 < dyn_menu.conf < host CLI 参数；README 命令行节有文档 | — |
| U3 | ⚠️ | MenuOptions 支持 11 项；README 命令行节缺 `--anchor/--side/--depth` | P1：README 补齐 |
| U4 | ⚠️ | v4.1 无外观参数：主题/字体/图标/动画/圆角全部平台默认+跟随系统 | P1：决策“明确不支持自定义（平台默认）”或暴露配置 |
| U5 | ❌ | IconMap.cs 硬编码 157+47；无用户图标映射文件 | P2：JSON 映射（标题→码点/字体）可扩展 |
| U6 | ✅ | mp.options 忽略未知键、坏值回退默认；host_menu_path 不存在 → 回退官方 | — |
| U7 | ❌ | README“行为（M1/M2 现状）”“视觉回归修复”等章节仍描述 v3.x 自绘行为（亚克力/300px 最小宽/两段式布局/NOACTIVATE+TOOLWINDOW/低层钩子），与 v4.1 现状不符 | P1：按 v4.1 重写行为与视觉章节 |
| U8 | ✅ | 生产跟随 Windows 深浅色（RequestedTheme 未强制）；`--theme` 仅测试/预览 | — |
| U9 | ✅ | 菜单内容自定义入口 = input.conf（官方机制）；host 不额外生成 | — |
| U10 | ⚠️ | host_log 可配置且已生效（临时诊断开启）；README 未记录该选项 | P2：README 增补 host_log 说明与“诊断用，正式留空” |

## 下一步（按优先级）

P1：
1. README 按 v4.1 重写“行为/视觉”章节，删除 v3.x 自绘描述；补齐 `--anchor/--side/--depth` 与 `host_log`。
2. MenuData.Load 递归化或文档化 3 层上限（防用户加深层级后静默缺项）。
3. 增补 mpv 版本矩阵（最低版本、menu.dll 依赖、Lua API 依赖）。
4. 外观/图标可配置性决策（明确“平台默认+跟随系统”或暴露配置）。

P2：
5. 双语模板/语言开关（按产品定位）。
6. 图标用户映射文件。
7. 全量静态条目与动态源逐场景实机核对（测试解禁后）。

> 旧 review-checklist.md / review-report.md 为 b2 自绘架构的历史审查记录，保留存档；
> v4.1 架构审查以本 v2 清单与报告为准。

## 执行记录（2026-08-07，v4.2 已实施）

| 报告项 | 状态 | 实施内容 |
|---|---|---|
| C5/P1 菜单深度 | ✅ 已实施 | MenuData.Load 改为递归 ParseItem，任意深度子菜单完整加载，不再限 3 层 |
| U5/P2 图标用户映射 | ✅ 已实施 | IconMap 新增可选 `icon-map.json`（exe 同目录）：exact/prefix 两级覆盖，用户优先，损坏静默忽略 |
| F4/J4/P2 动态项图标 | ✅ 已实施 | BuildItems 增加图标继承：无图标的叶子项（播放列表/轨道/配置/着色器等）继承所属子菜单图标，修复合集对齐 |
| F4/J4 图标覆盖核验 | ✅ 已实施 | 静态复刻图标查找+继承逻辑跑真实菜单样本：224/224 叶子全覆盖（补“脚本/滤镜”等映射后，159 精确 + 52 前缀） |
| F2 还原度（参照 ikas-mc/mpv-winui-player） | ✅ 已实施 | `&&`→`&` 标题还原（dyn_menu escape_title 转义）、空子菜单跳过、连续分隔线去重；数据模型与该仓库 mpv menu-data 完全一致（验证 F1/C5） |
| U3/U7/P1 README 修正 | ✅ 已实施 | “行为”章节按 v4.1 重写；CLI 补齐 `--anchor/--side/--depth` 与 host_log 说明；v3.x 视觉修复章节标注为历史 |
| C1/P1 版本矩阵 | ✅ 已实施 | README 新增 mpv/menu.dll/Lua/Windows 版本矩阵 |
| L2/L5 本地化决策 | ✅ 已实施（文档） | README “本地化”章节：语言跟随数据源、宿主不翻译、英文模板列为 P2 |
| L5/L6 英文默认+配置覆盖 | ✅ 已实施 | dyn_menu.lua 13 处硬编码中文改为 str_* 选项（mpv 上游英文原文）；dyn_menu.conf 默认附带中文覆盖组，注释即恢复英文；宿主示例菜单改英文 |
| U4/P1 外观决策 | ✅ 已实施（文档） | README “外观与可配置性决策”：平台默认+跟随系统，不暴露外观参数 |
| 部署 | ✅ | Release 发布，repo/安装目录双侧哈希一致（DLL `010F4646`），无残留进程 |
| 动态/实机核对 | ⏸ 待用户实机 | 按用户约束不做动态测试；manual-verification-checklist.md 已更新至 v4.2（18 项），解禁后按清单验收 |
