# mpv-winui-player 系统审计与长期路线图（2026-08-24）

> 基线：`github/main` HEAD `bfa4875`。本文取代已删除的
> `docs/archive/{feature-audit-20260816,menu-audit-20260807,ui-audit-20260816}.md`，
> 是当前唯一的审计/规划文档。全部结论基于对当前代码的直接验证，不沿用任何
> 旧文档声明；旧文档中与代码矛盾的部分已在本次一并修正（README 睡眠定时器、
> 六处 dialog.lua 引用）。

## 0. 总体评价

代码库处于**健康偏上**水平：C# 约 2.9 万行、C++ 约 4.1 千行、配置层 lua 约 1.3 万行
（多数为上游脚本）。分层清晰（WinUI 壳 / C++\WinRT 组件 / 配置层），原生库利用度高：

- CsWin32 承担全部 Win32 调用面（NativeMethods.txt 33 项），手写 P/Invoke 仅剩
  mpv 内部键码移植（composition 模式必需，见 §3.4）；
- mpv 侧大量使用属性观察、user-data 通道、render API（软件缩略图管线
  `MpvPreviewer` 实现干净、生命周期正确）；
- AOT + trimming 发布；设置链路单一事实源（`MpvSettings.ToCommand` 反射枚举 +
  `check-settings-drift.py` 防漂移）；`CachedDataSetting` 内存优先防抖回写；
  `PluginConfigWriter` 标记合并 + 读回校验——这些是经过验证的好设计。

主要问题集中在三类：**残留死代码**（约 900+ 行）、**文档与代码脱节**
（dialog.lua 删除后六处文档未跟进、README 一处功能宣称失实）、
**少数巨文件职责堆积**。以下按优先级列出。

## 1. P0 死代码与残留（可直接删，行为零变化）

| # | 位置 | 内容 | 验证方式 |
|---|---|---|---|
| A1 | `mpv-winrt/MpvMenuBuilder.cpp/.h` | 524 行原生菜单构建器，唯一调用点 `MpvPlayer.cpp:116` 处于注释状态（ta.c canary 教训后禁用）。编译进 DLL 但不可达 | 全库 grep 仅注释引用 |
| A2 | `MpvPlayer.h/cpp` | 四个无 C# 消费者且 observe 已被注释的事件：`NetworkInfoChanged` / `TrackListChanged` / `TrackListCountChanged` / `TrackChanged`，连带 `TrackListChangedEventArgs`、`TrackListCountChangedEventArgs`、`NetworkInfoChangedEventArgs` 三个类型与对应 dispatch 分支 | C# 侧 grep 零订阅 |
| A3 | `MpvPlayer.cpp` | 空 TODO 分支：`MenuData`(474)、`TrackList`(498)；注释掉的 observe 残骸（PlaybackTime/CacheSpeed/Aid/Sid/Filename 等） | 直接读源码 |
| A4 | `Modules/Mpv/W32Common.cs` | `handle_appcommand()`（空壳 stub，核心调用被注释）、`handle_char()` 无外部调用者；`W32Keyboard.mp_w32_appcmd_to_mpkey` 与 `appcmd_map` 表仅被前者调用，连带死 | grep 零消费者 |
| A5 | `Modules/Mpv/Keycodes.cs` | `mp_input_get_key_from_name` / `mp_input_get_key_name`（~100 行）仅服务 trace 日志 | grep 仅 MpvPlayerPage_Input 日志分支引用 |
| A6 | `Modules/Common/Utils/ColorUtil.cs` | `IsColorLight` 零调用 | grep |
| A7 | `Modules/Player/Menu/CustomMenuSource.cs` | 整文件死：用户菜单覆盖职责已由 `MenuDefinitionSource`（内置+用户双路径+空表回退）覆盖 | grep 零消费者 |
| A8 | 泛化属性观察通道 | `ObserveProperty/UnobserveProperty/PropertyChanged` + `NodeToString` 伪 JSON 序列化（~110 行）：应用侧零消费，仅 `mpv-winrt-test` 使用。组件若定位为可复用库可保留为公共 API；否则随测试一起删 | grep + 测试工程交叉验证 |
| A9 | 杂项 | `PlayerControl.xaml.cs:500` GBK 乱码注释（原版/居中）；`:794 //TODO`；`MpvPlayerPage_Input.cs:20` 空 TODO；5 个无 await 的 `async void`（去 async 即可）；缩放比例列表在 `ZoomButton_Click` 与 `BuildZoomSubmenu` 重复构建 | 读源码 |

