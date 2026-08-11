# Third-Party Notices

This config layer (`mpv-winui-lazy/`) bundles third-party components. Their
licenses are stated in their file headers / upstream repositories and
summarized below. Full license texts for fonts and MediaInfo ship in
`fonts/OFL-1.1.txt` and `licenses/MediaInfo-BSD-2-Clause.txt`.

## Scripts

| Component | Source | License |
|---|---|---|
| `scripts/dyn_menu.lua`, `scripts/dialog.lua` | [tsl0922/mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) | GPL-2.0-only (`dyn_menu.lua` contains project-local modifications; the previously bundled `menu.dll` binary was removed because the shipped mpv uses its native `menu-data` path) |
| `scripts/select.lua`, `scripts/console.lua`, `scripts/stats.lua` | [mpv-player/mpv](https://github.com/mpv-player/mpv) | See file headers (select = LGPL-2.1+, console = ISC-style, stats = project header) |
| `scripts/coverart.lua` | [CogentRedTester/mpv-coverart](https://github.com/CogentRedTester/mpv-coverart) | MIT |
| `scripts/recentmenu.lua` | [natural-harmonia-gropius/recent-menu](https://github.com/natural-harmonia-gropius/recent-menu) | MIT |
| `scripts/thumbfast.lua` | [po5/thumbfast](https://github.com/po5/thumbfast) (commit 9deb0733c4e36938cf90e42ddfb7a19a8b2f4641, mpv-lazy adapted) | MPL-2.0 |
| `scripts/metadata_osd.lua` | [vc-01/metadata-osd](https://github.com/vc-01/metadata-osd) | MIT |
| `scripts/hdr_auto.lua`, `scripts/vsr_auto.lua`, `scripts/seek_hold.lua`, `scripts/dynamic_menu.lua`, `scripts/mpvw_hdr_override.lua`, `scripts/stats_mediainfo.lua`, `scripts/auto_sub_fonts_dir.lua`, `scripts/save_global_props.lua` | Project-written or mpv-lazy derived (see file headers) | LGPL-2.1-or-later for project-written parts; provenance preserved for derived parts |

## Binaries

| Component | Source | License |
|---|---|---|
| `MediaInfo.exe` | [MediaArea/MediaInfo](https://mediaarea.net/en/MediaInfo) v26.05 | BSD-2-Clause (full text in `licenses/MediaInfo-BSD-2-Clause.txt`; SHA-256 `30F2828A45A1895B033C3CD7784581033327E7B393033C55F4A03BB15CAB0D89`) |
| `mpv.exe` (shipped in the release package for thumbfast subprocess rendering) | [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) 20260808 (mpv v0.41.0-920 / FFmpeg N-125994) | GPL-2.0+ (built from mpv/FFmpeg sources; see the upstream repository for source code) |

## Fonts

| Component | Source | License |
|---|---|---|
| `fonts/SourceHanSansSC-Regular-2.otf` | [Adobe/Source Han Sans](https://github.com/adobe-fonts/source-han-sans) | SIL OFL-1.1 (full text in `fonts/OFL-1.1.txt`) |
| `fonts/LXGWWenKaiMonoLite-Regular.ttf` | [lxgw/LxgwWenKai](https://github.com/lxgw/LxgwWenKai) | SIL OFL-1.1 (full text in `fonts/OFL-1.1.txt`) |

## App project fonts (`mpv-winui/mpv-winui/Assets/`)

| Component | Source | License |
|---|---|---|
| `FluentSystemIcons-Regular.ttf` | [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons) | MIT (c) 2020 Microsoft Corporation |

The top-bar/toolbar glyphs use ModernX's pin codepoints (U+E97E / U+E981)
plus Fluent camera (U+F255), panel-right-contract (U+E8C3) and
arrow-clockwise (U+F13E) glyphs from
[zydezu/ModernX](https://github.com/zydezu/ModernX). ModernX publishes no
LICENSE file, so the app bundles the official MIT-licensed Microsoft font
that contains the same outlines instead of ModernX's repackaged copy.

## Shaders (`shaders/`)

Licenses are declared in each file header:

| Shader | License |
|---|---|
| `Adaptive_sharpen/Adaptive_sharpen_lite_RT.glsl` | BSD-style (c) 2015-2021 bacondither |
| `Ani/Ani4Kv2_ArtCNN_C4F32_i2_dx.glsl`, `Ani/AniSD_ArtCNN_C4F32_i4_dx.glsl` | Model weights CC BY-NC 4.0 (non-commercial, trained by Sirosky); ArtCNN architecture MIT |
| `Anime4K/Anime4K_Restore_CNN_L.glsl`, `Anime4K/Anime4K_Upscale_GAN_x2_M.glsl` | MIT (c) 2019-2021 bloc97 |
| `EDI/nnedi3_nns64_win8x4.glsl` | LGPL-3.0 |
| `ESRGAN/k7_modernAnime_FHD_x2.glsl`, `ESRGAN/k7_modernAnime_DT_FHD_x2.glsl` | MIT (c) 2026 hooke007 |
| `FSRCNNX/FSRCNNX_x2_16_0_4_1.glsl` | LGPL-2.1+ (c) 2017-2021 igv |
| `NV/NVScaler_RT.glsl`, `NV/NVScaler_rgb_RT.glsl`, `NV/NVSharpen_RT.glsl`, `NV/NVSharpen_rgb_RT.glsl` | MIT (c) 2022 NVIDIA CORPORATION & AFFILIATES |
| `QCOM/QCOM_SGEDS_ms_RT.glsl` | See file header |

## VapourSynth scripts (`vs/`)

`vs/*.vpy` are VapourSynth templates maintained by the mpv-lazy project
([hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)); their
authorship and licensing follow the respective upstream discussions.

## Configuration files

`mpv.conf`, `profiles.conf`, `input.conf`, `script-opts.conf`,
`script-opts/*.conf` and the deploy script are based on
[hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit) (mpv-lazy).
mpv-lazy's `LICENSE.MD` states that files it does not explicitly list are
treated as UNLICENSED, so files copied from mpv-lazy keep their upstream
provenance and are distributed with that notice. Project-written additions
and modifications are released under LGPL-2.1-or-later.

## Notes

- The app project itself (`mpv-winui/`) is licensed under LGPL-2.1 (see the
  repository root `LICENSE.txt`).
- `scripts/dyn_menu.lua` and `scripts/dialog.lua` are GPL-2.0-only. They are
  distributed as source code in this repository; the GPL does not extend to
  the WinUI app code, which communicates with them only through mpv's public
  `menu-data` / `script-message` interfaces.
- The bundled `mpv.exe` is a GPL-2.0+ build. It is a separate program invoked
  by thumbfast to render preview thumbnails. Source code is available from the
  upstream repositories linked above.
