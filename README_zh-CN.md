# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[English](README.md)

> 一款 Windows 上的 mpv 图形播放器。内核还是真正的 mpv，界面换成了清爽的
> WinUI 3——不用命令行，常用操作点一下就行。

## 截图

待补充。

## 它能做什么

- 打开方式随你挑：菜单、拖拽、命令行、`mpv-winui://` 链接协议。
- 支持 mpv 能播放的格式：本地文件、网络串流、DVD/蓝光（取决于 mpv 的支持
  情况）。
- 有硬件解码就默认开启；HDR/WCG 内容自动适配，不用手动调颜色。
- 播放控制：播放/暂停、进度条、音量、倍速、上一首/下一首、循环、随机播放。
- 音轨与字幕：切换音轨/视频轨/字幕轨、加载外挂字幕、调整字幕外观。
- 记忆播放：记住上次看到哪里，还有观看历史和“稍后观看”。
- 播放列表侧栏，支持拖拽排序。
- 进度条上方有视频缩略图预览（悬停或拖动时显示）。
- 右键菜单包含完整的 mpv 命令菜单，并翻译成 8 种语言。
- 睡眠定时器、截图、MediaInfo 文件信息、快捷键搜索。
- PotPlayer 式设置窗口：约 190 项设置，分类清晰、可搜索、改完立即生效。
- 画中画：独立小窗，置顶，默认出现在屏幕右下角；按住视频任意处可拖动，
  拖边缘可缩放（保持画面比例），点 × 退出播放器。
- 界面语言：简体中文、English、日本語、한국어、Deutsch、Français、
  Español、Русский。

## 和原版 mpv 的差异

mpv 本身是命令行播放器。这个项目沿用同一个播放内核，把大部分命令行体验
换成了图形界面。

界面已经覆盖的部分：

- 播放控制、进度条、播放列表
- 音轨/视频轨/字幕轨选择
- 记忆播放、观看历史、稍后观看
- 截图、全屏、置顶
- 画中画
- 常用 mpv 设置的图形化页面
- 右键菜单、MediaInfo、快捷键搜索、缩略图预览

仍然来自 mpv 的部分：

- 屏幕提示（OSD）
- stats、console 等内置脚本
- 熟悉 mpv 的话，按键绑定仍可自行编辑

真实缺失（需要代码才能补，不是改设置能解决的）：

- 没有 MSIX/商店安装包，目前只发布免安装 zip。
- 设置窗口之外的 mpv 选项没有图形入口，只能直接改配置文件。
- 少量窗口命令（例如 window-scale）没有映射到图形界面。
- DVD/蓝光菜单没有完整的图形交互。
- 部分多显示器环境下，显示日志里的显示器名称可能为空（HDR 类型和刷新率
  仍会正常记录）。

mpv 插件（脚本）兼容性：

- 大多数纯 Lua 脚本可以直接放进配置目录的 `scripts` 文件夹使用，因为它们
  跑在同一个 mpv 引擎里。本应用已内置其中几个：thumbfast（缩略图预览）、
  dyn_menu（右键菜单）、coverart（封面）、metadata_osd（信息显示）、
  recent-menu（最近播放）、stats（播放统计）、console（控制台）。
- 依赖 mpv 原生屏幕控制条皮肤、窗口装饰、终端交互，或者自己画独立窗口的插件
  可能不兼容，需要改造。
- 依赖外部程序或运行库的插件（例如部分 VapourSynth 流程）需要自行安装。
- GPL 协议的插件使用时需遵守其许可。

## 快速开始

1. 从 [Releases 页面](https://github.com/saillill/mpv-winui-player/releases)
   下载 `mpv-winui-win-x64-Release.zip`。
2. 解压到任意目录（Windows 10/11 x64）。
3. 首次运行前部署一次配置文件：
   `powershell -File mpv-winui-lazy\deploy-config.ps1`
4. 运行 `mpv-winui.exe`，用菜单、拖拽、命令行或链接协议打开文件。

## 参考项目与致谢

- [mpv](https://github.com/mpv-player/mpv) —— 播放内核
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) —— 预置配置与脚本
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml)
  —— 界面框架
- [thumbfast](https://github.com/po5/thumbfast) —— 缩略图预览
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin)
  —— 右键菜单数据
- [MediaInfo](https://mediaarea.net/en/MediaInfo) —— 文件信息
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
  —— 本项目的上游

## 已知限制

- 文件播完会暂停（keep-open 行为）；恢复播放后播放列表继续。
- 免安装版首次使用需要先执行上面的配置部署步骤。
- 部分多显示器环境下，显示日志中的显示器名称可能为空。

## 开发者与许可

- 构建：`.\build.ps1 -Release x64`（或 `-Debug`）。
- 应用代码为 LGPL-2.1，详见 [LICENSE.txt](LICENSE.txt)。
- 第三方组件与许可：
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)。
