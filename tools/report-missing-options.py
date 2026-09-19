"""Report the mpv options the settings window does not expose.

The catalogue holds 820 options; the window exposes 212. The gap is not a
backlog - most of it is options a player's settings window should never show
(terminal graphics protocols, encoder output, decoder tuning, scripting
plumbing). The useful question is which of the remainder a normal user would
recognise and want, which is a judgement, so it lives here as an explicit
curated list rather than a heuristic.

Everything is excluded with a stated reason, so a new mpv release that adds
options reports them as unreviewed instead of silently joining either pile.
That is the point: this is meant to be re-run, not read once.

Run:  python tools/report-missing-options.py
      python tools/report-missing-options.py --check   # fail on anything unreviewed
"""
import argparse
import collections
import json
import re
import sys

CATALOGUE = "mpv-winui/mpv-winui/MpvConfOptions/mpv-options.json"
MPVSETTINGS = "mpv-winui/mpv-winui/Modules/Settings/MpvSettings.cs"

# --- excluded, each with the reason it will never earn a settings row --------
# Labels stay English because they identify the rule in code; the report shows
# REASON_ZH so the document itself reads in one language.
REASON_ZH = {
    "Encoder / muxer output": "编码 / 封装输出",
    "Terminal and console output": "终端与字符界面输出",
    "Graphics API internals": "图形 API 内部项",
    "Scaler tuning parameters": "缩放算法微调参数",
    "Decoder tuning": "解码器微调",
    "Demuxer probing internals": "解复用 / 探测内部项",
    "Scripting and config plumbing": "脚本与配置管线",
    "Diagnostics": "诊断与日志",
    "Platforms other than Windows": "非 Windows 平台",
    "Legacy or superseded": "过时或被取代",
    "Disc / capture hardware": "光盘 / 采集硬件",
    "Command-line-shaped or runtime state": "命令行形态或运行时状态",
    "Replaced by the app's own control bar": "已由应用自带控制条取代",
    "Feature removed from this build": "本版本已删除的功能",
    "Already written, just not by a one-value mapping": "已在写入，只是不是单值映射",
    "Superseded by an app setting": "已有对应的应用设置",
}

