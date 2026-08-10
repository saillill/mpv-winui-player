# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[English](README.md)

> 基于 libmpv 的 WinUI 3 媒体播放器，内置基于 mpv-lazy 裁剪的配置层。重点解决
> 内嵌合成模式下的 HDR/WCG 正确输出、PotPlayer 式设置界面（约 184 项即时生效）、
> 8 语言本地化，以及一套完整的画中画窗口。

## 截图

| 亮色 | 暗色 |
|---|---|
| ![主界面](screenshot/main.jpg) | ![主界面（暗色）](screenshot/main-dark.jpg) |
| ![设置](screenshot/settings.png) | ![设置（暗色）](screenshot/settings-dark.png) |

![画中画](screenshot/pip.png)

![右键菜单](screenshot/menu.jpg)

## 功能亮点

- **引擎**：通过 `mpv_winrt` C++/WinRT 组件内嵌 libmpv，`vo=gpu-next` +
  `d3d11-output-mode=composition` 渲染——无窗口边框闪烁、原生 WinUI 叠加层、
  硬解（d3d11va）。
- **HDR / WCG**：应用读取 `DisplayInformation`，把
  `user-data/mpvw/color-kind`（`SDR` / `WCG` / `HDR`）和刷新率写入 mpv，
  `profiles.conf` 自动切换输出参数，修复上游在合成模式下 HDR 发灰的问题。
- **设置**：PotPlayer 式双栏窗口（左侧分类、右侧选项卡片），约 184 项选项。
  所有选项即改即生效；冲突项置灰、无效项黄色提示；列表选项显示本地化预设
  （不暴露原始 mpv 键值）；路径选项带文件夹选择器和“打开目录”按钮。
- **本地化**：8 种语言（en-US、zh-CN、ja-JP、ko-KR、de-DE、fr-FR、es-ES、
  ru-RU），覆盖应用界面、菜单栏和 153 项 mpv 右键菜单
  （通过 `user-data/mpvw/language` 联动）。
- **画中画**：独立无边框置顶窗口，DWM 圆角、固定尺寸、视频区任意拖动，
  复用全屏紧凑控制栏（时间、播放、音量、进度）。
- **视频预览**：thumbfast 通过随附的独立 `mpv.exe` 渲染缩略图，应用以圆角
  WinUI 卡片画在进度条上方。
- **鼠标输入**：视频区滚轮控制音量/跳转，鼠标按键遵循 `input.conf`
  （左键播放/暂停、双击全屏、X1/X2 播放列表上一首/下一首），与 mpv-lazy
  文档一致。

## 相对上游的改进

上游应用：[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)

| 方面 | 上游 | 本项目 |
|---|---|---|
| HDR/WCG 输出 | 仅 SDR 方案；合成模式 HDR 发灰 | 自动 `mpvw-sdr/wcg/hdr` 配置；WCG → bt.2020（修复非法 `display-p3`）；HDR → `target-trc=pq` + `target-prim=bt.2020` + `target-peak=1000`；RTX HDR 期间开启 `target-colorspace-hint=yes` |
| 本地化 | 仅英文硬编码 | `AppLang` + JSON，8 种语言，即时切换 |
| 播放器设置 | 极少 | 约 184 项分类选项，即时生效 + 启动应用 |
| MediaInfo | 未内置 | 内置官方 MediaInfo CLI v26.05（BSD-2-Clause） |
| 命令行 / 协议 | unpackaged 下失效 | 命令行与 `mpv-winui://` 激活均已修复并验证 |
| 日志 | mpv 默认全量 verbose | 默认关闭（`log-file` 注释、`hdr_auto` `log=no`） |
| 配置层 | 不随包发布 | `mpv-winui-lazy/`（基于 mpv-lazy）：HDR/WCG 配置、RTX HDR/VSR 脚本、干净按键绑定、MediaInfo 配置 |
| 构建发布 | 手工 | `build.ps1` / `package.ps1`；GitHub Actions 免证书产出 unpackaged + Release zip |

