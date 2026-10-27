# `osc-preview-api` 支持说明

本文件解释本项目对 mpv 的 **OSC Preview API** 提供到什么程度，以及为什么默认
配置下它不会做任何事。

---

## 1 协议本身（mpv 侧）

mpv 的 OSC 不自己画缩略图。它在需要缩略图时，把请求写进两个 `user-data` 属性：

| 属性 | 内容 |
|---|---|
| `user-data/osc/draw-preview` | 一个 map：`x` / `y` / `w` / `h`（缩略图必须占据的矩形，原生像素）、`hover-sec`（要显示的时间点，秒）、`ass`（可选，随缩略图一起渲染的 ASS 装饰串） |
| `user-data/osc/draw-request` | 同一套字段的早期形态；缩略图实现可以监听它 |

指针离开进度条时，缩略图实现把 `draw-preview` 清掉。

> **版本注意**：`hover-sec` 原本是独立属性 `user-data/osc/hover-sec`，后来被
> 移进 `draw-preview` 这个 map 内（mpv commit `59c91de`，提交信息
> "osc: add back hover-sec in draw-preview"）。原因是：如果一个缩略图实现只监听
> `draw-request`，而矩形因为边界夹取没有变化，独立属性就不会触发重绘。
> 把它放进 map 让请求自包含。

---

## 2 本项目实现了什么

### 2.1 写出端（C++/WinRT）

`MpvPlayer` 一直有两个方法在写这些属性：

| 方法 | 位置 | 写什么 |
|---|---|---|
| `SetHoverSec(sec)` | `MpvPlayer.cpp:2514` | `user-data/osc/hover-sec`（为旧版 mpv 保留） |
| `SetDrawPreview(x,y,w,h)` | `MpvPlayer.cpp:2519` | `user-data/osc/draw-preview` 的 map |
| `ClearPreview()` | `MpvPlayer.cpp:2561` | 清空 `draw-preview` 并删除 `hover-sec` |

**2026-10-22 的修正**：`SetDrawPreview` 原来只写 `x/y/w/h` 四个字段，把
`hover-sec` 留在独立属性里——也就是上面说的**旧形态**。现在 map 里也带
`hover-sec`（取自 `m_lastHoverSec`），符合现行规范；独立属性仍然照写，供旧版
mpv 使用。

调用方是 `Modules/Player/MpvPlayerPage_Preview.cs` 的
`ShowNativePreviewAt()`（`:208`），它把指针位置换算成原生像素坐标后调用这两个方法。

### 2.2 读出端（Lua）

`data/scripts/preview_api_bridge.lua` 是 2026-10-22 新增的消费方：

- 监听 `user-data/osc/draw-preview`；
- 把请求规整成 `user-data/mpvw/preview-request`（同样字段），这样 WinUI 层可以
  用合成器画，而不是让 mpv 画在 OSD 上；
- 暴露 `script-message preview-request <json>` 和 `script-message preview-clear`，
  第三方插件可以主动请求；
- 卸载 / 关闭时清理。

它是**桥**，不是缩略图渲染器。真正解码渲染帧的是 `MpvPreviewer`（第二个 libmpv
实例）和两个 C# 后端；在 Lua 里再写一个渲染器属于重复实现，也是本项目明确避免的
「手搓」。

### 2.3 三个后端的关系

| 后端 | 开关 | 谁画 | 位置 |
|---|---|---|---|
| **Built-in（默认）** | `EnableVideoBuiltInPreview` 默认 **true** | `MpvPreviewer` → XAML `Image` | `ShowBuiltInPreviewAt()` |
| **Native** | `EnableVideoPreview` 默认 **false** | mpv 合成进 swapchain | `ShowNativePreviewAt()` |
| **插件 / OSC** | 需 `osc=yes` | 由监听协议的一方决定 | `preview_api_bridge.lua` |

选择逻辑在 `MpvPlayerPage_Preview.cs:73`：
`_useBuiltInPreview = AppSetting.EnableVideoBuiltInPreview`。

---

## 3 为什么默认配置下它静默

`data/mpv.conf:198` 写着：

```
osc = no   # 【osc.lua】 禁用原生OSC（WinUI 自带控制条）
```

WinUI 有自己的控制栏，原生 OSC 会和它叠在一起。OSC 关掉，就没有东西写
`draw-preview`，`preview_api_bridge.lua` 也就什么都不做。

**这不是缺陷，是设计**：桥是为了让「用户开启 OSC」或「插件说同一套协议」时能工作，
默认不动。

如果你要验证它：临时把 `osc = no` 改成 `osc = yes`，重启，把指针移到原生 OSC 的
进度条上——`user-data/mpvw/preview-request` 会出现。

---

## 4 验证

```bash
python tools/mvpw-mcp/verify_preview_api.py
```

实测输出（2026-10-22，9/9 通过）：

```
PASS  bridge answers script-message (script is live)
PASS  script-message payload carries hover-sec
PASS  draw-preview accepted
PASS  draw-preview mirrored to the app
PASS  mirror carries the rectangle
PASS  mirror carries hover-sec from inside the map
PASS  clear propagates
PASS  no mpvw process left
```

---

## 5 来源与许可

| 项 | 来源 | 许可 |
|---|---|---|
| 协议规范 | mpv 手册 "OSC Preview API" 一节；`59c91de` | mpv 文档，GPL-2.0-or-later（仅引用，未复制代码） |
| `preview_api_bridge.lua` | 本项目原创 | LGPL-2.1-or-later |
| `SetHoverSec` / `SetDrawPreview` | 上游 `mpv-winui-player`，本项目修正字段 | LGPL-2.1 |
