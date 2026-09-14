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

    // ===== sidebar (category pane) customization =====

    /// <summary>
    /// Reorders/hides the pane entries to match the stored layout. The pairs are
    /// (stable category key, localized label) so both lists stay in lockstep.
    /// </summary>
    private void ApplyCategoryLayout(List<(string Key, string Label)> categories)
    {
        if (_layout.CategoryOrder.Count == 0 && _layout.HiddenCategories.Count == 0)
        {
            return;
        }

        // Hidden categories are dropped from the pane entirely; their options
        // still exist, they are just not reachable from the sidebar.
        categories.RemoveAll(c => _layout.HiddenCategories.Contains(c.Key));

        if (_layout.CategoryOrder.Count == 0)
        {
            return;
        }

        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _layout.CategoryOrder.Count; i++)
        {
            rank[_layout.CategoryOrder[i]] = i;
        }

        var ordered = categories
            .Select((c, index) => (c, index, rankKey: rank.TryGetValue(c.Key, out var r) ? r : int.MaxValue))
            .OrderBy(x => x.rankKey)
            .ThenBy(x => x.index)
            .Select(x => x.c)
            .ToList();

        categories.Clear();
        categories.AddRange(ordered);
    }

    /// <summary>Keys currently shown in the pane, in display order.</summary>
    private readonly List<string> _displayedCategoryKeys = [];

    /// <summary>Moves a sidebar category one slot up or down.</summary>
    internal void MoveCategory(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= _displayedCategoryKeys.Count || target < 0 || target >= _displayedCategoryKeys.Count)
        {
            return;
        }

        var keys = _displayedCategoryKeys.ToList();
        (keys[index], keys[target]) = (keys[target], keys[index]);

        // Store the full order (hidden ones appended) so unlisted categories do
        // not snap back to their built-in position on the next launch.
        _layout.CategoryOrder = keys
            .Concat(CategoryKeys.Where(k => !keys.Contains(k)))
            .ToList();
        SaveLayout();
    }

    /// <summary>Hides or restores a sidebar category.</summary>
    internal void SetCategoryHidden(string key, bool hidden)
    {
        _layout.HiddenCategories.Remove(key);
        if (hidden)
        {
            _layout.HiddenCategories.Add(key);
        }
        SaveLayout();
    }

    // ===== user-added options =====

    /// <summary>
    /// Materializes the user's hand-added options as real rows. They carry their
    /// own Getter/Setter pair, so they behave like built-in rows while their
    /// value lives in settings-layout.json instead of the AppSettings store.
    /// </summary>
    private void AppendCustomOptions(List<Option> options)
    {
        if (_layout.Added.Count == 0)
        {
            return;
        }

        foreach (var added in _layout.Added)
        {
            if (string.IsNullOrWhiteSpace(added.MpvKey))
            {
                continue;
            }

            var option = new Option
            {
                Key = added.Id,
                Label = added.Label,
                Description = added.Description,
                Section = added.Section,
                MpvKey = added.MpvKey,
                MpvValue = added.Value,
                Getter = () => added.Value,
                Setter = value =>
                {
                    added.Value = value as string ?? string.Empty;
                    SaveLayout();
                    AppContext.SendMpvCommand($"no-osd set {added.MpvKey} {added.Value}");
                },
            };

            switch (added.Kind)
            {
                case CustomOptionKinds.Boolean:
                    option.Type = OptionType.Boolean;
                    // Boolean rows carry a real bool, so the toggle control works.
                    option.Getter = () => string.Equals(added.Value, "yes", StringComparison.OrdinalIgnoreCase);
                    option.Setter = value =>
                    {
                        added.Value = value is true ? "yes" : "no";
                        SaveLayout();
                        AppContext.SendMpvCommand($"no-osd set {added.MpvKey} {added.Value}");
                    };
                    break;

                case CustomOptionKinds.Choice:
                    option.Type = OptionType.StringList;
                    option.Choices = added.Choices
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => new OptionChoice(c, c))
                        .ToList();
                    option.AllowCustom = false;
                    break;

                default:
                    option.Type = OptionType.String;
                    break;
            }

            option.Category = ResolveCategoryName(added.CategoryKey) ?? CategoryOrder.FirstOrDefault() ?? "General";
            option.Edit.Refresh();
            options.Add(option);
        }
    }

    /// <summary>Maps a stable category key back to its localized display name.</summary>
    private string? ResolveCategoryName(string categoryKey)
    {
        var index = Array.IndexOf(CategoryKeys, categoryKey);
        return index >= 0 && index < CategoryOrder.Count ? CategoryOrder[index] : null;
    }

    /// <summary>Adds a hand-written option and persists it.</summary>
    internal void AddCustomOption(string categoryKey, string label, string description, string mpvKey, string kind, IEnumerable<string> choices)
    {
        if (string.IsNullOrWhiteSpace(mpvKey))
        {
            return;
        }

        var id = "custom:" + mpvKey.Trim();
        _layout.Added.RemoveAll(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        _layout.Added.Add(new CustomOption
        {
            Id = id,
            CategoryKey = categoryKey,
            Label = string.IsNullOrWhiteSpace(label) ? mpvKey.Trim() : label.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            MpvKey = mpvKey.Trim(),
            Kind = CustomOptionKinds.All.Contains(kind) ? kind : CustomOptionKinds.Text,
            Choices = choices.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList(),
            Value = string.Empty,
        });

        SaveLayout();
    }

    /// <summary>Removes a hand-added option.</summary>
    internal void RemoveCustomOption(string id)
    {
        _layout.Added.RemoveAll(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        _layout.Order.Remove(id);
        _layout.Entries.Remove(id);
        SaveLayout();
    }
}