## 快速开始

1. 从 [Releases 页面](https://github.com/saillill/mpv-winui-player/releases)
   下载 `mpv-winui-win-x64-Release.zip`。
2. 解压到任意目录（Windows 10/11 x64）。
3. 首次运行前部署配置层：

   ```powershell
   powershell -File mpv-winui-lazy\deploy-config.ps1
   ```

4. 运行 `mpv-winui.exe`。可通过菜单、拖放、命令行或 URL 协议打开文件：

   ```powershell
   mpv-winui.exe "D:\Videos\movie.mkv"
   mpv-winui://?file=D%3A%5CVideos%5Cmovie.mkv
   ```

## 配置

### HDR / WCG 自动配置（`mpv-winui-lazy/profiles.conf`）

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

注意：`d3d11-output-csp=display-p3` 是非法值；HDR 必须同时设置三个
`target-*` 选项，否则交换链是 PQ 而渲染管线仍是 SDR，驱动不会切换 HDR。

### RTX Video HDR / NVIDIA VSR

- `script-opts/hdr_auto.conf`：默认 `log=no`；`mode=auto|on|off`。
- `script-opts/mpvw_hdr_override.conf`：`mode=` 留空 = 跟随应用；
  `HDR` / `SDR` 强制覆盖。
- `script-opts/vsr_auto.conf`、`script-opts/seek_hold.conf`：自动 NVIDIA VSR
  与拖动进度条时的窗口防抖。

### 按键绑定（`mpv-winui-lazy/input.conf`）

视频区滚轮：音量（上下）/ 跳转（左右）· 左键：播放/暂停 · 双击：全屏 ·
X1/X2：播放列表上一首/下一首 · `` ` ``：控制台 · F6/F7：播放列表/轨道信息 ·
TAB：统计 · Alt+i：MediaInfo · Ctrl+1..0：调色 · w/W：画面缩放 ·
`[ ] { }`：倍速。

### 本地化 / MediaInfo / 日志

- 语言：设置页切换，或编辑 `Languages\<lang>.json`（键为 `AppLang` 属性名）。
  右键菜单通过 `user-data/mpvw/language` 跟随界面语言；8 种语言在
  `dyn_menu.lua` / `dynamic_menu.lua` 中全覆盖。
- MediaInfo：`script-opts/stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`。
- 排障：取消 `mpv.conf` 中 `log-file` 注释并设 `msg-level=all=v`；
  `hdr_auto.conf` 设 `log=yes`。

### 设置窗口

分类：桌面（界面/背景/字体/状态栏排版/语言日志/文件关联）、播放、记忆播放、
视频、音频、字幕、窗口、缓存、网络、输入、快捷键、OSD、截屏、测试。
所有选项即改即生效；底部有“重置当前分类 / 重置所有设置”。左侧搜索支持
拼音、日文罗马音和韩文罗马化匹配。

## 构建与发布

| 依赖 | 说明 |
|---|---|
| Windows 10/11 x64 | 目标平台 |
| [.NET 10 SDK](https://dotnet.microsoft.com/) | 构建 C# WinUI 3 应用 |
| Visual Studio Build Tools（C++ 工作负载） | 构建 `mpv_winrt`（依赖 `VCTargetsPath`） |
| Windows App SDK 2.3.x | NuGet 还原 |
| `mpv-2.dll` | 从 [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) 下载到 `mpv-winui\libs\` |

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # -> dist\mpv-winui-win-x64-Release.zip
```

CI（`.github/workflows/build.yml`，手动 `workflow_dispatch`）使用相同流程；
未配置签名证书时跳过 MSIX，上传 unpackaged 输出与 Release zip。

## 上游参考与库调用

### 项目来源

| 项目 | 用途 |
|---|---|
| [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) | 上游应用基线（WinUI 外壳 + `mpv_winrt` 组件） |
| [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy） | 配置层基线：`mpv.conf`、`profiles.conf`、`input.conf`、脚本、着色器 |
| [mpv-player/mpv](https://github.com/mpv-player/mpv) | 核心播放引擎（libmpv + thumbfast 用独立 CLI） |
| [haasn/libplacebo](https://github.com/haasn/libplacebo) | mpv `gpu-next` VO 内部渲染 |
| [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) | Windows `mpv-2.dll` 构建 |
| [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) | thumbfast 随附的独立 `mpv.exe` |
| [tsl0922/mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) | `dyn_menu.lua` / `dialog.lua`（mpv 右键菜单数据） |
| [po5/thumbfast](https://github.com/po5/thumbfast) | 缩略图预览引擎 |
| [CogentRedTester/mpv-coverart](https://github.com/CogentRedTester/mpv-coverart) | 封面加载 |
| [natural-harmonia-gropius/recent-menu](https://github.com/natural-harmonia-gropius/recent-menu) | 最近打开菜单 |
| [vc-01/metadata-osd](https://github.com/vc-01/metadata-osd) | 元数据 OSD |
| [MediaArea/MediaInfo](https://mediaarea.net/en/MediaInfo) | MediaInfo CLI（工具菜单） |
| [apades/dmMiniPlayer](https://github.com/apades/dmMiniPlayer) | 画中画交互参考（documentPictureInPicture） |

### 应用使用的运行时库

| 库 | 许可 | 用途 |
|---|---|---|
| Windows App SDK / WinUI 3 | MIT | UI 框架 |
| CsWinRT / CsWin32 | MIT | C#/WinRT 互操作与 Win32 P/Invoke 生成 |
| NLog | BSD-3 | 日志 |
| .NET | MIT | 托管运行时 |
| libmpv / libplacebo | LGPL-2.1+ | 播放与渲染 |
| MediaInfo CLI | BSD-2-Clause | 文件元数据 |
| 思源黑体 / LXGW WenKai Mono Lite | SIL OFL-1.1 | 随附可选字体 |
| 着色器（Anime4K、FSRCNNX、nnedi3、NVIDIA 等） | 见文件头 / THIRD_PARTY_NOTICES | 可选放大/增强 |
| VapourSynth 模板（`vs/*.vpy`） | mpv-lazy 维护 | 可选 VapourSynth 工作流 |

## 开源协议合规

- 应用代码：**LGPL-2.1**（[LICENSE.txt](LICENSE.txt)，与上游一致）。
- 配置层自有部分：**LGPL-2.1-or-later**；第三方组件保留各自许可。
- `dyn_menu.lua` 与 `dialog.lua` 为 **GPL-2.0-only** 源码脚本，随源码保留来源
  声明（已移除随附的 `menu.dll` GPL 二进制——自带 mpv 走原生 `menu-data` 路径）。
- 随附 `mpv.exe` 为 **GPL-2.0+**（thumbfast 调用的独立程序），源码见上游仓库。
- 配置层基于 mpv-lazy；其 `LICENSE.MD` 将未列出的文件视为 UNLICENSED，
  因此对直接复制的文件保留来源声明。
- 完整组件清单与许可文本：
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)、
  [fonts/OFL-1.1.txt](mpv-winui-lazy/fonts/OFL-1.1.txt)、
  [licenses/MediaInfo-BSD-2-Clause.txt](mpv-winui-lazy/licenses/MediaInfo-BSD-2-Clause.txt)。

## 已知限制

- `display-info.log` 中显示器名称字段在多显示器环境下可能为空
  （HDR 类型与刷新率仍会正常跟踪）。
- `keep-open=always` 按设计在文件播完后暂停；恢复播放后 `loop-playlist`
  再继续循环。
- unpackaged 模式首次运行需执行 `deploy-config.ps1`，把配置层同步到
  `%LOCALAPPDATA%\mpv-winui\mpv`。
