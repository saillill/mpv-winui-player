using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

public sealed class Option : INotifyPropertyChanged
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = "General";

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

    /// <summary>Optional section caption used to split a category into topic groups.</summary>
    public string? Section
    {
        get; set;
    }

    /// <summary>Marks the first option of a section; the list renders a separate section caption row above it.</summary>
    public bool ShowSectionHeader
    {
        get; set;
    }

    /// <summary>Localized warning shown in yellow when the option may be ineffective in the current state.</summary>
    public string? Warning
    {
        get => _warning;
        set => Set(ref _warning, value);
    }
    private string? _warning;

    /// <summary>Whether the option can be changed in the current state (mutually exclusive options).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }
    private bool _isEnabled = true;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
