# mpv-winui-lazy

mpv-winui-player 的配置层，基于 [hooke007/mpv_PlayKit](https://github.com/hooke007/mpv_PlayKit)（mpv-lazy）裁剪而成：

- 移除了 ModernZ / k7f_zen / 旧 WinUI 菜单宿主（工具目录与 dead 配置）；
- `input.conf` 只保留快捷键绑定，不含 160 条中文 `#menu:` 数据菜单（本地定制版备份在
  `%LOCALAPPDATA%\mpv-winui\mpv\input.conf.bak-20260807-menu`）；动态“增强脚本/着色器”
  子菜单仍由 `dynamic_menu.lua` 生成；
- `input_plus.lua` 已移除（F3-F5 临时清滤镜/着色器、`,`/`.` 按住倍速、Ctrl+←/→ 流媒体式跳转等绑定随之删除）；
- 针对 WinUI composition 模式（`d3d11-output-mode=composition`）修正 HDR/WCG 输出：
  `profiles.conf` 的 `[mpvw-sdr|mpvw-wcg|mpvw-hdr]` 按 App 写入的
  `user-data/mpvw/color-kind` 自动切换；
- `hdr_auto.lua`、`dynamic_menu.lua` 为本项目裁剪（RTX Video HDR 自动开关、移除 RIFE/K7s 预设）；
  `mpvw_hdr_override.lua` 为本项目新增（手动覆盖 App 报告的显示器 HDR 状态）；
  `vsr_auto.lua`、`seek_hold.lua` 等其余脚本保持 mpv-lazy 原样。

## 目录

| 路径 | 说明 |
|---|---|
| `mpv.conf` / `profiles.conf` / `input.conf` | mpv 主配置、条件配置、按键绑定 |
| `scripts/` | Lua/JS 脚本与 `menu.dll`（mpv-menu-plugin，GPL-2.0-only） |
| `script-opts/` | 脚本选项（`dyn_menu.conf` 控制菜单数据通道与标题长度上限） |
| `shaders/` | 可选着色器，许可证见各文件头 |
| `vs/` | 可选 VapourSynth 脚本（需自行安装 VapourSynth 运行库） |
| `fonts/` | OSD 菜单字体（Source Han Sans / LXGW WenKai） |
| `MediaInfo.exe` | MediaInfo CLI v26.05（BSD-2-Clause，`工具 > MediaInfo` 用） |
| `licenses/` | 随包第三方许可证全文（MediaInfo BSD-2-Clause） |

历史菜单整理记录见仓库根目录 [`docs/menu-audit-20260807.md`](../docs/menu-audit-20260807.md)。

## 部署

```powershell
powershell -File mpv-winui-lazy\deploy-config.ps1
```

目标目录：`%LOCALAPPDATA%\mpv-winui\mpv`。脚本使用 robocopy `/MIR`，会删除目标目录中
源里不存在的旧文件；`_cache/`、`cache/`、`*.log`、`recent.json`、`saved-props.json` 为运行数据，不部署、不删除。

## 许可证

自创内容以 LGPL-2.1-or-later 发布（与主程序一致）；第三方组件版权归原作者，
见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