# Matched against the option name; first matching rule wins.
EXCLUDED: list[tuple[str, list[str]]] = [
    (
        "Encoder / muxer output",
        [r"^o(vc|ac|f|fopts|acopts|vcopts|copy-metadata|remove-metadata|set-metadata|rawts)$",
         r"^ao-pcm-", r"^ao-null-", r"^stream-(record|dump)$", r"^muxer"],
    ),
    (
        "Terminal and console output",
        [r"^vo-(kitty|sixel|tct)", r"^vo-kitty$", r"^vo-sixel$", r"^vo-tct$",
         r"^term-", r"^terminal$", r"^quiet$", r"^really-quiet$", r"^teletext-page$"],
    ),
    (
        "Graphics API internals",
        [r"^angle-", r"^egl-", r"^opengl-", r"^vulkan-", r"^spirv-", r"^fbo-format$", r"^gpu-", r"^libplacebo-opts$", r"^vo-direct3d-",
         r"^vo-mmcss-profile$", r"^d3d11va-zero-copy$",
         r"^hwdec-(extra-frames|image-format|threads)$",
         r"^d3d11-(composition-size|feature-level|output-mode)$",
         r"^video-latency-hacks$", r"^untimed$"],
    ),
    (
        "Scaler tuning parameters",
        [r"^(scale|cscale|dscale|tscale)-(antiring|blur|clamp|param1|param2|radius|taper|window|wparam|wtaper)$",
         r"^sigmoid-(center|slope)$", r"^dither-size-fruit$", r"^deband-grain$",
         r"^(sws|zimg)-", r"^scaler-resizes-only$", r"^error-diffusion$",
         r"^temporal-dither", r"^correct-pts$", r"^interpolation-(preserve|threshold)$"],
    ),
    (
        "Decoder tuning",
        [r"^vd-lavc-", r"^ad-lavc-", r"^(vd|ad)-queue-", r"^video-backward-",
         r"^audio-backward-", r"^video-sync-max-", r"^video-timing-offset$",
         r"^vf$", r"^af$", r"^vd$", r"^ad$", r"^vo$", r"^ao$", r"^video-sync-max-factor$"],
    ),
    (
        "Demuxer probing internals",
        [r"^demuxer", r"^stream-lavf-o$", r"^stream-buffer-size$"],
    ),
    (
        "Scripting and config plumbing",
        [r"^script", r"^load-", r"^input-", r"^profile", r"^include$", r"^config$",
         r"^reset-on-next-file$", r"^stop-playback-on-init-failure$",
         r"^playlist-inherit-options$", r"^cover-art-", r"^screenshot-dir$",
         r"^screenshot-avif-(opts|pixfmt)$", r"^watch-history-path$",
         r"^save-watch-history$", r"^write-filename-in-watch-later-config$",
         r"^use-filedir-conf$", r"^autoload-files$", r"^index$",
         r"^sub-filter-(regex|jsre)", r"^sub-lavc-o$"],
    ),
    (
        "Diagnostics",
        [r"^(msg|log)-", r"^log-file$", r"^gpu-debug$", r"^status-msg$",
         r"^osd-status-msg$", r"^osd-msg[123]$", r"^dump-stats$", r"^frames$",
         r"^benchmark$", r"^list-", r"^access-references$", r"^js-memory-report$"],
    ),
    (
        "Platforms other than Windows",
        [r"^x11-", r"^wayland-", r"^vaapi-", r"^openal-",
         r"^clipboard-(xwayland|backends|monitor)$", r"^native-(fs|touch|keyrepeat)$",
         r"^macos", r"^cocoa", r"^treat-srgb-as-power22$"],
    ),
    (
        "Legacy or superseded",
        [r"^mf-(fps|type)$", r"^force-rgba-osd-rendering$", r"^show-dependent-tracks$",
         r"^vo-image-", r"^display-tags$", r"^container-fps-override$",
         r"^display-fps-override$", r"^screenshot-(avif|jpeg|jxl|png|webp)-",
         r"^sub-border-style$", r"^osd-border-style$", r"^sub-past-video-end$",
         r"^sub-gray$", r"^osd-bar-marker-(min-size|scale)$", r"^osd-playlist-entry$",
         r"^sub-vsfilter-bidi-compat$", r"^sub-fix-timing-keep$",
         r"^sub-(speed|fps)$", r"^sub-stretch-durations$",
         r"^osd-(bitmap-max-size|glyph-limit|prune-delay)$", r"^sub-glyph-limit$"],
    ),
    (
        "Disc / capture hardware",
        [r"^(bluray|dvd)-", r"^disc-", r"^stretch-dvd-subs$", r"^chapter-(merge|seek)-",
         r"^edition$", r"^flatten-editions$", r"^ordered-chapters", r"^chapters-file$",
         r"^external-files$", r"^merge-files$", r"^audio-files$", r"^sub-files$",
         r"^playlist-exts$", r"^media-controls$", r"^register", r"^unregister$",
         r"^rar-list-all-volumes$", r"^archive-exts$", r"^metadata-codepage$"],
    ),
    (
        "Command-line-shaped or runtime state",
        [r"^(start|end|length|geometry|rebase-start-time)$", r"^vd-apply-cropping$",
         r"^title$", r"^osd-bar-outline-size$", r"^curl-enabled$",
         r"^(audio|sub|video)-demuxer$"],
    ),
    (
        "Feature removed from this build",
        [r"^ytdl"],
    ),
    (
        "Already written, just not by a one-value mapping",
        [r"^glsl-shaders$", r"^(speed|volume)$",
         r"^(aid|sid|vid|secondary-sid|vlang|track-auto-selection)$",
         r"^pause$", r"^border-background$"],
    ),
    (
        "Replaced by the app's own control bar",
        [r"^osd-bar(-align-x|-align-y|-marker-style)?$", r"^osd-bar-(w|h)$"],
    ),
    (
        "Superseded by an app setting",
        [r"^backdrop-type$", r"^sub-scale-with-window$", r"^window-maximized$",
         r"^sub-ass$", r"^osc$", r"^force-media-title$", r"^force-window",
         r"^force-render$", r"^force-seekable$", r"^vo-null-fps$",
         r"^window-minimized$", r"^screen", r"^fs-screen-name$", r"^wid$", r"^mc$",
         r"^idle$", r"^sstep$", r"^playlist-start$", r"^focus-on$",
         r"^player-operation-mode$", r"^priority$", r"^window-affinity$",
         r"^initial-audio-sync$", r"^audio-set-media-role$", r"^audio-swresample-o$",
         r"^audio-fallback-to-null$", r"^autosync$", r"^video-osd$",
         r"^volume-gain", r"^sub-create-cc-track$",
         r"^sub-auto-exts$", r"^audio-resample-",
         r"^image-lut", r"^lavfi-complex$", r"^lut-type$", r"^icc-use-luma$"],
    ),
]

