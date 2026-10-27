using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MpvMenuHost;

/// <summary>菜单 JSON 数据模型，字段与 dyn_menu.lua 的 menu items 兼容。</summary>
public sealed class MenuItemModel
{
    public string? Title { get; set; }
    public string? Type { get; set; }      // "separator"
    public string? Shortcut { get; set; }
    public JsonElement? Cmd { get; set; }  // 字符串命令或命令数组
    public List<MenuItemModel>? Submenu { get; set; }
    public bool Disabled { get; set; }     // state 含 disabled
    public bool Checked { get; set; }      // state 含 checked
    public bool Hidden { get; set; }       // state 含 hidden
}

public sealed class MenuData
{
    public string? Title { get; set; }
    public List<MenuItemModel> Items { get; } = new();

    public static MenuData? Load(string path)
    {
        if (!File.Exists(path)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var data = new MenuData();
        if (root.TryGetProperty("title", out var t)) data.Title = t.GetString();
        if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
                data.Items.Add(ParseItem(el));
        }
        return data;
    }

    /// <summary>递归解析任意深度子菜单（v4.2 起不再限制 3 层）。</summary>
    private static MenuItemModel ParseItem(JsonElement el)
    {
        var item = new MenuItemModel();
        if (el.TryGetProperty("type", out var ty)) item.Type = ty.GetString();
        if (el.TryGetProperty("title", out var ti)) item.Title = ti.GetString();
        if (el.TryGetProperty("shortcut", out var sc)) item.Shortcut = sc.GetString();
        if (el.TryGetProperty("cmd", out var cmd) && cmd.ValueKind != JsonValueKind.Null)
            item.Cmd = cmd.Clone();
        if (el.TryGetProperty("submenu", out var sub) && sub.ValueKind == JsonValueKind.Array)
        {
            item.Submenu = new List<MenuItemModel>();
            foreach (var se in sub.EnumerateArray())
                item.Submenu.Add(ParseItem(se));
        }
        if (el.TryGetProperty("state", out var st) && st.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in st.EnumerateArray())
            {
                var v = s.GetString();
                if (v == "disabled") item.Disabled = true;
                if (v == "checked") item.Checked = true;
                if (v == "hidden") item.Hidden = true;
            }
        }
        return item;
    }

    /// <summary>把字符串命令拆成数组（尊重双引号），如 "set aid 1" → ["set","aid","1"]。</summary>
    public static List<string> Tokenize(string s)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        bool inQ = false, started = false;
        foreach (var c in s)
        {
            if (c == '"') { inQ = !inQ; started = true; }
            else if (char.IsWhiteSpace(c) && !inQ)
            {
                if (started) { parts.Add(cur.ToString()); cur.Clear(); started = false; }
            }
            else { cur.Append(c); started = true; }
        }
        if (started || inQ) parts.Add(cur.ToString());
        return parts;
    }

    public static List<string>? CmdToList(JsonElement? cmd)
    {
        if (cmd is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.String)
        {
            var s = e.GetString() ?? "";
            // dyn_menu 全菜单条目的 cmd 带 "#menu: ..." 注解与状态表达式，发给 mpv 前剥离
            int ann = s.IndexOf("#menu:", StringComparison.Ordinal);
            if (ann >= 0) s = s[..ann];
            return Tokenize(s.Trim());
        }
        if (e.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var x in e.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.String) list.Add(x.GetString() ?? "");
                else if (x.ValueKind == JsonValueKind.Number) list.Add(x.GetRawText());
            }
            return list;
        }
        return null;
    }

}
