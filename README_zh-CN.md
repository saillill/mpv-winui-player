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
- **右键菜单**：mpv 数据菜单（153 项，tsl0922/mpv-menu 排版）+ 固定文件/窗口项；“滤镜与增强”下直接显示两个滤镜（Nvidia VSR / RTX Video HDR / 清空所有脚本），不再多一层子菜单；菜单文案跟随应用语言（默认英文，切中文/其它语言自动切换，经 `user-data/mpvw/language`）。
- **设置窗口**：左侧分类、右侧内容双栏排版（常规/播放/视频/音频/字幕/路径），含主题、背景、8 种语言、调试日志，以及 PotPlayer 式选项：硬解、最大音量、播放结束、循环、默认速度、反交错、画面比例、字幕字号/位置/延迟、首选音频/字幕语言、音频设备、截图目录与文件名模板、缓存目录；改动即时下发 mpv，需要重启的设置（如语言）弹出系统提示。

## 相比原版的改进

| 方面 | 上游 ikas-mc/mpv-winui-player | 本项目 |
|---|---|---|
| HDR/WCG 输出 | 只有 SDR 示例；composition 模式下 HDR 发白 | `mpvw-sdr/wcg/hdr` 自动配置；WCG→`bt.2020`（修复非法的 `display-p3`）；HDR→`target-trc=pq`+`target-prim=bt.2020`+`target-peak=1000`；RTX HDR 期间固定 `target-colorspace-hint=yes` |
| 本地化 | 英文硬编码，无切换 | `AppLang` + `Languages/*.json`，8 种语言（en-US/zh-CN/ja-JP/ko-KR/de-DE/fr-FR/es-ES/ru-RU），设置页切换（重启生效） |
| 播放器设置 | 选项较少 | 分类的 PotPlayer 式选项（hwdec、volume-max、keep-open、循环、速度、反交错、画面比例、字幕字号/位置/延迟、alang/slang、音频设备、截图目录/模板、缓存目录），即时下发并在启动时应用；需重启的设置弹提示 |
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

- 语言：设置页切换，或编辑 `Languages\<lang>.json`（键为 `AppLang` 属性名）。
- MediaInfo：`script-opts/stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`。
- 排障：`mpv.conf` 取消 `log-file` 注释并设 `msg-level=all=v`；`hdr_auto.conf` 设 `log=yes`。

### 播放器设置（设置窗口）

硬解（`hwdec`）、最大音量（`volume-max`）、播放结束动作（`keep-open`）、循环当前文件（`loop-file`）、默认速度、反交错、画面比例、字幕字号/位置/延迟、首选音频/字幕语言（`alang`/`slang`）、音频设备、截图目录与文件名模板、缓存目录、视频预览缩略图。改动即时下发 mpv，启动时自动应用；需要重启的设置（如语言）弹出“立即重启”系统提示。

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
