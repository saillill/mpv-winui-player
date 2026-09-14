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

    // ===== section (2nd-level / column) customization =====

    /// <summary>Section ids in the order the given options present them.</summary>
    internal static List<string> SectionIdsInDisplayOrder(IEnumerable<Option> options)
    {
        var ids = new List<string>();
        foreach (var option in options)
        {
            var id = SettingsSectionIds.IdFor(option.Section);
            if (id is not null && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    /// Applies section hiding and reordering. Sections are contiguous in the
    /// tree already (the build clusters by category+section), so reordering is a
    /// matter of re-emitting each section's run in the stored section order.
    /// </summary>
    private void ApplySectionLayout(List<Option> options)
    {
        if (_layout.SectionOrder.Count == 0 && _layout.HiddenSections.Count == 0)
        {
            return;
        }

        if (_layout.HiddenSections.Count > 0)
        {
            options.RemoveAll(o =>
                SettingsSectionIds.IdFor(o.Section) is { } id && _layout.HiddenSections.Contains(id));
        }

        if (_layout.SectionOrder.Count == 0)
        {
            return;
        }

        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _layout.SectionOrder.Count; i++)
        {
            rank[_layout.SectionOrder[i]] = i;
        }

        // Rank each option by its section, then keep the original relative order
        // inside a section and between unlisted ones.
        var reordered = options
            .Select((option, index) => (option, index,
                rankKey: SettingsSectionIds.IdFor(option.Section) is { } id && rank.TryGetValue(id, out var r)
                    ? r
                    : int.MaxValue))
            .OrderBy(x => x.rankKey)
            .ThenBy(x => x.index)
            .Select(x => x.option)
            .ToList();

        options.Clear();
        options.AddRange(reordered);

        // The section captions must keep matching their (now reordered) runs.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            option.ShowSectionHeader = !string.IsNullOrEmpty(option.Section) && seen.Add(option.Section);
        }
    }

    /// <summary>Moves a section one slot up or down within its category.</summary>
    internal void MoveSection(string sectionId, int delta)
    {
        var order = SectionIdsInDisplayOrder(
            CurrentCategory is null ? Settings : Settings.Where(o => o.Category == CurrentCategory));
        var index = order.IndexOf(sectionId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= order.Count)
        {
            return;
        }

        (order[index], order[target]) = (order[target], order[index]);
        _layout.SectionOrder = order;
        SaveLayout();
    }

    /// <summary>Hides or restores a whole section.</summary>
    internal void SetSectionHidden(string sectionId, bool hidden)
    {
        _layout.HiddenSections.Remove(sectionId);
        if (hidden)
        {
            _layout.HiddenSections.Add(sectionId);
        }
        SaveLayout();
    }

    /// <summary>Restores every section the customize mode hid.</summary>
    internal void RestoreHiddenSections()
    {
        _layout.HiddenSections.Clear();
        SaveLayout();
    }

}
