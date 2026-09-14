using mpv_winui.Modules.Settings.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// The customizable-settings half of the page: merges the user's
/// settings-layout.json into the code-defined option tree, and exposes the
/// operations the inline customize mode performs (reorder, hide, restore
/// default, rename, raw mpv key/value).
/// </summary>
public sealed partial class SettingsPage
{
    private SettingsLayout _layout = SettingsLayoutStore.Load();

    /// <summary>Loaded customization; shared with <see cref="OptionListControl"/>.</summary>
    internal SettingsLayout Layout => _layout;

    /// <summary>
    /// True while the inline customize mode is on. The list then renders the
    /// edit template (drag handle, editable fields, delete menu) instead of the
    /// value controls, and hidden rows stay visible so they can be restored.
    /// </summary>
    private bool _customizeMode;

    /// <summary>Merges stored customization into the freshly built option tree.</summary>
    private void ApplyLayout(List<Option> options)
    {
        if (_layout.IsEmpty)
        {
            return;
        }

        foreach (var option in options)
        {
            if (!_layout.Entries.TryGetValue(option.Key, out var entry))
            {
                continue;
            }

            // Only fill in what the user actually overrode: an empty string is
            // a deliberate "make it blank", null means "keep the built-in text".
            if (entry.Label is not null)
            {
                option.Label = entry.Label;
            }
            if (entry.Description is not null)
            {
                option.Description = entry.Description;
            }

            option.MpvKey = entry.MpvKey;
            option.MpvValue = entry.MpvValue;

            // Hidden rows are filtered out of the normal list by IsVisible; the
            // customize mode ignores that flag so they can be brought back.
            if (entry.Hidden)
            {
                option.IsVisible = false;
            }
        }

        if (_layout.Order.Count == 0)
        {
            return;
        }

        // Rank by the stored order; anything unlisted keeps its built-in
        // relative position by inheriting the rank of the previous listed key.
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _layout.Order.Count; i++)
        {
            rank[_layout.Order[i]] = i;
        }

        var ordered = options
            .Select((option, index) => (option, index,
                rankKey: rank.TryGetValue(option.Key, out var r) ? r : int.MaxValue))
            .OrderBy(x => x.rankKey)
            .ThenBy(x => x.index)
            .Select(x => x.option)
            .ToList();

        options.Clear();
        options.AddRange(ordered);
    }

    /// <summary>Persists the current customization.</summary>
    internal void SaveLayout() => SettingsLayoutStore.Save(_layout);

    /// <summary>Records the visible order so a drag-reorder survives a rebuild.</summary>
    internal void StoreOrder(IEnumerable<string> keysInDisplayOrder)
    {
        _layout.Order = keysInDisplayOrder.ToList();
        SaveLayout();
    }

    /// <summary>Stores a label/description override; null removes the override.</summary>
    internal void StoreText(Option option, string? label, string? description)
    {
        var entry = _layout.EntryFor(option.Key);
        entry.Label = label;
        entry.Description = description;
        _layout.Prune(option.Key);
        SaveLayout();
    }

    /// <summary>Stores the raw mpv key/value override and pushes it to the player.</summary>
    internal void StoreMpvOverride(Option option, string? mpvKey, string? mpvValue)
    {
        var entry = _layout.EntryFor(option.Key);
        entry.MpvKey = string.IsNullOrWhiteSpace(mpvKey) ? null : mpvKey.Trim();
        entry.MpvValue = mpvValue;
        _layout.Prune(option.Key);
        SaveLayout();
        option.MpvKey = entry.MpvKey;
        option.MpvValue = entry.MpvValue;

        // A raw value is authoritative: apply it immediately so the user sees
        // the effect without leaving the settings page.
        if (!string.IsNullOrWhiteSpace(entry.MpvKey) && !string.IsNullOrWhiteSpace(entry.MpvValue))
        {
            AppContext.SendMpvCommand($"no-osd set {entry.MpvKey} {entry.MpvValue}");
        }
    }

    /// <summary>Hides a row (its stored setting value is kept) or brings it back.</summary>
    internal void SetHidden(Option option, bool hidden)
    {
        var entry = _layout.EntryFor(option.Key);
        entry.Hidden = hidden;
        _layout.Prune(option.Key);
        option.IsVisible = !hidden;
        SaveLayout();
    }

    /// <summary>
    /// "Restore default" for a single row: drop every override bought in the
    /// customize mode and reset the stored value, matching the category-level
    /// reset (which is itself just ResetKeys over the category's keys).
    /// </summary>
    internal void ResetRowToDefault(Option option)
    {
        _layout.Entries.Remove(option.Key);
        _layout.Order.Remove(option.Key);
        SaveLayout();

        if (!option.Key.StartsWith("Shortcut:", StringComparison.Ordinal))
        {
            AppContext.AppSetting.ResetKeys([option.Key]);
        }

        option.IsVisible = true;
        option.MpvKey = null;
        option.MpvValue = null;
    }

    /// <summary>The raw mpv command the row currently publishes, for display.</summary>
    internal static string DescribeRawCommand(Option option)
    {
        if (!string.IsNullOrWhiteSpace(option.MpvKey))
        {
            return $"set {option.MpvKey} {option.MpvValue}";
        }

        try
        {
            var value = option.Getter?.Invoke();
            return value is null ? string.Empty : MpvSettings.ToCommand(option.Key, value) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Lazily suggests the raw mpv option name for a row, e.g. "hwdec".</summary>
    internal static string SuggestMpvKey(Option option)
    {
        var command = DescribeRawCommand(option);
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0] == "set" ? parts[1] : string.Empty;
    }
}
