# mpv-winui-player 分功能区块完整性与合理性审计（2026-08-16）

## 审计对象与方法

- 对象：本地 reference 仓库当前 HEAD `7961511`（含上轮性能优化），与 `github/main` 一致。
- 范围：15 个功能区块；口径为“功能完整性 + 合理性”均衡（入口 → handler → 行为可达 → 边界/状态/生命周期是否合理）。
- 方法：README/配置/代码交叉核对、`check-settings-drift.py` 与 `check-localization.py`、TODO/FIXME 扫描、针对性运行时冒烟（IPC + UIA）。
- 交付：本报告 + 分级修复方案；本轮不改业务代码。

## 运行时冒烟证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 播放进度 | POS1=0.0018s → POS2=1.5201s（间隔 1.5s） | 窗口出现、`mpvpipe` 可连，`time-pos` 正常前进 |
| 播放列表 | `playlist-count` 2 → 4（两次 `loadfile append`） | 追加/属性读取路径正常（初始 2 为 `autocreate-playlist=same` 自动加入同目录 WAV） |
| 设置 ApplyAll | 注册表 `VolumeMax=130` → mpv `volume-max=130` | 设置缓存 → `BuildApplyAllCommands` → 批量下发整链生效 |
| 设置失败诊断 | 注册表 `VolumeMax=55` 被 mpv 拒绝 | 日志出现 `The volume-max option is out of range` + `ApplyCommandStrings failed`，错误可见可查（55 低于 mpv.conf 的 volume=100，设置页 UI 下限也是 100） |
| PiP | UIA 找到窗口 “Picture in picture” | 置 `WindowPiP=true` 启动后自动进入 PiP，窗口标题/进程匹配 |

冒烟期间的注册表改动（`VolumeMax`、`WindowPiP`）均已还原；临时 WAV 在 `%TEMP%\mpv-winui-smoke`，不属于仓库。

## Top 问题总表

| # | 优先级 | 区块 | 问题 | 最小修复路径 |
|---|---|---|---|---|
| 1 | P1 | 启动/文档 | README(zh/en) 宣称“睡眠定时器”，代码已移除 | 更新两份 README 删除该承诺（`README.md` / `README_zh-CN.md`） |
| 2 | P1 | 打包/激活 | unpackaged（便携/MSI）未注册 `mpv-winui://` 协议，README 宣称支持 | 在 `installer/product.wxs` 增加 HKCU `Software\Classes\mpv-winui\shell\open\command` 注册；或从 README 移除宣称 |
| 3 | P1 | 文件打开 | DVD/BD 入口是“选文件夹”占位（`OpenDvdAsync`/`OpenBdAsync` 均有 TODO） | 实现 `loadfile dvd://`/`bd://` 设备选择，或从菜单移除并改 README |
| 4 | P1 | 播放控制 | `speed` 变化未回传 UI：原生 `SpeedChanged` 已观察，C# 未订阅，倍速按钮无勾选/无当前值 | `MpvMediaPlayer` 订阅并转发 `SpeedChanged`，`PlayerControl` 更新倍速按钮文案/勾选 |
| 5 | P1 | 本地化 | de-DE 94%、es-ES 95%、fr-FR 94%；部分可译键值仍为英文（其余为技术名词，同词属正常） | 用 `check-localization.py` 的 residue 清单补齐三个语言文件 |
| 6 | P2 | 设置/工具 | `check-settings-drift.py` 对 UI-only 项误报：`PlaylistWidth`、`WindowTitle` 报 ERROR | 将两者加入 `UNMAPPED_OK` 白名单 |
| 7 | P2 | 历史 | `recent.json`（recentmenu/uosc 格式）与 App 的 WatchHistory 解析（`path/time/title`）不是同一数据源；“最近播放”无固定 UI 入口 | 实机播放后核对 mpv `watch-history-path` 文件字段；明确 recentmenu 入口或停用 |
| 8 | P2 | 日志 | `display-info.log` 追加写无轮转；`OnException` 只记日志不提示用户 | display-info 按天/大小轮转；对非静默异常加轻量提示 |
| 9 | P2 | 脚本层 | `dyn_menu.lua`（GPL-2.0）与 project-written `dynamic_menu.lua` 同时被 `load-scripts` 加载，职责边界未文档化 | 核对二者挂载的 menu-data/脚本消息是否重叠，补充 AGENTS 说明 |
| 10 | P2 | 播放列表 | 过滤时 `FilteredPlaylistItems` 仍整表 Clear+Add；m3u 导出标题不转义逗号 | 过滤改批处理/增量；导出时对标题做安全编码 |
| 11 | P2 | 菜单/注释 | `MainWindow.RebuildPlayerMenuBar` 注释仍提“menu editor”，编辑器已移除；多处 TODO 注释过期 | 清理过期注释与占位 TODO |

