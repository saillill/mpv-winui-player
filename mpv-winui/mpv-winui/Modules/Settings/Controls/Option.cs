using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// A selectable option value: <see cref="Value"/> is the value stored/sent to mpv,
/// <see cref="Label"/> is the localized text shown in the settings UI.
/// </summary>
public sealed class OptionChoice
{
    public OptionChoice(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }
    public string Label { get; }
}

public sealed class Option
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool RequiresRestart { get; set; }

    /// <summary>Explanatory text shown behind the info (i) button.</summary>
    public string? Description
    {
        get; set;
    }

    /// <summary>Localized display choices. When set, takes precedence over <see cref="Options"/>.</summary>
    public IList<OptionChoice>? Choices
    {
        get; set;
    }

    /// <summary>Lazily builds localized choices (e.g. runtime audio devices). Called when the control is (re)bound.</summary>
    public Func<IList<OptionChoice>>? ChoicesProvider
    {
        get; set;
    }

    /// <summary>Show a "Browse..." button that opens the system folder picker.</summary>
    public bool PickFolder
    {
        get; set;
    }

    /// <summary>Show an "Open" button that opens the folder in File Explorer.</summary>
    public bool OpenFolder
    {
        get; set;
    }

    public OptionType Type
    {
        get; set;
    }

    public double? Min
    {
        get; set;
    }
    public double? Max
    {
        get; set;
    }
    public double? Step
    {
        get; set;
    }

    public IList<string>? Options
    {
        get; set;
    }

    public bool AllowEmpty
    {
        get; set;
    }

    public Func<object>? Getter
    {
        get; set;
    }

    public Action<object>? Setter
    {
        get; set;
    }
}
