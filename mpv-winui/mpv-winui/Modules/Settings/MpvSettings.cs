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
        })
        {
            if (ToCommand(key, value) is { } cmd)
            {
                run(cmd);
            }
        }
    }
}
