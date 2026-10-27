# 外部参考研究：Richasy/mpv-winui + cnblogs D3D11 文章（2026-08-07）

用户提供两个链接，要求研究对本菜单宿主的可借鉴性。

## 1. cnblogs 文章（xymfblogs/p/17218256）

《[WinUI 3] 如何利用 D3D11 在 SwapChainPanel 控件上绘制 OpenGL（UWP通用）》

- 技术点：libmpv 用 OpenGL 渲染，而 XAML 只接受 DirectX 内容；通过
  `WGL_NV_DX_interop`（NVIDIA 扩展）把 OpenGL 渲染缓冲关联到 D3D11
  swapchain，再经 `ISwapChainPanelNative.SetSwapChain` 挂到
  SwapChainPanel；
- 关键限制：SwapChainPanel 实例 ≤4、交换链必须显式宽高、
  DXGI_SCALING_STRETCH、只能用 CreateSwapChainForComposition；
- 这是 Richasy/mpv-winui 渲染层的理论基础。

## 2. GitHub：Richasy/mpv-winui

- 定位：把 libmpv 集成进 WinUI 3 的**测试项目**（12 stars），核心两部分：
  - `Mpv.Core`：libmpv C API（client + render）P/Invoke 绑定、Player 封装；
  - `Mpv.UI`：SwapChainPanel + OpenGL 渲染控件（D3D11 interop）；
- 已被 Richasy/Bili.Copilot 实际使用；
- **无任何菜单实现**：无 MenuFlyout/ContextFlyout、无本地化资源、无配置文件 UI。

## 3. 对本项目的适用性

| 本宿主关注点 | 该仓库是否有可借鉴 | 说明 |
|---|---|---|
| 右键菜单渲染 | ❌ 无 | 仓库没有菜单代码；WinUI 官方 MenuFlyout 仍是唯一现成组件 |
| 菜单稳定/卡死 | ❌ 无 | 与该仓库无关 |
| 视频渲染 | ✅ 有（不适用本宿主） | 若未来把 mpv 视频嵌入 WinUI，D3D11 interop 是标准做法 |
| libmpv 调用 | ✅ 有（架构级参考） | Mpv.Core 展示进程内 libmpv 绑定；本宿主当前走 JSON IPC（mpv.exe 独立进程） |
| 本地化/配置 | ❌ 无 | 本地化机制仍以本仓库 dyn_menu str_* + script-opts 为准 |

## 4. 结论

- 两个链接均与**视频渲染集成**相关，不提供菜单渲染的现成库/案例；
- 当前 v4.2 菜单宿主无需改动；
- 若用户目标是“WinUI 进程内直接嵌入 libmpv（去掉独立宿主进程 + JSON IPC）”，
  该仓库可作为架构参考（Mpv.Core 绑定模式），但那是整体播放器重构，需单独决策。

## 5. 追加研究：ikas-mc/mpv-winui-player（2026-08-07）

用户追加提供；这是与本项目最接近的现成案例：

- 结构：`mpv-winrt`（C++/WinRT 投影 libmpv）+ `mpv-winui`（WinUI 3 播放器）；
- 菜单：`GetMenu()` 读 mpv 原生 `menu-data` 属性（MPV_FORMAT_NODE），
  `ParseMenuNode` 递归解析 title/cmd/type/state[checked,disabled,hidden]/submenu；
  `BuildMenuFlyoutFromData` 用 WinUI 官方 MenuFlyout 家族渲染——数据模型与
  我们 JSON 完全一致（验证 F1/C5 同源与字段兼容）；
- 渲染细节：`&` 助记符剥离（Replace("&","")）、空子菜单跳过、连续分隔线去重、
  checked→ToggleMenuFlyoutItem、点击→进程内 libmpv 命令；
- 本地化：`AppLang` C# 属性英文默认 + 语言包导入导出（与我们 str_* 方向一致）；
- 设置：Settings 模块提供 Boolean/Double/Integer/List/String/StringList 选项与
  FileSetting/AppDataSetting 配置存储。

### 已吸收到本宿主（v4.3）

