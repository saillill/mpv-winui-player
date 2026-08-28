# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[简体中文](README_zh-CN.md)

> A graphical mpv player for Windows, forked from
> [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player). The
> playback engine is the real mpv, wrapped in a clean WinUI 3 interface — no
> command line required, and the things you use every day are one click away.

## Installation

- **Installer**: download `mpv-winui-setup-x64-<version>.msi` and double-click.
  No administrator rights needed, a Start Menu shortcut is created
  automatically; newer versions upgrade in place, and uninstalling never
  touches your playback history or config.
- **Portable**: download `mpv-winui-win-x64-Release.zip`, extract and run
  `mpv-winui.exe`.

On first launch both set up the bundled mpv config automatically — no manual
steps. If you later edit mpv config, key bindings or menus, do it in
`%LOCALAPPDATA%\mpv-winui\mpv`; upgrades never overwrite your changes.

## Features

(Screenshots coming soon)

In one sentence: **everything mpv can do is wrapped in a graphical interface,
and the underlying mpv is always there if you want to go deeper.**

- Multiple UI languages: English, 简体中文, 繁體中文, 日本語, 한국어, Deutsch,
  Français, Español, Русский — switching takes effect immediately.
- Control bar: play/pause, progress, volume, speed and track switching in one
  bar; hover the progress bar to see video preview thumbnails.
- Settings: the common mpv options are presented as a clearly categorized,
  searchable graphical page, and changes apply immediately — no commands to
  memorize.
- Right-click menu: the full mpv command menu, also translated.
- Picture-in-picture: a small always-on-top window at the bottom-right of the
  screen; move it, resize it, and closing it quits the player.
- Filters: ships with mpv-lazy preset shaders and interpolation, upscaling and
  denoising scripts; you can add your own.

## What it can do

Open media however you like — menu, drag and drop, command line arguments, or
a `mpv-winui://` link (registered by the installer). Whatever mpv can play,
it plays: local files, network streams, DVDs and Blu-rays. Hardware decoding
is on by default when available, and HDR/wide-gamut content is adapted
automatically.

Playback controls are all there: play, pause, seek, volume, speed, previous/
next, loop and shuffle. Switch audio/video/subtitle tracks freely, load
external subtitles, tweak how subtitles look. The playlist sidebar supports
drag-and-drop reordering; the progress bar shows preview thumbnails; it
remembers where you stopped, with watch history and “watch later”. Screenshots,
file details (MediaInfo) and shortcut search round it out.

## How it compares to the original mpv

mpv is a command-line player with settings that are hard to discover. This
project keeps the same playback engine and moves most everyday operations into
the graphical interface.

The on-screen messages (OSD), statistics, console and other scripts are still
mpv's own; if you know mpv, key bindings can be edited as usual. A few things
still have no graphical entry and are done by editing config files — options
outside the settings window and some window commands. Currently there is an
installer and a portable zip; no store version or auto-update.

## What we inherited from upstream

The base framework comes from
[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player): the
WinUI 3 window and interface style, the embedded libmpv playback core, the
control bar, the settings window, the playlist, the right-click menu
mechanism, and the idea of automatic HDR/wide-gamut adaptation.

On top of that we:

- Tidied up the right-click menu: removed entries that do nothing in embedded
  mode or duplicate the UI.
- Fixed HDR: real HDR output in embedded mode and washed-out colors.
- Made the menu bar and UI strings config-driven with multi-language support;
  switching language applies immediately.
- Completely reworked picture-in-picture — see below.
- Cleaned up stale scripts and invalid config, and fixed small issues like the
  startup OSD popup and track selection.

## What is new

- Picture-in-picture: a small always-on-top window that can be moved and
  resized while keeping the aspect ratio; × in the top-right quits the player.
- Preview thumbnails: hover or scrub the progress bar to see video previews.
- Menu bar framework and full UI localization: the menu structure is generated
  from a configuration file — reorder items, hide entries, add icons, or add
  your own mpv commands.
- Works out of the box: the bundled mpv config is deployed automatically on
  first run.
- Shortcut search and file details (MediaInfo).
- A set of bundled mpv plugins: thumbnails, right-click menu, cover art,
  metadata display, recent files, statistics, console and more.

## Custom plugins, filters and scripts

Most plain Lua scripts can be dropped into the `scripts` folder of the config
directory and will work, because they run inside the same mpv engine. Preset
shaders (Anime4K, FSRCNNX, ESRGAN, NVIDIA sharpen, etc.) and VapourSynth
scripts (RIFE interpolation, BM3D denoising, upscaling, etc.) are included;
parts that depend on external programs must be installed separately. Scripts
that rely on mpv's own control-bar skin, window decorations or terminal
interaction may need modification; GPL-licensed plugins must be used in
accordance with their license.

## Projects it is based on

- [mpv](https://github.com/mpv-player/mpv) — the playback engine
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) —
  the upstream project this fork is based on
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) — the preset config and
  scripts
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml)
  — the interface
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) —
  the right-click menu data
- [MediaInfo](https://mediaarea.net/en/MediaInfo) — file details

## License

- The app code is LGPL-2.1; see [LICENSE.txt](LICENSE.txt).
- Third-party components and licenses:
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md).
