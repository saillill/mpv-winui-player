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
    /// <summary>
    /// Name and tooltip of a row's trailing overflow button. The button draws
    /// the "More" glyph and opens the rename / hide-or-restore menu, so both
    /// strings must describe that menu: naming it after the delete glyph it no
    /// longer shows makes a screen reader announce a destructive action the
    /// button never performs.
    /// </summary>
    public string MoreAutomationName { get; private set; } = "More actions";

    public string MoreTip { get; private set; } = "Rename, hide or restore this row";
    public string HideCaption { get; private set; } = "Hide from the page (keeps the value)";
    public string UnhideCaption { get; private set; } = "Show this row again";
    public string MoveUpCaption { get; private set; } = "Move up";
    public string MoveDownCaption { get; private set; } = "Move down";
    public string HideSectionCaption { get; private set; } = "Hide this section";
    public string SectionMoreTip { get; private set; } = "Rename, move or hide this section";
    public string DeleteSectionCaption { get; private set; } = "Delete this folder";
    public string MoveToSectionCaption { get; private set; } = "Move into a folder";
    public string RenameCaption { get; private set; } = "Rename…";
    public string DragHandleTip { get; private set; } = "Drag to reorder";
    public string EditTip { get; private set; } = "Rename this entry";

    /// <summary>
    /// Caption of the row's hide entry. It flips to "show again" once the row is
    /// hidden, so one menu slot covers both directions instead of leaving a
    /// hidden row with no way back.
    /// </summary>
    public string HideToggleCaption { get; private set; } = "Hide from the page (keeps the value)";

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
    /// <param name="hiddenByUser">
    /// True when the row is hidden by the user, which turns the hide entry into
    /// a restore entry. Only the stored (user) hide counts: a row the app hides
    /// because it is ineffective is not the user's doing and must not advertise
    /// itself as restorable.
    /// </param>
    public void Refresh(bool hiddenByUser = false)
    {
        var lang = AppContext.AppLang;
        MoreAutomationName = lang.CustomizeMore;
        MoreTip = lang.CustomizeMoreTip;
        HideCaption = lang.CustomizeHide;
        UnhideCaption = lang.CustomizeUnhide;
        HideToggleCaption = hiddenByUser ? UnhideCaption : HideCaption;
        MoveUpCaption = lang.CustomizeMoveUp;
        MoveDownCaption = lang.CustomizeMoveDown;
        HideSectionCaption = lang.CustomizeHideSection;
        SectionMoreTip = lang.CustomizeSectionMoreTip;
        DeleteSectionCaption = lang.CustomizeDeleteSection;
        MoveToSectionCaption = lang.CustomizeMoveToSection;
        RenameCaption = lang.CustomizeRename;
        DragHandleTip = lang.CustomizeDragHandleTip;
        EditTip = lang.CustomizeRename;
        BuildRowMenu();
    }

    /// <summary>
    /// Builds the card's context menu. Each entry forwards its own name to the
    /// owning list control, so a right-click and the ⋯ button share handlers.
    ///
    /// The entries are deliberately text-only: the paired ⋯ menu is text-only
    /// too, and two menus for one row that do not look alike read as two
    /// different menus.
    ///
    /// Only re-labelling and hiding are offered. Retyping a row's mpv key, and
    /// the copy/paste/duplicate trio that also produced rows, are deliberately
    /// absent: the customize surface arranges the rows the app defines, it does
    /// not author new ones.
    /// </summary>
    private void BuildRowMenu()
    {
        var menu = new MenuFlyout();

        void Add(string caption, string handlerName)
        {
            var item = new MenuFlyoutItem { Text = caption };
            item.Click += (s, _) => RowAction?.Invoke(s, handlerName);
            menu.Items.Add(item);
        }

        Add(RenameCaption, "RenameRow_Click");
        menu.Items.Add(new MenuFlyoutSeparator());

        // One entry for both directions: which way it goes is decided by the
        // control from the row's current state, so the caption and the action
        // can never disagree.
        Add(HideToggleCaption, "ToggleRowHidden_Click");

        RowMenu = menu;
    }

    /// <summary>
    /// Builds the node's context menu. A category node offers "new submenu" and
    /// rename; a folder node offers rename, a hide/show-again toggle, and —
    /// only when the user made it — delete. The handlers are supplied by the
    /// page, which owns the layout store.
    ///
    /// Text-only, like the card menus: one customize surface should not mix
    /// icon-carrying and plain menus depending on what was right-clicked.
    /// </summary>
    public void BuildMenu(
        bool canAddFolder,
        bool canDelete,
        Action? addFolder,
        Action? rename,
        Action? delete,
        Action? toggleHide = null,
        bool isHidden = false)
    {
        // MenuFlyoutItem derives from MenuFlyoutItemBase, and so does the
        // separator, so the list is typed as the base to hold both.
        var items = new List<MenuFlyoutItemBase>();

        if (canAddFolder && addFolder is not null)
        {
            var item = new MenuFlyoutItem { Text = AppContext.AppLang.CustomizeNewSubmenu };
            item.Click += (_, _) => addFolder();
            items.Add(item);
        }

        // Rename applies to either level: a category is as renamable as a folder.
        if (rename is not null)
        {
            var item = new MenuFlyoutItem { Text = RenameCaption };
            item.Click += (_, _) => rename();
            items.Add(item);
        }

        // Hiding is reversible and therefore one entry for both directions: a
        // hidden folder is on screen here precisely so it can be named and
        // brought back, and offering to hide it again would be a dead end.
        if (toggleHide is not null)
        {
            var item = new MenuFlyoutItem
            {
                Text = isHidden ? AppContext.AppLang.CustomizeUnhideSection : HideSectionCaption,
            };
            item.Click += (_, _) => toggleHide();
            items.Add(item);
        }

        // Delete is destructive and only ever applies to something the user
        // created, so it sits behind a separator and is left out entirely for
        // every built-in node.
        if (canDelete && delete is not null)
        {
            if (items.Count > 0)
            {
                items.Add(new MenuFlyoutSeparator());
            }

            var item = new MenuFlyoutItem { Text = DeleteSectionCaption };
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
