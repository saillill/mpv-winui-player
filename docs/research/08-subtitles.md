# 08 · 字幕渲染：线索

> **现状（实测）**
> - **渲染全部交给 mpv + libass**，项目里没有自己的字幕渲染器
> - UI 侧已相当完整：`SettingsPage.Options.Subtitles.cs` **30 个设置项**
>   （字体/字号/位置/边距/对齐/颜色/描边/HDR 峰值/位图字幕分辨率/ASS 覆盖策略……）
> - `AppSettings.Subtitles.cs` 14KB，`QuickControlPanel.Subtitles.cs` 6KB（快捷面板字幕页）
> - 播放控制条有字幕延迟滑块（`PlayerControl.Flyouts.cs:97-126`）
> - `data/mpv.conf` 里有 20+ 条 sub-* 配置
> - **双字幕已定义但部分不可达**：`MpvSettings.cs` 有 5 条 `secondary-sub-*` 映射，
>   但 `SetSecondSubVisibility(false)` 全仓无调用点（见 `PIVOT-MIGRATION-NOTES.md` §1.4）
>
> **所以本项不是"加渲染器"，是补功能缺口与配置暴露。**

---

## 一、线索清单

### 1.1 渲染核心：libass（不需要新引入）

| 项 | 内容 |
|---|---|
| 来源 | [libass 官方](https://github.com/libass/libass) · [镜像](https://www.repo.or.cz/libass.mirror.git) · [SMP README](https://raw.githubusercontent.com/ShiftMediaProject/libass/master/README.md) |
| 技术栈 | C，可移植的 ASS/SSA 渲染器，**基本兼容 VSFilter** |
| 与本项目关系 | **已通过 mpv 间接使用**。`data/mpv.conf:105` 的 `msg-level` 里出现 `sub/ass=no`，证明 libass 在跑 |
| 可参考 | 无需接入；但理解 libass 的能力边界有助于判断"哪些字幕效果 mpv 也只能近似" |

### 1.2 字幕生态的横向参考

| 项目 | 技术栈 | 可参考部分 | 许可注意 |
|---|---|---|---|
| **Kodi `CDVDSubtitlesLibass`** | C++，[文档](https://xbmc.github.io/docs.kodi.tv/master/kodi-base/d5/d8e/class_c_d_v_d_subtitles_libass.html) | **直接封装 libass 的完整类**：字体配置生命周期、native vs adapted 字幕的区分、按时间轴喂 ASS event | GPL，只读 |
| **rassa**（Rust） | [crates.io](https://lib.rs/crates/rassa) · [lib.rs](https://crates.io/crates/rassa) | 纯 Rust 重实现的 ASS 渲染器；API 设计可参考 | 独立实现，可读 |
| **ass.js / 前端 ASS 渲染** | [相关项目](https://relatedrepos.com/gh/libass/libass) | 浏览器内 ASS 显示 | 与本项目无关（非 Web） |
| **vapoursynth-subtext** | [PyPI](https://pypi.org/project/vapoursynth-subtext/) | 字幕叠图与 alpha 混合的思路 | 非实时渲染，参考价值低 |

### 1.3 mpv 侧的字幕能力（真正的"可复用面"）

| 能力 | 对应配置/属性 | 本项目现状 |
|---|---|---|
| 外挂字幕自动加载 | `sub-auto=fuzzy`、`sub-file-paths=sub;Subs;subtitles` | **已配** |
| 编码猜测 | `sub-codepage=auto` | **已配** |
| 字体提供 | `sub-font-provider`（auto/none/fontconfig） | **注释掉了**，说明曾评估过 |
| HDR 白点 | `sub-hdr-peak=100`、`image-subs-hdr-peak` | **已有设置项** |
| 位图字幕缩放 | `image-subs-video-resolution` | **已有设置项** |
| ASS vs 纯文本覆盖策略 | `sub-ass-override`、`secondary-sub-ass-override` | **已有** |
| VSFilter 兼容 | `sub-ass-vsfilter-color-compat` / `-aspect-compat` | **已有** |
| 双字幕 | `secondary-sid`、`secondary-sub-*`（5 项） | **映射已写，UI 不可达** |

### 1.4 官方文档锚点

| 文档 | 用途 |
|---|---|
| [mpv 手册 sub-* 章节](https://mpv.io/manual/master/) | 每个 sub 选项的准确语义与取值范围 |
| [libass README](https://raw.githubusercontent.com/ShiftMediaProject/libass/master/README.md) | 构建与特性支持 |
| [Kodi libass 封装类](https://xbmc.github.io/docs.kodi.tv/master/kodi-base/d5/d8e/class_c_d_v_d_subtitles_libass.html) | 若将来要脱离 mpv 自渲染，这是最完整的参考实现 |

---

## 二、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| libass（经 mpv） | **已具备** | 0 | 能力边界由 mpv 决定；某些 VSFilter 私有行为可能不一致 |
| Kodi 的封装类 | 低（GPL，只读） | — | 仅在"要自渲染"时有价值，而本项目没有这个需求 |
| 补双字幕可达性 | **高** | **低** | `secondary-sub-*` 映射已在，缺的是 UI 路径与 `SetSecondSubVisibility(false)` 的调用点 |
| 字体提供者（`sub-font-provider`） | 中 | 低 | 该行被注释掉（`mpv.conf:234`），说明曾评估后放弃；需查原因 |
| 位图字幕（PGS/VobSub） | 中 | 低 | 已有 `image-subs-*` 设置，但未验证实际效果 |

### 已识别的缺口（本项真正值得做的事）

1. **双字幕半成品**：5 条 `secondary-sub-*` 映射 + 无 UI 入口 + `SetSecondSubVisibility(false)` 无调用点
2. **`sub-font-provider` 被注释**：注释本身没写原因，属于"未记录的决定"
3. **字幕字体枚举/预览**：设置里有 `SubFont`，但未见字体列表来源
4. **位图字幕的 HDR 处理**：`ImageSubsHdrPeak` / `ImageSubsVideoResolution` 有设置项，效果未知

---

## 三、检索提示词

### 3.1 搜索式

```
mpv manual sub-ass-override secondary-sid secondary-sub-*
libass VSFilter compatibility differences
mpv sub-font-provider fontconfig windows
WinUI 3 font picker enumerate system fonts
mpv image-subs-video-resolution PGS VobSub scaling
Kodi CDVDSubtitlesLibass integration
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。字幕**渲染完全交给 mpv + libass**，
> 项目里没有自研渲染器，也不需要。
>
> 现状：`SettingsPage.Options.Subtitles.cs` 有 30 个设置项；`data/mpv.conf` 有 20+ 条 sub-*；
> 播放条有字幕延迟滑块。
>
> **已知缺口**：
> - `MpvSettings.cs` 有 5 条 `secondary-sub-*` 映射，但 `SetSecondSubVisibility(false)` 全仓无调用点，
>   双字幕实际不可达（详见 `docs/prompt-v3/PIVOT-MIGRATION-NOTES.md` §1.4）
> - `mpv.conf:234` 的 `sub-font-provider` 被注释掉，注释未记录原因
>
> 请先回答，不要改代码：
> 1. 双字幕要打通，最小改动面是什么？`secondary-sid` 与 `secondary-sub-visibility` 的语义差异？
> 2. `sub-font-provider` 当初为什么被注释？Windows 上 fontconfig 是必需还是有害？
> 3. 设置里的 `SubFont` 字体列表从哪来？WinUI 3 有没有现成的字体选择控件可复用？
> 4. 位图字幕（PGS/VobSub）的 `image-subs-hdr-peak` 实际效果如何验证？
>
> 参考：mpv 手册 sub-* 章节；Kodi 的 `CDVDSubtitlesLibass` 是 libass 封装的完整参考（GPL，只读）。

### 3.3 深挖方向

- 查 `AppSettings.Subtitles.cs` 的全部字段，与 `mpv.conf` 的 sub-* 逐条对照，找"有配置无 UI"与"有 UI 无配置"
- 实测位图字幕在 HDR 输出下的表现（用带 PGS 的 mkv）
- 查 `git log` 找 `sub-font-provider` 被注释的那次改动
