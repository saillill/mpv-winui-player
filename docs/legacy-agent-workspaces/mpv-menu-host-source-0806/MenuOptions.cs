using System;

namespace MpvMenuHost;

/// <summary>命令行参数（宿主进程由 mpv Lua 侧按需拉起）。</summary>
public sealed class MenuOptions
{
    public string? ItemsPath { get; set; }
    public string? Pipe { get; set; }
    public string? LogPath { get; set; }
    public int? TestSelect { get; set; }
    public int TestDelayMs { get; set; } = 800;
    public bool NoConnect { get; set; }
    public string Anchor { get; set; } = "top";  // "top"=菜单左上角在鼠标处；"bottom"=菜单底边在鼠标上方（按钮菜单）
    public string Side { get; set; } = "auto";   // "auto"/"left"/"right"：级联展开方向（继承上级）
    public string? Theme { get; set; }   // "dark" / "light"，测试用；缺省跟随系统
    public (int X, int Y)? Pos { get; set; }
    public int Depth { get; set; }       // 级联深度（根=0），保留用于日志/未来扩展

    public static MenuOptions Parse(string[] args)
    {
        var o = new MenuOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (args[i])
            {
                case "--items": o.ItemsPath = Next(); break;
                case "--pipe": o.Pipe = Next(); break;
                case "--log": o.LogPath = Next(); break;
                case "--test-select": o.TestSelect = int.TryParse(Next(), out var s) ? s : 0; break;
                case "--test-delay-ms": o.TestDelayMs = int.TryParse(Next(), out var d) ? d : 800; break;
                case "--no-connect": o.NoConnect = true; break;
                case "--anchor":
                    var an = Next();
                    if (an is "top" or "bottom") o.Anchor = an;
                    break;
                case "--side":
                    var sd = Next();
                    if (sd is "left" or "right") o.Side = sd;
                    break;
                case "--theme":
                    var th = Next();
                    if (th is "dark" or "light") o.Theme = th;
                    break;
                case "--pos":
                    var v = Next();
                    if (v != null)
                    {
                        var parts = v.Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out var px) && int.TryParse(parts[1], out var py))
                            o.Pos = (px, py);
                    }
                    break;
                case "--depth":
                    o.Depth = int.TryParse(Next(), out var dp) ? dp : 0;
                    break;
            }
        }
        return o;
    }
}
