# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[简体中文](README_zh-CN.md)

> A WinUI 3 media player powered by libmpv (C++/WinRT), with a curated config layer trimmed from mpv-lazy. Focused on correct HDR/WCG output in embedded mode, multilingual UI (8 languages), and a clean out-of-the-box experience.

## Brief Introduction

[mpv-winui-player](https://github.com/saillill/mpv-winui-player) embeds [libmpv](https://github.com/mpv-player/mpv) into a native WinUI 3 shell through a C++/WinRT component (`mpv_winrt`). The mpv configuration is provided by a dedicated config layer (`mpv-winui-lazy/`) based on [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit) (mpv-lazy), so the player works without extra setup:

- Rendering: `vo=gpu-next` + `d3d11-output-mode=composition` (no window border flicker, native WinUI overlay).
- Display detection: the app reads `DisplayInformation` and exposes `user-data/mpvw/color-kind` (`SDR` / `WCG` / `HDR`) and `user-data/mpvw/refresh-rate` to mpv; `profiles.conf` switches output parameters automatically.
- Deployment: unpackaged build, one zip, `deploy-config.ps1` syncs the config layer to `%LOCALAPPDATA%\mpv-winui\mpv`.

Related projects:

- Upstream app: [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
- Config base: [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)
- Core engine: [mpv-player/mpv](https://github.com/mpv-player/mpv) · [haasn/libplacebo](https://github.com/haasn/libplacebo)
- Windows libmpv builds: [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder)

## UI Features

- **Menu bar**: File (open file/folder/URL/clipboard, DVD/BD, watch history, watch later, add subtitle, screenshots, restart, quit), View (playlist, fullscreen/full-window, options, open config/mpv folders), Help (about).
- **Player controls**: play/pause, skip, shuffle, repeat, playback rate, audio/video track switching, zoom, full window/full screen, volume, seek bar with thumbnails.
- **Playlist panel**: context menu (play, move, remove, copy title/path, open file location), watch history and watch later.
- **Right-click menu**: mpv data menus (153 items, tsl0922/mpv-menu layout) plus fixed File/Window items; filters are shown directly under "Filters & Enhance" (no extra submenu level). Menu titles, dynamic items and the mpv `select` sub-menus (track/chapter/edition/audio device/key bindings/history/watch later/properties) are fully translated in all 8 languages via `dyn_menu.lua` / `dynamic_menu.lua` / `select.lua`, switched automatically by `user-data/mpvw/language`. Related OSD feedback (RTX HDR modes, cleared saved properties, thumbnail toggles) is localized as well.
- **Settings window**: two-pane layout (left categories, right options) with General / Playback / Video / Audio / Subtitle / Screenshot / Advanced / Paths. It ships ~78 PotPlayer-style options — every option uses a selectable list with localized labels (never raw mpv values), including a runtime audio-device dropdown populated from mpv and screenshot filename-template presets; boolean switches show localized On/Off, the language list shows each language in its own name (中文 / 日本語 / 한국어 ...), path options have a native Windows folder-picker "Browse" button plus an "Open" button that opens the folder in File Explorer, and explanations appear as a caption under the title (Windows Settings style) only when they add information. Bundled-script options are grouped under a "Plugin options" caption at the bottom of their category. Options that conflict are disabled (e.g. ASS-to-margins while subtitles are blended), and options that may become ineffective show a yellow note (e.g. interpolation without display-resample sync, per-format screenshot settings). Descriptions include recommended values and known conflicts. Changes apply to mpv live; restart-required settings (e.g. language) show a restart prompt.

## What's New vs. Upstream

| Area | Upstream `ikas-mc/mpv-winui-player` | This project |
|---|---|---|
| HDR/WCG output | SDR-only example workaround; HDR washed out in composition mode | Auto profiles `mpvw-sdr/wcg/hdr`; WCG→`bt.2020` (invalid `display-p3` fixed); HDR→`target-trc=pq` + `target-prim=bt.2020` + `target-peak=1000`; `target-colorspace-hint=yes` while RTX HDR is active |
| Localization | English-only hardcoded strings, no switch | `AppLang` + `Languages/*.json`, 8 languages (en-US / zh-CN / ja-JP / ko-KR / de-DE / fr-FR / es-ES / ru-RU), switch in Settings (restart) |
| Player settings | Minimal | ~78 categorized options applied live and on startup: hwdec, volume/volume-max, keep-open, loop file/playlist, speed, save/resume playback position, deinterlace, aspect, scaling/downscaling algorithms, rotation, deband, video sync, interpolation, HR seek (+framedrop), tone mapping, dithering, audio language/device (live dropdown)/channels/delay/exclusive mode/pitch/downmix/audio-file display, subtitle size/position/delay/language/ASS override/blur/font (system-default + Microsoft fonts)/font provider/codepage/fallback/blend/scale-with-window/ASS margins/image-sub stretching, OSD font/size/on-seek/duration, ICC auto profile + 3D LUT, video output levels, disk cache, screenshot directory/template (presets)/format/JPEG/PNG/WebP/bit depth/software capture, cache folder, NVIDIA VSR / RTX Video HDR / seek window-hold toggles; localized values, plugin section, conflict-aware disabling, yellow ineffectiveness warnings, folder picker + open-folder buttons, non-redundant descriptions with recommendations |
| MediaInfo | Not bundled | Official MediaInfo CLI v26.05 (BSD-2-Clause) bundled |
| Opening files | Protocol/CLI activation broken in unpackaged mode | Command line and `mpv-winui://` both fixed and verified |
| Logging | mpv logs verbose by default | Off by default (`log-file` commented, `hdr_auto` `log=no`) |
| Config layer | Not shipped | `mpv-winui-lazy/` (mpv-lazy based): HDR/WCG profiles, RTX HDR/VSR scripts, clean key bindings, MediaInfo config |
| Build & release | Manual | `build.ps1` / `package.ps1`; GitHub Actions produces the Release zip without a signing certificate |

## Quick Start

1. Download `mpv-winui-win-x64-Release.zip` from the [Releases page](https://github.com/saillill/mpv-winui-player/releases).
2. Extract it anywhere (Windows 10/11 x64).
3. Deploy the config layer (first run):

```powershell
powershell -File mpv-winui-lazy\deploy-config.ps1
```

4. Run `mpv-winui.exe`. Open files from the menu, drag & drop, the command line, or the URL protocol:

```powershell
mpv-winui.exe "D:\Videos\movie.mkv"
mpv-winui://?file=D%3A%5CVideos%5Cmovie.mkv
```

## Configuration

### HDR / WCG auto profiles (`mpv-winui-lazy/profiles.conf`)

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

Notes: `d3d11-output-csp=display-p3` is not a valid value; HDR needs all three `target-*` options, otherwise the swap chain is PQ but the render pipeline stays SDR and the driver never switches to HDR.

### RTX Video HDR / NVIDIA VSR (`mpv-winui-lazy/script-opts/`)

- `hdr_auto.conf`: `log=no` by default; `mode=auto|on|off`.
- `mpvw_hdr_override.conf`: `mode=` empty = follow the app; `HDR` / `SDR` force an override.

### Key bindings (`mpv-winui-lazy/input.conf`)

Wheel: volume/seek · `` ` ``: console · `F6/F7`: playlist/track info · `TAB`: stats · `Alt+i`: MediaInfo · `Ctrl+1..0`: color adjust · `w/W`: panscan · `[ ] { }`: speed. (`input_plus.lua` is intentionally not shipped.)

### Localization / MediaInfo / logs

- Language: Settings page (each language is shown in its own name) or edit `Languages\<lang>.json` (keys are `AppLang` property names). Right-click menus use the same language through `user-data/mpvw/language`; all 8 languages are fully covered in `dyn_menu.lua` and `dynamic_menu.lua`.
- MediaInfo: `script-opts/stats_mediainfo.conf` → `mediainfo_path=~~/MediaInfo.exe`.
- Troubleshooting: uncomment `log-file` in `mpv.conf` and set `msg-level=all=v`; set `log=yes` in `hdr_auto.conf`.

### Player settings (Settings window)

Hardware decoding (`hwdec`), max/startup volume (`volume-max`/`volume`), after-playback behavior (`keep-open`), loop file/playlist (`loop-file`/`loop-playlist`), default speed, save/resume playback position (`save-position-on-quit`/`resume-playback`), deinterlace, aspect ratio, scaling/downscaling algorithms (`scale`/`dscale`), rotation, deband, linear downscaling, sigmoid upscaling, video sync, interpolation, HR seek (`hr-seek`) and seek framedrop (`hr-seek-framedrop`), HDR tone mapping, dither depth, preferred audio/subtitle languages (`alang`/`slang`), audio device, channels, delay, exclusive mode, pitch correction, downmix normalization, auto-loaded audio files, audio-file display (`audio-display`), subtitle font size / position / delay / font (system default, Segoe UI, Microsoft YaHei, Arial, Times New Roman, Consolas, bundled Source Han Sans SC / LXGW WenKai) / font provider (`sub-font-provider`) / codepage / outline / shadow / ASS override / blur / embedded fonts / margins / ASS margins (`sub-ass-force-margins`) / image-sub stretching (`stretch-image-subs-to-screen`) / fallback (`subs-fallback`) / blend mode (`blend-subtitles`) / scale-with-window, OSD font / font size / on-seek display (`osd-on-seek`) / duration, ICC auto profile and 3D LUT size, video output levels, disk cache, screenshot folder (folder picker + open in Explorer) & filename template / format / JPEG quality / PNG compression / WebP quality / bit depth / colorspace tag / software capture (`screenshot-sw`), cache folder, video preview thumbnails. Video previews are driven by thumbfast through the app's progress bar; the Release zip bundles a standalone `mpv.exe` (fetched by `package.ps1`) that thumbfast spawns as its decoding child. An **Advanced** category groups low-level color/stream/script options: cache-on-disk, video output levels, ICC profile, ICC 3D LUT, audio display, subtitle fallback, blend subtitles, ASS scale-with-window, OSD font/size/on-seek/duration, auto NVIDIA VSR, RTX Video HDR mode, and keep-window-size-while-seeking. Bundled-script options sit under a "Plugin options" caption at the bottom of their category. Every value is displayed with a localized label; explanations appear under the title only when they add information, and mention recommended values; options that conflict are disabled and options that may be ineffective show a yellow note. Changes are sent to mpv immediately and applied on startup; restart-required settings (e.g. language) show a dialog with "Restart now".

### Help / About

The Help menu has a dedicated "mpv Official Manual" item (<https://mpv.io/manual/master/>); the About dialog links the mpv GitHub repository and this project (<https://github.com/saillill/mpv-winui-player>).

## Build

### Environment

| Requirement | Notes |
|---|---|
| Windows 10/11 x64 | target platform |
| [.NET 10 SDK](https://dotnet.microsoft.com/) | builds the C# WinUI 3 app |
| Visual Studio Build Tools (C++ workload) | builds `mpv_winrt` (`VCTargetsPath` is a VS component) |
| Windows App SDK 2.3.x | restored via NuGet |
| `mpv-2.dll` | downloaded from [ikas-mc/mpv-windows-builder](https://github.com/ikas-mc/mpv-windows-builder) into `mpv-winui\libs\` |

### Build & package

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\package.ps1 -Configuration Release -Platform x64   # -> dist\mpv-winui-win-x64-Release.zip
```

CI (`.github/workflows/build.yml`, manual `workflow_dispatch`) builds the same way; without certificate secrets it skips MSIX and uploads the unpackaged output plus the Release zip.

### References

- Runtime: mpv (LGPL-2.1+ / GPL-2.0+), libplacebo (LGPL-2.1+), Windows App SDK / WinUI 3 (MIT), CsWinRT / CsWin32 (MIT), NLog (BSD-3), MediaInfo (BSD-2), .NET (MIT), NUnit (MIT).
- Config layer: hooke007/mpv_PlayKit (baseline; unlisted files default UNLICENSED per its LICENSE.MD), tsl0922/mpv-menu (GPL-2.0-only), coverart / recent-menu / metadata-osd (MIT), thumbfast (MPL-2.0), mpv's console/select/stats scripts, Source Han Sans / LXGW WenKai fonts (OFL-1.1), shaders (see file headers).
- Full list and license texts: [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md).

## License

- App code: **LGPL-2.1** ([LICENSE.txt](LICENSE.txt), same as upstream).
- Config layer, project-written parts: **LGPL-2.1-or-later**; third-party components keep their own licenses (see [THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)).
