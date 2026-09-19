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
        //
        // The key is taken from the index-aligned key list rather than by
        // mapping the caption back, because a renamed category no longer
        // matches its built-in caption — and a node whose key never resolves
        // would silently drop out of the tree.
        for (var i = 0; i < Categories.Count; i++)
        {
            var category = Categories[i];
            if (i >= ActiveCategoryKeys.Count || ActiveCategoryKeys[i] is not { Length: > 0 } categoryKey)
            {
                continue;
            }

            var root = new TreeViewNode
            {
                Content = BuildTreeNodeContent(category, isSection: false, categoryKey: categoryKey),
            };

            foreach (var (sectionId, caption, isCustom) in SectionsOf(categoryKey))
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
            IsHidden = isSection && sectionId is not null && _layout.HiddenSections.Contains(sectionId),
            Edit = edit,
        };

        if (!isSection)
        {
            // A category node offers "new submenu" and rename — the 2nd-level
            // folder comes into being from here, exactly like adding a bookmark
            // folder, and the category's own name is editable like any other
            // label the app owns.
            //
            // The menu items are built from plain delegates, so the dialog a
            // handler opens is fire-and-forget: the click has already returned
            // by the time the ContentDialog is shown. Assigning to a discard
            // says that on purpose instead of leaving an unawaited task behind.
            content.CanAddFolder = true;
            edit.BuildMenu(
                canAddFolder: true,
                canDelete: false,
                addFolder: () => _ = CreateSectionInteractiveAsync(categoryKey),
                rename: categoryKey is null ? null : () => _ = RenameCategoryInteractiveAsync(categoryKey, caption),
                delete: null);
        }
        else
        {
            // A folder node carries the same three jobs as a card: rename it,
            // set it aside or bring it back, and — only for a folder the user
            // made — delete it. Hide is a toggle because the tree keeps listing
            // a hidden folder: that is what makes hiding reversible.
            var hidden = sectionId is not null && _layout.HiddenSections.Contains(sectionId);
            edit.BuildMenu(
                canAddFolder: false,
                canDelete: isCustom,
                addFolder: null,
                rename: () => _ = RenameSectionInteractiveAsync(sectionId!, caption),
                delete: () => _ = DeleteSectionInteractiveAsync(sectionId!, caption),
                toggleHide: () =>
                {
                    SetSectionHidden(sectionId!, !hidden);
                    RebuildLocalizedContent();
                },
                isHidden: hidden);
        }

        return content;
    }

    /// <summary>
    /// Folders of a category, in display order, with their display names.
    ///
    /// Keyed by the stable category key rather than by its caption: a category
    /// the user renamed no longer matches its built-in text, and looking a
    /// user-created folder up by caption is what would silently drop it from
    /// the tree.
    /// </summary>
    private List<(string Id, string Caption, bool IsCustom)> SectionsOf(string categoryKey)
    {
        var result = new List<(string, string, bool)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Rows carry the caption the category shows, which is the renamed one
        // when the user renamed it — RenameCategoryRows keeps the two in step.
        var categoryCaption = CategoryCaptionForKey(categoryKey);

        foreach (var option in Settings.Where(o => string.Equals(o.Category, categoryCaption, StringComparison.Ordinal)))
        {
            if (option.SectionId is not { } id || !seen.Add(id))
            {
                continue;
            }

            // A folder the layout holds is one the user made; a built-in folder
            // has no record there. Testing the id instead of the caption is what
            // keeps a *renamed* built-in folder from looking user-made — and
            // therefore from being offered a delete it must never have.
            var isCustom = _layout.FindSection(id) is not null;

            // A column is a pane-only grouping: it must not become a tree node,
            // or "keep it out of the sidebar" would be exactly what it fails to
            // do. It is still filtered out of `seen` above so the second pass
            // does not add it back either.
            if (isCustom && _layout.FindSection(id)?.PaneOnly == true)
            {
                continue;
            }

            var caption = SectionDisplayName(id, option.Section ?? string.Empty);
            result.Add((id, caption, isCustom));
        }

        // A folder the user created but which holds nothing has no row to be
        // discovered from, so it is appended from the stored list.
        foreach (var section in _layout.CustomSections)
        {
            if (section.PaneOnly
                || !string.Equals(section.CategoryKey, categoryKey, StringComparison.Ordinal)
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

        // The caption rides on the control's toolbar so it shares a row with
        // the create buttons; see OptionListControl.HeaderText.
        CustomizeOptionsControl.HeaderText = title;

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

            var sectionCaption = SettingsSections.CaptionFor(_treeSelectedKey) is { } builtIn
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

        var caption = SettingsSections.CaptionFor(sectionId);
        if (caption is null)
        {
            return null;
        }

        foreach (var option in Settings)
        {
            if (string.Equals(option.Section, caption, StringComparison.Ordinal))
            {
                return SettingsSections.CategoryKeyFor(option.Category);
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
}
