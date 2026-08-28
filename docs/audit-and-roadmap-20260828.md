# mpv-winui-player 系统审计与长期路线图（2026-08-28）

> 基线：`github/main` `5574dc4`。本文件是唯一活审计文档，由
> `audit-and-roadmap-20260824.md` 原地迭代而来（规则见 §6）。本轮对全部
> 文档声明逐条重验，未沿用任何旧结论；2026-08-24 批次的完成项已移入
> git 历史（c5eafb1…5574dc4），不再在此保留。

## 0. 总体验证结论（全部为本轮实测）

- 构建 Release x64：0 警告 0 错误；mpv-winrt-test 25/25；
  check-localization 1035 键 × 9 语言通过；check-settings-drift
  285 属性 / 219 映射 / 274 键通过。
- C# 侧零手写 P/Invoke，全部 Win32 调用面走 CsWin32
  （NativeMethods.txt 42 项）；mpv 键码/按键转译 571 行
  （Keycodes+W32Common+W32Keyboard）为 composition 输入必需的必要手写。
- 预览、设置链路、许可边界结构健康（与 2026-08-24 结论一致，本轮复核成立）。
- 残留问题集中在：**包装层/原生的死 API 面**（本轮已清）、
  **一处本地化漏接线**（本轮已修）、**文档过期五处**（本轮已修）。

## 1. 本轮修复（已落地，构建/测试/校验全绿后提交）

### 1.1 死 API 面（行为零变化）

| 层 | 删除项 | 依据 |
|---|---|---|
| 包装层 | `MpvMediaPlayer`：`LogMessage` Action（内部 NLog 映射保留）、`PreviewChanged` Action 及原生订阅、`SetHoverSec`/`SetDrawPreview`/`ClearPreview`、`AddSubtitles`、`Editions()`、`Profiles()`、`PlaylistShuffle()` | 全库 grep 零外部消费者 |
| 原生 | `MpvPlayer`：`LoadList`、`TogglePlayPause`、`GetEditions`、`GetProfiles`、`PlaybackEnded` 事件、`PreviewChanged` 事件 + `user-data/mpvw/preview` observe（无任何写入方）、`MpvObserveId::Preview` | C# 侧零调用/零订阅；`mpvw/preview` 仅此一处引用 |
| 类型 | `MpvEdition.h/.idl`、`MpvProfile.h/.idl` 及 vcxproj/filters 条目 | 仅被上述死方法引用 |

### 1.2 死设置与死成员

- `AppSettings`：`PatchVersion`、`LastAudioVolume`、`ControlBarCustomOrder`
  属性删除（单次迁移仍读旧注册表键，键名改字面量）；drift 白名单同步。
- `Win32WindowHelper.GetWindowScale/GetMonitorFromRect`、
  `ClipboardHelper.SetCopyUri`、`BindConvertor.InverseBool/HasTextVisibility`。

### 1.3 P1 本地化修复

播放列表右键菜单 9 项（播放/移到顶部/…/打开文件位置）在 XAML 中硬编码
英文，`AppLang` 九个 `Playlist*` 键与九语言翻译早已备好但从未接线。
已在 `MpvPlayerPage_MpvMenu.ApplyLocalizedStrings` 按 Tag 映射应用
（该函数在启动与切语言时均被调用）。

## 2. 文档核验（怀疑论逐条，过期即修）

已修：README.md / README_zh-CN.md 语言列表缺繁体中文；
mpv-winui-lazy/README.md 中 "dialog.lua 保留" 与删除事实矛盾、
`#menu:` 149→137、部署排除表补 `custom_menu.json`/`*.bak*`；
VERSIONS.md 的 coverart.lua/recentmenu.lua 来源行（实为
CogentRedTester/mpv-coverart 与 natural-harmonia-gropius/recent-menu，
与 NOTICES 及脚本头一致）；localization.md（9 份 json、
`-Debug x64` 是 PowerShell 公共参数不会设 `-Configuration`、
lua 侧 zh-TW→zh-CN 归一化说明）。

