# mpv-winui-player

A Windows video player built on libmpv with a WinUI 3 interface.

**Unfinished.** It builds and the core playback path works, but parts of the UI
are still being reworked and there are no releases.

## Building

Requires the .NET SDK, the Windows SDK, and the Visual Studio C++ build tools.

```powershell
./build.ps1 -Configuration Release -Platform x64
```

`./package.ps1` produces a zipped portable build.

## Layout

| Path | What it is |
|---|---|
| `src/mpvw-app/` | the WinUI 3 application |
| `src/mpvw-native/` | the C++/WinRT component hosting libmpv |
| `mpv-winui-lazy/` | the mpv configuration layer, installed on first run |
| `tests/` | the two test suites |
| `tools/` | generators and the checkers the build runs |
| `docs/` | design contracts and the upstream comparison |

## Licence

LGPL-2.1. Third-party components and their licences are in
[`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`](mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md).