## 2. P1 文档与部署一致性

1. **README.md 曾宣称"睡眠定时器"** —— 代码中无此功能（中文版早已删除，英文版漏改）。已修正。
2. **dialog.lua 于 d99a9c6 删除**，但 AGENTS.md、LOCAL-PATCHES.md、VERSIONS.md、
   lazy README、THIRD_PARTY_NOTICES 五处仍描述它存在。已全部修正。
3. **运行目录 `ChatGPT\MPV\mpv-winui-new-build` 陈旧**：exe 为最新构建，但
   `mpv-winui-lazy/scripts/` 仍残留 `dialog.lua`、`env_check.lua`
   （`load-scripts=yes` 会照常加载）。需要重新部署或手动删除这两个文件。
4. 三份 docs/archive 归档审计已删除（其 P1 项经逐条核验均已修复：
   DVD/BD 已实现、speed 回传 UI 已订阅、协议注册已入 product.wxs、
   display-info.log 已有 1MiB 轮转），历史在 git。

## 3. 架构观察（非缺陷，纳入长期改造）

### 3.1 包装层命名是 MediaElement 时代的化石
`MpvMediaPlayer` 的事件名沿袭自 Windows.Media.Playback：`BufferingStarted`
实际在 **Seeked 时触发**、`MediaLoaded` 触发 `BufferingEnded`、
`VolumeChangedChanged` 双写名、`NaturalDurationChanged`。行为正确但语义错位，
每个读者都要重新翻译一遍。建议 Phase 1 统一改为 mpv 语义命名
（Seeking/Seeked/MediaOpened…），一次性 sed 级改动。

### 3.2 巨文件拆分
- `PlayerControl.xaml.cs`（2091 行）：控制条布局引擎 / 面板显隐动画 /
  预览转发 / 章节标记四个互不依赖的职责，可拆成 partial 或协作类。
- `AppSettings.cs`（1972 行，280 属性）：反射 ApplyAll 单一事实源设计良好，
  但可按域拆 partial（Playback/Video/Subtitles/Network/OSD…），导航成本减半。
- `AppLang.cs`（1154 行属性袋 ×8 JSON）：机械重复，AOT 下源生成器是唯一出路，
  收益有限，最低优先级。

### 3.3 C++ 组件样板压缩
18 个事件的 add/remove 样板约 250 行是 C++/WinRT 固有噪音，可用宏或
CRTP helper 收敛；`GetTracks` 三段近似解析可表驱动。收益中等，
排在行为层重构之后。

### 3.4 手写键映射层的边界（保留理由存档）
`Keycodes.cs` + `W32Keyboard.cs` + `W32Common.decode_key/mod_state` 是 mpv
`input/` 内部代码的 LGPL 移植，用于 composition 模式下 VK→mpv 键码转发——
libmpv 不暴露该能力，属**必要手写**，不是对原生库的重造。瘦身目标只有：
删 A4/A5 死成员；`WinUser.cs` 的 VK_* 常量与 CsWin32 的 `VIRTUAL_KEY`
重叠，可整体替换（仅 MpvPlayerPage_Input 一个消费者）。

### 3.5 MpvMenuBuilder 的最终裁决（GPL 替代路径）
原生菜单构建器当年因 `mpv_free_node_contents` 只能释放 ta 分配内存而崩
（free 调用已移除但未复验）。它存在的意义是替代 GPL-2.0 的 dyn_menu.lua，
彻底消除许可边界。二选一，不许维持现状：
- **修复启用**：用 `mpv_observe_property(MPV_FORMAT_NODE)` 接收 menu-data
  （事件数据归 mpv 所有，只读不 free），绕开 get/free 路径；
- **彻底删除**：接受 dyn_menu.lua 的 GPL 边界（现有 LOCAL-PATCHES 流程
  已把边界维护得很机械）。

