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

        var language = ActiveLanguageKey;

        foreach (var option in options)
        {
            if (!_layout.Entries.TryGetValue(option.Key, out var entry))
            {
                continue;
            }

            // Renames are stored as an override, never written into the
            // language files: the built-in caption keeps changing with the UI
            // language underneath, and this simply wins when it applies.
            // A null result means "no override for this language", so the
            // built-in text stands; an empty string is a deliberate blank.
            if (LabelOverrides.ResolveLabel(entry, language) is { } label)
            {
                option.Label = label;
            }

            if (LabelOverrides.ResolveDescription(entry, language) is { } description)
            {
                option.Description = description;
            }

            // Hidden rows are filtered out of the normal list by IsVisible; the
            // customize mode can list them again (behind its "show hidden"
            // toggle) so they can be brought back. The caption is refreshed
            // here rather than with the rest of the page because the hide flag
            // is applied after that pass, and the entry has to read "show
            // again" the moment the row is known to be hidden.
            if (entry.Hidden)
            {
                option.IsVisible = false;
                option.IsHiddenByUser = true;
                option.Edit.Refresh(hiddenByUser: true);
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

    /// <summary>
    /// UI language overrides are keyed against, normalized so a missing or
    /// malformed setting cannot silently match nothing.
    /// </summary>
    private static string ActiveLanguageKey
    {
        get
        {
            var language = AppContext.AppSetting.CurrentLanguage;
            return string.IsNullOrWhiteSpace(language) ? "en-US" : language;
        }
    }

    /// <summary>Persists the current customization.</summary>
    internal void SaveLayout() => SettingsLayoutStore.Save(_layout);

    /// <summary>
    /// Sidebar glyph for a stable category key. A built-in key indexes the
    /// fixed glyph table; a key the user created resolves to the icon they
    /// picked when they made the category, falling back to a folder.
    /// </summary>
    private string GlyphForCategoryKey(string key)
    {
        var builtIn = Array.IndexOf(CategoryKeys, key);
        if (builtIn >= 0 && builtIn < CategoryGlyphs.Length)
        {
            return CategoryGlyphs[builtIn];
        }

        return _layout.FindCategory(key)?.Glyph ?? "\uE8B7";
    }

    /// <summary>
    /// Localized caption for a stable category key, covering the categories
    /// the user created (which have no AppLang caption to look up) and the
    /// rename the user may have put on top of a built-in one.
    /// </summary>
    private string? CategoryCaptionForKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        // A rename is an override on the built-in caption, exactly like a row's
        // or a folder's, so it is consulted before the language file. An empty
        // override (a deliberately blank name) keeps the built-in text, because
        // a nameless entry in the sidebar is unreachable rather than tidy.
        if (LabelOverrides.ResolveLabel(
                _layout.Entries.TryGetValue(CategoryEntryKey(key), out var entry) ? entry : null,
                ActiveLanguageKey) is { Length: > 0 } renamed)
        {
            return renamed;
        }

        return _layout.FindCategory(key)?.DisplayFor(ActiveLanguageKey)
            ?? SettingsSectionIds.CategoryCaptionFor(key);
    }

    /// <summary>
    /// Key a category rename is stored under. Prefixed so it cannot collide
    /// with a real option key, matching <c>SectionEntryKey</c>.
    /// </summary>
    private static string CategoryEntryKey(string categoryKey) => "category-label:" + categoryKey;

    /// <summary>
    /// Re-labels the rows of every renamed category so <c>option.Category</c>
    /// and the sidebar label stay one string.
    ///
    /// A category's rows are grouped and filtered by their own Category field,
    /// not by the stable key — the sidebar label, the bookmark pane, the search
    /// index and the per-category reset all compare against it. Renaming the
    /// sidebar alone would leave those four looking for a name no row carries
    /// any more, so the rows move with the label, the same way
    /// <c>ApplySectionLayout</c> moves a row into a renamed folder.
    /// </summary>
    private void RenameCategoryRows(List<(string Key, string Label)> categories)
    {
        foreach (var (key, label) in categories)
        {
            if (SettingsSectionIds.CategoryCaptionFor(key) is not { } builtIn
                || string.Equals(builtIn, label, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var option in Settings)
            {
                if (string.Equals(option.Category, builtIn, StringComparison.Ordinal))
                {
                    option.Category = label;
                }
            }
        }
    }

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
        option.IsHiddenByUser = hidden;

        // The row's own menu carries one entry that flips between "hide" and
        // "show again", so its caption has to follow the new state.
        option.Edit.Refresh(hiddenByUser: hidden);
        SaveLayout();
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

    /// <summary>Stable key of the pane entry currently shown at the given index.</summary>
    private string? DisplayedCategoryKeyAt(int index) =>
        index >= 0 && index < _displayedCategoryKeys.Count ? _displayedCategoryKeys[index] : null;

    /// <summary>
    /// Sidebar drag reorder: moves the pane entry at <paramref name="from"/> to
    /// <paramref name="to"/> and records the full order. The pane is a
    /// NavigationView, which has no built-in drag, so the gesture is
    /// implemented by hand and lands here as a pair of indices.
    /// </summary>
    internal void MoveCategoryTo(int from, int to)
    {
        if (from < 0 || from >= _displayedCategoryKeys.Count
            || to < 0 || to >= _displayedCategoryKeys.Count
            || from == to)
        {
            return;
        }

        var keys = _displayedCategoryKeys.ToList();
        var moved = keys[from];
        keys.RemoveAt(from);
        keys.Insert(to, moved);

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

    /// <summary>
    /// Applies the folders: rows the user moved into another folder are
    /// re-labelled, then hidden folders are dropped, then folders are
    /// re-emitted in the stored order. Runs before the per-row ordering so an
    /// explicitly ranked row still wins over its folder's position.
    /// </summary>
    private void ApplySectionLayout(List<Option> options)
    {
        // 1) Row -> folder assignments, including folders the user created.
        foreach (var option in options)
        {
            if (!_layout.Entries.TryGetValue(option.Key, out var entry) || entry.SectionId is null)
            {
                continue;
            }

            // The caption put on the row is the folder's *display* name, so a
            // renamed folder is renamed in the card pane as well as in the tree.
            // Which kind of folder it is travels as a flag, not as a caption
            // test: after a rename the caption no longer matches AppLang.
            if (_layout.FindSection(entry.SectionId) is { } custom)
            {
                option.Section = custom.DisplayFor(ActiveLanguageKey);
                option.SectionId = custom.Id;
                option.IsCustomSection = true;
            }
            else if (SettingsSectionIds.CaptionFor(entry.SectionId) is { } caption)
            {
                option.Section = SectionDisplayName(entry.SectionId, caption);
                option.SectionId = entry.SectionId;
            }
        }

        if (_layout.SectionOrder.Count == 0
            && _layout.HiddenSections.Count == 0
            && _layout.CustomSections.Count == 0)
        {
            // Nothing structural at all: no ordering to apply, no hidden
            // folder to drop and no user folder that needs a header. Applying
            // while CustomSections is non-empty matters even when SectionOrder
            // is still empty — that is exactly the state right after the first
            // folder is created in a category that had no stored order, and
            // returning here left the new folder invisible.
            return;
        }

        if (_layout.HiddenSections.Count > 0)
        {
            // A hidden folder's rows stay in the model and are marked invisible,
            // exactly like a hidden row — they are not removed. Removing them is
            // what made hiding a folder a one-way trip: with no row left to be
            // discovered from, the folder also vanished from the tree, so nothing
            // was left on screen to bring it back. Kept, they are still the
            // folder's members, and the customize mode can list them (behind its
            // "show hidden" switch) with a header that offers "show again".
            foreach (var option in options)
            {
                if (option.SectionId is { } id && _layout.HiddenSections.Contains(id))
                {
                    option.IsVisible = false;
                    option.IsHiddenByUser = true;
                    option.IsHiddenSection = true;
                    option.Edit.Refresh(hiddenByUser: true);
                }
            }
        }

        // 2) Folders hold contiguous runs; re-emit them in the stored order.
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _layout.SectionOrder.Count; i++)
        {
            rank[_layout.SectionOrder[i]] = i;
        }

        var reordered = options
            .Select((option, index) => (option, index,
                rankKey: option.SectionId is { } id && rank.TryGetValue(id, out var r) ? r : int.MaxValue))
            .OrderBy(x => x.rankKey)
            .ThenBy(x => x.index)
            .Select(x => x.option)
            .ToList();

        options.Clear();
        options.AddRange(reordered);

        RecomputeSectionHeaders(options);

        // 3) An empty user-created folder must still be visible, otherwise it
        //    cannot be dragged into or removed.
        AppendEmptyCustomFolders(options);
    }

    /// <summary>Re-flags which row starts each section run, after reordering.</summary>
    private static void RecomputeSectionHeaders(List<Option> options)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            option.ShowSectionHeader = option.SectionId is not null && seen.Add(option.SectionId);
        }
    }

    /// <summary>Adds a bare header row for each created folder that holds nothing yet.</summary>
    private void AppendEmptyCustomFolders(List<Option> options)
    {
        if (_layout.CustomSections.Count == 0)
        {
            return;
        }

        var populated = new HashSet<string>(
            options.Where(o => o.SectionId is not null).Select(o => o.SectionId!),
            StringComparer.Ordinal);

        foreach (var section in _layout.CustomSections)
        {
            if (populated.Contains(section.Id))
            {
                continue;
            }

            var categoryName = SettingsSectionIds.CategoryCaptionFor(section.CategoryKey);
            if (categoryName is null
                || !options.Any(o => string.Equals(o.Category, categoryName, StringComparison.Ordinal)))
            {
                continue;
            }

            // A placeholder row so an empty folder still renders and stays a
            // drop target. Action rows render as a labelled entry with no
            // value control, which is exactly what a bare folder needs.
            options.Add(new Option
            {
                Key = "custom-section:" + section.Id,
                Label = section.DisplayFor(ActiveLanguageKey),
                Description = AppContext.AppLang.CustomizeEmptySection,
                Category = categoryName,
                Section = section.DisplayFor(ActiveLanguageKey),
                SectionId = section.Id,
                IsCustomSection = true,
                Type = OptionType.Action,
                IsVisible = true,
                ShowSectionHeader = true,
                Getter = () => null!,
                Setter = _ => { },
            });
        }
    }

    /// <summary>
    /// Creates a 2nd-level grouping. <paramref name="paneOnly"/> separates the
    /// two gestures the toolbar offers: a plain folder also becomes a sidebar
    /// node, while a column groups cards in the pane and stays out of the tree.
    /// </summary>
    internal void CreateSection(string categoryKey, string name, bool paneOnly = false)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(categoryKey))
        {
            return;
        }

        var id = "sec:" + name.Trim();
        _layout.CustomSections.RemoveAll(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        _layout.CustomSections.Add(new CustomSection
        {
            Id = id,
            CategoryKey = categoryKey,
            Name = name.Trim(),
            PaneOnly = paneOnly,
        });

        // New folders go last so they do not jump above the built-in ones.
        if (!_layout.SectionOrder.Contains(id))
        {
            _layout.SectionOrder.Add(id);
        }

        SaveLayout();
    }

    /// <summary>
    /// Creates a top-level sidebar category.
    ///
    /// The id is derived from the name so re-adding the same name updates the
    /// existing category instead of stacking a second one — and so a category
    /// the user made is addressable by a stable key everywhere the layout
    /// stores per-category data (folder order, hidden state).
    /// </summary>
    internal void CreateCategory(string name, string glyph)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var id = "cat:" + trimmed;
        if (_layout.FindCategory(id) is { } existing)
        {
            existing.Name = trimmed;
            existing.Glyph = glyph;
        }
        else
        {
            _layout.CustomCategories.Add(new CustomCategory
            {
                Id = id,
                Name = trimmed,
                Glyph = glyph,
            });
        }

        // New categories go last in the sidebar so nothing already placed moves.
        if (!_layout.CategoryOrder.Contains(id))
        {
            _layout.CategoryOrder.Add(id);
        }

        SaveLayout();
    }

    /// <summary>
    /// Moves a row into a folder, or back out of every folder when
    /// <paramref name="sectionId"/> is null.
    /// </summary>
    internal void MoveRowToSection(string optionKey, string? sectionId)
    {
        if (string.IsNullOrEmpty(optionKey))
        {
            return;
        }

        var entry = _layout.EntryFor(optionKey);
        entry.SectionId = sectionId;

        // A row with nothing else overridden must not leave an empty entry
        // behind: those accumulate and are invisible in the file.
        if (entry.Label is null && entry.Description is null && entry.SectionId is null)
        {
            _layout.Entries.Remove(optionKey);
        }

        SaveLayout();
    }

    /// <summary>Removes a folder the user created; its rows fall back to their own section.</summary>
    internal void DeleteSection(string sectionId)
    {
        _layout.CustomSections.RemoveAll(s => string.Equals(s.Id, sectionId, StringComparison.Ordinal));
        _layout.SectionOrder.Remove(sectionId);
        _layout.HiddenSections.Remove(sectionId);

        foreach (var key in _layout.Entries
            .Where(p => string.Equals(p.Value.SectionId, sectionId, StringComparison.Ordinal))
            .Select(p => p.Key)
            .ToList())
        {
            _layout.Entries[key].SectionId = null;
        }

        SaveLayout();
    }

    /// <summary>Ranks the folders of the current category in display order.</summary>
    internal void StoreSectionOrder(IEnumerable<string> sectionIdsInDisplayOrder)
    {
        // Deduplicate first: a repeated id would otherwise be appended to the
        // stored order a second time, and the file would grow a phantom entry
        // that no folder ever matches again.
        var order = sectionIdsInDisplayOrder
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (order.Count == 0)
        {
            return;
        }

        // The stored order is global (folders from every category share one
        // list), so a reorder only rewrites the slots the moved folders occupy
        // and leaves the other categories' entries where they were.
        var moved = new HashSet<string>(order, StringComparer.Ordinal);
        var queue = new Queue<string>(order);
        var rebuilt = new List<string>();
        foreach (var id in _layout.SectionOrder)
        {
            if (moved.Contains(id))
            {
                // Guard rather than assert: a stale stored order can carry more
                // slots for a set than the live list has members, and Dequeue on
                // an empty queue would take the settings page down.
                if (queue.Count > 0)
                {
                    rebuilt.Add(queue.Dequeue());
                }
            }
            else
            {
                rebuilt.Add(id);
            }
        }
        while (queue.Count > 0)
        {
            rebuilt.Add(queue.Dequeue());
        }

        _layout.SectionOrder = rebuilt.Distinct(StringComparer.Ordinal).ToList();
        SaveLayout();
    }

    /// <summary>Section ids of a category, in display order.</summary>
    internal List<string> SectionIdsOf(string category)
    {
        var ids = new List<string>();
        foreach (var option in Settings.Where(o => string.Equals(o.Category, category, StringComparison.Ordinal)))
        {
            if (option.SectionId is { } id && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>Moves a section one slot up or down within its category.</summary>
    internal void MoveSection(string sectionId, int delta)
    {
        if (CurrentCategory is not { } category)
        {
            return;
        }

        var order = SectionIdsOf(category);
        var index = order.IndexOf(sectionId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= order.Count)
        {
            return;
        }

        (order[index], order[target]) = (order[target], order[index]);
        StoreSectionOrder(order);
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
