# 调研索引（线索搜集，不含修复方案）

> 本目录是**线索搜集与调研**，不是待办清单，也不含排查与修复方案。
> 排查与修复由新会话负责。每份文档都按同一结构组织：
> **一、线索清单 → 二、适用性评估 → 三、检索提示词**。
>
> 生成时间：2026-10-29。所有"现状"数字均来自当时的实测，不是引用旧文档。

---

## 一、文档一览

| # | 板块 | 文件 | 行数 | 一句话现状 |
|---|---|---|---|---|
| 01 | 播放/渲染嵌入 | [`01-playback-embedding.md`](01-playback-embedding.md) | 117 | **已修复**（commit `07a99fb`），本文只收备选方案 |
| 02 | 音乐页 | [`02-music-page.md`](02-music-page.md) | 79 | 已是 `ItemsRepeater`+`UniformGridLayout`，缺的是信息层次与交互 |
| 03 | 设置页 | [`03-settings-page.md`](03-settings-page.md) | 81 | NavigationView + **356 选项** + 18 个渲染器，IA 已存在但由代码驱动 |
| 04 | 侧栏 | [`04-sidebar.md`](04-sidebar.md) | 99 | Popup 320×≤640，**注释记录这是一次返工**，方向与上次相反 |
| 05 | 状态栏弹窗 | [`05-status-bar-popups.md`](05-status-bar-popups.md) | 86 | 四类浮层；尺寸一半用令牌一半用 `const double`，这是"不统一"的机械原因 |
| 06 | 主页图片卡片 | [`06-image-cards.md`](06-image-cards.md) | 106 | 根因是 **WMC9999 编译器崩溃**，不是绑定 Mode |
| 07 | 排版/图标/检测 | [`07-layout-tooling.md`](07-layout-tooling.md) | 120 | 30 个检查器但只 8 个在链路；缺 XamlStyler |
| 08 | 字幕渲染 | [`08-subtitles.md`](08-subtitles.md) | 93 | 渲染交给 libass，UI 已有 30 个设置项；双字幕半成品 |
| 09 | 播放列表 | [`09-playlist.md`](09-playlist.md) | 98 | 队列已在 `MpvPlayerPage_Playlist.cs`，diff 刷新/导入导出都有 |
| — | 国际化（暂缓） | [`10-i18n-interface.md`](10-i18n-interface.md) | — | **不做翻译**，仅记录接口形态 |

---

## 二、跨板块的高价值线索（去重后）

这几条在多个板块重复出现，单独列出以免各文档各查一遍。

### 2.1 许可可复用的实现