### 3.6 小项
右键菜单打开时 `MenuData()` 在 UI 线程同步 `mpv_get_property`（一次菜单
打开、通常 <1ms，可接受；若未来菜单变大再异步化）。`System.Management`
仅一处 WMI 查询（SettingsPage.Actions GPU 信息），包体积敏感时可换
`SetupAPI`/CsWin32。

## 4. 长期计划

原则：每阶段独立可发布、可回滚；动 C# 表面前先跑
`check-localization.py` 与 `check-settings-drift.py`；涉及 mpv_node 内存
的改动必须先读 AGENTS.md 的 canary 教训并在真机冒烟。

**Phase 0 — 清扫（半天）✅ 已完成（2026-08-24）**
§1 全部删除项落地：MpvMenuBuilder.cpp/.h、四个零消费事件及其 EventArgs、
泛化观察通道（含 IDL 表面与 vcxproj 引用）、W32Common/W32Keyboard 死成员、
Keycodes 键名函数（文件 270 行起整段移除）、WinUser.cs（VK_* 换 CsWin32
VIRTUAL_KEY，NativeMethods.txt 显式请求）、ColorUtil.cs、CustomMenuSource.cs；
PlayerControl 杂项（乱码注释/TODO/无 await async void ×7/缩放菜单去重）。
布局纯函数提取为 `ControlBarLayoutEngine`。包装层事件对齐 mpv 语义：
BufferingStarted/Ended→SeekingStarted/SeekingEnded、VolumeChangedChanged→
VolumeChanged、删除从未触发的 NaturalDurationChanged 与无人订阅的
Scrub 通知 SeekingStarted。验收记录：Release x64 构建 0 警告 0 错误；
mpv-winrt-test 5/5 通过；check-localization/check-settings-drift 均通过。

**Phase 1 — API 表面收敛 ✅ 并入 Phase 0 完成**
A2/A8 删除；A1 按 §3.5 裁决为**彻底删除**（禁用数周、GPL 边界已由
LOCAL-PATCHES 流程机械维护；如需恢复走 observe_property 只读路径，
实现可从 git 历史找回）。测试工程同步删除断言已删 API 的两个用例。

**Phase 2 — 巨类拆分（主体完成 2026-08-24）**
- PlayerControl：主分部 1988→1126 行；布局引擎、控制条组合
  （ControlBar.cs）、面板动画（PanelAnimation.cs）、进度标记
  （ProgressMarks.cs）、预览管线（Preview.cs）各成档。
  布局纯函数已独立，单元测试因 WinUI 程序集引用成本暂缓——逻辑为
  原样搬移，行为由冒烟覆盖。
- AppSettings：1972→592 行核心（基础设施+迁移+界面域），按名称前缀
  分出 Playback/Video/Audio/Subtitles/Osd/Network/Screenshot/Plugins 八个
  域 partial；`check-settings-drift.py` 已改为 glob `AppSettings*.cs`，
  校验结果与拆分前基线一致（288 属性/219 映射/274 键）。
- MpvPlayerPage：Input+Mouse 合并为 InputForwarding.cs。**裁决修正**：
  原计划把 Display 一并并入"输入转发"，核实后 Display 是显示器监测
  （色彩/刷新率/峰值/WM_DISPLAYCHANGE 子类），语义不属于输入，保持独立。
- CI 补 `check-settings-drift.py` 步骤（原 Phase 4 项提前完成）。

**Phase 3 — 许可与菜单终局 ✅ 已裁决并执行（删除路线）**
THIRD_PARTY_NOTICES / LOCAL-PATCHES / VERSIONS 已同步 dialog.lua 移除与
MpvMenuBuilder 删除。

**Phase 4 — 持续**
CI 补 `check-settings-drift.py`（现只跑 localization）；每次升级
WindowsAppSDK 必须整组升级（Bootstrap.dll 冲突教训）；新功能先问
"mpv 是否已有该属性/命令"，能用 user-data 通道就不写 C#。

## 5. 本文件维护规则

本文件是唯一活审计文档。下次大审计时直接在其上迭代版本号与日期，
不要另开新档；阶段性完成后把已完成项移入 git commit message 并从
清单划掉，保持文档短于 300 行。
