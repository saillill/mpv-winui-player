using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// 设置项 → mpv 命令 的映射：设置页改动时即时下发，播放器初始化后批量应用。
/// </summary>
public static class MpvSettings
{
    public static string? ToCommand(string key, object value)
    {
        return key switch
        {
            nameof(AppSettings.Hwdec) => $"set hwdec {value}",
            nameof(AppSettings.VolumeMax) => $"set volume-max {value}",
            nameof(AppSettings.KeepOpen) => $"set keep-open {value}",
            nameof(AppSettings.LoopFile) => $"set loop-file {(value is true ? "inf" : "no")}",
            nameof(AppSettings.Deinterlace) => $"set deinterlace {value}",
            nameof(AppSettings.AspectRatio) => $"set video-aspect-override {value}",
            nameof(AppSettings.SubFontSize) => $"set sub-font-size {value}",
            nameof(AppSettings.SubDelay) => $"set sub-delay {value}",
            nameof(AppSettings.Speed) => $"set speed {value}",
            nameof(AppSettings.SubPos) => $"set sub-pos {value}",
            nameof(AppSettings.AudioLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set alang {(string)value}",
            nameof(AppSettings.SubtitleLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set slang {(string)value}",
            nameof(AppSettings.AudioDevice) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-device {(string)value}",
            nameof(AppSettings.ScreenshotDirectory) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-directory {(string)value}",
            nameof(AppSettings.ScreenshotTemplate) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-template {(string)value}",
            nameof(AppSettings.SavePositionOnQuit) => $"set save-position-on-quit {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotFormat) => $"set screenshot-format {(string)value}",
            nameof(AppSettings.ScreenshotJpegQuality) => $"set screenshot-jpeg-quality {value}",
            nameof(AppSettings.VideoSync) => $"set video-sync {(string)value}",
            nameof(AppSettings.Interpolation) => $"set interpolation {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CorrectDownscaling) => $"set correct-downscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioChannels) => $"set audio-channels {(string)value}",
            nameof(AppSettings.AudioDelay) => $"set audio-delay {value}",
            nameof(AppSettings.SubAssOverride) => $"set sub-ass-override {(string)value}",
            nameof(AppSettings.SubBlur) => $"set sub-blur {value}",
            nameof(AppSettings.CacheSecs) => (value is int n && n > 0) ? $"set cache-secs {n}" : null,
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
            (nameof(AppSettings.LoopFile), s.LoopFile),
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
            (nameof(AppSettings.AudioChannels), s.AudioChannels),
            (nameof(AppSettings.AudioDelay), s.AudioDelay),
            (nameof(AppSettings.SubAssOverride), s.SubAssOverride),
            (nameof(AppSettings.SubBlur), s.SubBlur),
            (nameof(AppSettings.CacheSecs), s.CacheSecs),
        })
        {
            if (ToCommand(key, value) is { } cmd)
            {
                run(cmd);
            }
        }
    }
}
