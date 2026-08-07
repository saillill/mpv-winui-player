# mpv-winui-player

[简体中文](README_zh-CN.md)

A WinUI 3 media player that embeds [libmpv](https://github.com/mpv-player/mpv) through a C++/WinRT component, bundled with a trimmed config layer based on [mpv-lazy](https://github.com/hooke007/mpv_PlayKit).

## Links

- Releases: <https://github.com/saillill/mpv-winui-player/releases>
- Upstream app: [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
- Config base: [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit) (mpv-lazy)
- mpv: [mpv-player/mpv](https://github.com/mpv-player/mpv) · libplacebo: [haasn/libplacebo](https://github.com/haasn/libplacebo)

## Features

- HDR/WCG auto output: the app detects the display and writes `user-data/mpvw/color-kind`; `profiles.conf` switches output automatically (WCG→bt.2020, HDR→PQ/bt.2020/1000nit).
- RTX Video HDR / NVIDIA VSR auto toggling (`hdr_auto.lua` / `vsr_auto.lua`, temporarily removed while seeking).
- en-US / zh-CN UI: all strings come from `AppLang` + `Languages/*.json`; switch in Settings (restart required).
- MediaInfo: official CLI v26.05 bundled; works from the OSD/menu.
- Open files via command line (`mpv-winui.exe "file"`) or `mpv-winui://?file=<url-encoded path>`.
- Logs off by default (no `mpv.log` / `hdr_auto.log`).

## Quick Start

1. Download the zip from [Releases](https://github.com/saillill/mpv-winui-player/releases) and extract it.
2. Deploy the config layer:
   `powershell -File mpv-winui-lazy\deploy-config.ps1`
3. Run `mpv-winui.exe`.

## Configuration

### HDR/WCG auto profiles (`profiles.conf`)

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

Notes: `d3d11-output-csp=display-p3` is invalid; HDR requires the three `target-*` options or the driver never enters HDR.

### RTX HDR / VSR (`script-opts/`)

- `hdr_auto.conf`: `log=no` (default), `mode=auto|on|off`.
- `mpvw_hdr_override.conf`: `mode=` empty = follow the app; `HDR`/`SDR` to force.

### Keys (`input.conf`)

Wheel = volume/seek, `ESC` = fullscreen, `` ` `` = console, `F6/F7` = playlist/track info, `TAB` = stats, `Alt+i` = MediaInfo, `Ctrl+1..0` = color, `[ ] { }` = speed. `input_plus.lua` is not shipped.

### Localization / MediaInfo / Logs

- Language: Settings page, or edit `Languages\<lang>.json`.
- MediaInfo: `stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`.
- Troubleshooting logs: uncomment `log-file` in `mpv.conf` and set `msg-level=all=v`; set `log=yes` in `hdr_auto.conf`.

## Build

Requirements: .NET 10 SDK, VS Build Tools (C++), Windows App SDK 2.3.x.

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # -> dist\*.zip
```

`mpv-2.dll` is downloaded from [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) into `mpv-winui\libs\`; CI passes `/p:BuildMpvWinrtWithReference=true` (see `.github/workflows/build.yml`).

## License

- App code: LGPL-2.1 ([LICENSE.txt](LICENSE.txt), same as upstream).
- Config layer, project-written parts: LGPL-2.1-or-later; third-party components keep their own licenses (see [THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)).
