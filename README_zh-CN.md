# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[English](README.md)

> 本项目是 [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
> 的复刻版，一款 Windows 上的 mpv 图形播放器。内核还是真正的 mpv，界面换成
> 了清爽的 WinUI 3——不用命令行，常用操作点一下就行。

## 功能介绍

（界面截图待补充）

- 多种界面语言：简体中文、English、日本語、한국어、Deutsch、Français、
  Español、Русский，切换后立即生效。
- 状态栏：播放/暂停、进度条、音量、倍速、轨道切换都在一条栏里，悬停进度条
  还能看到视频缩略图预览。
- 设置：常用 mpv 设置都做成了图形页面，分类清晰、可以搜索，改完立即生效，
  不用再记命令和参数。
- 右键菜单：完整的 mpv 命令菜单，已翻译成 8 种语言。
- 画中画：独立小窗、置顶、默认出现在屏幕右下角；可以拖动、可以缩放，退出
  画中画就是退出播放器。
- 滤镜：自带 mpv-lazy 预置的着色器与补帧、超分、降噪脚本，也支持放入自己
  的滤镜和脚本。

## 它能做什么

- 打开方式随你挑：菜单、拖拽、命令行、`mpv-winui://` 链接协议。
- 支持 mpv 能播放的格式：本地文件、网络串流、DVD/蓝光（取决于 mpv 的支持
  情况）。
- 有硬件解码就默认开启；HDR/WCG 内容自动适配，不用手动调颜色。
- 播放控制：播放/暂停、进度、音量、倍速、上一首/下一首、循环、随机播放。
- 音轨与字幕：切换音轨/视频轨/字幕轨，加载外挂字幕，调整字幕外观。
- 记忆播放：记住上次看到哪里，还有观看历史和“稍后观看”。
- 播放列表侧栏，支持拖拽排序。
- 进度条上方有视频缩略图预览（悬停或拖动时显示）。
- 睡眠定时器、截图、MediaInfo 文件信息、快捷键搜索。
- 画中画：独立小窗，置顶，默认出现在屏幕右下角；按住视频任意处可拖动，
  拖四边或四角可缩放（保持画面比例），点 × 退出播放器。

## 和原版 mpv 的差异

mpv 本身是命令行播放器，设置和配置不清晰。这个项目沿用同一个播放内核，
把大部分命令行体验换成了图形界面。

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

## 从上游继承的功能

本项目从 [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
继承了基础框架：WinUI 3 主窗口与界面风格、内嵌 libmpv 播放核心、状态栏、
设置窗口、播放列表、右键菜单数据机制，以及 HDR/WCG 自动适配的思路。

在上游基础上做了这些修改：

- 右键菜单：从 195 条精简到 160 条，删掉了嵌入模式下无效的窗口/退出/缩放
  命令和与界面重复的项。（为什么：菜单是给 mpv 窗口用的，嵌进 WinUI 后很多
  命令不再有效；参考：mpv 嵌入限制、mpv-lazy 菜单结构。）
- HDR/WCG：修正了 composition 模式下真正输出 HDR 和画面发白的问题。
  （为什么：嵌入模式下 mpv 拿不到显示器信息，只设置颜色空间不会真正进入
  HDR；参考：mpv 官方手册、display-info.log 实测数据。）
- 菜单栏与界面文案：改为配置驱动 + 8 种语言 JSON，切语言即时生效。
  （为什么：原版菜单是写死的英文，无法维护和翻译；参考：Windows App SDK
  的 MenuBar。）
- 画中画：完全重做，见下方“新增加的功能”。
- 清理：移除了 ModernZ 持久进度条、k7f_zen 等失效脚本和无效配置。
- 修复：启动时的 OSD 弹窗、轨道选择、音频设备参数、设置与命令行不一致等
  问题。

## 新增加的功能

- 画中画：独立置顶小窗，默认出现在屏幕右下角；按住视频任意处拖动，拖四边
  或四角缩放并锁定画面比例，右上角 × 退出播放器。
  （参考：Windows 原生窗口行为。也调研过 WinUI 的 CompactOverlay，因为它有
  固定尺寸限制 [WindowsAppSDK#1593](https://github.com/microsoft/WindowsAppSDK/issues/1593)，
  最终保留原生窗口边框方案。）
- 缩略图预览：进度条悬停或拖动时显示视频缩略图。（参考：thumbfast。）
- 菜单栏框架与全 UI 本地化：菜单结构由配置文件生成，文案走 8 种语言 JSON；
  用户可以自定义菜单顺序、隐藏菜单项、加图标，甚至加自定义 mpv 命令项。
- 睡眠定时器、快捷键搜索、MediaInfo 文件信息。
- 内置插件生态：thumbfast（缩略图）、dyn_menu（右键菜单）、coverart（封面）、
  metadata_osd（信息显示）、recentmenu（最近播放）、stats（统计）、console
  （控制台）、select（菜单选择）等。

自定义插件、滤镜、脚本支持：

- 大多数纯 Lua 脚本可以直接放进配置目录的 `scripts` 文件夹使用，因为它们跑
  在同一个 mpv 引擎里。
- 依赖 mpv 原生屏幕控制条皮肤、窗口装饰、终端交互，或者自己画独立窗口的
  插件可能不兼容，需要改造。
- 预置了着色器（Anime4K、FSRCNNX、ESRGAN、NVIDIA 锐化等）和 VapourSynth
  脚本（RIFE 补帧、BM3D 降噪、超分等）；依赖外部程序或运行库的部分需要
  自行安装。
- GPL 协议的插件使用时需遵守其许可。

## 参考项目与致谢

- [mpv](https://github.com/mpv-player/mpv) —— 播放内核
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)
  —— 原版上游项目，本作是它的复刻版
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) —— 预置配置与脚本
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml)
  —— 界面框架
- [thumbfast](https://github.com/po5/thumbfast) —— 缩略图预览
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin)
  —— 右键菜单数据
- [MediaInfo](https://mediaarea.net/en/MediaInfo) —— 文件信息

## 许可

- 应用代码为 LGPL-2.1，详见 [LICENSE.txt](LICENSE.txt)。
- 第三方组件与许可：
  [mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)。