- `&&` → `&` 标题还原（dyn_menu escape_title 的 Win32 转义，WinUI 不解释 &）；
- 空子菜单（含全部 hidden）不再渲染；
- 连续分隔线去重。

### 未吸收（架构差异）

- 进程内 libmpv（`mpv_command_string`）vs 本宿主 JSON IPC（mpv.exe 独立进程）：
  前者是整体播放器重构方向，非菜单层改动；
- `CheckMpvMenu` 过滤内置 Open/Stop/Quit：本宿主数据源无这些重复项。

## 6. 试构建记录（2026-08-07）

按用户要求试跑 ikas-mc/mpv-winui-player：

- 已浅克隆到 `work/reference/mpv-winui-player`；
- `dotnet build mpv-winui.slnx`：C# 三个项目（mpv-winui/mpv-winrt-projection/
  mpv-winrt-test）NuGet 恢复成功；`mpv-winrt.vcxproj`（C++/WinRT）失败：
  `MSB4278 Microsoft.Cpp.Default.props 不存在`——本机未安装 Visual Studio
  C++ 工具链（vswhere 返回空）；
- 另缺 libmpv 开发文件：仓库不包含 `libs/include`、`libs/lib`、`libs/bin/mpv-2.dll`
  （csproj/vcxproj 显式引用），需自备 libmpv dev 包。

要真正试跑还需：① VS Build Tools “使用 C++ 的桌面开发”工作负载（含 Windows SDK
10.0.26100）；② libmpv dev（mpv-2.dll + mpv.lib + 头文件）放入 `libs/`。
两者均属外部安装/下载，未擅自执行。

## 7. 试跑成功记录（2026-08-07，用户已授权安装工具链）

- 经 winget 安装 VS Build Tools 2022 17.14（`C:\BuildTools`，v143 工具集
  14.44 + Windows SDK 10.0.26100）；
- libmpv dev 取自作者 CI 同源：ikas-mc/mpv-windows-builder 20260731
  `mpv-2.dll.zip`（含 include/lib/bin，58MB mpv-2.dll），已解到
  `mpv-winui/libs`；
- 沙箱副本修复（未动上游）：vcxproj 补 OutDir/IntDir、去掉
  `ApplicationType=Windows Store`（本机无 UWP C++ 组件）、LanguageStandard 固定
  stdcpp20、补 WebView2 winmd 引用；csproj 改直连 winmd（引用名带 .winmd 后缀）
  + 拷贝 mpv_winrt.dll、去掉重复 WebView2Loader、关闭 MSIX 签名；
- 构建：`mpv_winrt.dll`（C++/WinRT）用 VS MSBuild 单独编过；
  `mpv-winui`（C#）用 dotnet build Debug x64 编过，**0 警告 0 错误**；
- 启动：`bin\...\win-x64\mpv-winui.exe` 已运行，窗口标题 `mpv winui`；
- 与上游差异仅限本沙箱副本；上游仓库自身在 VS2026 CI 上构建，本机做法是
  等价降级（v143 + 桌面 DLL 形态）。

## 8. 接入 mpv-lazy 自定义配置（2026-08-07，用户指示“按照他的来”）

- 他的机制：未打包模式 `config-dir = %LOCALAPPDATA%\mpv-winui\mpv`；
- 已把 `C:\Program Files\mpv-lazy\portable_config` 灌入该目录（69 文件/32.4MB）：
  - 排除：`tools\winui-menu-host`（266MB 菜单宿主，本架构不需要）、
    `_cache`（55MB 缓存）、`recent.json`/`saved-props.json`（用户状态，重建）；
- 种子配置微调（仅 App 数据副本，未动 mpv-lazy 原目录）：
  - `host_menu=no`：菜单由 WinUI App 自己渲染（读 `menu-data`），不再拉起
    MpvMenuHost.exe；
  - `use_mpv_impl=yes`：dyn_menu 直写 `menu-data`，与 App 的 `GetMenu()`
    读取链路闭合；
- 验证：App 重启正常；脚本（dyn_menu/console/modernz 等）加载、状态文件
  `saved-props.json` 与着色器缓存均成功写入（目录可写）；NLog 无异常；
