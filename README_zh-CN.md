# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[English](README.md)

> WinUI 3 + libmpv 的 Windows 播放器（C++/WinRT 组件），配置层基于 mpv-lazy 裁剪。重点解决嵌入模式下的 HDR/WCG 正确输出、8 种语言界面，开箱即用。

## 简要介绍

[mpv-winui-player](https://github.com/saillill/mpv-winui-player) 通过 C++/WinRT 组件（`mpv_winrt`）把 [libmpv](https://github.com/mpv-player/mpv) 嵌进 WinUI 3 原生界面。mpv 配置由独立配置层 `mpv-winui-lazy/` 提供，基于 [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy）裁剪，无需额外折腾：

- 渲染：`vo=gpu-next` + `d3d11-output-mode=composition`（无边框闪烁，WinUI 原生覆盖层）。
- 显示检测：应用读 `DisplayInformation`，把 `user-data/mpvw/color-kind`（`SDR`/`WCG`/`HDR`）与 `user-data/mpvw/refresh-rate` 写给 mpv，`profiles.conf` 自动切换输出参数。
- 形态：unpackaged，一个 zip 解压即用；`deploy-config.ps1` 把配置层同步到 `%LOCALAPPDATA%\mpv-winui\mpv`。

相关项目：

- 上游应用：[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
- 配置来源：[hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)
- 播放核心：[mpv](https://github.com/mpv-player/mpv) · [libplacebo](https://github.com/haasn/libplacebo)
- Windows libmpv 构建：[ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder)

## UI 功能

- **菜单栏**：文件（打开文件/文件夹/URL/剪贴板、DVD/蓝光、观看历史、稍后观看、加载字幕、截屏、重启、退出）、查看（播放列表、全屏/全窗口、选项、打开配置/mpv 目录）、帮助（关于）。
- **播放控制**：播放/暂停、跳转、随机、循环、倍速、音视频轨道切换、缩放、全窗口/全屏、音量、带缩略图的进度条。
- **播放列表**：右键菜单（播放、移动、移除、复制标题/路径、打开文件位置）、观看历史、稍后观看。
- **右键菜单**：mpv 数据菜单（153 项，tsl0922/mpv-menu 排版）+ 固定文件/窗口项；“滤镜与增强”下直接显示 Nvidia VSR / RTX Video HDR / 清空所有脚本；菜单标题、动态项以及 mpv `select` 子菜单（轨道/章节/版本/音频设备/按键绑定/历史/稍后观看/属性）都在 8 种语言下全量翻译（`dyn_menu.lua` / `dynamic_menu.lua` / `select.lua` 内置翻译表），经 `user-data/mpvw/language` 自动切换；RTX HDR 模式、清除属性、缩略图开关等 OSD 反馈也已本地化。
- **设置窗口**：左侧分类、右侧内容双栏排版（常规/播放/视频/音频/字幕/截屏/高级/路径），共约 78 项 PotPlayer 式选项。所有选项都是可选项且显示本地化标签（不再显示 `auto`/`yes` 等原始键值），音频设备为从 mpv 实时枚举的下拉列表，截图文件名模板提供预设；开关显示本地化的开/关；语言下拉按各语言母语名称显示（中文 / 日本語 / 한국어…）；路径类选项带由设置窗口自己拥有的系统文件夹选择“浏览”按钮，并有“打开”按钮直接跳转文件管理器；说明以 Windows 设置风格显示在标题下方，仅在能补充信息时显示，并给出推荐值与已知冲突。脚本类选项集中在各分类末尾的“插件选项”小节；会冲突的选项直接置灰不可选，可能无效的选项显示黄色提示（如插帧需配合特定同步模式、截图质量项与当前格式不符）。改动即时下发 mpv，需要重启的设置（如语言）弹出系统提示。

## 相比原版的改进

| 方面 | 上游 ikas-mc/mpv-winui-player | 本项目 |
|---|---|---|
| HDR/WCG 输出 | 只有 SDR 示例；composition 模式下 HDR 发白 | `mpvw-sdr/wcg/hdr` 自动配置；WCG→`bt.2020`（修复非法的 `display-p3`）；HDR→`target-trc=pq`+`target-prim=bt.2020`+`target-peak=1000`；RTX HDR 期间固定 `target-colorspace-hint=yes` |
| 本地化 | 英文硬编码，无切换 | `AppLang` + `Languages/*.json`，8 种语言（en-US/zh-CN/ja-JP/ko-KR/de-DE/fr-FR/es-ES/ru-RU），设置页切换（重启生效） |
| 播放器设置 | 选项较少 | 约 78 项分类选项，即时下发并在启动时应用：硬解、音量/最大音量、keep-open、循环文件/播放列表、速度、保存/恢复播放进度、反交错、画面比例、放大/缩小算法、旋转、去色带、视频同步、插帧、HR seek（含丢帧）、色调映射、抖动、音频语言/设备（实时下拉）/声道/延迟/独占/音调/降混/音频文件显示、字幕字号/位置/延迟/语言/ASS 覆盖/模糊/字体（系统默认 + 微软字体）/字体提供程序/编码/回退/混合/随窗口缩放/ASS 黑边/图形字幕拉伸、OSD 字体/字号/跳转显示/时长、ICC 自动校色与 3D LUT、视频输出电平、磁盘缓存、截图目录/模板（预设）/格式/JPEG/PNG/WebP/色深/软件截图、缓存目录、NVIDIA VSR / RTX Video HDR / 拖动保持窗口开关；选项值全部本地化，插件分区，冲突项禁用，可能无效项黄色提示，路径带文件夹选择与打开按钮，说明去重且含推荐 |
| MediaInfo | 未随包 | 随包官方 CLI v26.05（BSD-2-Clause） |
| 开文件 | unpackaged 下协议/命令行失效 | 命令行与 `mpv-winui://` 均已修复并实测 |
| 日志 | mpv 默认 verbose | 默认关闭（`log-file` 注释、`hdr_auto` `log=no`） |
| 配置层 | 未随项目发布 | `mpv-winui-lazy/`：HDR/WCG 配置、RTX HDR/VSR 脚本、干净快捷键、MediaInfo 配置 |
| 构建发布 | 手动 | `build.ps1`/`package.ps1`；Actions 免证书直接产出 Release zip |

## 快速开始

1. 从 [Releases](https://github.com/saillill/mpv-winui-player/releases) 下载 `mpv-winui-win-x64-Release.zip` 并解压（Windows 10/11 x64）。
2. 首次运行前部署配置层：

```powershell
powershell -File mpv-winui-lazy\deploy-config.ps1
```

3. 运行 `mpv-winui.exe`。开文件支持菜单、拖拽、命令行和协议：

```powershell
mpv-winui.exe "D:\Videos\movie.mkv"
mpv-winui://?file=D%3A%5CVideos%5Cmovie.mkv
```

## 配置说明

### HDR / WCG 自动切换（`mpv-winui-lazy/profiles.conf`）

```ini
[mpvw-sdr]
profile-cond=p["user-data/mpvw/color-kind"] == "SDR"
profile-restore=copy
d3d11-output-csp=srgb
d3d11-output-format=rgb10_a2

[mpvw-wcg]
profile-cond=p["user-data/mpvw/color-kind"] == "WCG"
profile-restore=copy
d3d11-output-csp=bt.2020

[mpvw-hdr]
profile-cond=p["user-data/mpvw/color-kind"] == "HDR"
profile-restore=copy
d3d11-output-csp=pq
target-trc=pq
target-prim=bt.2020
target-peak=1000
```

注意：`d3d11-output-csp=display-p3` 是非法值；HDR 必须带三个 `target-*`，否则交换链虽是 PQ、渲染仍按 SDR 走，驱动不会真正进入 HDR。

### RTX Video HDR / NVIDIA VSR（`mpv-winui-lazy/script-opts/`）

- `hdr_auto.conf`：默认 `log=no`；`mode=auto|on|off`。
- `mpvw_hdr_override.conf`：`mode=` 留空=跟随 App；`HDR`/`SDR` 强制覆盖。

### 快捷键（`mpv-winui-lazy/input.conf`）

滚轮：音量/跳转 · `` ` ``：控制台 · `F6/F7`：播放列表/轨道信息 · `TAB`：统计 · `Alt+i`：MediaInfo · `Ctrl+1..0`：调色 · `w/W`：去黑边 · `[ ] { }`：速度。（`input_plus.lua` 有意不随包。）

### 本地化 / MediaInfo / 日志

- 语言：设置页切换（下拉按各语言母语显示），或编辑 `Languages\<lang>.json`（键为 `AppLang` 属性名）。右键菜单经 `user-data/mpvw/language` 同步，8 种语言在 `dyn_menu.lua` / `dynamic_menu.lua` 中全量覆盖。
- MediaInfo：`script-opts/stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`。
- 排障：`mpv.conf` 取消 `log-file` 注释并设 `msg-level=all=v`；`hdr_auto.conf` 设 `log=yes`。

### 播放器设置（设置窗口）

硬解（`hwdec`）、启动音量/最大音量（`volume`/`volume-max`）、播放结束动作（`keep-open`）、循环文件/播放列表（`loop-file`/`loop-playlist`）、默认速度、保存/恢复播放进度（`save-position-on-quit`/`resume-playback`）、反交错、画面比例、放大/缩小算法（`scale`/`dscale`）、旋转、去色带、线性缩小、S 形放大、视频同步、插帧、HR seek（`hr-seek`）与跳转丢帧（`hr-seek-framedrop`）、HDR 色调映射、抖动深度、首选音频/字幕语言（`alang`/`slang`）、音频设备、声道、延迟、独占模式、音调校正、降混标准化、自动加载外挂音频、音频文件显示（`audio-display`）、字幕字号/位置/延迟/字体（系统默认、Segoe UI、微软雅黑、Arial、Times New Roman、Consolas、随包思源黑体/LXGW WenKai）/字体提供程序（`sub-font-provider`）/编码/描边/阴影/ASS 覆盖/模糊/内嵌字体/黑边/ASS 黑边（`sub-ass-force-margins`）/图形字幕拉伸（`stretch-image-subs-to-screen`）/回退（`subs-fallback`）/混合模式（`blend-subtitles`）/随窗口缩放、OSD 字体/字号/跳转显示（`osd-on-seek`）/显示时长、ICC 自动校色与 3D LUT 大小、视频输出电平、磁盘缓存、截图目录（系统文件夹选择 + 打开资源管理器）与文件名模板/格式/JPEG 质量/PNG 压缩/WebP 质量/高色深/色彩空间标记/软件截图（`screenshot-sw`）、缓存目录、视频预览缩略图。视频预览由 thumbfast 配合应用进度条实现，Release 包内随附独立 `mpv.exe`（`package.ps1` 自动获取）作为 thumbfast 的解码子进程。**高级**分类收纳底层色彩/流/脚本选项：磁盘缓存、视频输出电平、ICC 配置、ICC 3D LUT、音频文件显示、字幕回退、字幕混合、ASS 随窗口缩放、OSD 字体/字号/跳转显示/时长、自动 NVIDIA VSR、RTX Video HDR 模式、拖动进度保持窗口大小。脚本类选项集中在“插件选项”小节；说明仅在补充信息时显示并给出推荐值；冲突项置灰（如启用字幕混合后 ASS 黑边无效），可能无效项以黄色提示。改动即时下发 mpv，启动时自动应用；需要重启的设置（如语言）弹出“立即重启”系统提示。

### 帮助 / 关于

帮助菜单新增独立“mpv 官方手册”项（<https://mpv.io/manual/master/>）；关于对话框提供 mpv GitHub 与本项目（<https://github.com/saillill/mpv-winui-player>）链接。

## 构建

### 环境

| 要求 | 说明 |
|---|---|
| Windows 10/11 x64 | 目标平台 |
| [.NET 10 SDK](https://dotnet.microsoft.com/) | 编译 C# WinUI 3 应用 |
| Visual Studio Build Tools（C++ 工作负载） | 编译 `mpv_winrt`（`VCTargetsPath` 属 VS 组件） |
| Windows App SDK 2.3.x | NuGet 还原 |
| `mpv-2.dll` | 从 [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) 下载到 `mpv-winui\libs\` |

### 构建与打包

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # 产出 dist\mpv-winui-win-x64-Release.zip
```

CI（`.github/workflows/build.yml`，手动 `workflow_dispatch`）与本地一致；未配证书 secrets 时自动跳过 MSIX，产出 unpackaged 目录 + Release zip。

### 引用项目

- 运行时：mpv（LGPL-2.1+/GPL-2.0+）、libplacebo（LGPL-2.1+）、Windows App SDK / WinUI 3（MIT）、CsWinRT / CsWin32（MIT）、NLog（BSD-3）、MediaInfo（BSD-2）、.NET（MIT）、NUnit（MIT）。
- 配置层：hooke007/mpv_PlayKit（基线，未列出文件默认 UNLICENSED）、tsl0922/mpv-menu（GPL-2.0-only）、coverart / recent-menu / metadata-osd（MIT）、thumbfast（MPL-2.0）、mpv 自带 console/select/stats、思源黑体 / LXGW WenKai（OFL-1.1）、着色器（以文件头为准）。
- 完整清单与许可证全文：[mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)。

## 许可证

- 应用代码：**LGPL-2.1**（[LICENSE.txt](LICENSE.txt)，与上游一致）。
- 配置层自创内容：**LGPL-2.1-or-later**；第三方组件以各自许可证为准（见 [THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)）。
