# mpv-winui-player

WinUI 3 媒体播放器，C++/WinRT 组件（`mpv_winrt`）内嵌 [libmpv](https://github.com/mpv-player/mpv)，并附带一套从 [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy）裁剪的配置层。

A WinUI 3 media player that embeds [libmpv](https://github.com/mpv-player/mpv) through a C++/WinRT component (`mpv_winrt`), shipped with a curated config layer trimmed from [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit) (mpv-lazy).

---

## Highlights / 功能特性

- **HDR/WCG 自动输出**：`d3d11-output-mode=composition` 嵌入模式下 mpv 拿不到显示器信息，应用用 `DisplayInformation` 检测并把 `SDR/WCG/HDR` 写入 `user-data/mpvw/color-kind`，`profiles.conf` 的 auto-profile 自动切换输出参数（WCG→bt.2020，HDR→PQ/bt.2020/1000nit，发白问题已修复）。
- **RTX Video HDR / NVIDIA VSR**：`hdr_auto.lua`（仅 SDR 片源 + 屏幕 HDR 时挂 `d3d11vpp=nvidia-true-hdr`）、`vsr_auto.lua`，跳转/拖拽时 `seek_hold.lua` 临时摘除。
- **本地化**：全部界面文案（菜单栏、播放控制、播放列表、设置）经 `AppLang` + `Languages/*.json` 加载，内置 en-US / zh-CN；unpackaged WinUI 3 不支持 x:Uid/x:Bind 运行时切语言，故用代码后置绑定（官方 issues：WindowsAppSDK #3052、microsoft-ui-xaml #10430）。
- **MediaInfo**：随包附官方 MediaInfo CLI v26.05（BSD-2-Clause），`工具 > MediaInfo` 可用。
- **开文件**：支持命令行 `mpv-winui.exe "file"` 与 `mpv-winui://?file=<url 编码路径>` 协议（unpackaged 激活已修复）。
- **日志默认关闭**：默认不生成 `mpv.log` / `hdr_auto.log`；排障时按教程开启。

- **Automatic HDR/WCG output**: with `d3d11-output-mode=composition`, mpv cannot query the display, so the app detects it via `DisplayInformation` and writes `SDR/WCG/HDR` to `user-data/mpvw/color-kind`. `profiles.conf` auto-profiles switch output params (WCG→bt.2020, HDR→PQ/bt.2020/1000nit; the washed-out issue is fixed).
- **RTX Video HDR / NVIDIA VSR**: `hdr_auto.lua` (enables `d3d11vpp=nvidia-true-hdr` only for SDR sources while the screen is HDR), `vsr_auto.lua`, and `seek_hold.lua` which temporarily removes them while seeking.
- **Localization**: every UI string (menu bar, player controls, playlist, settings) is loaded through `AppLang` + `Languages/*.json` (en-US / zh-CN shipped). Unpackaged WinUI 3 cannot switch `x:Uid`/`x:Bind` resources at runtime, so strings are applied from `AppLang` in code.
- **MediaInfo**: the official MediaInfo CLI v26.05 (BSD-2-Clause) is bundled; the MediaInfo OSD/menu works out of the box.
- **Opening files**: command line `mpv-winui.exe "file"` and `mpv-winui://?file=<url-encoded path>` are both supported (unpackaged activation fixed).
- **Logs off by default**: no `mpv.log`/`hdr_auto.log` is created unless you enable them (see tutorial below).

---

## Quick Start / 快速开始

1. 从 [Releases](https://github.com/saillill/mpv-winui-player/releases) 下载 `mpv-winui-win-x64-Release.zip` 并解压。
2. 运行 `mpv-winui.exe`（首次建议先部署配置层）。
3. 部署 mpv 配置到 `%LOCALAPPDATA%\mpv-winui\mpv`：

```powershell
powershell -File mpv-winui-lazy\deploy-config.ps1
```

1. Download `mpv-winui-win-x64-Release.zip` from [Releases](https://github.com/saillill/mpv-winui-player/releases) and extract it.
2. Run `mpv-winui.exe` (deploying the config layer first is recommended).
3. Deploy the mpv config to `%LOCALAPPDATA%\mpv-winui\mpv`:

```powershell
powershell -File mpv-winui-lazy\deploy-config.ps1
```

> `deploy-config.ps1` uses robocopy `/MIR`: it syncs the source tree and deletes stale files. Runtime data (`_cache/`, `cache/`, `*.log`, `recent.json`, `saved-props.json`, `*.bak*`) is preserved.

---

## Configuration Tutorial / 配置教程

### 1) HDR/WCG 自动切换（`profiles.conf`）

应用在启动和显示器变化时把检测结果写入：

```ini
user-data/mpvw/color-kind : SDR | WCG | HDR
user-data/mpvw/refresh-rate : 60
```

`profiles.conf` 里的 auto-profile 按此切换（示例）：

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

> 说明：`d3d11-output-csp=display-p3` 是非法值（启动即报错），WCG 用 `bt.2020`；HDR 必须同时设 `target-trc=pq` / `target-prim=bt.2020` / `target-peak=1000`，否则交换链虽建为 PQ，渲染仍按 SDR 走、驱动不会进入 HDR。

### 2) RTX Video HDR / VSR（`scripts/` + `script-opts/`）

- `script-opts/hdr_auto.conf`：`log=no` 默认关闭日志；`mode=auto|on|off` 可强制。
- 滤镜生效期间 `target-colorspace-hint=yes`（固定），避免 composition 模式下目标色域失效导致回退 sRGB/发白。
- `script-opts/mpvw_hdr_override.conf`：`mode=` 留空=跟随 App；`HDR`/`SDR` 可强制覆盖 App 检测。

### 3) 快捷键（`input.conf`）

默认包含：滚轮音量/跳转、`ESC` 退出全屏、`` ` `` 控制台、`F6/F7` 播放列表/轨道信息、`TAB` 统计、`Alt+i` MediaInfo、`Ctrl+1..0` 调色、`w/W` 去黑边、`[ ] { }` 速度等。`input_plus.lua` 已移除（F3-F5 等绑定不存在）。

### 4) 本地化（`Languages/*.json`）

设置页 → 语言 → `en-US`/`zh-CN`（重启生效）。也可直接编辑 `mpv-winui\Languages\<lang>.json`（键=`AppLang` 属性名）。

### 5) MediaInfo

`stats_mediainfo.conf` 的 `mediainfo_path=~~/MediaInfo.exe` 指向配置目录内的官方 CLI（随包/随配置层部署）。

### 6) 排障日志

`mpv.conf` 取消 `log-file` 注释并把 `msg-level` 改为 `all=v`（注意：`--log-file` 会把日志级别强制提到 verbose，`msg-level=info` 这类裸值非法）；`hdr_auto.conf` 的 `log=yes` 开启滤镜日志。

---

## Build / 构建

Requirements / 环境：.NET 10 SDK、Visual Studio Build Tools（C++ 工作负载）、Windows App SDK 2.3.x。

```powershell
# 先用 VS MSBuild 编 C++ 组件，再用 dotnet 编 C# 应用
.\build.ps1 -Configuration Release -Platform x64
```

Output / 输出：`mpv-winui\mpv-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\`

Notes / 说明：
- `dotnet build` 无法编译 C++ 组件（`VCTargetsPath` 属于 VS 组件），本地默认引用 `mpv-winui\bin\<Platform>\<Configuration>\mpv_winrt\` 的预编译产物；CI 传 `/p:BuildMpvWinrtWithReference=true` 恢复 ProjectReference。
- `mpv-2.dll`（libmpv）从 [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) 下载到 `mpv-winui\libs\`（见 `.github/workflows/build.yml`）。
- 打包：`package.ps1 -Configuration Release -Platform x64` 产出 `dist\mpv-winui-win-x64-Release.zip`（标准 zip 路径、剔除 PDB、附带配置层）。
- CI（GitHub Actions，`workflow_dispatch`）：无签名证书时自动跳过 MSIX，产出 unpackaged 目录 + Release zip；配置 `BASE64_ENCODED_PFX`/`Pfx_Key` secrets 后启用 MSIX。

---

## References / 引用项目

### 应用 / Runtime

| Project | Role | License |
|---|---|---|
| [mpv-player/mpv](https://github.com/mpv-player/mpv) | 播放核心（libmpv v0.41.0-615） | 大部分 LGPL-2.1-or-later，部分 GPL-2.0-or-later（见其 `Copyright`） |
| [haasn/libplacebo](https://github.com/haasn/libplacebo) | `gpu-next` 渲染（v7.364.0） | LGPL-2.1-or-later |
| [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) | Windows libmpv 构建 | 见其仓库 |
| [Microsoft.WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK)（WinUI 3 等 2.x 包） | UI 框架/运行时 | MIT |
| [microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml) | WinUI 3 | MIT |
| [microsoft/CsWinRT](https://github.com/microsoft/CsWinRT) / [microsoft/CsWin32](https://github.com/microsoft/CsWin32) | C#/WinRT 互操作 | MIT |
| [NLog](https://github.com/NLog/NLog) | 日志 | BSD-3-Clause |
| [MediaArea/MediaInfo](https://mediaarea.net/en/MediaInfo) | MediaInfo CLI v26.05 | BSD-2-Clause |
| [NUnit](https://nunit.org/)（`mpv-winrt-test`） | 测试 | MIT |
| [.NET](https://dotnet.microsoft.com/) | 运行时（net10.0） | MIT |

### 配置层 / Config layer

| Project | Content | License |
|---|---|---|
| [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy） | 配置基线（`mpv.conf`/`profiles.conf`/`input.conf`/脚本） | 各文件头为准；未列出文件其 LICENSE.MD 默认 UNLICENSED |
| [tsl0922/mpv-menu](https://github.com/tsl0922/mpv-menu) | `dyn_menu.lua`/`dialog.lua`/`menu.dll` | GPL-2.0-only |
| [CogentRedTester/mpv-coverart](https://github.com/CogentRedTester/mpv-coverart) | `coverart.lua` | MIT |
| [natural-harmonia-gropius/recent-menu](https://github.com/natural-harmonia-gropius/recent-menu) | `recentmenu.lua` | MIT |
| [po5/thumbfast](https://github.com/po5/thumbfast) | `thumbfast.lua` | MPL-2.0 |
| [vc-01/metadata-osd](https://github.com/vc-01/metadata-osd) | `metadata_osd.lua` | MIT |
| [mpv-player/mpv](https://github.com/mpv-player/mpv) | `console.lua`/`select.lua`/`stats.lua` | 见各文件头（LGPL-2.1+/ISC 风格） |
| [Adobe/Source Han Sans](https://github.com/adobe-fonts/source-han-sans) / [lxgw/LxgwWenKai](https://github.com/lxgw/LxgwWenKai) | OSD 字体 | SIL OFL-1.1（全文 `fonts/OFL-1.1.txt`） |
| shaders（Anime4K/FSRCNNX/nnedi3/NVIDIA 等） | 可选着色器 | 以各文件头为准 |

完整第三方清单与许可证全文位置见 [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)。

---

## License / 许可证

- 上游 [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)（本仓库基线）以 **LGPL-2.1** 发布（`LICENSE.txt` 为 LGPL v2.1 全文）。
- 本仓库应用代码沿用 **LGPL-2.1**（见 [LICENSE.txt](LICENSE.txt)）。
- 配置层 `mpv-winui-lazy/` 中本项目自创内容以 **LGPL-2.1-or-later** 发布；第三方组件版权归原作者，许可证以其文件头/上游仓库为准（见 [THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)）。
