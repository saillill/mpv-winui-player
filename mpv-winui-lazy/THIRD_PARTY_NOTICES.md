# Third-Party Notices

本配置层聚合了以下第三方组件，许可证以其文件头/上游仓库为准。发布前请随包附上对应许可证全文。

| 组件 | 来源 | 许可证 |
|---|---|---|
| `scripts/dyn_menu.lua`、`scripts/dialog.lua`、`scripts/menu.dll` | [tsl0922/mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) | GPL-2.0-only（menu.dll 为 mpv-lazy 随附二进制；dyn_menu.lua 含本项目裁剪） |
| `scripts/select.lua`、`scripts/console.lua`、`scripts/stats.lua` | [mpv-player/mpv](https://github.com/mpv-player/mpv) | 见文件头（select=LGPL-2.1+，console=ISC 风格许可） |
| `scripts/coverart.lua` | [CogentRedTester/mpv-coverart](https://github.com/CogentRedTester/mpv-coverart) | MIT |
| `scripts/recentmenu.lua` | [natural-harmonia-gropius/recent-menu](https://github.com/natural-harmonia-gropius/recent-menu) | MIT |
| `scripts/thumbfast.lua` | [po5/thumbfast](https://github.com/po5/thumbfast) | MPL-2.0（mpv-lazy 适配版） |
| `scripts/metadata_osd.lua` | [vc-01/metadata-osd](https://github.com/vc-01/metadata-osd) | MIT |
| `fonts/SourceHanSansSC-Regular-2.otf` | [Adobe/Source Han Sans](https://github.com/adobe-fonts/source-han-sans) | SIL OFL-1.1（全文见 `fonts/OFL-1.1.txt`） |
| `fonts/LXGWWenKaiMonoLite-Regular.ttf` | [lxgw/LxgwWenKai](https://github.com/lxgw/LxgwWenKai) | SIL OFL-1.1（全文见 `fonts/OFL-1.1.txt`） |
| `MediaInfo.exe` | [MediaArea/MediaInfo](https://mediaarea.net/en/MediaInfo) v26.05 | BSD-2-Clause（全文见 `licenses/MediaInfo-BSD-2-Clause.txt`；SHA-256 `30F2828A45A1895B033C3CD7784581033327E7B393033C55F4A03BB15CAB0D89`） |
| `mpv.exe`（发布包随附，供 thumbfast 子进程使用） | [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) 20260808（mpv v0.41.0-920 / FFmpeg N-125994） | GPL-2.0+（mpv/FFmpeg 构建产物，见上游仓库） |
| `shaders/*` | 见各文件头（Anime4K/FSRCNNX/nnedi3/NVIDIA 等） | 以文件头为准 |
| `vs/*.vpy` | mpv-lazy 维基（VapourSynth 脚本模板） | 见 mpv-lazy 维基 |
| 配置文件（`mpv.conf`/`profiles.conf`/`input.conf`/`script-opts.conf` 等） | [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit) | 该项目 LICENSE.MD 未列出者默认 UNLICENSED，保留来源声明 |

OFL-1.1 全文已随包放在 `fonts/OFL-1.1.txt`。发布前请把着色器许可证文本也复制到本目录。
