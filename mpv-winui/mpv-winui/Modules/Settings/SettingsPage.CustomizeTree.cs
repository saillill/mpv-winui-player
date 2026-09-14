using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Settings.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// The customize mode's bookmark-manager surface: a folder tree on the left
/// (categories, each holding its 2nd-level folders) and the cards of the
/// selected node on the right.
///
/// The tree is a second, independent control from the option list, so both
/// subscribe to the same page-side layout operations and share one persisted
/// layout. Only the customize mode uses it; the normal browsing view keeps the
/// single-column card list.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>Node selected in the folder tree: a category key or a section id.</summary>
    private string? _treeSelectedKey;

    /// <summary>Node was a section (true) rather than a category (false).</summary>
    private bool _treeSelectedIsSection;

    /// <summary>Builds the tree and points the right pane at the selection.</summary>
    private void BuildCustomizeTree()
    {
        FolderTree.RootNodes.Clear();
        TreeTitleText.Text = AppContext.AppLang.CustomizeAddCategory;

        // Root = one node per category. Children = that category's folders
        // (both the built-in ones and the folders the user created), so the
        // two levels of the bookmark manager map onto one tree.
        foreach (var category in Categories)
        {
            var categoryKey = CategoryKeyForCaption(category);
            if (categoryKey is null)
            {
                continue;
            }

            var root = new TreeViewNode
            {
                Content = BuildTreeNodeContent(category, isSection: false, categoryKey: categoryKey),
            };

            foreach (var (sectionId, caption, isCustom) in SectionsOf(category))
            {
                root.Children.Add(new TreeViewNode
                {
                    Content = BuildTreeNodeContent(caption, isSection: true, sectionId: sectionId, isCustom: isCustom),
                });
            }

            FolderTree.RootNodes.Add(root);
        }

        RestoreTreeSelection();
    }

    /// <summary>
    /// The label shown in the tree. A section node additionally carries the
    /// folder's edit affordances (rename / new / delete), so the tree is where
    /// the structure is managed.
    /// </summary>
    private TreeViewNodeContent BuildTreeNodeContent(
        string caption,
        bool isSection,
        string? categoryKey = null,
        string? sectionId = null,
        bool isCustom = false)
    {
        var edit = new OptionEditText();
        edit.Refresh();

        var content = new TreeViewNodeContent
        {
            Text = caption,
            IsSection = isSection,
            CategoryKey = categoryKey,
            SectionId = sectionId,
            IsCustom = isCustom,
            Edit = edit,
        };

        if (!isSection)
        {
            // A category node offers "new folder" — that is how a 2nd-level
            // folder comes into being, exactly like adding a bookmark folder.
            content.CanAddFolder = true;
            edit.BuildMenu(
                isSection: false,
                canAddFolder: true,
                canDelete: false,
                addFolder: () => CreateSectionInteractiveAsync(),
                rename: null,
                delete: null);
        }
        else
        {
            edit.BuildMenu(
                isSection: true,
                canAddFolder: false,
                canDelete: isCustom,
                addFolder: null,
                rename: () => RenameSectionInteractiveAsync(sectionId!, caption),
                delete: () => DeleteSectionInteractiveAsync(sectionId!, caption));
        }

        return content;
    }

    /// <summary>Folders of a category, in display order, with their display names.</summary>
    private List<(string Id, string Caption, bool IsCustom)> SectionsOf(string category)
    {
        var result = new List<(string, string, bool)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in Settings.Where(o => string.Equals(o.Category, category, StringComparison.Ordinal)))
        {
            if (option.SectionId is not { } id || !seen.Add(id))
            {
                continue;
            }

            var isCustom = SettingsSectionIds.IdFor(option.Section) is null;
            var caption = SectionDisplayName(id, option.Section ?? string.Empty);
            result.Add((id, caption, isCustom));
        }

        // A folder the user created but which holds nothing has no row to be
        // discovered from, so it is appended from the stored list.
        foreach (var section in _layout.CustomSections)
        {
            if (CategoryCaptionForKey(section.CategoryKey) is not { } categoryCaption
                || !string.Equals(categoryCaption, category, StringComparison.Ordinal)
                || !seen.Add(section.Id))
            {
                continue;
            }

            result.Add((section.Id, section.DisplayFor(ActiveLanguageKey), true));
        }

        return result;
    }

    /// <summary>Selects the node matching the last selection, or the first category.</summary>
    private void RestoreTreeSelection()
    {
        TreeViewNode? target = null;

        if (_treeSelectedKey is not null)
        {
            target = FindTreeNode(_treeSelectedKey, _treeSelectedIsSection);
        }

        target ??= FolderTree.RootNodes.FirstOrDefault();
        if (target is null)
        {
            UpdateCustomizePane();
            return;
        }

        FolderTree.SelectedNode = target;
    }

    /// <summary>Locates a node by category key or section id.</summary>
    private TreeViewNode? FindTreeNode(string key, bool isSection)
    {
        foreach (var root in FolderTree.RootNodes)
        {
            if (!isSection && root.Content is TreeViewNodeContent { CategoryKey: { } ck }
                && string.Equals(ck, key, StringComparison.Ordinal))
            {
                return root;
            }

            foreach (var child in root.Children)
            {
                if (isSection && child.Content is TreeViewNodeContent { SectionId: { } sid }
                    && string.Equals(sid, key, StringComparison.Ordinal))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private void FolderTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count > 0 && args.AddedItems[0] is TreeViewNode node)
        {
            RememberTreeNode(node);
        }
        UpdateCustomizePane();
    }

    private void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node)
        {
            RememberTreeNode(node);
        }
        UpdateCustomizePane();
    }

    private void RememberTreeNode(TreeViewNode node)
    {
        if (node.Content is not TreeViewNodeContent content)
        {
            return;
        }

        if (content.IsSection && content.SectionId is { } id)
        {
            _treeSelectedKey = id;
            _treeSelectedIsSection = true;
        }
        else if (content.CategoryKey is { } categoryKey)
        {
            _treeSelectedKey = categoryKey;
            _treeSelectedIsSection = false;
        }
    }

    /// <summary>
    /// Fills the right pane from the selected node: a category shows every card
    /// in that category, a folder shows only its members plus the un-filed
    /// cards, so the cards outside any folder stay reachable.
    /// </summary>
    private void UpdateCustomizePane()
    {
        if (!_customizeMode)
        {
            return;
        }

        var nodes = CurrentRepositoryList();
        var (title, options) = ResolvePaneContents(nodes);
        CustomizePaneTitle.Text = title;

        // The right pane is the same control type as the browsing list, so it
        // needs the mode flag and its own item source.
        CustomizeOptionsControl.CustomizeMode = true;

        // The pane stands for the folder on display, so a card dropped anywhere
        // in it joins that folder — that is the "drag a bookmark into a folder"
        // gesture. A category page files nothing, hence the null target.
        if (_treeSelectedIsSection && _treeSelectedKey is { } paneSectionId)
        {
            CustomizeOptionsControl.SetPaneDropTarget(paneSectionId);
        }
        else
        {
            CustomizeOptionsControl.SetPaneDropTarget(null);
        }

        CustomizeOptionsControl.OptionList = options;
    }

    /// <summary>The full option list the page currently holds.</summary>
    private List<Controls.Option> CurrentRepositoryList() => Settings;

    private (string Title, List<Controls.Option> Options) ResolvePaneContents(List<Controls.Option> all)
    {
        if (_treeSelectedKey is null)
        {
            return (string.Empty, all);
        }

        if (_treeSelectedIsSection)
        {
            // Which category the folder belongs to decides the un-filed cards
            // shown alongside it.
            var category = CategoryKeyOfSection(_treeSelectedKey);

            var sectionCaption = SettingsSectionIds.CaptionFor(_treeSelectedKey) is { } builtIn
                ? SectionDisplayName(_treeSelectedKey, builtIn)
                : _layout.FindSection(_treeSelectedKey)?.DisplayFor(ActiveLanguageKey) ?? string.Empty;

            var categoryCaption = category is null ? null : CategoryCaptionForKey(category);

            var members = all
                .Where(o => string.Equals(o.SectionId, _treeSelectedKey, StringComparison.Ordinal))
                .ToList();

            // The cards filed under no folder stay visible under the folder's
            // own cards, so a card can always be dragged into the folder and a
            // mis-filed one dragged back out.
            var unFiled = all
                .Where(o => o.SectionId is null
                    && (categoryCaption is null || string.Equals(o.Category, categoryCaption, StringComparison.Ordinal)))
                .ToList();

            return (sectionCaption, members.Concat(unFiled).ToList());
        }

        var selectedCategory = CategoryCaptionForKey(_treeSelectedKey);
        var inCategory = selectedCategory is null
            ? all
            : all.Where(o => string.Equals(o.Category, selectedCategory, StringComparison.Ordinal)).ToList();
        return (selectedCategory ?? string.Empty, inCategory);
    }

    /// <summary>Stable category key a folder belongs to, resolved from its rows or its record.</summary>
    private string? CategoryKeyOfSection(string sectionId)
    {
        if (_layout.FindSection(sectionId) is { } custom)
        {
            return custom.CategoryKey;
        }

        var caption = SettingsSectionIds.CaptionFor(sectionId);
        if (caption is null)
        {
            return null;
        }

        foreach (var option in Settings)
        {
            if (string.Equals(option.Section, caption, StringComparison.Ordinal))
            {
                return SettingsSectionIds.CategoryKeyFor(option.Category);
            }
        }

        return null;
    }

    private void FolderTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        StoreTreeOrder();
    }

    /// <summary>Persists the tree's current category and folder order.</summary>
    private void StoreTreeOrder()
    {
        var categoryKeys = new List<string>();
        foreach (var root in FolderTree.RootNodes)
        {
            if (root.Content is TreeViewNodeContent { CategoryKey: { } key })
            {
                categoryKeys.Add(key);
            }
        }

        if (categoryKeys.Count > 0)
        {
            _layout.CategoryOrder = categoryKeys
                .Concat(CategoryKeys.Where(k => !categoryKeys.Contains(k)))
                .ToList();
        }

        // Folder order is stored per category: collect each category's children
        // and write them through the shared section-order store.
        var sectionIds = new List<string>();
        foreach (var root in FolderTree.RootNodes)
        {
            foreach (var child in root.Children)
            {
                if (child.Content is TreeViewNodeContent { SectionId: { } id })
                {
                    sectionIds.Add(id);
                }
            }
        }

        if (sectionIds.Count > 0)
        {
            StoreSectionOrder(sectionIds);
        }
        else
        {
            SaveLayout();
        }
    }

    /// <summary>Maps a localized category caption back to its stable key.</summary>
    private string? CategoryKeyForCaption(string caption)
    {
        // A category the user made has no AppLang caption, so it resolves by
        // its own name; that is the only thing the sidebar and the layout both
        // know about it.
        if (_layout.CustomCategories.FirstOrDefault(c => string.Equals(c.DisplayFor(ActiveLanguageKey), caption, StringComparison.Ordinal)) is { } custom)
        {
            return custom.Id;
        }

        return SettingsSectionIds.CategoryKeyFor(caption);
    }
}
