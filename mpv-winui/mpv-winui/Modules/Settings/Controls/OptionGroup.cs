using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>设置分组（按 Category 聚合），供设置列表的分组头绑定。</summary>
public sealed class OptionGroup : List<Option>
{
    public string Key { get; set; } = string.Empty;
}
