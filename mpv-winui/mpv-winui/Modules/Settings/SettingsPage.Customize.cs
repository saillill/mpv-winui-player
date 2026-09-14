using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Linq;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Page-side side of the inline customize mode: the toggle in the action bar,
/// and the bridge between <see cref="Controls.OptionListControl"/>'s edit
/// events and the persisted <see cref="Layout.SettingsLayout"/>.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>Subscribes to the option list's customize-mode notifications.</summary>
    private void InitCustomizeMode()
    {
        OptionsControl.HideRequested += option =>
        {
            SetHidden(option, true);
            RebuildLocalizedContent();
        };

        OptionsControl.ResetRequested += option =>
        {
            ResetRowToDefault(option);
            RebuildLocalizedContent();
        };

        OptionsControl.OrderChanged += keys =>
        {
            StoreOrder(keys);
            // Rebuild so the new order is the one the normal (non-customize)
            // view shows when the user leaves the mode.
            RebuildLocalizedContent();
        };

        // 2nd-level (section / column) editing. Captions are localized, so they
        // are resolved to a stable AppLang property name before being stored.
        OptionsControl.SectionMoveRequested += (caption, delta) =>
        {
            if (SettingsSectionIds.IdFor(caption) is { } id)
            {
                MoveSection(id, delta);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.SectionHideRequested += caption =>
        {
            if (SettingsSectionIds.IdFor(caption) is { } id)
            {
                SetSectionHidden(id, true);
                RebuildLocalizedContent();
            }
        };

        UpdateCustomizeToggleText();
    }

    private void OnCustomizeToggleClick(object sender, RoutedEventArgs e)
    {
        _customizeMode = CustomizeToggle.IsChecked == true;
        OptionsControl.CustomizeMode = _customizeMode;

        // Section cards are a browse affordance; the flat customizable list is
        // what the mode edits, so hide them while it is on.
        if (_customizeMode)
        {
            SectionsHost.Visibility = Visibility.Collapsed;
            BreadcrumbBar.Visibility = Visibility.Collapsed;
        }

        // Rebuild either way: entering the mode is what attaches the pane's own
        // edit menus, and leaving it is what drops them again.
        RebuildLocalizedContent();
        UpdateCustomizeToggleText();
    }

    private void UpdateCustomizeToggleText()
    {
        var lang = AppContext.AppLang;
        CustomizeToggleText.Text = lang.SettingsCustomize;
        ToolTipService.SetToolTip(CustomizeToggle, lang.SettingsCustomizeHint);
    }

    // ===== sidebar (category pane) commands =====

    /// <summary>Attaches the sidebar edit menu to a pane item while customizing.</summary>
    internal void ApplyCategoryEditMenu(NavigationViewItem item, string categoryKey, int index)
    {
        if (!_customizeMode)
        {
            return;
        }

        var lang = AppContext.AppLang;

        var moveUp = new MenuFlyoutItem { Text = lang.CustomizeMoveUp };
        moveUp.Click += (_, _) => { MoveCategory(index, -1); RebuildLocalizedContent(); };

        var moveDown = new MenuFlyoutItem { Text = lang.CustomizeMoveDown };
        moveDown.Click += (_, _) => { MoveCategory(index, 1); RebuildLocalizedContent(); };

        var hide = new MenuFlyoutItem { Text = lang.CustomizeHideCategory };
        hide.Click += (_, _) => { SetCategoryHidden(categoryKey, true); RebuildLocalizedContent(); };

        var flyout = new MenuFlyout();
        flyout.Items.Add(moveUp);
        flyout.Items.Add(moveDown);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(hide);

        // Right-click and the context-menu key both open it; the flyout is
        // rebuilt per rebuild so the captured index stays correct.
        item.ContextFlyout = flyout;
    }

    /// <summary>Restores every sidebar category the customize mode hid.</summary>
    internal void RestoreHiddenCategories()
    {
        _layout.HiddenCategories.Clear();
        SaveLayout();
    }
}
