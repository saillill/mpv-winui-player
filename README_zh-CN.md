# mpv-winui-player

[![License: LGPL-2.1](https://img.shields.io/badge/License-LGPL--2.1-blue.svg)](LICENSE.txt)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d6.svg)]()
[![Release](https://img.shields.io/github/v/release/saillill/mpv-winui-player)](https://github.com/saillill/mpv-winui-player/releases)

[English](README.md)

> 这是一款 Windows 上的 mpv 图形播放器（复刻自
> [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)）。
> 内核是真正的 mpv，界面换成了清爽的 WinUI 3——不用命令行，常用操作点一下就行。

## 安装

- **安装版**：下载 `mpv-winui-setup-x64-<版本>.msi`，双击安装。不需要管理员权限，
  开始菜单会有快捷方式；以后装新版本直接覆盖，卸载也不会删掉你的播放记录和配置。
- **便携版**：下载 `mpv-winui-win-x64-Release.zip`，解压后直接运行 `mpv-winui.exe`。

两种方式第一次打开都会自动把内置的 mpv 配置放好，不用任何手动步骤。之后想改
mpv 配置、键位或菜单，都在 `%LOCALAPPDATA%\mpv-winui\mpv` 目录里改，升级不会
覆盖你的改动。

## 功能介绍

（截图待补充）

一句话：**mpv 能做的事，这里都用图形界面包好了；想深入折腾，mpv 本体也一直在。**

- 多种界面语言：中、英、日、韩、德、法、西、俄，随时切换立即生效。
- 状态栏：播放、进度、音量、倍速、轨道切换都在一条栏里；把鼠标停在进度条上，
  还能看到视频缩略图预览。
- 设置：常用 mpv 选项都做成了图形页面，分类清楚、可以搜索，改完立刻生效，
  不用记命令。
- 右键菜单：完整的 mpv 命令菜单，同样支持多语言。
- 画中画：小窗置顶播放，出现在屏幕右下角，能拖动能缩放；关掉画中画就是退出
  播放器。
- 滤镜：自带 mpv-lazy 预置的着色器和补帧、超分、降噪脚本，也可以放自己的。

## 它能做什么

打开方式随你挑：菜单、拖拽、命令行参数，或者 `mpv-winui://` 链接。凡是 mpv 能
播的它都能播——本地文件、网络串流、DVD、蓝光都行。有硬件解码就默认开着，
HDR/广色域内容自动适配，不用手动调颜色。

播放、暂停、进度、音量、倍速、上下首、循环、随机，音轨和字幕随意切换，还能
加载外挂字幕、调整字幕样子。播放列表侧栏支持拖拽排序；进度条上能看到缩略图
预览；记得住上次看到哪，有观看历史和"稍后观看"。睡眠定时器、截图、文件信息
（MediaInfo）、快捷键搜索，也都齐了。

## 和原版 mpv 的差异

mpv 本身是命令行播放器，设置藏得深。这个项目沿用同一个播放内核，把大部分
日常操作搬到了图形界面里。

屏幕提示（OSD）、统计、控制台等脚本仍是 mpv 原生的；懂 mpv 的话，按键绑定
照旧能自己改。还有少数功能没有图形入口，得直接改配置文件，比如设置窗口之外的
mpv 选项和一些窗口命令。目前提供安装版和便携版，没有商店版和自动更新。

## 从上游继承的功能

基础框架来自 [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player)：
WinUI 3 窗口和界面风格、内嵌的 libmpv 播放核心、状态栏、设置窗口、播放列表、
右键菜单机制，以及 HDR/广色域自动适配的思路。

在此基础上做了这些事：

- 整理右键菜单：删掉了嵌入模式下没用、或者和界面重复的项。
- 修好 HDR：修正了嵌入模式下真正输出 HDR 和画面发白的问题。
- 菜单栏和界面文案改成配置驱动，支持多语言，切语言即时生效。
- 画中画完全重做，见下面"新增加的功能"。
- 清理失效脚本和无效配置，修了一些启动弹窗、轨道选择之类的小问题。

## 新增加的功能

- 画中画：独立小窗置顶播放，能拖动、能缩放、锁定画面比例，右上角 × 退出。
- 缩略图预览：悬停或拖动进度条时显示视频画面预览。
- 菜单栏框架 + 全界面本地化：菜单结构由配置文件生成，可以自己调顺序、隐藏项、
  加图标，甚至加自定义 mpv 命令。
- 开箱即用：内置 mpv 配置随包分发，第一次运行自动部署。
- 睡眠定时器、快捷键搜索、文件信息（MediaInfo）。
- 内置一批 mpv 插件：缩略图、右键菜单、封面、信息显示、最近播放、统计、
  控制台等。

## 自定义插件、滤镜、脚本

大多数纯 Lua 脚本直接放进配置目录的 `scripts` 文件夹就能用——它们跑在同一个
mpv 引擎里。预置了着色器（Anime4K、FSRCNNX、ESRGAN、NVIDIA 锐化等）和
VapourSynth 脚本（RIFE 补帧、BM3D 降噪、超分等），依赖外部程序的部分需要自己
装。依赖 mpv 自带控制条皮肤、窗口装饰或终端交互的插件可能不兼容，需要改造；
GPL 插件使用时请遵守其许可。

## 参考项目与致谢

- [mpv](https://github.com/mpv-player/mpv) —— 播放内核
- [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) —— 原版上游项目
- [mpv-lazy](https://github.com/hooke007/mpv_PlayKit) —— 预置配置与脚本
- [WinUI 3 / Windows App SDK](https://github.com/microsoft/microsoft-ui-xaml) —— 界面框架
- [thumbfast](https://github.com/po5/thumbfast) —— 缩略图预览
- [dyn_menu / mpv-menu-plugin](https://github.com/tsl0922/mpv-menu-plugin) —— 右键菜单数据
- [MediaInfo](https://mediaarea.net/en/MediaInfo) —— 文件信息

## 许可

应用代码为 LGPL-2.1，详见 [LICENSE.txt](LICENSE.txt)。第三方组件与许可见
[mpv-winui-lazy/THIRD_PARTY_NOTICES.md](mpv-winui-lazy/THIRD_PARTY_NOTICES.md)。