## 分区块结论

### 1. 启动 / 激活 / 生命周期

- 完整性：完整。单实例重定向（`Program.FindOrRegisterForKey("main")`）、Launch/File/Protocol 三类激活解析、首次配置部署（`ConfigDeployer`）、窗口位置恢复、退出清理（设置 flush、命令队列 drain、键盘钩子/子类卸载、PiP teardown）均有实现。
- 合理性：启动等待链（`AppContext.Init → WaitAll → mpv init → ApplyAll→Drain→Open`）顺序正确；`Unloaded` 与 `Window.Closed` 双路径清理无冲突。
- 待改进（P2）：`MpvPlayerPage_Loaded` 的 `_isPlayerInitialized == false` 分支为空 TODO，无降级提示；`Program.cs` 的 `//TODO config` 是占位注释。
- 证据：`Program.cs`、`AppContext.cs`、`MainWindow_Size.cs`、`ActivationService.cs`、`ConfigDeployer.cs`。

### 2. 文件打开 / 播放源

- 完整性：文件/文件夹/URL/剪贴板、拖放、命令行多参数、`mpv-winui://?file=`、外挂字幕/音轨、批量 append/insert-next 均可达。
- 合理性：URL 与本地路径判定清晰；批量打开顺序按参数序。
- 待改进（P1）：`open-dvd`/`open-bd` 目前与“打开文件夹”行为重复（源码 TODO），与 README“DVD、蓝光都行”不符。P2：协议参数仅支持 `?file=` 一种形式，无 playlist/URL 形态。
- 证据：`MpvPlayerPage_Open.cs`（OpenDvd/OpenBd TODO）、`MpvMediaPlayer_Open.cs`、`ActivationService.cs`、`menus.json`。

### 3. 播放控制 / 状态栏

- 完整性：播放/暂停/停止、上下首、跳过、音量/静音、倍速、AB 循环、章节、轨道切换、缩放/比例、全窗口/全屏/PiP、overflow 菜单齐全。
- 合理性：pause/repeat/shuffle/ontop/fullscreen/volume 均经 mpv 属性事件回同步。
- 待改进（P1）：`speed` 状态未回传——原生已 `mpv_observe_property(Speed)`，但 `MpvMediaPlayer` 未订阅 `SpeedChanged`，倍速按钮/菜单无当前值勾选，外部改速（input.conf、右键菜单）后 UI 不更新。P2：`VolumeFlyoutControl` 是一次性快照（TODO use state），外部音量变化不刷新；EQ 值只在面板内维护。
- 证据：`MpvMediaPlayer.StartListen` 订阅清单（无 SpeedChanged）、`PlayerControl.xaml.cs`、`VolumeFlyoutControl.xaml.cs`。

### 4. 进度条 / 预览 / 时间显示

- 完整性：拖动/点按 seek、时间文本、A/B 标记、章节标记、thumbfast 预览开关均可用。
- 合理性：上轮已改为原生 `time-pos` 观察 + 100ms 合并；章节缓存、预览单飞、拖动缓冲抑制均落地；时长未知流不产生误操作。
- 待改进（P2）：预览解码队列仍可在极快拖动时丢弃中间帧（当前靠 generation 防串图，可接受）；无 P0/P1。
- 证据：`PlayerControl.xaml.cs`、`MpvPlayerPage_Preview.cs`、`mpv-winrt/MpvPlayer.cpp`。

### 5. 播放列表

- 完整性：显示/过滤/高亮、点击播放、拖拽排序、上下文菜单、m3u 导入导出、刷新齐全。
- 合理性：上轮改为事件合并 + id diff，避免整表 Clear/Add；current 行只在变化时滚动。
- 待改进（P2）：过滤仍整表重建 `FilteredPlaylistItems`；m3u 导出 `#EXTINF:-1,{title}` 不转义标题逗号。运行时冒烟确认 `playlist-count` 随 append 正确增长。
- 证据：`MpvPlayerPage_Playlist.cs`、冒烟结果。

### 6. 菜单栏 / 右键菜单

