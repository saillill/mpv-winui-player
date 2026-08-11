# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[简体中文](README_zh-CN.md)

> This is a fork of
> [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player), a
> graphical mpv player for Windows. It keeps the real mpv playback engine and
> wraps it in a clean WinUI 3 interface — no command line required, and the
> things you use every day are one click away.

## Installation

Two options:

- **MSI installer**: `dist\mpv-winui-setup-x64-<version>.msi`. Per-user install
  into `%LocalAppData%\Programs\mpv-winui-player` (no admin rights), Start Menu
  shortcut created automatically. Newer versions upgrade in place; uninstall
  keeps your playback data, logs and config in `%LOCALAPPDATA%\mpv-winui`.
- **Portable zip**: `dist\mpv-winui-win-x64-Release.zip` — extract and run
  `mpv-winui.exe`.

On first run both deploy the bundled mpv config (`mpv-winui-lazy\`) into
`%LOCALAPPDATA%\mpv-winui\mpv` automatically. All later mpv config edits, menu
overrides (`menus.json`) and key bindings live in that directory and survive
upgrades/re-deploys.

## Features

(Screenshots coming soon)

- Multiple UI languages: English, 简体中文, 日本語, 한국어, Deutsch, Français,
  Español, Русский — switching takes effect immediately.
- Control bar: play/pause, progress, volume, speed and track switching in one
  bar; hover the progress bar to see video preview thumbnails.
- Settings: the most commonly used mpv options are available as a graphical
  page, clearly categorized and searchable, and changes apply immediately —
  no need to memorize commands and parameters.
- Right-click menu: the full mpv command menu, translated into 8 languages.
- Picture-in-picture: a small always-on-top window that opens at the
  bottom-right of your screen; move it, resize it, and closing it quits the
  player.
- Filters: ships with mpv-lazy preset shaders and frame-interpolation,
  upscaling and denoising scripts, and you can add your own filters and
  scripts.

## What it can do

- Open media the way you like: menu, drag and drop, command line, or the
  `mpv-winui://` link protocol.
- Plays the same formats as mpv: local files, network streams, DVDs and
  Blu-rays (as supported by mpv).
- Hardware decoding is on by default when available, and HDR/WCG content is
  adapted automatically — no manual color settings needed.
- Playback controls: play/pause, seek, volume, speed, previous/next, loop and
  shuffle.
- Tracks and subtitles: switch audio/video/subtitle tracks, load external
  subtitles, adjust subtitle appearance.
- Resume: remembers where you stopped, plus watch history and “watch later”.
- Playlist sidebar with drag-and-drop reordering.
- Video preview thumbnails above the progress bar while hovering or seeking.
- Sleep timer, screenshots, MediaInfo file details, shortcut search.
- Picture-in-picture: a small always-on-top window that opens at the
  bottom-right of your screen. Drag anywhere on the video to move it, drag any
  edge or corner to resize (the aspect ratio is kept), and click × to quit
  the player.

## How it compares to the original mpv

mpv itself is a command-line player, and its settings are hard to discover.
This project keeps the same playback engine but replaces most of the
command-line experience with a graphical interface.

What the interface already covers:

- Playback controls, progress bar and playlist
- Audio/video/subtitle track selection
- Resume, watch history and watch-later
- Screenshots, fullscreen, always-on-top
- Picture-in-picture
- Settings for the most common mpv options
- Right-click menu, MediaInfo, shortcut search, video preview thumbnails

What still comes from mpv itself:

- On-screen messages (OSD)
- Scripts such as playback statistics and the built-in console
- Key bindings can still be edited if you know mpv

What is genuinely missing (needs code, not a setting):

- No MSIX/store installer — only a portable zip is published.
- Options beyond the ones in the settings window have no graphical entry;
  they can only be changed by editing the config files.
- A few window commands (for example window-scale) are not mapped to the
  graphical interface.
- DVD/Blu-ray menus do not have full graphical interaction.
- On some multi-monitor setups the monitor name in the display log can be
  empty (HDR type and refresh rate are still tracked).

## What we inherited from upstream

This project inherits the base framework from
[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player): the
WinUI 3 window and interface style, the embedded libmpv playback core, the
control bar, the settings window, the playlist, the right-click menu data
mechanism, and the idea of automatic HDR/WCG adaptation.

Changes on top of upstream:

- Right-click menu: trimmed from 195 entries to 160, removing window, quit and
  zoom commands that do not work in embedded mode, plus entries duplicated
  with the UI. (Why: the menu was designed for a standalone mpv window, and
  many commands are useless once mpv is embedded. Reference: mpv embedding
  constraints and the mpv-lazy menu structure.)
- HDR/WCG: fixed true HDR output and washed-out colors in composition mode.
  (Why: embedded mpv cannot query display information, so setting the color
  space alone never really enters HDR. Reference: the mpv manual and
  display-info.log measurements.)
- Menu bar and UI strings: now config-driven with 8 language JSON files, and
  language switching takes effect immediately. (Why: the original menu bar was
  hard-coded in English. Reference: the Windows App SDK MenuBar.)
- Picture-in-picture: completely reworked — see below.
- Cleanup: removed the ModernZ persistent progress bar, the k7f_zen script and
  other stale scripts and invalid configuration.
- Fixes: startup OSD popup, track selection, audio device quoting, and
  settings/CLI consistency issues.

## What is new

- Picture-in-picture: a small always-on-top window that opens at the
  bottom-right of your screen; drag anywhere on the video to move it, drag any
  edge or corner to resize while keeping the aspect ratio, and click × in the
  top-right to quit the player. (Reference: native Windows window behavior.
  WinUI CompactOverlay was evaluated but has a fixed-size limitation
  [WindowsAppSDK#1593](https://github.com/microsoft/WindowsAppSDK/issues/1593),
  so the native window border approach was kept.)
- Preview thumbnails: hover or scrub the progress bar to see video previews.
  (Reference: thumbfast.)
- Menu bar framework and full UI localization: the menu structure is generated
  from a configuration file and all labels come from 8 language JSON files.
  You can reorder menu items, hide entries, add icons, and add your own mpv
  commands.
- Sleep timer, shortcut search, and MediaInfo file details.
- Bundled plugin ecosystem: thumbfast (thumbnails), dyn_menu (right-click
  menu), coverart (album art), metadata_osd (metadata display), recentmenu
  (recent files), stats (statistics), console (built-in console), select
  (menu selection) and more.

Custom plugins, filters and scripts:

- Most plain Lua scripts can be dropped into the `scripts` folder of the
  config directory and will work, because they run inside the same mpv engine.
- Scripts that depend on mpv's original on-screen controller skin, window
  decorations, terminal interaction, or that draw their own external window
  may not work or need modification.
- Preset shaders (Anime4K, FSRCNNX, ESRGAN, NVIDIA sharpen, etc.) and
  VapourSynth scripts (RIFE interpolation, BM3D denoising, upscaling, etc.)
  are included; parts that depend on external programs or runtimes must be
  installed separately.
- GPL-licensed plugins must be used in accordance with their license.

## Projects it is based on

- [mpv](https://github.com/mpv-player/mpv) — the playback engine
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) —
  the upstream project this fork is based on
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) — the preset config and
  scripts
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml)
  — the interface
- [thumbfast](https://github.com/po5/thumbfast) — preview thumbnails
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) —
  the right-click menu data
- [MediaInfo](https://mediaarea.net/en/MediaInfo) — file details

## License

- The app code is LGPL-2.1; see [LICENSE.txt](LICENSE.txt).
- Third-party components and licenses:
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md).
