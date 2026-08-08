using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// 设置项 → mpv 命令 的映射：设置页改动时即时下发，播放器初始化后批量应用。
/// </summary>
public static class MpvSettings
{
    /// <summary>Quote a value for mpv's command string parser (paths may contain spaces).</summary>
    private static string Q(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    public static string? ToCommand(string key, object value)
    {
        return key switch
        {
            nameof(AppSettings.Hwdec) => $"set hwdec {value}",
            nameof(AppSettings.VolumeMax) => $"set volume-max {value}",
            nameof(AppSettings.KeepOpen) => $"set keep-open {value}",
            nameof(AppSettings.LoopPlaylist) => $"set loop-playlist {value}",
            nameof(AppSettings.LoopFile) => $"set loop-file {(value is true ? "inf" : "no")}",
            nameof(AppSettings.Volume) => $"set volume {value}",
            nameof(AppSettings.CacheDirectory) => string.IsNullOrWhiteSpace((string)value) ? null : $"set cache-dir {Q((string)value)}",
            nameof(AppSettings.Deinterlace) => $"set deinterlace {value}",
            nameof(AppSettings.AspectRatio) => $"set video-aspect-override {value}",
            nameof(AppSettings.SubFontSize) => $"set sub-font-size {value}",
            nameof(AppSettings.SubDelay) => $"set sub-delay {value}",
            nameof(AppSettings.Speed) => $"set speed {value}",
            nameof(AppSettings.SubPos) => $"set sub-pos {value}",
            nameof(AppSettings.AudioLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set alang {(string)value}",
            nameof(AppSettings.SubtitleLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set slang {(string)value}",
            nameof(AppSettings.AudioDevice) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-device {(string)value}",
            nameof(AppSettings.ScreenshotDirectory) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-directory {Q((string)value)}",
            nameof(AppSettings.ScreenshotTemplate) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-template {(string)value}",
            nameof(AppSettings.SavePositionOnQuit) => $"set save-position-on-quit {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotFormat) => $"set screenshot-format {(string)value}",
            nameof(AppSettings.ScreenshotJpegQuality) => $"set screenshot-jpeg-quality {value}",
            nameof(AppSettings.VideoSync) => $"set video-sync {(string)value}",
            nameof(AppSettings.Interpolation) => $"set interpolation {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CorrectDownscaling) => $"set correct-downscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.Scale) => $"set scale {(string)value}",
            nameof(AppSettings.DScale) => $"set dscale {(string)value}",
            nameof(AppSettings.VideoRotate) => $"set video-rotate {(string)value}",
            nameof(AppSettings.Deband) => $"set deband {(value is true ? "yes" : "no")}",
            nameof(AppSettings.LinearDownscaling) => $"set linear-downscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SigmoidUpscaling) => $"set sigmoid-upscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ToneMapping) => $"set tone-mapping {(string)value}",
            nameof(AppSettings.DitherDepth) => $"set dither-depth {(string)value}",
            nameof(AppSettings.HrSeek) => $"set hr-seek {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CacheOnDisk) => $"set cache-on-disk {(value is true ? "yes" : "no")}",
            nameof(AppSettings.VideoOutputLevels) => $"set video-output-levels {(string)value}",
            nameof(AppSettings.IccProfileAuto) => $"set icc-profile-auto {(value is true ? "yes" : "no")}",
            nameof(AppSettings.Icc3dlutSize) => $"set icc-3dlut-size {(string)value}",
            nameof(AppSettings.AudioChannels) => $"set audio-channels {(string)value}",
            nameof(AppSettings.AudioExclusive) => $"set audio-exclusive {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioPitchCorrection) => $"set audio-pitch-correction {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioNormalizeDownmix) => $"set audio-normalize-downmix {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioFileAuto) => $"set audio-file-auto {(string)value}",
            nameof(AppSettings.AudioDisplay) => $"set audio-display {(string)value}",
            nameof(AppSettings.AudioDelay) => $"set audio-delay {value}",
            nameof(AppSettings.SubAssOverride) => $"set sub-ass-override {(string)value}",
            nameof(AppSettings.SubBlur) => $"set sub-blur {value}",
            nameof(AppSettings.SubAuto) => $"set sub-auto {(string)value}",
            nameof(AppSettings.SubFont) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-font {Q((string)value)}",
            nameof(AppSettings.SubAssScaleWithWindow) => $"set sub-ass-scale-with-window {(value is true ? "yes" : "no")}",
            nameof(AppSettings.BlendSubtitles) => $"set blend-subtitles {(string)value}",
            nameof(AppSettings.SubFallback) => $"set subs-fallback {(string)value}",
            nameof(AppSettings.SubCodePage) => $"set sub-codepage {(string)value}",
            nameof(AppSettings.SubOutlineSize) => $"set sub-outline-size {value}",
            nameof(AppSettings.SubShadowOffset) => $"set sub-shadow-offset {value}",
            nameof(AppSettings.SubEmbeddedFonts) => $"set embeddedfonts {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubUseMargins) => $"set sub-use-margins {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdFontSize) => $"set osd-font-size {value}",
            nameof(AppSettings.OsdDuration) => $"set osd-duration {value}",
            nameof(AppSettings.CacheSecs) => (value is int n && n > 0) ? $"set cache-secs {n}" : null,
            nameof(AppSettings.ScreenshotPngCompression) => $"set screenshot-png-compression {value}",
            nameof(AppSettings.ScreenshotWebpQuality) => $"set screenshot-webp-quality {value}",
            nameof(AppSettings.ScreenshotHighBitDepth) => $"set screenshot-high-bit-depth {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotTagColorspace) => $"set screenshot-tag-colorspace {(value is true ? "yes" : "no")}",
            nameof(AppSettings.VsrAutoEnabled) => $"set user-data/mpvw/vsr-auto {(value is true ? "yes" : "no")}",
            nameof(AppSettings.HdrAutoMode) => $"script-message-to hdr_auto mode {(string)value}",
            nameof(AppSettings.SeekHoldEnabled) => $"set user-data/mpvw/seek-hold {(value is true ? "yes" : "no")}",
            _ => null,
        };
    }

    public static void ApplyAll(Action<string> run)
    {
        var s = AppContext.AppSetting;
        foreach (var (key, value) in new (string, object)[]
        {
            (nameof(AppSettings.Hwdec), s.Hwdec),
            (nameof(AppSettings.VolumeMax), s.VolumeMax),
            (nameof(AppSettings.KeepOpen), s.KeepOpen),
            (nameof(AppSettings.LoopPlaylist), s.LoopPlaylist),
            (nameof(AppSettings.LoopFile), s.LoopFile),
            (nameof(AppSettings.Volume), s.Volume),
            (nameof(AppSettings.CacheDirectory), s.CacheDirectory),
            (nameof(AppSettings.Deinterlace), s.Deinterlace),
            (nameof(AppSettings.AspectRatio), s.AspectRatio),
            (nameof(AppSettings.SubFontSize), s.SubFontSize),
            (nameof(AppSettings.SubDelay), s.SubDelay),
            (nameof(AppSettings.Speed), s.Speed),
            (nameof(AppSettings.SubPos), s.SubPos),
            (nameof(AppSettings.AudioLanguage), s.AudioLanguage),
            (nameof(AppSettings.SubtitleLanguage), s.SubtitleLanguage),
            (nameof(AppSettings.AudioDevice), s.AudioDevice),
            (nameof(AppSettings.ScreenshotDirectory), s.ScreenshotDirectory),
            (nameof(AppSettings.ScreenshotTemplate), s.ScreenshotTemplate),
            (nameof(AppSettings.SavePositionOnQuit), s.SavePositionOnQuit),
            (nameof(AppSettings.ScreenshotFormat), s.ScreenshotFormat),
            (nameof(AppSettings.ScreenshotJpegQuality), s.ScreenshotJpegQuality),
            (nameof(AppSettings.VideoSync), s.VideoSync),
            (nameof(AppSettings.Interpolation), s.Interpolation),
            (nameof(AppSettings.CorrectDownscaling), s.CorrectDownscaling),
            (nameof(AppSettings.Scale), s.Scale),
            (nameof(AppSettings.DScale), s.DScale),
            (nameof(AppSettings.VideoRotate), s.VideoRotate),
            (nameof(AppSettings.Deband), s.Deband),
            (nameof(AppSettings.LinearDownscaling), s.LinearDownscaling),
            (nameof(AppSettings.SigmoidUpscaling), s.SigmoidUpscaling),
            (nameof(AppSettings.ToneMapping), s.ToneMapping),
            (nameof(AppSettings.DitherDepth), s.DitherDepth),
            (nameof(AppSettings.HrSeek), s.HrSeek),
            (nameof(AppSettings.CacheOnDisk), s.CacheOnDisk),
            (nameof(AppSettings.VideoOutputLevels), s.VideoOutputLevels),
            (nameof(AppSettings.IccProfileAuto), s.IccProfileAuto),
            (nameof(AppSettings.Icc3dlutSize), s.Icc3dlutSize),
            (nameof(AppSettings.AudioChannels), s.AudioChannels),
            (nameof(AppSettings.AudioExclusive), s.AudioExclusive),
            (nameof(AppSettings.AudioPitchCorrection), s.AudioPitchCorrection),
            (nameof(AppSettings.AudioNormalizeDownmix), s.AudioNormalizeDownmix),
            (nameof(AppSettings.AudioFileAuto), s.AudioFileAuto),
            (nameof(AppSettings.AudioDisplay), s.AudioDisplay),
            (nameof(AppSettings.AudioDelay), s.AudioDelay),
            (nameof(AppSettings.SubAssOverride), s.SubAssOverride),
            (nameof(AppSettings.SubBlur), s.SubBlur),
            (nameof(AppSettings.SubAuto), s.SubAuto),
            (nameof(AppSettings.SubFont), s.SubFont),
            (nameof(AppSettings.SubAssScaleWithWindow), s.SubAssScaleWithWindow),
            (nameof(AppSettings.BlendSubtitles), s.BlendSubtitles),
            (nameof(AppSettings.SubFallback), s.SubFallback),
            (nameof(AppSettings.SubCodePage), s.SubCodePage),
            (nameof(AppSettings.SubOutlineSize), s.SubOutlineSize),
            (nameof(AppSettings.SubShadowOffset), s.SubShadowOffset),
            (nameof(AppSettings.SubEmbeddedFonts), s.SubEmbeddedFonts),
            (nameof(AppSettings.SubUseMargins), s.SubUseMargins),
            (nameof(AppSettings.OsdFontSize), s.OsdFontSize),
            (nameof(AppSettings.OsdDuration), s.OsdDuration),
            (nameof(AppSettings.CacheSecs), s.CacheSecs),
            (nameof(AppSettings.ScreenshotPngCompression), s.ScreenshotPngCompression),
            (nameof(AppSettings.ScreenshotWebpQuality), s.ScreenshotWebpQuality),
            (nameof(AppSettings.ScreenshotHighBitDepth), s.ScreenshotHighBitDepth),
            (nameof(AppSettings.ScreenshotTagColorspace), s.ScreenshotTagColorspace),
            (nameof(AppSettings.VsrAutoEnabled), s.VsrAutoEnabled),
            (nameof(AppSettings.HdrAutoMode), s.HdrAutoMode),
            (nameof(AppSettings.SeekHoldEnabled), s.SeekHoldEnabled),
        })
        {
            if (ToCommand(key, value) is { } cmd)
            {
                run(cmd);
            }
        }
    }
}