| 项目 | 许可 | 覆盖板块 | 链接 |
|---|---|---|---|
| **Fusion**（YesPlayMusic） | **MIT** | 02 音乐页、03 设置页、04 侧栏、05 弹窗、09 播放列表 | [zoinve/Fusion](https://github.com/zoinve/Fusion) |
| **mpv-uwp** | 见仓库 | 01 播放嵌入（`SwapChainPanel` + d3d11 最早雏形） | [Dixeran/mpv-uwp](https://github.com/Dixeran/mpv-uwp) |
| **Windows Community Toolkit** | MIT | 03 设置页（`SettingsCard`/`SettingsExpander`）、07 工具链 | [CommunityToolkit/Windows](https://github.com/CommunityToolkit/Windows) |
| **XamlStyler** | Apache-2.0 | 07 格式统一 | [Xavalon/XamlStyler](https://github.com/Xavalon/XamlStyler) |

### 2.2 只能读、不能抄（许可不兼容）

| 项目 | 许可 | 覆盖板块 |
|---|---|---|
| **mpv.net** | **GPL v2** | 01、02、09 —— 与本仓库 LGPL-2.1 不兼容 |
| **Tauon** | GPL | 02、04、09 |
| **Kodi** | GPL | 08（`CDVDSubtitlesLibass` 是 libass 封装最完整参考） |
| **MusicBee** | 闭源 | 02、04（只有设计讨论可读） |

> 本仓库 `docs/upstream-gap.md` 已记录上游 `b5ddfb6` 之后改为 GPLv3 的先例，
> 处理方式相同：**读行为描述，独立实现**。

### 2.3 已实测的硬约束（不要再试）

| 约束 | 出处 | 影响板块 |
|---|---|---|
| **WMC9999**：`{x:Bind AlbumTitle}` 让 WASDK 2.3.9 编译器内部崩溃 | `MpvPlayerPage.xaml:340`、`Library.Cards.cs:768` | 06 |
| `ItemsRepeater` **不设** `DataContext`，`{Binding}` 全部为 null | `Library.Cards.cs:762-779` | 06 |
| WinUI 3 **不支持** HWND 宿主；子 HWND 永远画在 XAML 之上 | [WASDK #3879](https://github.com/microsoft/WindowsAppSDK/discussions/3879) | 01、04、05 |
| `mpv.conf` **覆盖** `mpv_initialize` 之前设的选项（与旧文档相反） | 已修正 `docs/runtime-facts.md` §1 | 01 |
| `SetOption` 曾丢弃返回值，失败静默 | 已修（commit `07a99fb`） | 01 |

### 2.4 官方文档锚点（各文档共用）

| 文档 | 用在 |
|---|---|
| [Dialog controls](https://learn.microsoft.com/windows/apps/design/controls/dialogs-and-flyouts/dialogs) | 05（CloseButton 必须存在且最右） |
| [Geometry in Windows 11](https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry) | 05（8px 圆角） |
| [ItemsView 指南](https://learn.microsoft.com/windows/apps/design/controls/itemsview) | 02、03、06 |
| [ItemsRepeater 文档](https://learn.microsoft.com/windows/apps/design/controls/items-repeater) | 06 |
| [mpv 手册](https://mpv.io/manual/master/) | 01、08 |
| [mpv `libmpv/render.h`](https://github.com/weiplanet/mpv/blob/master/libmpv/render.h) | 01 |

---

## 三、板块之间的依赖关系

后续会话若只做一个板块，需知道它牵动谁：

```
01 播放嵌入 ──┬─→ 04 侧栏（若走 HWND，所有 Popup 要改 owned window）
              ├─→ 05 弹窗（同上）
              └─→ 08 字幕（渲染目标变化影响字幕叠加）

04 侧栏 ─────────→ 09 播放列表（共用 PlaylistItems 一份集合）
05 弹窗 ─────────→ 04 侧栏（共用 Popup 机制与关闭规则）

06 图片卡片 ─────→ 02 音乐页（同一套卡片模板与 ElementPrepared 回调）
07 检测工具 ─────→ 全部（排版规范适用于所有 XAML）
```

**最容易互相踩到的两对**：`04 ↔ 09`（同一份集合、两个视图，注释要求不可漂移）、
`05 ↔ 04`（同一套 Popup 关闭规则）。

---

## 四、本目录**不包含**什么

按用户要求，以下内容刻意未写：

- **不包含**排查过程与修复方案
- **不包含**具体代码改动
- **不包含**国际化翻译（仅保留接口说明，见 `10-i18n-interface.md`）
- **不包含**新写的检测脚本（用户明确要求先搜现成方案，见 07）

---

## 五、给后续会话的总提示词

> 你在一个 WinUI 3 + C#/C++ 的 mpv 播放器仓库（`mpvw`，LGPL-2.1）。
>
> 本目录（`docs/research/`）是上一轮**只做调研、不做修复**的产物。
> 九份文档按同一结构组织：线索清单 / 适用性评估 / 检索提示词。
>
> **请先读 `README.md`（本文件）的 §二、§三**，再按需读具体板块。
> 不要一次读完九份——每份 80–120 行，全部读完会占掉大量上下文。
>
> 各文档内的"交给后续会话的提示词"一节，可直接作为该板块的开工输入。
>
> **已知的许可约束**：mpv.net（GPL v2）、Tauon（GPL）、Kodi（GPL）只能读不能抄；
> Fusion（MIT）、CommunityToolkit（MIT）、XamlStyler（Apache-2.0）可复用。
>
> **已知的已试失败项**（不要重复）：见 §2.3。
