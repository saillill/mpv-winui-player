# mpv-winui-player

A video player for Windows that combines the mpv engine with a native WinUI 3
interface. Playback is performed by mpv itself; the interface is built with
Windows App SDK, so everyday use requires neither a command line nor hand-editing
configuration files.

## Differences from the original

The settings window is reorganised. Instead of a single flat list of options, they
are grouped into cards and folders, and the arrangement is adjustable — rows can be
reordered, moved, renamed or hidden, so the settings actually in use stay close at
hand. The mpv configuration editor, the menu editor and the file-association
section follow the same approach.

Playback gains a purpose-built control bar, with an adaptive layout,
chapter and A–B loop markers, and animated panels, alongside quick panels for
audio, subtitles and video, a track selector, picture-in-picture with a resizable
window, and aspect-ratio and cropping controls that respect the actual size of the
video panel. Thumbnail preview is built in and also supports plugins implementing
the `osc-preview-api`.

The bundled mpv configuration is maintained rather than a one-time drop: it is
installed on first run and kept current afterwards, including scripts for automatic
HDR and VSR switching, cover art, recently opened files, playback statistics and
console output, together with a set of shaders.

## Building

Building requires the .NET SDK, the Windows SDK and the Visual Studio C++ build
tools. A Release build is produced with:

```bash
./build.ps1 -Configuration Release -Platform x64
```

Consistency checks for settings and interface text are available under `tools/`.
Documentation intended for contributors is in `docs/` and `AGENTS.md`.

## Licence

The application code is licensed under LGPL-2.1. Third-party components and their
licences are listed in
[`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`](mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md).
