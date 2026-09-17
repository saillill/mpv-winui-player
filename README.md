# mpv-winui-player

A video player for Windows that combines the mpv engine with a native WinUI 3
interface. Playback is performed by mpv itself; the interface is built with
Windows App SDK, so everyday use requires neither a command line nor hand-editing
configuration files.

This repository is a fork of
[ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player), maintained
for personal use. Upstream remains the reference point: where the two differ, this
fork follows the direction upstream has taken, and upstream changes are adopted
rather than reinvented. The work here concentrates on the areas that matter most in
daily use.

## Differences from the original

The most noticeable change is the settings window. The original presents a single
flat list of options. Here the options are organised into cards and second-level
folders, and the arrangement is adjustable: rows can be reordered, moved between
folders, renamed or hidden, so the settings that are actually used stay close at
hand. The mpv configuration editor, the menu editor and the file-association
section follow the same approach.

The interface is also fully localised. The original ships English text only,
whereas this fork provides nine languages covering roughly 1,200 user-facing
strings, with consistency verified by checks included in the repository.

Playback gained a control bar designed for this fork, with an adaptive layout,
chapter and A–B loop markers on the progress bar, and animated panels. Alongside it
are quick panels for audio, subtitles and video, a track selector, picture-in-picture
with a resizable window, and aspect-ratio and cropping controls that respect the
actual size of the video panel. Thumbnail preview is built in and additionally
supports plugins that implement the `osc-preview-api`.

Finally, the bundled mpv configuration is treated as maintained content rather than
a one-time drop. It is installed on first run and kept current afterwards, and it
includes scripts for automatic HDR and VSR switching, cover art, recently opened
files, playback statistics and console output, together with a set of shaders.

## Relationship to upstream

The two are best described as a superset with a shared direction. Every capability
exposed by upstream is present here, the interface between the two has no gap in
either direction, and the recent compatibility work has been carried in the direction
upstream chose rather than around it. The outstanding fixes from upstream have been
adopted as well.

A detailed comparison, including the parts of upstream that are still under
consideration, is kept in [`docs/compare-upstream.md`](docs/compare-upstream.md).

## Current limitations

Playback uses mpv's composition output mode, in which mpv cannot obtain display
information directly. The characteristics of the display are therefore supplied
explicitly, and may be set in a profile:

```
user-data/mpvw/color-kind    : SDR / WCG / HDR
user-data/mpvw/refresh-rate  : 60
```

A number of mpv commands are intentionally not supported, most notably those that
terminate the player or manipulate its own window. Only Windows x64 is maintained.

## Building

Building requires the .NET SDK, the Windows SDK and the Visual Studio C++ build
tools. A Release build is produced with:

```bash
./build.ps1 -Configuration Release -Platform x64
```

Consistency checks for localisation, settings and interface text are available under
`tools/`. Documentation intended for contributors is in `docs/` and `AGENTS.md`.

## Licence

The application code is licensed under LGPL-2.1, as upstream. Third-party components
and their licences are listed in
[`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`](mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md).
