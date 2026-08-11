# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[简体中文](README_zh-CN.md)

> A modern Windows player for mpv, forked from
> [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player). It
> keeps the real mpv playback engine and wraps it in a clean WinUI 3 interface
> — no command line required, and the things you use every day are one click
> away.

## Screenshots

Coming soon.

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
- Right-click menu with the full mpv command menu, translated into 8
  languages.
- Sleep timer, screenshots, MediaInfo file details, shortcut search.
- PotPlayer-style settings window: about 190 options, categorized,
  searchable, applied immediately.
- Picture-in-picture: a small always-on-top window that opens at the
  bottom-right of your screen. Drag anywhere on the video to move it, drag the
  edges to resize (the aspect ratio is kept), and click × to quit the player.
- Interface languages: English, 简体中文, 日本語, 한국어, Deutsch, Français,
  Español, Русский.

## How it compares to the original mpv

mpv is a command-line player. This project keeps the same playback engine but
replaces most of the command-line experience with a graphical interface.

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

mpv plugins (scripts) compatibility:

- Most plain Lua scripts can be dropped into the `scripts` folder of the
  config directory and will work, because they run inside the same mpv engine.
  Several are already bundled: thumbfast (preview thumbnails), dyn_menu
  (right-click menu), coverart (album art), metadata_osd (metadata display),
  recent-menu (recent files), stats (playback statistics) and console.
- Scripts that depend on mpv’s original on-screen controller skin, window
  decorations, terminal interaction, or that draw their own external window
  may not work or need modification.
- Plugins that need an external program or runtime (for example some
  VapourSynth workflows) must be installed separately.
- GPL-licensed plugins must be used in accordance with their license.

## Quick start

1. Download `mpv-winui-win-x64-Release.zip` from the
   [Releases page](https://github.com/saillill/mpv-winui-player/releases).
2. Extract it anywhere (Windows 10/11 x64).
3. On first run, deploy the config files once:
   `powershell -File mpv-winui-lazy\deploy-config.ps1`
4. Run `mpv-winui.exe` and open a file from the menu, drag & drop, command
   line, or link protocol.

## Projects it is based on

- [mpv](https://github.com/mpv-player/mpv) — the playback engine
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) — the preset config and
  scripts
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml)
  — the interface
- [thumbfast](https://github.com/po5/thumbfast) — preview thumbnails
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) —
  the right-click menu data
- [MediaInfo](https://mediaarea.net/en/MediaInfo) — file details
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) —
  the upstream project this fork is based on (still actively maintained)

## Known limitations

- The player pauses when a file finishes (keep-open behavior); the playlist
  continues when playback resumes.
- The portable version needs the one-time config deploy step above.
- On some multi-monitor setups the monitor name in the display log can be
  empty.

## For developers and licensing

- Build: `.\build.ps1 -Release x64` (or `-Debug`).
- The app code is LGPL-2.1; see [LICENSE.txt](LICENSE.txt).
- Third-party components and licenses:
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md).