# --- reviewed and worth exposing ---------------------------------------------
# mpv option -> (where it belongs, why a user would want it)
CANDIDATES: dict[str, tuple[str, str]] = {
    # --- Player --------------------------------------------------------------
    "shuffle": ("Player", "随机播放整个播放列表。"),
    "resume-playback-check-mtime": ("Player", "文件被改过就不恢复上次进度。"),
    "ignore-path-in-watch-later-config": ("Player", "阻止每个文件自己的配置覆盖全局设置。"),
    "play-direction": ("Player", "正向或反向播放。"),
    "keep-open-pause": ("Player", "播完最后一帧时暂停，而不是停住不动。"),
    "hr-seek-demuxer-offset": ("Playback", "为结构特殊的容器微调定位点。"),

    # --- Window --------------------------------------------------------------
    "fs-screen": ("Window", "全屏用哪块显示器。多屏时很有用。"),
    "window-scale": ("Window", "按视频尺寸的几分之一开窗。"),
    "ontop-level": ("Window", "把窗口钉在最上层。"),
    "corner-rounding": ("Window", "Windows 11 窗口圆角半径。"),
    "window-corners": ("Window", "系统是否给窗口加圆角。"),
    "autofit": ("Window", "窗口最大不超过屏幕的某个比例。"),
    "autofit-larger": ("Window", "autofit 的上限。"),
    "autofit-smaller": ("Window", "autofit 的下限。"),
    "hidpi-window-scale": ("Window", "高 DPI 屏幕上缩放窗口。常见困扰。"),
    "monitoraspect": ("Window", "显示器宽高比，用于非 16:9 屏幕。"),
    "monitorpixelaspect": ("Window", "显示器像素宽高比，与上一条配套。"),
    "keepaspect": ("Window", "缩放窗口时保持宽高比。"),
    "keepaspect-window": ("Window", "允许窗口比视频大而不拉伸画面。"),
    "taskbar-progress": ("Window", "在任务栏按钮上显示播放进度。"),
    "on-all-workspaces": ("Window", "让播放器出现在所有虚拟桌面上。"),
    "show-in-taskbar": ("Window", "是否在任务栏显示。"),
    "window-dragging": ("Window", "按住画面拖动窗口。"),
    "stop-screensaver": ("Window", "播放时不让屏幕休眠。"),
    "drag-and-drop": ("Window", "拖文件到窗口时是否打开。"),
    "snap-window": ("Window", "移动到屏幕边缘时自动吸附。"),

    # --- Video ---------------------------------------------------------------
    "video-scale-x": ("Video", "额外的水平缩放。"),
    "video-scale-y": ("Video", "额外的垂直缩放。"),
    "video-recenter": ("Video", "把缩放和平移复位到居中。"),
    "video-crop": ("Video", "裁剪画面，例如切掉压制进去的黑边。"),
    "video-margin-ratio-left": ("Video", "左侧留白，给 OSD 和字幕用。"),
    "video-margin-ratio-right": ("Video", "右侧留白。"),
    "video-margin-ratio-top": ("Video", "顶部留白。"),
    "video-margin-ratio-bottom": ("Video", "底部留白。"),
    "video-aspect-method": ("Video", "覆盖宽高比时用哪种算法计算。"),
    "background": ("Video", "画面背后用纯色还是棋盘格。"),
    "background-color": ("Video", "棋盘格关闭时的背景色。"),
    "background-blur-radius": ("Video", "把画面背后的区域做模糊。"),
    "deband-iterations": ("Video", "去色带的强度，也就是开销。"),
    "deband-threshold": ("Video", "渐变差异多大才启动去色带。"),
    "deband-range": ("Video", "去色带时向多远处找相近颜色。"),
    "hwdec-software-fallback": ("Video", "硬解失败时回退到软解。"),
    "deinterlace-field-parity": ("Video", "隔行片源里哪个场算先出现。"),
    "framedrop": ("Video", "是否允许丢帧以保持音画同步。"),
    "swapchain-depth": ("Video", "缓冲帧数；调低可能减少延迟。"),
    "cuda-decode-device": ("Video", "双显卡机器上用哪块卡解码。"),

    # --- Colour --------------------------------------------------------------
    "use-embedded-icc-profile": ("Colour", "采用文件内嵌的 ICC 配置。"),
    "icc-intent": ("Colour", "ICC 转换使用的渲染意图。"),
    "hdr-peak-percentile": ("Colour", "从直方图里取哪个位置作为实测峰值。"),
    "hdr-reference-white": ("Colour", "HDR 转 SDR 时的参考白电平。"),
    "target-contrast": ("Colour", "计算色调映射曲线时对标的对比度。"),
    "target-lut": ("Colour", "色调映射要对的 LUT。"),
    "tone-mapping-param": ("Colour", "需要参数的色调映射曲线用的那个参数。"),
    "allow-delayed-peak-detect": ("Colour", "等峰值检测稳定后再做色调映射。"),
    "sdr-adjust-gamma": ("Colour", "与 HDR 并排时 SDR 内容的 gamma。"),
    "gamma-factor": ("Colour", "额外的 gamma 校正。"),
    "lut": ("Colour", "加载 3D LUT 做校色。"),

    # --- Audio ---------------------------------------------------------------
    "pitch": ("Audio", "播放音高，用于修正变速后的声音。"),
    "mute": ("Audio", "启动时静音。"),
    "wasapi-exclusive-buffer": ("Audio", "WASAPI 独占模式的缓冲大小。"),
    "audio-client-name": ("Audio", "音量合成器里显示的播放器名称。"),
    "replaygain-preamp": ("Audio", "配合 ReplayGain 使用的前级增益。"),
    "replaygain-clip": ("Audio", "允许 ReplayGain 削波，而不是整体压低音量。"),
    "replaygain-fallback": ("Audio", "文件没有 ReplayGain 标签时的回退增益。"),

    # --- Subtitles -----------------------------------------------------------
    "sub-ass-styles": ("Subtitles", "ASS 字幕使用的样式。"),

    # --- OSD -----------------------------------------------------------------

    # --- Network -------------------------------------------------------------
    "cookies": ("Network", "是否发送 Cookie（我们只暴露了文件路径）。"),
    "tls-ca-file": ("Network", "自定义 HTTPS 的 CA 证书包。"),
    "tls-cert-file": ("Network", "客户端证书。"),
    "tls-key-file": ("Network", "客户端证书私钥。"),
    "hls-bitrate": ("Network", "指定 HLS 的画质档位。"),
    "rtsp-transport": ("Network", "RTSP 流的传输方式。"),
    "curl-http-version": ("Network", "对难缠的服务器强制某个 HTTP 版本。"),
}


