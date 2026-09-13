# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

## Screenshot

<img src="https://raw.githubusercontent.com/ikas-mc/mpv-winui-player/main/screenshot/screenshot.png" width="600" />

## Download
[Github Actions](https://github.com/ikas-mc/mpv-winui-player/actions/workflows/build.yml)

[Github Releases](https://github.com/ikas-mc/mpv-winui-player/releases)


## Limitation

> A graphical mpv player for Windows, forked from
> [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player). The
> playback engine is the real mpv, wrapped in a clean WinUI 3 interface — no
> command line required, and the things you use every day are one click away.

Some commands aren't supported, like quit and window related cmd...

- **Installer**: download `mpv-winui-setup-x64-<version>.msi` and double-click.
  No administrator rights needed, a Start Menu shortcut is created
  automatically; newer versions upgrade in place, and uninstalling never
  touches your playback history or config.
- **Portable**: download `mpv-winui-win-x64-Release.zip`, extract and run
  `mpv-winui.exe`.

The player uses the `d3d11-output-mode=composition` mode, mpv can't get display information.

Use these custom properties as a workaround

```
user-data/mpvw/color-kind : SDR, WCG, HDR
user-data/mpvw/refresh-rate : 60
```

example:

```
[mpvw-sdr]
profile-cond=p["user-data/mpvw/color-kind"] == "SDR"
profile-restore=copy
d3d11-output-csp=srgb
d3d11-output-format=rgb10_a2
```
## Thumbnail Preview

* Built-in preview
* Supports plugins using [osc-preview-api](https://mpv.io/manual/master/#osc-preview-api)


## Msix or Unpackaged

|  | Msix | Unpackaged |
| :--- | :--- | :--- |
| **Data** | `C:\Users\user\AppData\Local\Packages\--\LocalState` | `C:\Users\user\AppData\Local\ikas-mc\mpvw` |
| **Settings** | `C:\Users\user\AppData\Local\Packages\--\Settings` | `HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\ikas-mc\mpvw\app` |
| **File Association** | Auto | Register in the settings page |
| **Protocol** | mpvw://?file=[path] | Register in the settings page |
| **Command Line** | mpvw [path] | [App Folder]\mpvw.exe [path] |


## Mpv Conf Editor

https://github.com/ikas-mc/mpv-winui-player/wiki/Mpv-Conf-Editor

## License

- The app code is LGPL-2.1; see [LICENSE.txt](LICENSE.txt).
- Third-party components and licenses:
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md).
