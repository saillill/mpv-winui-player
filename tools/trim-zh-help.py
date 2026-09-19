#!/usr/bin/env python3
"""Trim the zh-CN help strings down to short, accurate, complete wording.

Only the zh-CN file is touched. The wording keeps every fact the original
carried; what goes away is the explanation-of-the-explanation -- the
"because ...", the second example, the reassurance -- which is what made
a row three lines tall in a normal window and six in a narrow one.

Rules applied per entry:
  * at most two clauses; the trailing rationale is dropped
  * enumerated values lose their sales pitch and keep the meaning
  * "留空 = 自动跟随显示器上报的信息" survives, it is the default
  * no fact, range, default value or unit is removed
  * the en dash range form (0–100) is kept as-is

Run with --dry-run to see a length table without writing.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LANG = REPO / "mpv-winui" / "mpv-winui" / "Languages" / "zh-CN.json"

# key -> trimmed text. Every entry here replaces one that was over the
# two-line budget; nothing shorter than the budget is listed, because
# those already fit.
TRIMMED: dict[str, str] = {
    # ---- colour / HDR -------------------------------------------------
    "SettingsHelpTargetPrim":
        "显示器的原色坐标，决定屏幕能显示多大范围的颜色。bt.709 为普通 SDR，"
        "bt.2020 为广色域/HDR，display-p3 常见于苹果设备。留空 = 自动跟随显示器上报的信息。",
    "SettingsHelpD3d11OutputCsp":
        "D3D11 输出交给系统的色彩空间。留空 = 自动；srgb 普通 SDR；bt.2020 广色域；"
        "pq 为 HDR10 曲线。仅在自动选择不正确时手动指定。",
    "SettingsHelpTargetTrc":
        "显示器的传递曲线——亮度数值与实际发光强度的换算关系。pq 用于 HDR，"
        "srgb 和 gamma2.2 为普通 SDR 屏幕的常见曲线。留空 = 自动跟随显示器上报的信息。",
    "SettingsHelpToneMapping":
        "HDR 片源在 SDR 屏幕上播放时压低亮度的算法。BT.2390 最均衡；"
        "低亮度屏幕可试 BT.2446a；Clip 保留细节但会切平高光。",
    "SettingsHelpGamutMappingMode":
        "超出色域的颜色怎么处理。clip 切到最近的可显示颜色；desaturate 降低饱和度使其落回色域（默认）；"
        "warn 仅高亮标出，用于排查；darken 压暗进色域。",
    "SettingsHelpHdrAutoMode":
        "RTX Video HDR（仅 NVIDIA）：把 SDR 内容实时转换成 HDR 输出。"
        "自动 = 检测到 HDR 屏幕时启用；开 = 强制；关 = 停用。需要较新的驱动和 RTX 显卡。",
    "SettingsHelpIccForceContrast":
        "强制指定显示器的静态对比度（尼特，0–1000000，0 = 不强制）。"
        "OLED 可填 1000000，能让 ICC 校色更准；普通液晶屏不要照填，否则暗部会被压得过重。",
    "SettingsHelpDisplayPeak":
        "显示器的峰值亮度（尼特，0–10000）。0 = 自动检测，读不到时按 1000 处理。"
        "它也用于判断屏幕是否算 HDR，并在「目标峰值亮度」为 0 时作为映射目标。",
    "SettingsHelpTargetColorspaceHintMode":
        "自动切换色彩空间时参考哪一份信息。target 用你设定的目标值；"
        "source 用片源声明的色彩空间（默认）；source-dynamic 用片源逐帧变化的动态元数据。",
    "SettingsHelpIcc3dlutSize":
        "把 ICC 配置文件转换成 3D 查找表时的精度。表越大越准，但转换更慢、占用显存更多。"
        "自动在画质和开销之间取平衡，通常不必改。",
    "SettingsHelpVsrAuto":
        "NVIDIA 显卡可用时，自动用 VSR 把低分辨率视频提升得更清晰，可叠加 RTX HDR。"
        "检测到其他视频滤镜时会自动让位。非 N 卡无效。",
    "SettingsHelpVideoSync":
        "画面与显示器刷新率如何对齐。audio 以声音为基准，最稳（默认）；"
        "display-resample 微调播放速度，是开启帧插值的前提；desync 不管两者的匹配。",
    "SettingsHelpD3d11OutputFormat":
        "交换链（最终交给显示器的图像缓冲）的像素格式。留空 = 自动。"
        "rgb10_a2 为 10 位色深，可减少色带；rgba16f 更精确但更占显存。",
    "SettingsHelpTargetGamut":
        "把输出限制在指定色域内，填色彩空间名称，例如 bt.709、bt.2020、display-p3。"
        "留空则使用片源自带的元数据。填错会明显偏色。",
    "SettingsHelpTargetPeak":
        "色调映射的目标亮度（尼特，0–10000），即画面最亮处对应的实际亮度。"
        "0 = 使用「显示峰值亮度」的值（默认）。",
    "SettingsHelpGpuContext":
        "上面的 API 接在哪个窗口后端上，一般与它配套选择（如 d3d11 配 win、vulkan 配 winvk）。"
        "自动由 mpv 决定。不匹配会导致黑屏。",
    "SettingsHelpHdrPeakDecayRate":
        "峰值亮度下降的速度（dB/秒，默认 20）。数值小 = 记得更久、亮度稳定；"
        "数值大 = 跟随更快，但整体亮度会一直起伏。仅在「峰值亮度」设为开时生效。",
    "SettingsHelpTargetColorspaceHint":
        "自动把输出色彩空间切换成与片源匹配，播 HDR 片源自动切到 HDR 输出，播 SDR 自动切回。"
        "想用 HDR 直通时建议开启。",
    "SettingsHelpHdrComputePeak":
        "峰值亮度的来源。自动 = 由 mpv 决定；开 = 逐帧实测实际峰值，更准但更吃性能；"
        "关 = 直接相信片源元数据，最省性能但经常不准。",
    "SettingsHelpInterpolation":
        "帧插值：在原始帧之间生成过渡帧，让快速平移更顺滑。需要显存和 GPU 余量，"
        "并且只有把同步方式设为 display-resample 才会真正生效。",
    "SettingsHelpD3d11Flip":
        "使用翻转模型呈现方式。现代 Windows 上通常是最佳选择，能减少卡顿和撕裂，默认开启。"
        "仅在全屏卡顿或画面冻结时尝试关闭。",
    "SettingsHelpHdrOverrideMode":
        "覆盖界面自动检测的显示器 HDR 能力。自动 = 跟随界面（默认）；"
        "强制 HDR / 强制 SDR 用于系统检测错误时。下次启动生效。",
    "SettingsHelpIccProfile":
        "指定要加载的 ICC 配置文件路径（.icc / .icm）。"
        "留空 = 使用系统为当前显示器配置的那一份。仅在想用特定校色文件时才填写。",
    "SettingsHelpIccProfileAuto":
        "自动加载系统为当前显示器配置的 ICC 配置文件。关闭后不做 ICC 校正，画面按屏幕原生色彩输出。",
    "SettingsHelpD3d11SyncInterval":
        "垂直同步：每隔几个刷新周期呈现一帧。1 = 开启，无撕裂但可能增加延迟；"
        "0 = 关闭，延迟最低但可能出现横向撕裂。",
    "SettingsHelpOverrideDisplayFps":
        "强制指定显示器刷新率，用于系统上报错误的情况（如 60Hz 实际是 59.94）。"
        "0 = 不覆盖。也可写分数，如 1000/1001。",
    "SettingsHelpTscale":
        "帧插值在时间轴上的算法，决定两个真实帧之间怎么生成过渡帧。"
        "只在开启帧插值后生效。Oversample 更锐利，Mitchell 更柔和。",
    "SettingsHelpHdrSceneThresholdLow":
        "判定换场景的低阈值（dB，默认 5）。前后帧亮度差超过它就重置峰值检测。"
        "调小 = 更容易触发，暗部小幅变化也会被当成换场景。",
    "SettingsHelpD3d11Warp":
        "用软件渲染（WARP）代替显卡渲染，仅用于排查驱动问题。"
        "开启后正常、关闭后不正常，说明问题在显卡驱动。软件渲染非常慢，正常观看请选否。",
    "SettingsHelpDScale":
        "缩小视频时使用的算法——视频比窗口大时用它。默认 Bicubic 较柔和，"
        "明显缩小时（如在 1080p 窗口看 4K）Lanczos 更锐利。",
    "SettingsHelpHdrSceneThresholdHigh":
        "判定换场景的高阈值（dB，默认 10）。要超过这个差值才重置峰值检测。"
        "调高 = 更保守，只有明显换场景才重置。",
    "SettingsHelpD3d11ExclusiveFs":
        "使用独占全屏模式，绕过桌面合成器以降低输入延迟。"
        "代价是切换窗口更慢，部分设备或驱动会黑屏、卡死。",
    "SettingsHelpDeband":
        "消除渐变区域的色彩阶梯纹（天空、暗部最容易出现）。原理是加入极细的噪点，"
        "可能带来轻微噪点，也会让硬件解码失效。",
    "SettingsHelpHdrAutoLog":
        "把 RTX Video HDR 的检测过程写入 hdr_auto.log，用于排查未生效的原因。"
        "日志会持续增长，排查完请关掉。下次启动生效。",
    "SettingsHelpHdrContrastRecovery":
        "色调映射压暗画面后补回一部分对比度（0 = 关闭，1 = 补回 100%，默认 1）。"
        "对比度本身就很高的内容应调低，否则容易过曝。",
    "SettingsHelpSubBitmapMaxSize":
        "位图字幕（PGS、VobSub 等）解码后的最大像素体积，0 = 不限制（默认）。"
        "调小可避免巨幅图形字幕吃掉显存，代价是超限的字幕不显示。",
    "SettingsHelpTargetColorspaceHintStrict":
        "严格按目标色彩空间输出，不再自适应片源色域。开启后超出目标色域的颜色会被强制映射进来；"
        "关闭则允许输出跟随片源放宽。",
    "SettingsHelpScale":
        "放大视频时使用的算法——视频比窗口小时用它。Lanczos 画质与性能均衡，"
        "EWA Lanczos 更锐利但更吃 GPU。",
    "SettingsHelpSigmoidUpscaling":
        "放大前先套一条 S 形曲线，压低接近纯白和纯黑的部分以减少边缘光晕，代价是画面略微变灰。"
        "与「线性光上采样」互斥。",
    "SettingsHelpHdrContrastSmoothness":
        "对比度恢复的平滑强度（默认 50）。调低更贴合画面本身，"
        "但可能在亮暗交界处出现闪烁或光晕；小屏幕建议调低。",
    "SettingsHelpLinearUpscaling":
        "在线性光下做放大，颜色混合更符合物理规律，渐变和半透明处更自然，代价是暗部可能发灰。"
        "与「S 形曲线放大」互斥。",
    "SettingsHelpCscale":
        "色度上采样算法。视频的彩色信息分辨率低于明暗信息，显示前要补回来，这一步用它。"
        "只影响彩色边缘的锐度。",
    "SettingsHelpIccCacheDir":
        "上面那份转换缓存放哪个目录。留空使用内置默认位置（应用数据目录下的 mpv 文件夹）。",
    "SettingsHelpSubAssPruneDelay":
        "离屏的 ASS 事件保留多久再丢弃（秒，-1 = 永不丢弃，默认 -1）。"
        "保留能让字幕滚动更平滑，但复杂特效字幕会持续占用内存。",
    "SettingsHelpDitherDepth":
        "抖动输出的目标位深。自动 = 与视频源一致；显示器是 8 位就选 8，10 位面板可试 10。"
        "选得比屏幕高不会有额外好处。",
    "SettingsHelpToneMappingMaxBoost":
        "色调映射允许的最大提亮倍数（1–10，默认 1）。调高会把暗部提亮更多，"
        "但可能发灰、失去黑位。",
    "SettingsHelpGpuShaderCache":
        "把编译好的着色器存到磁盘，下次启动直接读取，省掉重复编译的等待。"
        "只影响启动速度，不影响画面。",
    "SettingsHelpIccCache":
        "把 ICC 到 3D 查找表的转换结果存到磁盘，下次启动直接读取，省掉每次转换的等待。"
        "只影响启动速度，不影响画面。",
    "SettingsHelpVideoSyncMaxVideoChange":
        "重采样时允许的最大播放速度调整幅度（百分比）。数值越大越容易和刷新率对齐，"
        "但超过约 1% 人耳可能听出音调变化。",
    "SettingsHelpScaleAntiring":
        "放大时的抗振铃强度（0–1）。能减轻边缘光晕，代价是画面略微变软。0 为关闭。",

    # ---- OSD ----------------------------------------------------------
    "SettingsHelpOsdFontProvider":
        "从哪里取 OSD 用的字体。自动按平台选择；Fontconfig 用于自带 fontconfig 配置的情况；"
        "无则只用内置字体。改错会让 OSD 文字变成方块。",
    "SettingsHelpOsdShaper":
        "OSD 字体怎么把字符组合成字形。复杂（默认）能正确显示阿拉伯语、天城文等连写文字；"
        "简单更省性能。中日韩和拉丁文两者相同。",
    "SettingsHelpOsdSelectedColor":
        "mpv 自带 OSD 列表里当前选中项的颜色，默认琥珀色。"
        "只在用到 mpv 的 OSD 菜单（如章节、播放列表）时才看得见。",
    "SettingsHelpOsdShadowOffset":
        "OSD 文字投影的距离（默认 0 关闭）。描边在四周勾边，阴影在单侧压暗，"
        "后者观感更像印刷品。",
    "SettingsHelpOsdBackColor":
        "OSD 文字背后的底色，默认半透明黑。它是 OSD 在复杂画面上保持可读的主要手段；"
        "改成全透明就只剩描边和阴影可用。",
    "SettingsHelpOsdFontsDir":
        "OSD 渲染时额外查找字体的目录。需要显示系统里没装的字体时才填。"
        "这也会影响 ASS 字幕的字体查找。",
    "SettingsHelpOsdScale":
        "在字体大小之上再整体缩放 OSD（默认 1）。换了大屏幕后 OSD 显得小时，用这个统一放大。",
    "SettingsHelpOsdMarginYOffset":
        "在正常位置基础上把 OSD 整体上移或下移（正数向上）。"
        "不想改边距、只想避开画面某个区域时用它。",
    "SettingsHelpOsdScaleByWindow":
        "让 OSD 随窗口大小一起缩放（默认开）。小窗口下 OSD 不会显得过大，全屏时又会自动变大。",
    "SettingsHelpOsdSpacing":
        "OSD 文字之间额外增加（正数）或减少（负数）间距，默认 0。",
    "SettingsHelpBackgroundTileSize":
        "棋盘格每一格的边长（像素）。数值小则格子密，数值大则格子明显、更容易看出画面边界。",
    "SettingsHelpBackgroundTileColor0":
        "画面背后棋盘格的第一个颜色，只在视频有黑边或透明区域时可见。"
        "棋盘格用来区分「画面之外」和「画面本身就是黑的」，所以默认不是纯黑。",

    # ---- subtitles ----------------------------------------------------
    "SettingsHelpSubShaper":
        "字幕字体怎么把字符组合成字形。复杂（默认）会处理阿拉伯语、天城文等连写语言；"
        "简单更快，但这类文字会显示错乱。中日韩和拉丁文两者相同。",
    "SettingsHelpSubFilterSdhEnclosures":
        "被当作音效描述包裹符号的字符，默认 (),[],（）只改这里可以避免误删正文里的括号。",
    "SettingsHelpSubFilterSdh":
        "去掉 SDH 字幕里给听障观众的提示，例如 [敲门声]、[音乐]。这些内容靠括号识别。",
    "SettingsHelpSubFilterSdhHarder":
        "连括号里像是正文的部分也一起删掉。有些字幕组整条都用方括号包裹时需要开启；"
        "但正文里正常出现括号时会误删内容。",
    "SettingsHelpSubOutlineColor":
        "字幕文字外面那圈描边的颜色，默认黑色，用于在浅色画面上把字和背景分开。"
        "ASS 字幕有自己的样式，不受这里影响。",
    "SettingsHelpSubAssOverride":
        "播放器对 ASS/SSA 字幕样式的覆盖强度。推荐缩放（保持版式）；"
        "移除会删除样式，强制可能与特效字幕冲突。",
    "SettingsHelpSubsWithMatchingAudio":
        "只在有与音轨语言一致的字幕时才自动选中它；「仅强制」是只在存在强制字幕时匹配。"
        "可避免给国语片自动挂英文字幕。",
    "SettingsHelpSubGauss":
        "对字幕边缘做高斯模糊（0–3，默认 0 关闭），让字体看起来柔和。"
        "数值大时会明显发虚，影响可读性。",
    "SettingsHelpSubFixTiming":
        "修复时间轴有问题的字幕：把叠在一起或过短的条目调整到可读。对制作粗糙的字幕有用，"
        "正常字幕没有影响。",
    "SettingsHelpSubFontFile":
        "选择目录，mpv 会扫描其中的字幕字体（sub-fonts-dir），然后在「字幕字体」中选择字族。",
    "SettingsHelpSubScale":
        "在字体自身大小之上再整体放大缩小字幕（默认 1）。片源字号偏小或换了更大屏幕时用它。",
    "SettingsHelpSubAssStyleOverrides":
        "覆盖 ASS 样式值，例如 Fontname=Microsoft YaHei。留空保留文件样式。",
    "SettingsHelpSubForcedEventsOnly":
        "在一条字幕轨内只显示被标记为「强制」的条目——也就是翻译外语台词的那些。"
        "适合配合原声看外语片。",
    "SettingsHelpSubFixTimingThreshold":
        "两条字幕间隔小于这个毫秒数时才认为需要合并（默认 210）。调小更保守，只处理明显重叠的条目。",
    "SettingsHelpSubAssJustify":
        "让 ASS 字幕的多行文本按两端对齐，而不是各自居中。原本用空格精细排版的对白会更好看，"
        "但依赖空格定位的特效字幕可能错位。",
    "SettingsHelpSecondarySubAssOverride":
        "第二字幕轨是否套用 ASS 自带的样式。与主字幕处理方式一致（见字幕样式里的设置），"
        "两条字幕用同一套规则才不会互相打架。",
    "SettingsHelpSubClearOnSeek":
        "跳转时隐藏文本字幕。部分旧字幕渲染器可能异常。",
    "SettingsHelpSubBorderColor":
        "字幕描边/边框颜色（#AARRGGBB）。",
    "SettingsHelpSubBackColor":
        "字幕背景颜色（#AARRGGBB）。",
    "SettingsHelpSubBlur":
        "字幕模糊（0 关闭）。",
    "SettingsHelpSubShadowOffset":
        "字幕阴影偏移（像素）。",
    "SettingsHelpSubLineSpacing":
        "字幕行之间的额外间距（可为负数）。",
    "SettingsHelpSubDelay":
        "字幕提前或延后（秒）。",
    "SettingsHelpSubMarginX":
        "字幕距屏幕左右边缘的距离（像素）。",
    "SettingsHelpSubMarginY":
        "字幕距屏幕上下边缘的距离（像素）。",
    "SettingsHelpSubMarginYOffset":
        "在正常位置基础上把字幕整体上移或下移（正数向上）。",
    "SettingsHelpSubHinting":
        "字幕字体的微调方式：无、轻度、正常或原生（可能导致文字位置偏移）。",
    "SettingsHelpSubEmbeddedFonts":
        "使用媒体容器（如 MKV）内嵌的字体。",
    "SettingsHelpStretchImageSubsToScreen":
        "把图形字幕（如 PGS）拉伸到屏幕分辨率，使其可以输出到黑边。",
    "SettingsHelpImageSubsHdrPeak":
        "HDR 输出时图形字幕（PGS 等）的峰值白亮度。",

    # ---- audio --------------------------------------------------------
    "SettingsHelpAudioSpdif":
        "将压缩音频（AC3/EAC3/DTS/TrueHD）不解码直接透传给接收设备。关闭则输出普通 PCM。",
    "SettingsHelpAudioBuffer":
        "音频输出缓冲（秒，0–10）。0 由 mpv 决定（默认 0.2 秒）；调大可减少爆音但增加延迟。",
    "SettingsHelpAudioWaitOpen":
        "mpv 等待音频输出打开的时间（秒）。0 使用 mpv 默认值，通常需配合静音填充才有意义。",
    "SettingsHelpAudioChannels":
        "输出到音频设备的声道布局；自动会回退到受支持的布局。",
    "SettingsHelpAudioDevice":
        "音频输出设备名，如 auto 或 wasapi/...",
    "SettingsHelpAudioFormat":
        "音频输出使用的采样格式。自动 = 使用 mpv 默认。",
    "SettingsHelpAudioSampleRate":
        "音频输出的采样率（Hz），0 表示使用 mpv 默认。",
    "SettingsHelpAudioDelay":
        "音频延迟（秒），负值使声音提前。",
    "SettingsHelpAudioPitchCorrection":
        "变速播放时保持音调，否则加速会变高音。",
    "SettingsHelpAudioStreamSilence":
        "没有音频数据时填充静音帧，有助于独占模式输出。",
    "SettingsHelpVolumeMax":
        "音量上限（%）。超过 100 相当于放大器增益。",
    "SettingsHelpReplaygain":
        "应用 ReplayGain 响度归一化。Track 使用单曲增益标签，Album 使用整张专辑增益标签。",

    # ---- network / cache / demuxer ------------------------------------
    "SettingsHelpGlslShadersAppend":
        "启动时额外加载的着色器文件，多个用分号隔开，例如 ~~/shaders/a.glsl;D:/mpv/b.glsl。"
        "~~/ 代表 mpv 的配置目录。这里的条目不会出现在上面的列表里，也不受界面改动影响。",
    "SettingsHelpGlslShaders":
        "要叠加到画面上的 GLSL 着色器文件列表，用箭头调整先后顺序，顺序不同结果不同。"
        "关掉的条目会保留在列表里但不生效。",
    "SettingsHelpGlslShaderOpts":
        "给可调参数的着色器传值，写成 参数名=值，多个用逗号隔开，例如 param1=2,param2=0.5。"
        "参数名由着色器定义，写错会被忽略。",
    "SettingsHelpGpuApi":
        "用哪套图形接口渲染。自动 = 由 mpv 挑选（推荐）；d3d11 是 Windows 上的常规选择；"
        "vulkan 功能更全但驱动问题更多；opengl 兼容性最老。改错通常表现为黑屏或启动失败。",
    "SettingsHelpGpuShaderCacheDir":
        "着色器编译缓存放哪个目录。留空使用内置默认位置（应用数据目录下的 mpv 文件夹）。",
    "SettingsHelpDemuxerMaxBytes":
        "每个媒体流在解复用器中保留的缓存量。调大有助于网络播放时回退定位，但会占用更多内存"
        "（默认 150 MiB）。",
    "SettingsHelpDemuxerReadahead":
        "解复用器向前预读的数据量（秒）。网络播放可调大，但占用更多内存。",
    "SettingsHelpCurlMaxRetries":
        "可跳转流在超时、断连等可恢复错误后的自动重试次数（默认 5）。",
    "SettingsHelpCurlConnectTimeout":
        "TCP/TLS 连接超时，0 使用 libcurl 默认（默认 30）。",
    "SettingsHelpCurlMaxRedirects":
        "报告错误前允许的最大 HTTP 重定向次数（默认 16）。",
    "SettingsHelpCurlBufferSize":
        "网络线程的生产端环形缓冲大小（默认 4 MiB）。",
    "SettingsHelpNetworkTimeout":
        "网络请求超时秒数，0 使用 FFmpeg 默认（默认 60）。",
    "SettingsHelpHttpHeaderFields":
        "逗号分隔的 HTTP 头字段，例如 'Field1: value1','Field2: value2'。",
    "SettingsHelpHttpProxy":
        "HTTP/HTTPS 代理 URL，必须以 http:// 开头，不影响 https 直连。",
    "SettingsHelpDirectoryFilterTypes":
        "打开文件夹时收集进播放列表的文件类型：video,audio,image,archive,playlist。",

    # ---- window / program ---------------------------------------------
    "SettingsHelpD3d11Adapter":
        "指定用哪块显卡渲染，双显卡笔记本上常见。留空 = 由系统决定，通常就是正确的那块。"
        "选错会导致黑屏或画面卡住。",
    "SettingsHelpPiPOpacity":
        "画中画窗口的不透明度（0.2–1.0），下次打开时生效。",
    "SettingsHelpPiPControls":
        "鼠标移到迷你播放器底部时显示控制条（音量、播放、进度）。",
    "SettingsHelpThemeOpacity":
        "窗口背景材质的透明程度（100 为完全透明）。",
    "SettingsHelpOpenMpvConf":
        "在文本编辑器中打开 mpv.conf。这里的配置行优先级高于设置界面，会覆盖界面上的值。",
    "SettingsHelpControlBarLayout":
        "选择状态栏风格：原版布局或居中布局。点「自定义」可拖拽调整顺序、隐藏图标，"
        "或点「+」添加图标。",
    "SettingsHelpDebugLog":
        "把调试日志写入 %LOCALAPPDATA%\\mpv-winui\\logs，报告问题时很有用。",
    "SettingsHelpMetadataOsdEnabled":
        "在播放开始时显示元数据 OSD（艺术家、专辑、标题）。关闭后不显示，下次启动生效。",

    # ---- playback -----------------------------------------------------
    "SettingsHelpSpeed":
        "下次启动时使用的播放速度（1.0 为正常）。",
    "SettingsHelpPanscan":
        "用于填满屏幕、去掉黑边的裁剪或放大比例（0–1）。",
    "SettingsHelpImageDisplayDuration":
        "目录/幻灯片模式下每张图片停留的时长（秒）。",
    "SettingsHelpLoopPlaylist":
        "播放列表播完后循环（关闭 / 无限 / 强制）。",
    "SettingsHelpThumbnailInterval":
        "悬停时缩略图刷新的间隔（毫秒）。值越大越省 CPU。",
    "SettingsHelpDither":
        "位深转换时加入抖动：用极细的噪点模拟出更丰富的色阶，避免渐变色带。",
    "SettingsHelpToneMappingVisualize":
        "调试图层：把当前色调映射曲线画在画面上，用来检查曲线形状。正常观看请关闭。",
    "SettingsHelpInverseToneMapping":
        "反向色调映射：把 SDR 内容向上扩展成 HDR。属于猜测而非还原，效果因片源而异。",

    # ---- screenshots --------------------------------------------------
    "SettingsHelpScreenshotJpegQuality": "JPEG 质量（0–100）。",
    "SettingsHelpScreenshotJxlEffort": "JPEG XL 编码努力程度（1–9）；越高越慢但文件越小。",
    "SettingsHelpScreenshotPngCompression": "PNG 压缩等级（0–9），越高越省空间但更慢。",
    "SettingsHelpScreenshotPngFilter": "PNG 压缩滤镜预设（0 无，5 压缩率最高）。",
    "SettingsHelpScreenshotWebpCompression": "WebP 压缩级别（0–6）；越高压缩越慢但更小。",
    "SettingsHelpScreenshotWebpQuality": "WebP 质量（0–100）。",

    # ---- picture -------------------------------------------------------
    "SettingsHelpPictureBrightness": "画面亮度（-100 到 100），默认 0，实时生效。",
    "SettingsHelpPictureContrast": "画面对比度（-100 到 100），默认 0。",
    "SettingsHelpPictureGamma": "画面伽马（-100 到 100），默认 0。",
    "SettingsHelpPictureHue": "画面色相（-100 到 100），默认 0。",
    "SettingsHelpPictureSaturation": "画面饱和度（-100 到 100），默认 0。",
    "SettingsHelpPictureSharpen": "锐化强度（0 到 5），仅 gpu-next 支持。",
    "SettingsHelpOsdBarHeight": "进度/音量条的高度（屏幕百分比）。",
    "SettingsHelpOsdBarWidth": "进度/音量条的宽度（屏幕百分比）。",
    "SettingsHelpOsdBlur": "OSD 文字的高斯模糊（0 关闭）。",
    "SettingsHelpOsdColor": "OSD 文字颜色（#AARRGGBB）。",
    "SettingsHelpOsdDuration": "屏幕消息显示时长（毫秒）。",
    "SettingsHelpOsdFontSize": "屏幕提示文字的字体大小（相对屏幕高度）。",
    "SettingsHelpOsdMarginX": "OSD 文本距屏幕左右边缘的距离（像素）。",
    "SettingsHelpOsdMarginY": "OSD 文本距屏幕上下边缘的距离（像素）。",
    "SettingsHelpOsdOutlineColor": "OSD 文字描边颜色（#AARRGGBB）。",
    "SettingsHelpOsdPlayingMsgDuration": "播放开始消息的显示时长（毫秒），0 使用全局 OSD 时长。",
    "SettingsHelpHwdecCodecs":
        "允许硬件解码的编解码器。all 尝试所有格式，也可写 h264,hevc 等逗号列表限制范围。",
    "SettingsHelpHwdec":
        "硬解 API。推荐自动；关闭则使用软解，部分视频滤镜仅在软解下可靠工作。",
}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    raw = LANG.read_bytes()
    bom = raw[:3] == b"\xef\xbb\xbf"
    crlf = b"\r\n" in raw
    data = json.loads(raw.decode("utf-8-sig"))

    applied, skipped_missing, unchanged = 0, [], []
    before_total = after_total = 0
    rows = []
    for key, new in TRIMMED.items():
        if key not in data:
            skipped_missing.append(key)
            continue
        old = data[key]
        if old == new:
            unchanged.append(key)
            continue
        before_total += len(old)
        after_total += len(new)
        rows.append((key, len(old), len(new)))
        data[key] = new
        applied += 1

    rows.sort(key=lambda r: -(r[1] - r[2]))
    print(f"{'old':>5} {'new':>5} {'cut':>5}  key")
    for key, o, n in rows:
        print(f"{o:5d} {n:5d} {o - n:5d}  {key}")
    print()
    print(f"applied        : {applied}")
    print(f"unchanged      : {len(unchanged)}")
    print(f"missing in json: {len(skipped_missing)} {skipped_missing}")
    if before_total:
        print(f"total chars    : {before_total} -> {after_total} "
              f"({100 * (before_total - after_total) / before_total:.1f}% shorter)")

    # Remaining over-budget entries, so a second pass has a target list.
    over = sorted((k for k, v in data.items()
                   if k.startswith("SettingsHelp") and len(v) > 60),
                  key=lambda k: -len(data[k]))
    print(f"\nstill >60 chars: {len(over)}")
    for k in over[:15]:
        print(f"  [{len(data[k]):3d}] {k}")

    if args.dry_run:
        print("\n--dry-run: nothing written")
        return

    out = json.dumps(data, ensure_ascii=False, indent=2)
    sep = "\r\n" if crlf else "\n"
    out = out.replace("\n", sep)
    if not out.endswith(sep):
        out += sep
    payload = out.encode("utf-8")
    if bom:
        payload = b"\xef\xbb\xbf" + payload
    LANG.write_bytes(payload)
    print(f"\nwrote {LANG.relative_to(REPO)} (bom={bom} crlf={crlf})")


if __name__ == "__main__":
    sys.exit(main())