复核无误的关键声明：AGENTS.md 全部 architecture gotchas 与 test facts
（缩略图 180/248/320、字形 F175/F172/EF34/EF37、ApplyBarOrders 三栏重建、
ControlPanelGradient 120、`-Release x64` 静默落回 Debug——参数按位置绑定
所致，实测复现）；MediaInfo.exe v26.05 与 SHA-256 实测一致。

## 3. 扫描方法教训（下一轮审计必读）

**extension 方法类对"类型名引用数"扫描天然免疫**：
`DispatcherExtensions`/`TaskExtensions`/`WindowExtensions` 的类名全库零引用，
但 `RunAsync`/`FireAndForget`/`ShowWindow`/`SetWindowMinSize` 以实例语法
（`this`/C#14 `extension(...)`）调用，类型级扫描误判整文件死。本轮靠构建
失败当场拦截并恢复。规则：**类型级死代码判定必须先用构建验证，扩展方法
容器一律按成员级判定**。

## 4. 剩余观察（非缺陷，长期改造对象）

1. **包装层中继冗余**：`MpvMediaPlayer` 16 个 Action 中多数是
   原生事件 1:1 转发；仅 `MediaInfoChanged`（缓存 VideoWidth/Height）、
   `LoopFile/LoopPlaylistChanged`（合成 RepeatStateChanged）、`MediaOpened`
   等少数含状态。可评估让 UI 直接订阅原生事件，包装层只保留有状态的
   （`MpvCommandQueue`、Open/Playlist 语义封装保留）。
2. **C++ 事件样板**：约 16 组 add/remove ≈ 200 行 C++/WinRT 固有噪音。
3. **巨文件**：`SettingsPage.Actions.cs` 1101 行（动作+快捷键+GPU 查询+
   重置混居）、`ControlBarCanvasControl.xaml.cs` 965 行（布局编辑画布）、
   `AppLang.cs` 1157 行属性袋（AOT 下源生成器是唯一出路，收益有限）。
4. **System.Management 仅一处**（SettingsPage.Actions GPU WMI 查询），
   包体积敏感时可换 SetupAPI/CsWin32。
5. `MpvPreviewInfo` 投影现仅 MpvPreviewer 组件使用，MpvPlayer 侧已无消费者。

## 5. 长期计划（允许大幅重构；每阶段独立可发布、可回滚）

原则：动 UI 前跑两校验；涉 mpv_node 的改动先读 AGENTS.md canary 教训；
每阶段完成即 commit+push，勿跨阶段攒大提交。

- **Phase A — 包装层收敛**：按 §4.1 裁决中继事件的去留（逐个列出消费者，
  纯转发者删除，UI 改订原生事件）；`MediaFailed`/`SwapChainChanged` 一并
  复核。目标：MpvMediaPlayer 只剩"有状态 + 有语义"的成员。
- **Phase B — C++ 样板压缩**：事件 add/remove 用宏或 CRTP helper 收敛；
  `GetTracks` 三段近似解析表驱动（沿用 2026-08-24 §3.3 结论，本轮确认仍成立）。
- **Phase C — 巨类拆分**：SettingsPage.Actions.cs 按职责切分（动作分发 /
  快捷键编辑 / 系统信息 / 重置）；ControlBarCanvasControl 评估抽布局编辑
  纯函数（可仿 ControlBarLayoutGrammar 编入测试工程的做法）。
- **Phase D — 持续**：CI 已含两校验；WindowsAppSDK 整组升级；
  新功能先问"mpv 是否已有该属性/命令"。

## 6. 本文件维护规则

唯一活审计文档，下次大审计原地迭代日期与版本（触发词「全面体检」/
深度巡检 / repo-checkup：按 AGENTS.md + 提示词执行 基线核对 → 文档
怀疑论验证 → 死代码/门控扫描 → 实证修复 → 部署 → 推送 → 本文件迭代）。
完成项移入 git commit message，保持文档短于 300 行。
