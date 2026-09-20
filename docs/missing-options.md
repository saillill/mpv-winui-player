# 设置界面尚未暴露的 mpv 选项

本文件由 `tools/report-missing-options.py --write-md` 生成，**不要手工编辑**。
重新运行即可随 mpv 版本更新。

## 总量

- mpv 选项目录：**820** 条
- 设置界面已暴露：**266** 条
- 已废弃、跳过：**8** 条
- 未暴露且未废弃：**550** 条
  - 其中**不建议暴露**：**478** 条（理由见下）
  - 其中**建议补充**：**72** 条

## 一、建议补充的选项（72）

按归属分类。这些是普通用户能看懂、也确实会想改的项。

### Video（20）

| mpv 选项 | 为什么值得加 |
|---|---|
| `background` | 画面背后用纯色还是棋盘格。 |
| `background-blur-radius` | 把画面背后的区域做模糊。 |
| `background-color` | 棋盘格关闭时的背景色。 |
| `cuda-decode-device` | 双显卡机器上用哪块卡解码。 |
| `deband-iterations` | 去色带的强度，也就是开销。 |
| `deband-range` | 去色带时向多远处找相近颜色。 |
| `deband-threshold` | 渐变差异多大才启动去色带。 |
| `deinterlace-field-parity` | 隔行片源里哪个场算先出现。 |
| `framedrop` | 是否允许丢帧以保持音画同步。 |
| `hwdec-software-fallback` | 硬解失败时回退到软解。 |
| `swapchain-depth` | 缓冲帧数；调低可能减少延迟。 |
| `video-aspect-method` | 覆盖宽高比时用哪种算法计算。 |
| `video-crop` | 裁剪画面，例如切掉压制进去的黑边。 |
| `video-margin-ratio-bottom` | 底部留白。 |
| `video-margin-ratio-left` | 左侧留白，给 OSD 和字幕用。 |
| `video-margin-ratio-right` | 右侧留白。 |
| `video-margin-ratio-top` | 顶部留白。 |
| `video-recenter` | 把缩放和平移复位到居中。 |
| `video-scale-x` | 额外的水平缩放。 |
| `video-scale-y` | 额外的垂直缩放。 |

### Window（20）

| mpv 选项 | 为什么值得加 |
|---|---|
| `autofit` | 窗口最大不超过屏幕的某个比例。 |
| `autofit-larger` | autofit 的上限。 |
| `autofit-smaller` | autofit 的下限。 |
| `corner-rounding` | Windows 11 窗口圆角半径。 |
| `drag-and-drop` | 拖文件到窗口时是否打开。 |
| `fs-screen` | 全屏用哪块显示器。多屏时很有用。 |
| `hidpi-window-scale` | 高 DPI 屏幕上缩放窗口。常见困扰。 |
| `keepaspect` | 缩放窗口时保持宽高比。 |
| `keepaspect-window` | 允许窗口比视频大而不拉伸画面。 |
| `monitoraspect` | 显示器宽高比，用于非 16:9 屏幕。 |
| `monitorpixelaspect` | 显示器像素宽高比，与上一条配套。 |
| `on-all-workspaces` | 让播放器出现在所有虚拟桌面上。 |
| `ontop-level` | 把窗口钉在最上层。 |
| `show-in-taskbar` | 是否在任务栏显示。 |
| `snap-window` | 移动到屏幕边缘时自动吸附。 |
| `stop-screensaver` | 播放时不让屏幕休眠。 |
| `taskbar-progress` | 在任务栏按钮上显示播放进度。 |
| `window-corners` | 系统是否给窗口加圆角。 |
| `window-dragging` | 按住画面拖动窗口。 |
| `window-scale` | 按视频尺寸的几分之一开窗。 |

### Colour（11）

