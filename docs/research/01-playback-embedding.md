# 01 · 播放/渲染嵌入：备选方案线索

> **状态：当前问题已修复（commit `07a99fb`），本文只收集备选方案。**
> 修复内容：`MpvPlayer::Initialize` 在 `mpv_initialize` 之后用
> `mpv_set_property_string` 重设 vo 选项，因为配置文件会覆盖初始化前设的值。
> 结果为单窗口、画面在 `SwapChainPanel` 内、HDR 与封面正常。
> **本文不是待办，是"如果将来 composition 路线出问题，还有哪些路可走"。**

---

## 一、线索清单

### 1.1 当前路线：composition swapchain（已采用）

| 项 | 内容 |
|---|---|
| 来源 | 本仓库 `src/mpvw-native/MpvPlayer.cpp` + mpv 官方文档 |
| 技术栈 | libmpv `d3d11-output-mode=composition` + `display-swapchain` 属性 + `ISwapChainPanelNative` |
| 核心机制 | mpv 用 D3D11 创建 composition swapchain，把 `IDXGISwapChain*` 通过 `display-swapchain` 属性暴露；宿主用 `ISwapChainPanelNative::SetSwapChain` 交给 XAML 合成 |
| 可参考部分 | 本仓库 `AttachSwapChain` / `CompletePendingSwapChainAttach` / `SetSwapChainOnPanel` 已完整实现，含 DPI 缩放矩阵 |
| 与当前项目的关系 | **即是现状**。作为后续方案的基线 |

**关联文档**（都有可复用的互操作细节）：

