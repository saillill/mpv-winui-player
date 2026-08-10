using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace mpv_winui.Modules.Player;

/// <summary>
/// 菜单条目图标映射：系统预设 Segoe 字形（Win11 用 Fluent，Win10 回落 MDL2）。
/// 码点来自微软官方 Segoe MDL2/Fluent 图标表，并已用 fontTools 确认两字体均包含；
/// 精确匹配优先，前缀匹配兜底（前缀仅用于语义稳定的条目）。
/// </summary>
public static class IconMap
{
    public static readonly string Font = File.Exists(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "SegoeIcons.ttf"))
        ? "Segoe Fluent Icons"
        : "Segoe MDL2 Assets";

    private static readonly Lazy<Dictionary<string, string>> _userExact = new(LoadUserExact);
    private static readonly Lazy<List<KeyValuePair<string, string>>> _userPrefixes = new(LoadUserPrefixes);
    private static Dictionary<string, string>? _exactMap;
    private static KeyValuePair<string, string>[]? _builtInPrefixes;

    /// <summary>
    /// 可选用户覆盖文件 icon-map.json（放在程序目录）：
    /// {"exact":{"标题":"\uE768"},"prefix":[["前缀","\uE721"]]}。
    /// 用户精确匹配优先于内置精确匹配，用户前缀优先于内置前缀；文件缺失/损坏时忽略。
    /// </summary>
    private static Dictionary<string, string> LoadUserExact()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var path = Path.Combine(System.AppContext.BaseDirectory, "icon-map.json");
            if (!File.Exists(path)) return map;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("exact", out var exact) && exact.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in exact.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                        map[p.Name] = p.Value.GetString() ?? "";
                }
            }
        }
        catch { }
        return map;
    }

    private static List<KeyValuePair<string, string>> LoadUserPrefixes()
    {
        var list = new List<KeyValuePair<string, string>>();
        try
        {
            var path = Path.Combine(System.AppContext.BaseDirectory, "icon-map.json");
            if (!File.Exists(path)) return list;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("prefix", out var pre) && pre.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in pre.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 2) continue;
                    var a = e[0];
                    var b = e[1];
                    if (a.ValueKind == JsonValueKind.String && b.ValueKind == JsonValueKind.String)
                        list.Add(new KeyValuePair<string, string>(a.GetString() ?? "", b.GetString() ?? ""));
                }
            }
        }
        catch { }
        return list;
    }

    public static string? For(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        var t = title.Trim();
        if (_userExact.Value.TryGetValue(t, out var ug)) return ug;
        var exact = _exactMap ??= new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // 根/一级
            ["打开"] = "\uE8E5", ["播放"] = "\uE768", ["暂停"] = "\uE769", ["停止"] = "\uE71A",
            ["播放列表"] = "\uE8FD", ["导航"] = "\uE81C", ["章节"] = "\uE8A4", ["版本"] = "\uE8B2",
            ["轨道"] = "\uE8AB", ["视频"] = "\uE714", ["音频"] = "\uE8D6", ["字幕"] = "\uE8BA",
            ["音量"] = "\uE767", ["速度"] = "\uE945", ["查看"] = "\uE890", ["窗口"] = "\uE922",
            ["工具"] = "\uE713", ["截屏"] = "\uE722", ["滤镜与增强"] = "\uE9E9", ["关于"] = "\uE946",
            // 打开
            ["文件..."] = "\uE8A5", ["文件夹..."] = "\uE8B7", ["添加到播放列表..."] = "\uE8FD",
            ["蓝光 ISO..."] = "\uE7B8", ["DVD ISO..."] = "\uE7B8", ["URL"] = "\uE71B",
            ["剪贴板"] = "\uE8C8", ["最近播放"] = "\uE823",
            // 导航
            ["下个文件"] = "\uE893", ["上个文件"] = "\uE892", ["下一章节"] = "\uE893",
            ["上一章节"] = "\uE892", ["下一帧"] = "\uE893", ["上一帧"] = "\uE892",
            ["前进 5 秒"] = "\uE72A", ["后退 5 秒"] = "\uE72B", ["前进 30 秒"] = "\uE72A",
            ["后退 30 秒"] = "\uE72B", ["前进 5 分钟"] = "\uE72A", ["后退 5 分钟"] = "\uE72B",
            // 视频
            ["切换轨道"] = "\uE8AB", ["加载文件..."] = "\uE8E5", ["调色"] = "\uE9E9",
            ["对比度 -1"] = "\uE9E9", ["对比度 +1"] = "\uE9E9", ["亮度 -1"] = "\uE706",
            ["亮度 +1"] = "\uE706", ["伽马 -1"] = "\uE9E9", ["伽马 +1"] = "\uE9E9",
            ["饱和度 -1"] = "\uE9E9", ["饱和度 +1"] = "\uE9E9", ["色调 -1"] = "\uE9E9",
            ["色调 +1"] = "\uE9E9", ["重置"] = "\uE72C", ["自动 ICC 配置"] = "\uE8A5",
            ["顺时针旋转"] = "\uE7AD", ["逆时针旋转"] = "\uE7AD", ["缩放"] = "\uE71E",
            ["缩小 -1%"] = "\uE71F", ["放大 +1%"] = "\uE8A3", ["帧位"] = "\uE890",
            ["左移"] = "\uE72B", ["右移"] = "\uE72A", ["上移"] = "\uE74A", ["下移"] = "\uE74B",
            ["比例"] = "\uE799", ["16:9"] = "\uE799", ["4:3"] = "\uE799", ["2.35:1"] = "\uE799",
            ["自动"] = "\uE799", ["去黑边 -"] = "\uE799", ["去黑边 +"] = "\uE799",
            ["去色带"] = "\uE9E9", ["反交错"] = "\uE9E9", ["自动校色"] = "\uE9E9",
            ["时间码解析模式"] = "\uE916", ["截屏 (不包含字幕)"] = "\uE722",
            // 音频
            ["重置 音频与字幕同步"] = "\uE72C", ["输出设备"] = "\uE772", ["延迟 +0.1"] = "\uE916",
            ["延迟 -0.1"] = "\uE916", ["音频输出设备"] = "\uE772",
            // 字幕
            ["主字幕"] = "\uE8BA", ["主字幕选项"] = "\uE8BA", ["可见性"] = "\uE890",
            ["次字幕"] = "\uE8BA", ["次字幕选项"] = "\uE8BA", ["减少字体大小"] = "\uE8E7",
            ["增加字体大小"] = "\uE8E8",
            // 音量
            ["增加"] = "\uE74A", ["降低"] = "\uE74B", ["静音"] = "\uE74F",
            // 速度
            ["-10%"] = "\uE945", ["+10%"] = "\uE945", ["减半"] = "\uE945", ["翻倍"] = "\uE945",
            ["0.2 倍"] = "\uE945", ["0.5 倍"] = "\uE945", ["1.0 倍"] = "\uE945",
            ["1.5 倍"] = "\uE945", ["2.0 倍"] = "\uE945", ["64.0 倍"] = "\uE945",
            // 查看
            ["放大"] = "\uE8A3", ["缩小"] = "\uE71F", ["50 %"] = "\uE71E", ["100 %"] = "\uE71E",
            ["200 %"] = "\uE71E", ["300 %"] = "\uE71E", ["切换 OSC 可见性"] = "\uE890",
            ["显示 OSD 时间轴"] = "\uE916", ["显示进度"] = "\uE916",
            ["显示统计信息"] = "\uE9D2", ["显示控制台"] = "\uE756",
            // 窗口
            ["全屏"] = "\uE740", ["截屏 (导出文件)"] = "\uE74E", ["边框"] = "\uE799",
            ["置顶"] = "\uE718", ["画中画"] = "\uE8B2",
            // 工具
            ["复制文件路径"] = "\uE8C8", ["复制视频元数据"] = "\uE8C8",
            ["显示 MediaInfo 信息"] = "\uE946", ["复制 MediaInfo 信息"] = "\uE8C8",
            ["打乱播放列表"] = "\uE8B1", ["导出播放列表"] = "\uE74E",
            ["设置/清除 A-B 循环点"] = "\uE8ED", ["切换循环播放"] = "\uE8EE",
            ["切换硬件解码"] = "\uE950", ["配置文件"] = "\uE713", ["退出 (稍后观看)"] = "\uE8BB",
            ["按键绑定列表"] = "\uE92E", ["常驻显示统计信息"] = "\uE9D2",
            ["显示OSD命令行/控制台"] = "\uE756", ["清除已记录的属性值"] = "\uE74D",
            ["按键名检测"] = "\uE92E", ["打开select总菜单"] = "\uE8FD",
            ["打开select分菜单-属性列表"] = "\uE8FD",
            // 截屏
            ["窗口-无OSD"] = "\uE722", ["原始"] = "\uE8B2",
            // 滤镜与增强
            ["视频滤镜"] = "\uE9E9", ["清空"] = "\uE74D", ["着色器"] = "\uE9E9",
            ["增强脚本"] = "\uE943", ["脚本"] = "\uE943", ["滤镜"] = "\uE9E9",
            // 关于
            ["项目主页"] = "\uE80F", ["mpv手册"] = "\uE8C3", ["中文手册"] = "\uE8C3",
            ["FAQ"] = "\uE897", ["滤镜指北"] = "\uE8C3",
            // 既有兜底
            ["退出"] = "\uE8BB", ["搜索"] = "\uE721", ["设置"] = "\uE713",
            ["文件夹"] = "\uE8B7", ["复制"] = "\uE8C8", ["保存"] = "\uE74E",
            ["删除"] = "\uE74D", ["刷新"] = "\uE72C", ["信息"] = "\uE946", ["链接"] = "\uE71B",
        };
        if (exact.TryGetValue(t, out var g)) return g;
        foreach (var kv in _userPrefixes.Value)
        {
            if (t.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
        }
        foreach (var kv in _builtInPrefixes ??= new[]
        {
            // 前缀兜底：仅用于语义稳定、不会误伤的条目
            KeyValuePair.Create("预设", "\uE945"), KeyValuePair.Create("前进", "\uE72A"),
            KeyValuePair.Create("后退", "\uE72B"), KeyValuePair.Create("主字幕", "\uE8BA"),
            KeyValuePair.Create("次字幕", "\uE8BA"), KeyValuePair.Create("加载文件", "\uE8E5"),
            KeyValuePair.Create("打开", "\uE8E5"), KeyValuePair.Create("播放", "\uE768"),
            KeyValuePair.Create("暂停", "\uE769"), KeyValuePair.Create("停止", "\uE71A"),
            KeyValuePair.Create("切换", "\uE8AB"), KeyValuePair.Create("截屏", "\uE722"),
            KeyValuePair.Create("全屏", "\uE740"), KeyValuePair.Create("静音", "\uE74F"),
            KeyValuePair.Create("退出", "\uE8BB"), KeyValuePair.Create("关于", "\uE946"),
            KeyValuePair.Create("复制", "\uE8C8"), KeyValuePair.Create("保存", "\uE74E"),
            KeyValuePair.Create("刷新", "\uE72C"), KeyValuePair.Create("轨道", "\uE8AB"),
            KeyValuePair.Create("延迟", "\uE916"), KeyValuePair.Create("重置", "\uE72C"),
            KeyValuePair.Create("调色", "\uE9E9"), KeyValuePair.Create("比例", "\uE799"),
            KeyValuePair.Create("去黑边", "\uE799"), KeyValuePair.Create("去色带", "\uE9E9"),
            KeyValuePair.Create("反交错", "\uE9E9"), KeyValuePair.Create("自动校色", "\uE9E9"),
            KeyValuePair.Create("显示", "\uE890"), KeyValuePair.Create("输出设备", "\uE772"),
            KeyValuePair.Create("可见性", "\uE890"), KeyValuePair.Create("蓝光", "\uE7B8"),
            KeyValuePair.Create("DVD", "\uE7B8"), KeyValuePair.Create("文件", "\uE8A5"),
            KeyValuePair.Create("文件夹", "\uE8B7"), KeyValuePair.Create("最近播放", "\uE823"),
            KeyValuePair.Create("配置文件", "\uE713"), KeyValuePair.Create("按键", "\uE92E"),
            KeyValuePair.Create("视频滤镜", "\uE9E9"), KeyValuePair.Create("着色器", "\uE9E9"),
            KeyValuePair.Create("增强脚本", "\uE943"), KeyValuePair.Create("Nvidia", "\uE9E9"),
            KeyValuePair.Create("RTX", "\uE9E9"), KeyValuePair.Create("RIFE", "\uE9E9"),
            KeyValuePair.Create("K7", "\uE713"), KeyValuePair.Create("NVScaler", "\uE9E9"),
            KeyValuePair.Create("搜索", "\uE721"),
            KeyValuePair.Create("设置", "\uE713"), KeyValuePair.Create("删除", "\uE74D"),
            KeyValuePair.Create("信息", "\uE946"), KeyValuePair.Create("链接", "\uE71B"),
            KeyValuePair.Create("字幕", "\uE8BA"),
        })
        {
            if (t.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
        }
        return null;
    }
}
