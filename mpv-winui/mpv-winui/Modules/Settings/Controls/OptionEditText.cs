using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Localized captions for the inline customize-mode row. They live on the row
/// model because WinUI DataTemplates can only bind to the item they are
/// templating, and this keeps the edit card translatable like the rest of the
/// page instead of hard-coding strings in XAML.
/// </summary>
public sealed class OptionEditText
{
    public string DeleteTooltip { get; private set; } = "Hide / restore default";
    public string DeleteAutomationName { get; private set; } = "Delete this entry";
    public string HideCaption { get; private set; } = "Hide from the page (keeps the value)";
    public string ResetCaption { get; private set; } = "Restore default (clears customization and resets this entry)";
    public string MoveUpCaption { get; private set; } = "Move up";
    public string MoveDownCaption { get; private set; } = "Move down";
    public string HideSectionCaption { get; private set; } = "Hide this section";
    public string SectionTooltip { get; private set; } = "Move or hide this section";
    public string DeleteSectionCaption { get; private set; } = "Delete this folder";
    public string MoveToSectionCaption { get; private set; } = "Move into a folder";
    public string RenameCaption { get; private set; } = "Rename…";
    public string EditAdvancedCaption { get; private set; } = "Edit option…";
    public string DragHandleTip { get; private set; } = "Drag to reorder";
    public string EditTip { get; private set; } = "Edit";
    public string EditItemTip { get; private set; } = "Edit the name, description and mpv key";
    public string CopyItemCaption { get; private set; } = "Copy settings";
    public string PasteItemCaption { get; private set; } = "Paste settings";
    public string DuplicateItemCaption { get; private set; } = "Duplicate";

    /// <summary>
    /// Context menu for a customize-mode card. Same entries as the row's own
    /// overflow button, so a right-click and the ⋯ button are one code path.
    /// </summary>
    public MenuFlyout RowMenu { get; private set; } = new();

    /// <summary>
    /// Context menu of a folder-tree node. It is built once per node and then
    /// handed to the tree's item template, because a DataTemplate cannot reach
    /// out to the page to build a flyout of its own.
    /// </summary>
    public MenuFlyout Menu { get; private set; } = new();

    /// <summary>
    /// Routes a context-menu click back to the list control that owns the row.
    /// Set by <see cref="OptionListControl"/> when it hands a row its edit
    /// text; the menu is built here but the actions live on the control.
    /// </summary>
    internal Action<object, string>? RowAction { get; set; }

    /// <summary>Pulls the current language into the captions.</summary>
    public void Refresh()
    {
        var lang = AppContext.AppLang;
        DeleteTooltip = lang.CustomizeDeleteTip;
        DeleteAutomationName = lang.CustomizeDelete;
        HideCaption = lang.CustomizeHide;
        ResetCaption = lang.CustomizeReset;
        MoveUpCaption = lang.CustomizeMoveUp;
        MoveDownCaption = lang.CustomizeMoveDown;
        HideSectionCaption = lang.CustomizeHideSection;
        SectionTooltip = lang.CustomizeSectionTip;
        DeleteSectionCaption = lang.CustomizeDeleteSection;
        MoveToSectionCaption = lang.CustomizeMoveToSection;
        RenameCaption = lang.CustomizeRename;
        EditAdvancedCaption = lang.CustomizeEditAdvanced;
        DragHandleTip = lang.CustomizeDragHandleTip;
        EditTip = lang.CustomizeEditItemTip;
        EditItemTip = lang.CustomizeEditItemTip;
        CopyItemCaption = lang.CustomizeCopyItem;
        PasteItemCaption = lang.CustomizePasteItem;
        DuplicateItemCaption = lang.CustomizeDuplicateItem;
        BuildRowMenu();
    }

    /// <summary>
    /// Builds the card's context menu. Each entry forwards its own name to the
    /// owning list control, so a right-click and the ⋯ button share handlers.
    /// </summary>
    private void BuildRowMenu()
    {
        var menu = new MenuFlyout();

        void Add(string caption, string glyph, string handlerName)
        {
            var item = new MenuFlyoutItem
            {
                Text = caption,
                Icon = new FontIcon { Glyph = glyph },
            };
            item.Click += (s, _) => RowAction?.Invoke(s, handlerName);
            menu.Items.Add(item);
        }

        Add(RenameCaption, "\uE8AC", "RenameRow_Click");
        Add(EditAdvancedCaption, "\uE70F", "EditAdvanced_Click");
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(CopyItemCaption, "\uE8C8", "CopyRow_Click");
        Add(PasteItemCaption, "\uE77F", "PasteRow_Click");
        Add(DuplicateItemCaption, "\uE8C8", "DuplicateRow_Click");
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(HideCaption, "\uE738", "HideRow_Click");
        Add(ResetCaption, "\uE7A7", "ResetRow_Click");

        RowMenu = menu;
    }

    /// <summary>
    /// Builds the node's context menu. A category node offers "new folder";
    /// a folder node offers rename and — only when the user made it — delete.
    /// The handlers are supplied by the page, which owns the layout store.
    /// </summary>
    public void BuildMenu(
        bool isSection,
        bool canAddFolder,
        bool canDelete,
        Action? addFolder,
        Action? rename,
        Action? delete)
    {
        // MenuFlyoutItem derives from MenuFlyoutItemBase, and so does the
        // separator, so the list is typed as the base to hold both.
        var items = new List<MenuFlyoutItemBase>();

        if (canAddFolder && addFolder is not null)
        {
            var item = new MenuFlyoutItem
            {
                Text = AppContext.AppLang.CustomizeNewSection,
                Icon = new FontIcon { Glyph = "\uE8F4" },
            };
            item.Click += (_, _) => addFolder();
            items.Add(item);
        }

        if (isSection && rename is not null)
        {
            var item = new MenuFlyoutItem
            {
                Text = RenameCaption,
                Icon = new FontIcon { Glyph = "\uE8AC" },
            };
            item.Click += (_, _) => rename();
            items.Add(item);
        }

        // Delete is destructive and only ever applies to a folder the user
        // created, so it sits behind a separator and is left out entirely for
        // every built-in node.
        if (canDelete && delete is not null)
        {
            if (items.Count > 0)
            {
                items.Add(new MenuFlyoutSeparator());
            }

            var item = new MenuFlyoutItem
            {
                Text = DeleteSectionCaption,
                Icon = new FontIcon { Glyph = "\uE74D" },
            };
            item.Click += (_, _) => delete();
            items.Add(item);
        }

        Menu = new MenuFlyout();
        foreach (var item in items)
        {
            Menu.Items.Add(item);
        }
    }
}