- [SwapChainPanel 类（Windows App SDK）](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.swapchainpanel) — 明确写 WinUI 3 的 `SwapChainPanel` 不支持透明度
- [How to consume ISwapChainPanelNative from SwapChainPanel IntPtr](https://stackoverflow.com/questions/64932443/how-to-consume-iswapchainpanelnative-interface-from-swapchainpanel-intptr) — WinUI 3 要 include `microsoft.ui.xaml.media.dxinterop.h`，不是 `windows.ui.xaml...`
- [Exception in ISwapChainPanelNative Only in Release Mode](https://www.simonmourier.com/blog/Exception-in-ISwapChainPanelNative-Only-in-Release-Mode-WinUI-3-csharp/) — C# 侧用源生成 COM interop（`GeneratedComInterface`）替代手写 vtable，Release 模式才暴露的坑
- [DirectN issue #8](https://github.com/smourier/DirectN/issues/8) — `mypanel.As<ISwapChainPanelNative>()` 的可用写法
- [WinUI DirectX & SwapChainPanel Interop Tutorial](https://cosmiclearn.com/winui/directx-swapchainpanel.php) — D3D11/D3D12 两条路径的入门

### 1.2 备选 A：libmpv render API（`mpv_render_context`）

| 项 | 内容 |
|---|---|
| 来源 | [mpv 官方 `libmpv/render.h`](https://github.com/weiplanet/mpv/blob/master/libmpv/render.h)，引入于 [b037121](https://github.com/mpv-player/mpv/commit/b03712143012f08591de59d09675fdd3a0eaeed8) |
| 技术栈 | libmpv render API + 自建 D3D11 设备 + `SwapChainPanel` |
| 核心功能 | 宿主自己创建 D3D11 device/swapchain，把 mpv 的帧渲染进宿主的纹理，mpv 不再碰窗口 |
| **关键前置事实（实测）** | 随包的 `mpv-2.dll` **已导出** `mpv_render_context_create`、`mpv_render_context_render`、`mpv_render_context_set_update_callback`、`mpv_render_context_report_swap`。**不需要换库** |
| 可参考或可复用的部分 | `mpv_render_context_set_update_callback` → 通知哪一帧可渲染；`mpv_render_context_report_swap` → 告诉 mpv 已呈现（帧时序）；`MPV_RENDER_PARAM_D3D11_*` 参数组 |
| 与当前项目的关系 | **互为替代**。当前用 mpv 的 swapchain，本条用宿主的 swapchain。都走 `SwapChainPanel`，UI 侧不受影响 |

**取舍**：

- 优势：渲染目标完全由宿主掌控，可以叠加 XAML 内容、做自定义后处理、支持多面板同时渲染同一帧
- 成本：要自己管 D3D11 device、resize、present、device-lost 恢复；mpv 的 `d3d11-composition-size` 这套自适配要重新实现

### 1.3 备选 B：`wid` 子窗口嵌入（HWND embedding）

| 项 | 内容 |
|---|---|
| 来源 | mpv 官方 `--wid` 选项；[`x11_common: always create a window, even with --wid`](https://git.redxen.eu/RepoMirrors/mpv/commit/b23dce6d7cd66c11a770ce921659f7a19c804cf7) 记录了这个语义变迁 |
| 技术栈 | Win32 `SetParent` / `CreateWindowEx` + mpv `wid` |
| 核心功能 | 把 mpv 的窗口设成宿主某个 HWND 的子窗口，由 Windows 负责合成 |
| 可参考部分 | mpv.net 的 WinForms 宿主实现（见 1.5） |
| 与当前项目的关系 | **不推荐**。见下方风险 |

**已知限制（有出处，不是推测）**：

- WinUI 3 **没有**官方 HWND 宿主支持，WinUI 对嵌入的窗口一无所知
- 子 HWND **总是绘制在 XAML 内容之上**，压住所有浮层与提示
  出处：[microsoft/WindowsAppSDK Discussion #3879](https://github.com/microsoft/WindowsAppSDK/discussions/3879)、[StackOverflow #75764641](https://stackoverflow.com/questions/75764641/create-a-window-through-createwindow-in-winui3-and-place-the-xaml-control-befor)
- `SetParent` 在 WinUI 3 下的 resize / drag 有已知缺陷：[microsoft-ui-xaml issue #8779](https://github.com/microsoft/microsoft-ui-xaml/issues/8779)
- 要救浮层只能把 popup 改成独立 owned window，工作量翻倍

### 1.4 备选 C：离屏渲染 + 前端合成（Web 方案）

| 项 | 内容 |
|---|---|
| 来源 | [在 Tauri 里播 4GB MKV：我如何把 libmpv 塞进 WebView](https://xzd.co/post/2026-06-06-tauri-libmpv) |
| 技术栈 | `vo=libmpv` + 离屏 FBO + IPC 推帧 → Web Canvas |
| 核心功能 | mpv 在屏幕外渲染，帧数据经 IPC 送到前端，HTML/前端框架叠 UI |
| **最有价值的部分** | 作者记录了**失败路径**：`wid` 与 Web 层抢 layer 全线失败，最终放弃嵌入改离屏。这对判断"某条路走不通"比成功案例更有用 |
| 已知代价 | 作者自述预览限约 720p / 15fps |
| 与当前项目的关系 | **不适用**。本项目是 WinUI 原生，不是 WebView；且逐帧 IPC 会牺牲 HDR 与全帧率 |

**若将来考虑 Tauri/Electron 方向，可参考**：

- [tauri-plugin-libmpv](https://github.com/nini22P/tauri-plugin-libmpv) — 现成的 Tauri 插件，README 提到"嵌进 Tauri 窗口需要配置透明度"
- 同上 [docs.rs 页面](https://docs.rs/crate/tauri-plugin-libmpv/0.2.0) 有 `force-window: yes` 与 `keep-open: yes` 的配置示例

### 1.5 同类项目：C# / .NET 宿主 libmpv

| 项目 | 来源 | 技术栈 | 可参考部分 |
|---|---|---|---|
| **mpv.net** | [mpvnet-player/mpv.net](https://github.com/mpvnet-player/mpv.net) | C# / WinForms + WPF | 完整的 libmpv P/Invoke 封装、属性/事件映射、播放器 UI 分层；GPL v2（**许可不兼容，只能读不能抄**） |
| **Mpv.NET-lib-** | [hudec117/Mpv.NET-lib-](https://github.com/hudec117/Mpv.NET-lib-) | C# / WinForms + WPF | 更轻量的可嵌入封装，151 stars；适合看"最小可用 API 面" |
| **mpv-uwp** | [Dixeran/mpv-uwp](https://github.com/Dixeran/mpv-uwp) | UWP / C++ | **与本项目最接近的思路**：用 `SwapChainPanel` + d3d11，无需 ANGLE。虽然是 UWP 但 XAML/D3D 互操作部分可直接迁移 |

**mpv-uwp 值得单列**：它的 README 明确写"create a SwapChainPanel element, which can be used to render d3d11 or d2d scene... your video will render inside the SwapChainPanel"，正是本项目当前走的路线的最早雏形。

### 1.6 官方文档锚点

| 文档 | 用途 |
|---|---|
| [`--d3d11-output-mode=<auto｜window｜composition>`](https://raw.githubusercontent.com/mpv-player/mpv/master/DOCS/man/options.rst) | composition 模式的官方语义；同时说明 `d3d11-composition-size` 只在该模式生效 |
| [`--wid`](https://raw.githubusercontent.com/mpv-player/mpv/master/DOCS/man/options.rst) | 窗口嵌入的官方契约（"mpv will create its own window, and set the wid window as parent"） |
| [`libmpv/render.h`](https://github.com/weiplanet/mpv/blob/master/libmpv/render.h) | render API 的完整头文件；含 D3D11 参数说明 |
| [mpv.net Manual](https://github.com/mpvnet-player/mpv.net/blob/master/docs/Manual.md) | 了解 mpv.net 的命令/属性组织方式 |

---

## 二、适用性评估

| 方案 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| **1.1 composition（现状）** | 已上线 | 0（已完成） | WinUI 3 `SwapChainPanel` 不支持透明度；`AttachSwapChain` 必须在 UI 线程；Panel 一旦 detach 可能破坏 mpv 绑定（本仓库注释已记录） |
| **1.2 render API** | **高** | 中（2–4 人日） | 需自管 D3D11 device 生命周期与 device-lost；`report_swap` 不调用会导致帧时序漂移；已有 dll 支持，无供应链风险 |
| **1.3 `wid` 嵌入** | **低** | 高 | **airspace 问题无解**，浮层永久被压；`SetParent` 在 WinUI 3 有已知 resize 缺陷；要救浮层就得重做全部 popup |
| **1.4 离屏 + Web** | **不适用** | 极高 | 本项目非 WebView；逐帧 IPC 牺牲 HDR 与帧率；作者自测仅 720p/15fps |
| **1.5 同类项目参考** | **高（只读）** | 低 | mpv.net 是 **GPL v2**，与本仓库 LGPL-2.1 不兼容，**只能读实现思路，不能复制代码**（详见 `docs/upstream-gap.md` 的许可边界） |
| **1.6 官方文档** | 高 | 低 | `options.rst` 随版本变化，引用时应固定 commit |

### 许可特别提示

本仓库是 LGPL-2.1，`docs/upstream-gap.md` 记录了上游 `b5ddfb6` 之后改成 GPLv3、因此不能直接取用的先例。
**mpv.net 是 GPL v2，同样不能复制代码。** 可读其 API 组织方式后独立实现。

---

## 三、检索提示词

### 3.1 直接可用的搜索式

```
"mpv_render_context_create" D3D11 SwapChainPanel WinUI
libmpv render API example d3d11 host texture
mpv d3d11-output-mode composition display-swapchain
"ISwapChainPanelNative" WinUI 3 C# GeneratedComInterface
mpv wid embed child window WinUI 3 airspace
libmpv offscreen FBO IPC canvas frame pipeline
site:github.com mpv SwapChainPanel d3d11 嵌入
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C++/WinRT 的 mpv 播放器仓库（`mpvw`）。播放已能嵌入（composition swapchain 路线，见 `docs/runtime-facts.md` §1 与 `src/mpvw-native/MpvPlayer.cpp` 的 `AttachSwapChain`）。
>
> 现在要评估是否改用 **libmpv render API** 路线：宿主自建 D3D11 device + swapchain，用 `mpv_render_context_create` / `mpv_render_context_render` 把帧渲染进宿主纹理。
>
> 已知：随包的 `mpv-2.dll` 已导出全部 render API 符号，不需要换库。
>
> 请回答：
> 1. `MPV_RENDER_PARAM_D3D11_*` 这组参数在 WinUI 3 的 `SwapChainPanel` 场景下如何填？与直接拿 `display-swapchain` 有何本质区别？
> 2. `set_update_callback` 与 `report_swap` 的调用时机，以及漏调的后果？
> 3. device-lost、窗口 resize、DPI 变化三条路径怎么处理？
> 4. 与现有 `d3d11-composition-size` 自适配逻辑的冲突点在哪？
>
> 不要写代码，先给结论与对照表。

### 3.3 深挖方向

- mpv 的 `MPV_RENDER_PARAM_D3D11_BACKEND` / `_RA` / `_SWAPCHAIN` 逐个查 `render.h` 注释
- 搜 `mpv_render_context_report_swap` 的社区用法（漏调是最常见坑）
- 查 mpv 官方 `libmpv/client.h` 里 "Embedding the video window" 小节，对照 `wid` 与 render API 的官方推荐