- 完整性：`menus.json` 驱动、8 语言 label、全部 action id 有 handler、右键 menu-data 动态菜单（checked/disabled/hidden 均应用）、`custom_menu.json` 自定义项、快捷键搜索齐全。
- 合理性：`MenuBarBuilder` 对未知 action/空子菜单/悬挂分隔线有降级；`check-localization.py` 校验 labelKey/action 全部通过。
- 待改进（P1）：de/es/fr 语言缺失键最多（94-95%），含 `MenuAudio`/`MenuVideo`/`MoreZoom` 顶级菜单标签，回退英文。P2：右键动态菜单状态是打开时的快照，长播后可能滞后。
- 证据：`check-localization.py` 输出、`MenuBarBuilder.cs`、`MpvPlayerPage_MpvMenu.cs`。

### 7. 设置

- 完整性：10 分类选项树、搜索/历史、导入/导出、分类/全部重置、Profile 管理、音频/GPU 设备枚举、script-opts 与 managed mpv.conf 写回、文件关联均实现。
- 合理性：上轮新增 `ConfigOnlyKeys`/`StartupDeferredKeys`；`VolumeMax` UI 下限 100 与 mpv 约束一致；冒烟确认设置经缓存→批量下发→mpv 生效，且非法值（55）会被 mpv 拒绝并写入日志。
- 待改进（P2）：`check-settings-drift.py` 对 `PlaylistWidth`、`WindowTitle` 误报 ERROR（UI-only，应入 `UNMAPPED_OK`）；导入配置后无“部分设置需重启生效”提示。
- 证据：`check-settings-drift.py` 输出、`SettingsPage.Actions.cs`、冒烟结果。

### 8. HDR / 显示

- 完整性：颜色信息监听、`color-kind` 写入、`profiles.conf` 三档切换、刷新率启动期应用 + `user-data/mpvw/refresh-rate` 维护、display-info 日志齐全。
- 合理性：上轮修正 `override-display-fps` 为启动期选项并实现用户覆盖优先；轮询降为 15s 且最小化跳过；多显示器事件覆盖。
- 待改进（P2）：`display-info.log` 无轮转；`//TODO use player view rect`（DisplayInformation 绑定窗口而非画面矩形，常规场景等价）。
- 证据：`MpvPlayerPage_Display.cs`、`profiles.conf`、上轮实现记录。

### 9. 输入 / 快捷键

- 完整性：键盘钩子转 mpv keydown/keyup、鼠标 wheel/双击/侧键转发、F10 抑制、IME、slider 焦点互斥、窗口失活 keyup 均实现。
- 合理性：钩子只挂当前 UI 线程（非全局钩子），风险可控；`MP_INPUT_RELEASE_ALL` 策略保守；`input.conf` 与转发名称一致。
- 待改进（P2）：快捷键搜索按“首个空格”切分 `input.conf` 行，复杂绑定（含 tab/尾随注释）可能误解析；影响小。
- 证据：`MpvPlayerPage_Input.cs`、`MpvPlayerPage_Mouse.cs`、`input.conf`。

### 10. PiP

- 完整性：进出 PiP、拖动/缩放/圆角/置顶、位置尺寸持久化、主窗口隐藏/恢复、右上 × 退出、Alt+F4 恢复、PiP 控制条均实现。
- 合理性：swapchain 重挂、尺寸时序、全屏 presenter 循环等关键竞态已有专门防护；冒烟确认 `WindowPiP=true` 启动即出现 “Picture in picture” 窗口。
- 待改进（P2）：`WindowPiPRect` 用逗号 4 元组持久化，解析失败静默回退（可接受）。
- 证据：`PiPWindow.xaml.cs`、`MpvPlayerPage_PiP.cs`、冒烟结果。

### 11. 历史 / 稍后观看

- 完整性：WatchHistory/WatchLater 对话框、解析、点击播放、清空、目录路径均有实现；WatchLater 依赖 `write-filename-in-watch-later-config=yes`（mpv.conf 已设）。
- 合理性：解析器对空/损坏文件有容错；列表按时间倒序。
- 待改进（P2）：WatchHistory 依赖 mpv 生成的 watch-history 文件，当前机器尚未生成（`recent.json` 是 recentmenu 的 uosc 数据，App 未消费）；“最近播放”无固定菜单入口，recentmenu 是否经 dyn_menu 动态暴露需实机验证。
- 证据：`WatchHistoryParser.cs`、`WatchLaterParser.cs`、`recentmenu.lua`、配置目录文件检查。

