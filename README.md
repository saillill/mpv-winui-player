# mpv-winui-player

WinUI 3 + libmpv 播放器，附带 mpv-lazy 裁剪配置层。
A WinUI 3 + libmpv player with a trimmed mpv-lazy config layer.

## Features / 功能

- HDR/WCG 自动输出：composition 模式下自动检测显示器，写入 `user-data/mpvw/color-kind`，`profiles.conf` 自动切换（WCG→bt.2020，HDR→PQ/bt.2020/1000nit）。
- RTX Video HDR / NVIDIA VSR 自动开关（`hdr_auto.lua` / `vsr_auto.lua`，跳转时 `seek_hold.lua` 临时摘除）。
- 中英双语界面（`AppLang` + `Languages/*.json`，设置页切换，重启生效）。
- MediaInfo 信息（随包官方 CLI v26.05）。
- 命令行/协议开文件：`mpv-winui.exe "file"`、`mpv-winui://?file=<url 编码路径>`。
- 日志默认关闭（不生成 `mpv.log`/`hdr_auto.log`）。

## Quick Start / 快速开始

1. 下载 [Release](https://github.com/saillill/mpv-winui-player/releases) zip 并解压。
2. 部署配置：`powershell -File mpv-winui-lazy\deploy-config.ps1`
3. 运行 `mpv-winui.exe`。

## Config Tutorial / 主要配置

### HDR/WCG 自动切换（`profiles.conf`）

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

注意：`d3d11-output-csp=display-p3` 是非法值；HDR 必须设 `target-trc=pq` 等三项，否则不会真正进入 HDR。

### RTX HDR / VSR（`script-opts/`）

- `hdr_auto.conf`：`log=no`（默认），`mode=auto|on|off`。
- `mpvw_hdr_override.conf`：`mode=` 留空=跟随 App，`HDR`/`SDR` 强制覆盖。

### 快捷键（`input.conf`）

滚轮音量/跳转、`ESC` 全屏、`` ` `` 控制台、`F6/F7` 播放列表/轨道、`TAB` 统计、`Alt+i` MediaInfo、`Ctrl+1..0` 调色、`[ ] { }` 速度。`input_plus.lua` 已移除。

### 本地化 / MediaInfo / 日志

- 语言：设置页切换，或编辑 `Languages\<lang>.json`。
- MediaInfo：`stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`。
- 排障日志：`mpv.conf` 取消 `log-file` 注释并设 `msg-level=all=v`；`hdr_auto.conf` 设 `log=yes`。

## Build / 构建

环境：.NET 10 SDK、VS Build Tools（C++）、Windows App SDK 2.3.x。

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # 产出 dist\*.zip
```

`mpv-2.dll` 从 [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) 下载到 `mpv-winui\libs\`；CI 传 `/p:BuildMpvWinrtWithReference=true` 构建 C++ 引用。详细说明见 `.github/workflows/build.yml`。

## References / 引用项目

- 运行时：mpv (LGPL-2.1+/GPL-2.0+)、libplacebo (LGPL-2.1+)、Windows App SDK / WinUI 3 (MIT)、CsWinRT/CsWin32 (MIT)、NLog (BSD-3)、MediaInfo (BSD-2)、NUnit (MIT)、.NET (MIT)、ikas-mc/mpv-windows-builder。
- 配置层：hooke007/mpv_PlayKit（基线，未列出文件默认 UNLICENSED）、tsl0922/mpv-menu (GPL-2.0-only)、coverart/recent-menu/metadata-osd (MIT)、thumbfast (MPL-2.0)、mpv 自带 console/select/stats、Source Han Sans / LXGW WenKai 字体 (OFL-1.1)、着色器（以文件头为准）。

完整清单与许可证全文位置：[mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)

## License / 许可证

- 应用代码：LGPL-2.1（[LICENSE.txt](LICENSE.txt)，上游 ikas-mc/mpv-winui-player 同）。
- 配置层自创内容：LGPL-2.1-or-later；第三方组件以各自许可证为准（见 THIRD_PARTY_NOTICES.md）。
