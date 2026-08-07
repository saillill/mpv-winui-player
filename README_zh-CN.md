# mpv-winui-player

[English](README.md)

一个把 [libmpv](https://github.com/mpv-player/mpv) 塞进 WinUI 3 的播放器（C++/WinRT 组件），配置层基于 [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) 裁剪，_解压即用，用后真香_。

## 相关项目

- 发布下载：[Releases](https://github.com/saillill/mpv-winui-player/releases)
- 上游应用：[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
- 配置来源：[hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy）
- 播放核心：[mpv](https://github.com/mpv-player/mpv) · 渲染：[libplacebo](https://github.com/haasn/libplacebo)

## 功能简述

- HDR/WCG 自动输出：应用检测显示器并写入 `user-data/mpvw/color-kind`，`profiles.conf` 自动切换（WCG→bt.2020，HDR→PQ/bt.2020/1000nit）。
- RTX Video HDR / NVIDIA VSR 自动开关（`hdr_auto.lua` / `vsr_auto.lua`，跳转/拖拽时临时摘除）。
- 中英双语界面：所有文案走 `AppLang` + `Languages/*.json`，设置页切换，重启生效。
- MediaInfo：随包官方 CLI v26.05，OSD/菜单直接用。
- 开文件：命令行 `mpv-winui.exe "文件"` 或协议 `mpv-winui://?file=<url 编码路径>`。
- 日志默认关闭（不生成 `mpv.log` / `hdr_auto.log`）。

## 快速开始

1. 从 [Releases](https://github.com/saillill/mpv-winui-player/releases) 下载 zip 解压。
2. 部署配置层：`powershell -File mpv-winui-lazy\deploy-config.ps1`
3. 运行 `mpv-winui.exe`。

## 配置说明

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

注意：`d3d11-output-csp=display-p3` 是非法值；HDR 必须带三个 `target-*`，否则驱动不会真正进入 HDR。

### RTX HDR / VSR（`script-opts/`）

- `hdr_auto.conf`：`log=no`（默认），`mode=auto|on|off`。
- `mpvw_hdr_override.conf`：`mode=` 留空=跟随 App；`HDR`/`SDR` 强制覆盖。

### 快捷键（`input.conf`）

滚轮=音量/跳转、`ESC`=全屏、`` ` ``=控制台、`F6/F7`=播放列表/轨道信息、`TAB`=统计、`Alt+i`=MediaInfo、`Ctrl+1..0`=调色、`[ ] { }`=速度。`input_plus.lua` 已移除。

### 本地化 / MediaInfo / 日志

- 语言：设置页切换，或直接编辑 `Languages\<lang>.json`。
- MediaInfo：`stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`。
- 排障日志：`mpv.conf` 取消 `log-file` 注释并设 `msg-level=all=v`；`hdr_auto.conf` 设 `log=yes`。

## 构建

环境：.NET 10 SDK、VS Build Tools（C++）、Windows App SDK 2.3.x。

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # 产出 dist\*.zip
```

`mpv-2.dll` 从 [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) 下载到 `mpv-winui\libs\`；CI 传 `/p:BuildMpvWinrtWithReference=true`（见 `.github/workflows/build.yml`）。

## 许可证

- 应用代码：LGPL-2.1（[LICENSE.txt](LICENSE.txt)，与上游一致）。
- 配置层自创内容：LGPL-2.1-or-later；第三方组件以各自许可证为准（见 [THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)）。