def load_exposed() -> set[str]:
    src = open(MPVSETTINGS, encoding="utf-8-sig").read()
    names = {m.group(2) for m in re.finditer(
        r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*"([^"]+)"', src)}
    # user-data/mpvw/* are runtime properties, not options.
    return {n for n in names if "/" not in n}


def classify(name: str) -> str | None:
    for reason, patterns in EXCLUDED:
        for p in patterns:
            if re.search(p, name):
                return reason
    return None


def write_markdown(opts, exposed, by_reason, curated, path="docs/missing-options.md"):
    areas: dict[str, list[str]] = collections.defaultdict(list)
    for name in curated:
        areas[CANDIDATES[name][0]].append(name)

    lines = [
        "# 设置界面尚未暴露的 mpv 选项",
        "",
        "本文件由 `tools/report-missing-options.py --write-md` 生成，**不要手工编辑**。",
        "重新运行即可随 mpv 版本更新。",
        "",
        "## 总量",
        "",
        f"- mpv 选项目录：**{len(opts)}** 条",
        f"- 设置界面已暴露：**{len(exposed)}** 条",
        f"- 已废弃、跳过：**{sum(1 for o in opts if o.get('deprecated'))}** 条",
        f"- 未暴露且未废弃：**{sum(by_reason.values()) + len(curated)}** 条",
        f"  - 其中**不建议暴露**：**{sum(by_reason.values())}** 条（理由见下）",
        f"  - 其中**建议补充**：**{len(curated)}** 条",
        "",
        "## 一、建议补充的选项（%d）" % len(curated),
        "",
        "按归属分类。这些是普通用户能看懂、也确实会想改的项。",
        "",
    ]
    for area in sorted(areas, key=lambda a: (-len(areas[a]), a)):
        lines.append(f"### {area}（{len(areas[area])}）")
        lines.append("")
        lines.append("| mpv 选项 | 为什么值得加 |")
        lines.append("|---|---|")
        for name in sorted(areas[area]):
            lines.append(f"| `{name}` | {CANDIDATES[name][1]} |")
        lines.append("")

    lines += [
        "## 二、不建议暴露的选项（%d）" % sum(by_reason.values()),
        "",
        "每一类都给了理由。这不是「暂时没做」，而是「设计上不该出现在设置界面」。",
        "",
        "| 类别 | 条数 |",
        "|---|---|",
    ]
    for reason, _ in EXCLUDED:
        if by_reason[reason]:
            lines.append(f"| {REASON_ZH.get(reason, reason)} | {by_reason[reason]} |")
    lines += [
        "",
        "判定规则写在 `tools/report-missing-options.py` 的 `EXCLUDED` 里，",
        "每条规则带中文理由；新版本 mpv 新增的选项若两边都没落到，",
        "`--check` 会直接失败，以便逐条判断而不是默认忽略。",
        "",
    ]
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    print(f"wrote {path}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="fail on any option that is neither exposed, curated nor excluded")
    ap.add_argument("--write-md", action="store_true",
                    help="also write docs/missing-options.md")
    args = ap.parse_args()

    opts = json.load(open(CATALOGUE, encoding="utf-8-sig"))
    exposed = load_exposed()
    catalogue = {o["name"] for o in opts}

    # A curated name that mpv does not have is a typo or an invented option, and
    # it would quietly inflate the list of things to add.
    bogus = sorted(set(CANDIDATES) - catalogue)
    if bogus:
        print(f"FAIL: curated names that are not mpv options: {bogus}")
        return 1

    # A curated name that is already exposed is finished work still sitting
    # in the to-do list, which overstates the remaining effort.
    shipped = sorted(n for n in CANDIDATES if n in exposed)
    if shipped:
        print(f"FAIL: curated but already exposed: {shipped}")
        return 1

    # Two piles at once means the rule and the curation disagree.
    both = sorted(n for n in CANDIDATES if classify(n) is not None)
    if both:
        print(f"FAIL: curated but also excluded by a rule: {both}")
        return 1

    deprecated = [o["name"] for o in opts if o.get("deprecated")]
    missing = [o["name"] for o in opts
               if o["name"] not in exposed and not o.get("deprecated")]

    by_reason = collections.Counter()
    unreviewed = []
    for name in missing:
        reason = classify(name)
        if reason:
            by_reason[reason] += 1
        elif name not in CANDIDATES:
            unreviewed.append(name)

    curated = [n for n in missing if classify(n) is None and n in CANDIDATES]

    print(f"catalogue                      : {len(opts)}")
    print(f"exposed by the settings window : {len(exposed)}")
    print(f"deprecated, skipped            : {len(deprecated)}")
    print(f"unexposed, not deprecated      : {len(missing)}")
    print()
    print("excluded, by reason:")
    for reason, _ in EXCLUDED:
        if by_reason[reason]:
            print(f"   {by_reason[reason]:4d}  {reason}")
    print()
    print(f"curated as worth exposing      : {len(curated)}")
    areas = collections.Counter(CANDIDATES[n][0] for n in curated)
    for area, c in areas.most_common():
        print(f"   {c:4d}  {area}")

    if unreviewed:
        print()
        print(f"UNREVIEWED ({len(unreviewed)}) - each needs a decision:")
        for name in sorted(unreviewed):
            print(f"      {name}")
        if args.check:
            print("FAIL: options are neither exposed, curated nor excluded")
            return 1
    else:
        print()
        print("OK: every unexposed option is either excluded with a reason or curated")

    if args.write_md:
        write_markdown(opts, exposed, by_reason, curated)

    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
