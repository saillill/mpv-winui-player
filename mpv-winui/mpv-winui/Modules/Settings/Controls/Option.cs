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

public enum OptionActionKind
{
    None,
    Button,
}

/// <summary>Display tier used to fold advanced/experimental options by default.</summary>
/// <summary>A checkbox entry used by <see cref="OptionType.CheckList"/> options.</summary>
public sealed class OptionCheckItem
{
    public OptionCheckItem(string value, string label, bool isChecked, string? glyph = null, string? group = null, string? target = null)
    {
        Value = value;
        Label = label;
        IsChecked = isChecked;
        Glyph = glyph;
        Group = group;
        Target = target;
    }

    public string Value { get; }
    public string Label { get; }
    public bool IsChecked { get; set; }
    public string? Glyph { get; }

    /// <summary>Optional localized caption shown above the first item of a group.</summary>
    public string? Group { get; }

    /// <summary>Optional secondary line shown under the checkbox label (e.g. profile-desc).</summary>
    public string? Description { get; set; }

    /// <summary>Optional target key the item writes to (e.g. a per-style setting).</summary>
    public string? Target { get; set; }
}

/// <summary>A selectable layout preview shown by <see cref="OptionType.Layout"/> options.</summary>
public sealed class OptionLayoutChoice
{
    public OptionLayoutChoice(string value, string label, string? description = null)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    public string Value { get; }
    public string Label { get; }
    public string? Description { get; }
}

public sealed class Option : INotifyPropertyChanged
{
    public const string GroupOtherKey = "other";

    public string Key
    {
        get; set;
    } = string.Empty;

    public string Label
    {
        get; set;
    } = string.Empty;

    public string GroupKey
    {
        get; set;
    } = GroupOtherKey;

    public string GroupLabel
    {
        get; set;
    } = string.Empty;
    public string? Description
    {
        get; set;
    }

    public string? Icon
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

    /// <summary>Raised after a control commits a new value through <see cref="Setter"/>.</summary>
    public event Action<Option>? Changed;

    public void NotifyChanged()
    {
        Changed?.Invoke(this);
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