| mpv 选项 | 为什么值得加 |
|---|---|
| `allow-delayed-peak-detect` | 等峰值检测稳定后再做色调映射。 |
| `gamma-factor` | 额外的 gamma 校正。 |
| `hdr-peak-percentile` | 从直方图里取哪个位置作为实测峰值。 |
| `hdr-reference-white` | HDR 转 SDR 时的参考白电平。 |
| `icc-intent` | ICC 转换使用的渲染意图。 |
| `lut` | 加载 3D LUT 做校色。 |
| `sdr-adjust-gamma` | 与 HDR 并排时 SDR 内容的 gamma。 |
| `target-contrast` | 计算色调映射曲线时对标的对比度。 |
| `target-lut` | 色调映射要对的 LUT。 |
| `tone-mapping-param` | 需要参数的色调映射曲线用的那个参数。 |
| `use-embedded-icc-profile` | 采用文件内嵌的 ICC 配置。 |

### Audio（7）

| mpv 选项 | 为什么值得加 |
|---|---|
| `audio-client-name` | 音量合成器里显示的播放器名称。 |
| `mute` | 启动时静音。 |
| `pitch` | 播放音高，用于修正变速后的声音。 |
| `replaygain-clip` | 允许 ReplayGain 削波，而不是整体压低音量。 |
| `replaygain-fallback` | 文件没有 ReplayGain 标签时的回退增益。 |
| `replaygain-preamp` | 配合 ReplayGain 使用的前级增益。 |
| `wasapi-exclusive-buffer` | WASAPI 独占模式的缓冲大小。 |

### Network（7）

| mpv 选项 | 为什么值得加 |
|---|---|
| `cookies` | 是否发送 Cookie（我们只暴露了文件路径）。 |
| `curl-http-version` | 对难缠的服务器强制某个 HTTP 版本。 |
| `hls-bitrate` | 指定 HLS 的画质档位。 |
| `rtsp-transport` | RTSP 流的传输方式。 |
| `tls-ca-file` | 自定义 HTTPS 的 CA 证书包。 |
| `tls-cert-file` | 客户端证书。 |
| `tls-key-file` | 客户端证书私钥。 |

### Player（5）

| mpv 选项 | 为什么值得加 |
|---|---|
| `ignore-path-in-watch-later-config` | 阻止每个文件自己的配置覆盖全局设置。 |
| `keep-open-pause` | 播完最后一帧时暂停，而不是停住不动。 |
| `play-direction` | 正向或反向播放。 |
| `resume-playback-check-mtime` | 文件被改过就不恢复上次进度。 |
| `shuffle` | 随机播放整个播放列表。 |

### Playback（1）

| mpv 选项 | 为什么值得加 |
|---|---|
| `hr-seek-demuxer-offset` | 为结构特殊的容器微调定位点。 |

### Subtitles（1）

| mpv 选项 | 为什么值得加 |
|---|---|
| `sub-ass-styles` | ASS 字幕使用的样式。 |

## 二、不建议暴露的选项（478）

每一类都给了理由。这不是「暂时没做」，而是「设计上不该出现在设置界面」。

| 类别 | 条数 |
|---|---|
| 编码 / 封装输出 | 24 |
| 终端与字符界面输出 | 40 |
| 图形 API 内部项 | 49 |
| 缩放算法微调参数 | 68 |
| 解码器微调 | 36 |
| 解复用 / 探测内部项 | 39 |
| 脚本与配置管线 | 58 |
| 诊断与日志 | 13 |
| 非 Windows 平台 | 13 |
| 过时或被取代 | 39 |
| 光盘 / 采集硬件 | 25 |
| 命令行形态或运行时状态 | 11 |
| 本版本已删除的功能 | 3 |
| 已在写入，只是不是单值映射 | 11 |
| 已由应用自带控制条取代 | 4 |
| 已有对应的应用设置 | 45 |

判定规则写在 `tools/report-missing-options.py` 的 `EXCLUDED` 里，
每条规则带中文理由；新版本 mpv 新增的选项若两边都没落到，
`--check` 会直接失败，以便逐条判断而不是默认忽略。