### 12. 本地化

- 完整性：8 语言文件、菜单/设置/控制条/右键文案、语言切换即时生效、用户目录覆盖全部实现；`check-localization.py` 通过（缺键允许回退英文）。
- 合理性：`AppLang.LoadFromJson` 缺失键保留默认值，回退稳定。
- 待改进（P1）：de/es/fr 完整率 94-95%，部分可译键值仍为英文（顶级菜单词如 Audio/Video/Zoom 在目标语言中同词，属正常）；已按 residue 清单补齐可译项。
- 证据：`check-localization.py` 输出、`AppLang.cs`。

### 13. 日志 / 诊断

- 完整性：NLog 级别切换、mpv warn/info 转发（上轮默认 warn）、display-info.log、env_check、MediaInfo/统计入口齐全。
- 合理性：mpv 日志转发在正常播放保持 warn，降低开销；NLog 15 天归档。
- 待改进（P2）：`display-info.log` 追加无轮转；`OnException` 仅记日志，非静默异常无用户提示。
- 证据：`LoggerHelper.cs`、`MpvMediaPlayer.cs`、`env_check.lua`、`stats_mediainfo.lua`。

### 14. 打包 / 更新 / 部署

- 完整性：build/package/installer 脚本、ConfigDeployer 首次部署、UpdateChecker、便携版与 MSI 产物、第三方许可清单齐全；AOT/裁剪已启用。
- 合理性：升级不覆盖用户配置；更新检查静默失败且只引导打开 release 页，与 README“无自动更新”一致。
- 待改进（P1）：MSI（unpackaged）未注册 `mpv-winui://` 协议，README 宣称支持；文件关联仅在设置页手动注册（设计如此，合理）。
- 证据：`installer/product.wxs`、`Package.appxmanifest`、`ActivationService.cs`、`package.ps1`。

### 15. 第三方脚本 / 滤镜层

- 完整性：预置脚本、着色器、VapourSynth 模板、script-opts 映射、许可清单（含 GPL 脚本与模型权重说明）齐全。
- 合理性：thumbfast/hdr_auto/vsr_auto 与 App 设置联动已接线；上轮修复 hdr_auto 启动竞态；未启用滤镜保持注释状态，符合“按需开启”。
- 待改进（P2）：`dyn_menu.lua`（GPL-2.0）与 project-written `dynamic_menu.lua` 同时加载，职责边界建议在 AGENTS.md 明确；`env_check.lua` 头注释声称 Alt+E，实际 input.conf 为菜单项（`_`），注释需同步。
- 证据：`THIRD_PARTY_NOTICES.md`、`scripts/` 清单、`PluginConfigWriter.cs`。

## 附：建议的后续实施顺序

1. P1 快赢：README 睡眠定时器/协议宣称修正；de/es/fr 补齐；`SpeedChanged` 接线。
2. P1 功能补齐：DVD/BD 真实加载或移除入口；MSI 协议注册。
3. P2 批量：drift 白名单、过滤增量、display-info 轮转、recentmenu 入口确认、注释清理。

## 实施状态（2026-08-16 第二轮）

- README(zh/en) 移除“睡眠定时器”宣称；`mpv-winui://` 说明与 `installer/product.wxs` 协议注册补齐。
- DVD/BD 菜单改为真实加载：选择光盘根目录后 `set dvd-device`/`set bluray-device` + `loadfile dvd://`/`bd://`。
- `SpeedChanged` 原生事件接线到 `MpvMediaPlayer`，倍速菜单项改为可勾选并显示当前倍速提示（新增单测）。
- de/es/fr/ja/ko/ru/zh 未翻译键补齐（可译项翻译，技术名词保留原词）。
- `check-settings-drift.py` 白名单补 `PlaylistWidth`/`WindowTitle`；播放列表过滤改增量 diff；m3u 导出清洗换行；`display-info.log` 1 MiB 轮转；`OnException` 增加 5s 节流提示；过期 TODO/注释清理；AGENTS.md 补充 recentmenu 与 menu 脚本说明。
- `VolumeFlyoutControl` 订阅 `VolumeChangedChanged` 保持外部音量/静音状态同步；mpv 实测 `watch-history-path` 为每行 `{time,path}` JSON，与 App 解析器兼容。
- 验证：Debug/Release 构建 0 错误，单测 7/7 通过，IPC/UIA 冒烟通过（进度前进、播放列表追加、倍速设置、PiP 窗口）。
