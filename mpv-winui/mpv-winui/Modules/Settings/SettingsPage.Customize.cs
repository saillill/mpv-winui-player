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
        OptionsControl.RawEdited += (option, rawKey, rawValue) =>
        {
            // A hand-added row owns its mpv key outright; a built-in row falls
            // back to the key derived from its own mapping, so the user can
            // retype just the value.
            var key = string.IsNullOrWhiteSpace(rawKey) ? SuggestMpvKey(option) : rawKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            StoreMpvOverride(option, key, rawValue);
        };

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
        AddOptionButton.Visibility = _customizeMode ? Visibility.Visible : Visibility.Collapsed;

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
        AddOptionButtonText.Text = lang.CustomizeAdd;
        ToolTipService.SetToolTip(AddOptionButton, lang.CustomizeAddHint);
    }

    // ===== add a hand-written option =====

    /// <summary>
    /// Collects a new option from the user and appends it to the layout. The
    /// control kind is chosen explicitly (text box / dropdown / toggle) because
    /// a raw mpv option gives no hint about how it should be presented.
    /// </summary>
    private async void OnAddOptionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var lang = AppContext.AppLang;

            var kindBox = new ComboBox
            {
                Header = lang.CustomizeAddKind,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    lang.CustomizeKindText,
                    lang.CustomizeKindChoice,
                    lang.CustomizeKindBoolean,
                },
                SelectedIndex = 0,
            };

            var categoryBox = new ComboBox
            {
                Header = lang.CustomizeAddCategory,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = Categories.ToList(),
                SelectedIndex = Math.Max(0, Categories.IndexOf(CurrentCategory ?? string.Empty)),
            };

            var labelBox = new TextBox { Header = lang.CustomizeFieldName, PlaceholderText = lang.CustomizeAddLabelHint };
            var keyBox = new TextBox { Header = lang.CustomizeFieldRawKey, PlaceholderText = lang.CustomizeFieldRawKeyHint };
            var descriptionBox = new TextBox { Header = lang.CustomizeFieldDescription, PlaceholderText = lang.CustomizeFieldDescriptionHint };
            var choicesBox = new TextBox
            {
                Header = lang.CustomizeAddChoices,
                PlaceholderText = lang.CustomizeAddChoicesHint,
                // Only meaningful for the dropdown kind; the dialog keeps it
                // visible but the value is ignored for the other two.
                Visibility = Visibility.Collapsed,
            };

            kindBox.SelectionChanged += (_, _) =>
            {
                choicesBox.Visibility = kindBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            };

            var panel = new StackPanel { Spacing = 10, Width = 380 };
            panel.Children.Add(kindBox);
            panel.Children.Add(categoryBox);
            panel.Children.Add(labelBox);
            panel.Children.Add(keyBox);
            panel.Children.Add(descriptionBox);
            panel.Children.Add(choicesBox);

            var dialog = new ContentDialog
            {
                Title = lang.CustomizeAdd,
                Content = panel,
                XamlRoot = XamlRoot,
                PrimaryButtonText = lang.Add,
                CloseButtonText = lang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                return;
            }

            var kind = kindBox.SelectedIndex switch
            {
                1 => CustomOptionKinds.Choice,
                2 => CustomOptionKinds.Boolean,
                _ => CustomOptionKinds.Text,
            };

            var categoryIndex = Math.Max(0, categoryBox.SelectedIndex);
            var categoryKey = categoryIndex < ActiveCategoryKeys.Count
                ? ActiveCategoryKeys[categoryIndex]
                : "program";

            AddCustomOption(
                categoryKey,
                labelBox.Text,
                descriptionBox.Text,
                keyBox.Text,
                kind,
                choicesBox.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            RebuildLocalizedContent();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "add option failed");
        }
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
