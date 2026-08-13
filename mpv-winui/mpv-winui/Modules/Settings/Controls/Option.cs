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
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = "General";

    /// <summary>Placeholder text shown when the value is empty (e.g. the built-in default path).</summary>
    public string? Placeholder
    {
        get; set;
    }

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

    /// <summary>Whether the list control adds a "Custom" entry for arbitrary values. Disabled when the presets cover every legal value.</summary>
    public bool AllowCustom
    {
        get; set;
    } = true;

    /// <summary>Show a "Browse..." button that opens the system folder picker.</summary>
    public bool PickFolder
    {
        get; set;
    }

    /// <summary>Show a "Browse..." button that opens the system file picker (fonts, etc.).</summary>
    public bool PickFile
    {
        get; set;
    }

    /// <summary>Show an "Open" button that opens the folder in File Explorer.</summary>
    public bool OpenFolder
    {
        get; set;
    }

    /// <summary>File extensions offered by the picker when <see cref="PickFile"/> is true.</summary>
    public IReadOnlyList<string>? FileTypeFilter
    {
        get; set;
    }

    /// <summary>Relative local-data folder opened by "Open" when the input is empty.</summary>
    public string? FallbackOpenFolder
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

    /// <summary>Whether the option is shown at all (ineffective options are hidden, not just disabled).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }
    private bool _isVisible = true;

    /// <summary>Read-only display (e.g. shortcut bindings); the value can still be selected and copied.</summary>
    public bool ReadOnly
    {
        get; set;
    }

    /// <summary>Clicking the read-only value starts key capture and rebinds the shortcut.</summary>
    public bool KeyCaptureEditable
    {
        get; set;
    }

    /// <summary>Default key from the bundled input.conf, used by the per-row reset.</summary>
    public string? KeyCaptureDefault
    {
        get; set;
    }

    /// <summary>Called after a new key is captured (option, new key).</summary>
    public Action<Option, string>? KeyCaptureReplaced
    {
        get; set;
    }

    /// <summary>Restores this binding to its default key.</summary>
    public Action<Option>? KeyCaptureReset
    {
        get; set;
    }

    /// <summary>Extra row behavior for <see cref="OptionType.Action"/> options.</summary>
    public OptionActionKind ActionKind
    {
        get; set;
    }

    /// <summary>Localized text for the action button.</summary>
    public string? ActionLabel
    {
        get; set;
    }

    /// <summary>Invoked when the action button is pressed.</summary>
    public Action<Option>? ActionHandler
    {
        get; set;
    }

    /// <summary>Live status text for the action row (captured key, operation result).</summary>
    public Func<string>? ActionStatus
    {
        get; set;
    }

    /// <summary>Checkbox entries for <see cref="OptionType.CheckList"/> options.</summary>
    public IList<OptionCheckItem>? CheckItems
    {
        get; set;
    }

    /// <summary>Lazily rebuilds checklist entries (e.g. per selected layout style).</summary>
    public Func<IList<OptionCheckItem>>? CheckItemsProvider
    {
        get; set;
    }

    /// <summary>Rebuilds checklist entries for an explicitly named variant
    /// (layout cards pass the selected style so each card edits its own data).</summary>
    public Func<string, IList<OptionCheckItem>>? CheckItemsProviderForStyle
    {
        get; set;
    }

    /// <summary>Layout presets for <see cref="OptionType.Layout"/> options.</summary>
    public IList<OptionLayoutChoice>? LayoutChoices
    {
        get; set;
    }

    /// <summary>Raised when a checklist entry is toggled (option, value, checked, target key).</summary>
    public Action<Option, string, bool, string?>? CheckChanged
    {
        get; set;
    }

    /// <summary>Localized text for the optional Apply button shown inside the expanded panel.</summary>
    public string? CheckApplyLabel
    {
        get; set;
    }

    /// <summary>Invoked when the expanded panel's Apply button is pressed.</summary>
    public Action<Option>? CheckApplyHandler
    {
        get; set;
    }

    /// <summary>Localized expand/collapse button text for checklist rows.</summary>
    public string? CheckExpandLabel
    {
        get; set;
    }

    public string? CheckCollapseLabel
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