- 已知预期差异：mpv.conf 的 `vo/gpu-context/force-window` 等会被 App 的
  libmpv 选项覆盖；README 自述 quit/窗口类命令在 App 内需 App 处理，
  部分 input.conf 绑定可能无效。

## 9. 汉化 + 图标 + 精简滤镜 + 独立配置项目（2026-08-07，用户反馈后）

- **独立配置项目**：`work/reference/mpv-winui-player/mpv-winui-lazy/`
  （复刻 mpv-lazy 文件夹结构，69 文件/约 32MB），含
  `deploy-config.ps1` 一键同步到 `%LOCALAPPDATA%\mpv-winui\mpv`；
- **精简滤镜**：dynamic_menu.lua 只保留 Nvidia VSR + RTX Video HDR +
  “清空所有脚本”，删除 RIFE/K7F/着色器；input.conf 删除 VF 预设
  （补帧/UAI）；`vs/`、`shaders/` 不再引用；
- **汉化**：AppLang 默认值改中文；MpvPlayerPage 菜单栏/播放列表右键、
  PlayerControl 按钮提示、SettingsWindow 标题改中文；
- **右键菜单图标**：把菜单宿主的 IconMap（157+47 + icon-map.json 覆盖 +
  & 转义还原）移植进 App，MenuFlyout 叶子/子菜单/勾选项补图标；
- 构建 0 警告 0 错误，配置已部署，App 已重启运行；
- 待实测：VSR（vf d3d11vpp）应可用；RTX HDR 自动模式仍受
  `d3d11-output-mode=composition` 拿不到显示信息的限制（README 已知），
  若自动不生效可先用手动“强制开启”项。

## 10. HDR 自动 + 进度条 + 菜单重排（2026-08-07，用户实测反馈后）

- **HDR 自动模式修复**：hdr_auto.lua 的 display_hdr() 在
  `video-target-params` 不可用时回退读取 App 写入的
  `user-data/mpvw/color-kind == "HDR"`，并 observe 该属性即时重估；
  自动模式在 WinUI 嵌入下可正常触发；
- **进度条清除**：移除 ModernZ（modernz.lua/conf/locale）——持久进度条
  （persistent_progress=yes）随之消失；mpv.conf 的 osc=no 保留并更新注释；
- **菜单重排**：195 → 160 条（删除 35 条），逐项审计见
  `mpv-winui-lazy/menu-audit.md`；删除：打开子菜单（与 App 文件菜单重复）、
  窗口子菜单（嵌入无效）、查看>缩放（window-scale 无效）、
  查看>切换 OSC（ModernZ 移除）、工具>退出稍后观看/OSD命令行/重复输出设备、
  视频>自动校色/截屏（重复）、顶层退出；
- **附带清理**：删除 k7f_zen.lua 及其 mpv.conf 选项（复杂滤镜已移除）；
- 部署：配置项目已同步 AppData，App 已重启；AppData 中 ModernZ/K7F
  残留文件已清除（robocopy 不会自动删多余文件，需手动清）。

## 11. RTX HDR 输出色彩空间修复（2026-08-07，泛白反馈后）

- 根因：App 只写 `user-data/mpvw/color-kind`，mpv 输出仍停留在 sRGB
  （d3d11-output-csp 默认），RTX HDR 的 PQ 内容被当 sRGB 显示 → 泛白；
- 修复：按作者 README 的 workaround 在 profiles.conf 增加
  `mpvw-sdr/mpvw-wcg/mpvw-hdr`，按 color-kind 切换
  `d3d11-output-csp`（srgb/display-p3/pq）+ `d3d11-output-format=rgb10_a2`；
- 顺带删除 ModernZ 的 ui_scale/portrait_ui profiles（脚本已移除）；
- 自动模式诊断：hdr_auto 在切换自动模式或 color-kind 变化时 OSD 显示
  “屏幕检测 SDR/HDR/未知”，便于确认 App 是否识别到 HDR 显示器；
- 重要前提：RTX Video HDR 需要 Windows HDR 开启（设置→系统→屏幕→HDR），
  否则 color-kind=SDR，自动不启用；强制模式在 SDR 输出下泛白属预期。
