using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Page-side side of the inline customize mode: the toggle in the action bar,
/// and the bridge between <see cref="Controls.OptionListControl"/>'s edit
/// events and the persisted <see cref="Layout.SettingsLayout"/>.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>
    /// Subscribes to the option list's customize-mode notifications.
    ///
    /// Two lists can render the editing affordances: the browsing page's list
    /// and the customize pane's. Only the pane is on screen while customizing
    /// (the browse host is collapsed), so wiring the browsing list alone is
    /// what made "add option" and "edit" behave like dead buttons — the click
    /// was raised on a control nobody had subscribed to. Both are wired here so
    /// a mode switch can never leave a visible list without a listener.
    /// </summary>
    private void InitCustomizeMode()
    {
        WireCustomizeEvents(OptionsControl);
        WireCustomizeEvents(CustomizeOptionsControl);

        UpdateCustomizeToggleText();
    }

    /// <summary>
    /// Binds one option list's edit events to this page's handlers.
    ///
    /// The customize surface only rearranges and re-labels the rows the app
    /// defines — hence hide, rename and reorder (plus the structural folder
    /// operations) and nothing that would author or retype an mpv key.
    /// </summary>
    private void WireCustomizeEvents(Controls.OptionListControl list)
    {
        list.HideRequested += option =>
        {
            SetHidden(option, true);
            RebuildLocalizedContent();
        };

        // The card menu carries one entry that flips between "hide" and "show
        // again", so the page has to handle the way back as its own intent.
        list.UnhideRequested += option =>
        {
            SetHidden(option, false);
            RebuildLocalizedContent();
        };

        list.OrderChanged += keys =>
        {
            // Store only. A drag already left the ListView in the new order, so
            // rebuilding here would tear the list down and repaint it right
            // under the user's cursor — several times per drag, because a
            // reorder can commit more than once. The stored order is what the
            // next rebuild (or a mode exit) reads back.
            StoreOrder(keys);
        };

        // 2nd-level (folder / column) editing. The pane reports the folder's
        // stable id rather than its caption, because the caption is exactly what
        // a rename changes: a command that had to resolve the caption back into
        // an id would quietly stop doing anything once the user renamed the
        // folder it was pointed at.
        list.SectionMoveRequested += (sectionId, delta) =>
        {
            MoveSection(sectionId, delta);
            RebuildLocalizedContent();
        };

        list.SectionHiddenChanged += (sectionId, hidden) =>
        {
            SetSectionHidden(sectionId, hidden);
            RebuildLocalizedContent();
        };

        list.SectionDeleteRequested += sectionId => _ = DeleteSectionInteractiveAsync(sectionId, SectionNameFor(sectionId));

        list.MoveRowRequested += (optionKey, sectionId) =>
        {
            MoveRowToSection(optionKey, sectionId);
            RequestDeferredRebuild();
        };

        // The right pane of the two-pane view: a card dropped into it joins the
        // folder that pane is showing.
        list.PaneJoinRequested += (optionKey, sectionId) =>
        {
            MoveRowToSection(optionKey, sectionId);
            RequestDeferredRebuild();
        };

        list.CreateSectionRequested += async () => await CreateSectionInteractiveAsync();

        // A column groups cards here and nowhere else, so it is created as a
        // pane-only section: no sidebar node grows out of it.
        list.CreateColumnRequested += async () => await CreatePaneColumnInteractiveAsync();

        list.RenameRowRequested += async (optionKey, current) => await RenameRowInteractiveAsync(optionKey, current);

        list.RenameSectionRequested += async (sectionId, current) => await RenameSectionInteractiveAsync(sectionId, current);
    }

    /// <summary>
    /// Coalesces rebuild requests that arrive while a drag is still settling.
    ///
    /// A pointer-drop can raise more than one notification, and each rebuild
    /// re-runs BuildSettings and re-sets ItemsSource. Doing that synchronously
    /// per notification is what made the list visibly flash mid-drag, so the
    /// work is queued once and merged: many requests collapse into a single
    /// rebuild on the next dispatcher turn.
    /// </summary>
    private bool _rebuildQueued;

    private void RequestDeferredRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _rebuildQueued = false;
            RebuildLocalizedContent();
        });
    }

    /// <summary>
    /// Resolves a folder id to the name it currently shows, for the dialogs that
    /// have to name it. Covers both a built-in folder (its own caption, or the
    /// user's override) and one the user created (which has no AppLang caption).
    /// </summary>
    private string SectionNameFor(string sectionId) =>
        SectionDisplayName(sectionId, SettingsSections.CaptionFor(sectionId) ?? string.Empty);

    /// <summary>
    /// Deletes a folder the user created, after confirming.
    ///
    /// Deleting a folder only removes the grouping: the cards inside it return
    /// to the un-filed run rather than being deleted, so this is safe and the
    /// confirmation says so instead of implying the rows are lost.
    /// </summary>
    private async Task DeleteSectionInteractiveAsync(string sectionId, string caption)
    {
        var lang = AppContext.AppLang;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeDeleteSection,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap },
                    // A warning, not a question: the consequence is what the
                    // user needs to know before pressing the destructive button.
                    new InfoBar
                    {
                        IsClosable = false,
                        IsOpen = true,
                        Severity = InfoBarSeverity.Warning,
                        Message = lang.CustomizeDeleteConfirmBody,
                    },
                },
            },
            PrimaryButtonText = lang.Confirm,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        PushCustomizeEdit();
        DeleteSection(sectionId);

        // The deleted node may have been the selection, so drop back to the
        // page's own category rather than leaving a dangling key behind.
        if (string.Equals(_treeSelectedKey, sectionId, StringComparison.Ordinal))
        {
            _treeSelectedKey = null;
            _treeSelectedIsSection = false;
        }

        CommitCustomizeEdit();
        RequestDeferredRebuild();
    }

    /// <summary>Asks for a name and creates a 2nd-level folder in this category.</summary>
    private async Task CreateSectionInteractiveAsync() => await CreateSectionInteractiveAsync(null);

    /// <summary>Asks for a name and creates a pane-only grouping column.</summary>
    private async Task CreatePaneColumnInteractiveAsync() =>
        await CreateSectionInteractiveAsync(CurrentCategoryKey, paneOnly: true);

    /// <summary>
    /// Asks for a name and creates a 2nd-level grouping.
    ///
    /// The two-pane view knows the category from the tree node that was
    /// right-clicked rather than from the page's current category, so the
    /// caller may pass it explicitly; browsing mode passes null and falls back
    /// to whatever category the page is showing.
    ///
    /// <paramref name="paneOnly"/> picks between the toolbar's two gestures:
    /// a folder (also a sidebar node) and a column (pane grouping only).
    /// </summary>
    private async Task CreateSectionInteractiveAsync(string? categoryKey, bool paneOnly = false)
    {
        categoryKey ??= CurrentCategoryKey;
        if (categoryKey is null)
        {
            return;
        }

        var lang = AppContext.AppLang;
        var title = paneOnly ? lang.CustomizeNewColumn : lang.CustomizeNewSubmenu;
        var input = new TextBox
        {
            PlaceholderText = title,
            Header = title,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    input,
                    new TextBlock
                    {
                        Text = paneOnly ? lang.CustomizeNewColumnHint : lang.CustomizeNewSubmenuHint,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
            PrimaryButtonText = lang.Save,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        // Wrapped in the draft boundary like every other edit: without it the
        // folder appeared but could not be undone, because undo only ever walks
        // the recorded steps.
        PushCustomizeEdit();
        CreateSection(categoryKey, input.Text, paneOnly);
        CommitCustomizeEdit();
        RebuildLocalizedContent();
    }

    /// <summary>
    /// Renames a settings row.
    ///
    /// This is where the localization problem gets solved: the built-in caption
    /// belongs to the language files and would be overwritten by the next
    /// language switch, so the rename is stored as an override and resolved on
    /// top of the built-in text at render time. The dialog therefore offers a
    /// scope — "this language only" (the default) or "every language" — and
    /// leaving the box empty clears the override instead of blanking the row.
    /// </summary>
    private async Task RenameRowInteractiveAsync(string optionKey, string currentLabel)
    {
        var lang = AppContext.AppLang;
        var option = Settings.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal));
        if (option is null)
        {
            return;
        }

        // Pre-fill with whatever the row shows now, so the user edits rather
        // than retypes. A row with no override yet starts from its built-in
        // caption, which is exactly what they see on screen.
        var entry = _layout.Entries.TryGetValue(optionKey, out var existing) ? existing : null;
        var currentDescription = LabelOverrides.ResolveDescription(entry, ActiveLanguageKey)
            ?? option.Description
            ?? string.Empty;

        var labelBox = new TextBox
        {
            Text = currentLabel,
            PlaceholderText = lang.CustomizeRenamePlaceholder,
            Header = lang.CustomizeFieldName,
            MaxLength = MaxLabelLength,
        };

        var descriptionBox = new TextBox
        {
            Text = currentDescription,
            PlaceholderText = lang.CustomizeRenameDescriptionPlaceholder,
            Header = lang.CustomizeFieldDescription,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = MaxDescriptionLength,
        };

        var scopeToggle = new ToggleSwitch
        {
            IsOn = string.Equals(existing?.Scope, RenameScopes.All, StringComparison.Ordinal),
            OnContent = lang.CustomizeScopeAll,
            OffContent = lang.CustomizeScopeCurrent,
            Header = lang.CustomizeScope,
        };

        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeRename,
            Content = new StackPanel
            {
                Spacing = 12,
                MinWidth = 420,
                Children =
                {
                    labelBox,
                    descriptionBox,
                    scopeToggle,
                    new TextBlock
                    {
                        Text = lang.CustomizeRenameHint,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        TextWrapping = TextWrapping.Wrap,
                    },
                    errorText,
                },
            },
            PrimaryButtonText = lang.Save,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        // Validate before closing: an over-long caption or a blank label must
        // not silently produce a row that cannot be found again.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var label = labelBox.Text.Trim();
            var description = descriptionBox.Text.Trim();

            if (label.Length > MaxLabelLength)
            {
                errorText.Text = string.Format(lang.CustomizeErrorLabelTooLong, MaxLabelLength);
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (description.Length > MaxDescriptionLength)
            {
                errorText.Text = string.Format(lang.CustomizeErrorDescriptionTooLong, MaxDescriptionLength);
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ApplyRowRename(optionKey, labelBox.Text.Trim(), descriptionBox.Text.Trim(), scopeToggle.IsOn);
        RequestDeferredRebuild();
    }

    /// <summary>Caption/description length caps, generous but not unbounded.</summary>
    private const int MaxLabelLength = 120;
    private const int MaxDescriptionLength = 600;

    /// <summary>
    /// Stores a row rename. An empty label clears the override (falling back to
    /// the built-in caption) rather than blanking the row, which would leave an
    /// unidentifiable entry behind.
    /// </summary>
    private void ApplyRowRename(string optionKey, string label, string description, bool allLanguages)
    {
        var entry = _layout.EntryFor(optionKey);

        entry.Label = string.IsNullOrEmpty(label) ? null : label;
        entry.Description = string.IsNullOrEmpty(description) ? null : description;

        if (entry.Label is null && entry.Description is null)
        {
            // Nothing overridden: drop scope too so the file stays clean.
            entry.Scope = null;
            entry.Language = null;
        }
        else
        {
            entry.Scope = allLanguages ? RenameScopes.All : RenameScopes.Current;
            entry.Language = allLanguages ? null : ActiveLanguageKey;
        }

        _layout.Prune(optionKey);
        SaveLayout();
    }

    /// <summary>
    /// Renames a folder. The folder's id — which every row points at — is left
    /// alone and a display name is stored instead, so renaming never has to
    /// rewrite the rows that reference it.
    /// </summary>
    private async Task RenameSectionInteractiveAsync(string sectionId, string currentCaption)
    {
        var lang = AppContext.AppLang;

        // Built-in folders keep their localized caption underneath, so they get
        // the same scope choice as a row. A folder the user created has no
        // built-in text, so its name is pure content and always applies.
        var builtIn = SettingsSections.CaptionFor(sectionId) is not null;
        var custom = _layout.FindSection(sectionId);

        var labelBox = new TextBox
        {
            Text = currentCaption,
            PlaceholderText = lang.CustomizeRenamePlaceholder,
            Header = lang.CustomizeFieldName,
            MaxLength = MaxLabelLength,
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            Children =
            {
                labelBox,
                new TextBlock
                {
                    Text = builtIn ? lang.CustomizeRenameHint : lang.CustomizeRenameFolderHint,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        ToggleSwitch? scopeToggle = null;
        if (builtIn)
        {
            scopeToggle = new ToggleSwitch
            {
                IsOn = string.Equals(
                    _layout.Entries.TryGetValue(SectionEntryKey(sectionId), out var e) ? e.Scope : null,
                    RenameScopes.All,
                    StringComparison.Ordinal),
                OnContent = lang.CustomizeScopeAll,
                OffContent = lang.CustomizeScopeCurrent,
                Header = lang.CustomizeScope,
            };
            panel.Children.Insert(1, scopeToggle);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeRename,
            Content = panel,
            PrimaryButtonText = lang.Save,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var name = labelBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (custom is not null)
        {
            custom.DisplayName = string.Equals(name, custom.Name, StringComparison.Ordinal) ? null : name;
        }
        else
        {
            // Built-in folder: reuse the row-style override entry so one
            // mechanism covers rows and folders alike.
            var entry = _layout.EntryFor(SectionEntryKey(sectionId));
            entry.Label = string.Equals(name, currentCaption, StringComparison.Ordinal) ? null : name;
            entry.Scope = scopeToggle?.IsOn == true ? RenameScopes.All : RenameScopes.Current;
            entry.Language = scopeToggle?.IsOn == true ? null : ActiveLanguageKey;
            _layout.Prune(SectionEntryKey(sectionId));
        }

        SaveLayout();
        RequestDeferredRebuild();
    }

    /// <summary>
    /// Key a built-in folder's rename override is stored under. Prefixed so it
    /// cannot collide with a real option key.
    /// </summary>
    private static string SectionEntryKey(string sectionId) => "section-label:" + sectionId;

    /// <summary>
    /// Renames a sidebar category, reached from the pane's own menu or from the
    /// matching node in the bookmark tree.
    ///
    /// A built-in category's caption lives in the language files, so — exactly
    /// like a row or a folder — the rename is stored as an override on top of it
    /// and offers the same "this language / all languages" scope. A category the
    /// user created has no built-in text to fall back to, so its name is content
    /// and applies everywhere.
    ///
    /// The stable key is never touched: the sidebar order, the folder order and
    /// the hidden flags are all stored against it, which is the whole reason the
    /// key and the caption are separate fields.
    /// </summary>
    private async Task RenameCategoryInteractiveAsync(string categoryKey, string currentCaption)
    {
        var lang = AppContext.AppLang;

        var custom = _layout.FindCategory(categoryKey);
        var builtInCaption = SettingsSections.CategoryCaptionFor(categoryKey);
        if (custom is null && builtInCaption is null)
        {
            return;
        }

        var labelBox = new TextBox
        {
            Text = currentCaption,
            PlaceholderText = lang.CustomizeRenamePlaceholder,
            Header = lang.CustomizeFieldName,
            MaxLength = MaxLabelLength,
        };

        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            Children =
            {
                labelBox,
                new TextBlock
                {
                    Text = builtInCaption is null ? lang.CustomizeRenameFolderHint : lang.CustomizeRenameHint,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    TextWrapping = TextWrapping.Wrap,
                },
                errorText,
            },
        };

        ToggleSwitch? scopeToggle = null;
        if (builtInCaption is not null)
        {
            scopeToggle = new ToggleSwitch
            {
                IsOn = string.Equals(
                    _layout.Entries.TryGetValue(CategoryEntryKey(categoryKey), out var e) ? e.Scope : null,
                    RenameScopes.All,
                    StringComparison.Ordinal),
                OnContent = lang.CustomizeScopeAll,
                OffContent = lang.CustomizeScopeCurrent,
                Header = lang.CustomizeScope,
            };
            panel.Children.Insert(1, scopeToggle);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeRename,
            Content = panel,
            PrimaryButtonText = lang.Save,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        // Two categories wearing one name cannot be told apart in the sidebar,
        // and every lookup that goes through a caption would resolve to whichever
        // came first — so the clash is refused rather than stored.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var typed = labelBox.Text.Trim();

            if (typed.Length > MaxLabelLength)
            {
                errorText.Text = string.Format(lang.CustomizeErrorLabelTooLong, MaxLabelLength);
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            // The category's own current name is not a clash: keeping it is a
            // legitimate "no change", so every entry showing it is skipped.
            if (typed.Length > 0
                && Categories
                    .Where(shown => !string.Equals(shown, currentCaption, StringComparison.Ordinal))
                    .Any(shown => string.Equals(shown, typed, StringComparison.Ordinal)))
            {
                errorText.Text = string.Format(lang.CustomizeErrorNameTaken, typed);
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var name = labelBox.Text.Trim();

        PushCustomizeEdit();
        if (custom is not null)
        {
            // The user's own text, so it applies in every language; DisplayName
            // carries it without disturbing the id or the original name.
            custom.DisplayName = string.Equals(name, custom.Name, StringComparison.Ordinal) ? null : name;
        }
        else
        {
            var entry = _layout.EntryFor(CategoryEntryKey(categoryKey));

            // Empty, or typed back to the built-in caption, means "no override":
            // clearing it is how the user gets the app's own name back, which is
            // what the placeholder promises.
            entry.Label = name.Length == 0 || string.Equals(name, builtInCaption, StringComparison.Ordinal)
                ? null
                : name;
            entry.Scope = scopeToggle?.IsOn == true ? RenameScopes.All : RenameScopes.Current;
            entry.Language = scopeToggle?.IsOn == true ? null : ActiveLanguageKey;
            _layout.Prune(CategoryEntryKey(categoryKey));
        }

        SaveLayout();
        CommitCustomizeEdit();
        RebuildLocalizedContent();
    }

    /// <summary>Current display name of a folder, honouring any rename.</summary>
    private string SectionDisplayName(string sectionId, string fallbackCaption)
    {
        if (LabelOverrides.ResolveLabel(
                _layout.Entries.TryGetValue(SectionEntryKey(sectionId), out var e) ? e : null,
                ActiveLanguageKey) is { } renamed && !string.IsNullOrEmpty(renamed))
        {
            return renamed;
        }

        return _layout.FindSection(sectionId)?.DisplayFor(ActiveLanguageKey) ?? fallbackCaption;
    }

    private void OnCustomizeToggleClick(object sender, RoutedEventArgs e)
    {
        var entering = CustomizeToggle.IsChecked == true;
        _customizeMode = entering;
        OptionsControl.CustomizeMode = entering;
        CustomizeOptionsControl.CustomizeMode = entering;

        // Customize mode swaps the flat card list for the bookmark-manager
        // surface: a folder tree on the left, the selected node's cards on the
        // right. Leaving it swaps back, which is why both hosts are toggled
        // here rather than left to the option list.
        CustomizeRoot.Visibility = entering ? Visibility.Visible : Visibility.Collapsed;
        BrowseHost.Visibility = entering ? Visibility.Collapsed : Visibility.Visible;

        // The tree lists the same categories the NavigationView pane does, so
        // while it is up the pane is redundant: two identical sidebars read as
        // a bug. The pane returns on exit, so the page's own navigation is
        // untouched.
        CategoryNav.IsPaneVisible = !entering;

        if (entering)
        {
            BeginCustomizeSession();
            ApplyCustomizeChrome();

            // A fresh session starts expanded: a collapsed rail with no cards
            // on screen would look empty.
            SetSidebarCollapsed(false);
        }
        else
        {
            EndCustomizeSession();
        }

        // Rebuild either way: entering the mode is what attaches the pane's own
        // edit menus and fills the tree, and leaving it is what drops them.
        RebuildLocalizedContent();
        UpdateCustomizeToggleText();
    }


    /// <summary>Fills the customize mode's own captions from the current language.</summary>
    private void ApplyCustomizeChrome()
    {
        var lang = AppContext.AppLang;
        CustomizeScopeBar.Title = lang.CustomizeScopeBannerTitle;
        CustomizeScopeBar.Message = $"{lang.CustomizeReadOnlyNotice} {lang.CustomizeScopeLanguageNote}";
        AddCategoryButtonText.Text = lang.CustomizeAddTopLevel;
        ToolTipService.SetToolTip(AddCategoryButton, lang.CustomizeAddTopLevelHint);
        TreeHintText.Text = lang.CustomizeAddTopLevelHint;
        ToolTipService.SetToolTip(CollapseSidebarButton, lang.CustomizeCollapseSidebarTip);
        ToolTipService.SetToolTip(ExpandSidebarButton, lang.CustomizeExpandSidebarTip);

        // The "add" button is a glyph plus a TextBlock, and the two sidebar
        // arrows and the toolbar switch are icon-only: none of them exposes a
        // UIA name on its own, so the tooltip text doubles as the accessible
        // name or a screen reader announces them all as "button".
        AutomationProperties.SetName(AddCategoryButton, lang.CustomizeAddTopLevel);
        AutomationProperties.SetName(CollapseSidebarButton, lang.CustomizeCollapseSidebarTip);
        AutomationProperties.SetName(ExpandSidebarButton, lang.CustomizeExpandSidebarTip);

        // The footer is a different set of jobs while customizing, so the
        // browsing buttons step aside rather than sitting next to controls that
        // mean something else. Without this swap the customize buttons stay
        // collapsed and the footer still offers only the two reset actions.
        BrowseActions.Visibility = Visibility.Collapsed;
        CustomizeActions.Visibility = Visibility.Visible;

        // Undo and redo are icon-only buttons: their whole meaning fits in the
        // two curved arrows that are the universal gesture for them, and the
        // footer already carries two text buttons. Setting Content here is what
        // used to replace the XAML's FontIcon with the word, so only the
        // tooltips are refreshed now.
        CustomizeApplyButtonText.Text = lang.ApplyAndExit;
        CustomizeExitButton.Content = lang.CustomizeExit;

        ToolTipService.SetToolTip(CustomizeUndoButton, lang.CustomizeUndoTip);
        ToolTipService.SetToolTip(CustomizeRedoButton, lang.CustomizeRedoTip);
        ToolTipService.SetToolTip(CustomizeApplyButton, lang.CustomizeApplyTip);
        ToolTipService.SetToolTip(CustomizeExitButton, lang.CustomizeExitTip);
    }

    /// <summary>Restores the browsing footer after the customize mode closes.</summary>
    private void RestoreBrowseChrome()
    {
        BrowseActions.Visibility = Visibility.Visible;
        CustomizeActions.Visibility = Visibility.Collapsed;

        // Leaving reports its outcome once, then the line goes back to being
        // the browse footer's status area.
        if (!_draft.IsDirty)
        {
            ShowCustomizeStatus(CustomizeStatus.None);
        }
    }

    private void UpdateCustomizeToggleText()
    {
        var lang = AppContext.AppLang;
        CustomizeToggleText.Text = lang.SettingsCustomize;
        ToolTipService.SetToolTip(CustomizeToggle, lang.SettingsCustomizeHint);
        AutomationProperties.SetName(CustomizeToggle, lang.SettingsCustomize);
    }

    // ===== sidebar (category pane) commands =====

    /// <summary>
    /// Attaches the browse-mode sidebar's edit menu to a pane item.
    ///
    /// NOTE: this is currently unreachable. The menu is only attached while
    /// customizing, and customizing is exactly when the pane is hidden in favour
    /// of the folder tree (see <c>CategoryNav.IsPaneVisible</c>), so nothing on
    /// screen can open it. The tree's category node — which offers rename and
    /// "new submenu" — is what the user actually reaches, and a category is
    /// hidden from the tree's own menu.
    ///
    /// Kept rather than deleted because the same two operations are the pane's
    /// own reorder and hide, and reviving the pane (or the drag below) is a
    /// decision about the customize layout, not a cleanup. Deleting it is safe
    /// today if that decision goes the other way; the folder tree does not use
    /// any of it.
    /// </summary>
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

    // ===== sidebar drag reorder =====

    /// <summary>Index in the pane of the item currently being dragged.</summary>
    private int _draggingCategoryIndex = -1;

    /// <summary>
    /// Attaches hand-written drag-to-reorder to a pane entry. NavigationView
    /// has no built-in item drag, so the gesture is done with the item's own
    /// pointer events: press captures the index, release over another entry
    /// commits the move. Only active while customizing, where the pane is a
    /// flat list of categories.
    /// </summary>
    private void AttachCategoryDrag(NavigationViewItem item, int index)
    {
        if (!_customizeMode)
        {
            return;
        }

        var start = new Windows.Foundation.Point();
        var captured = false;

        item.PointerPressed += (_, e) =>
        {
            start = e.GetCurrentPoint(item).Position;
            captured = true;
        };

        // PointerReleased only fires on the item the press started on, so the
        // drop target is found from the pointer's own position instead.
        item.PointerReleased += (_, e) =>
        {
            if (!captured)
            {
                return;
            }
            captured = false;

            var end = e.GetCurrentPoint(item).Position;
            if (Math.Abs(end.Y - start.Y) < 12 && Math.Abs(end.X - start.X) < 12)
            {
                // A click, not a drag: let the pane's own selection run.
                return;
            }

            var target = CategoryIndexAtPoint(e.GetCurrentPoint(CategoryNav).Position);
            if (target >= 0)
            {
                _draggingCategoryIndex = index;
                MoveCategoryTo(index, target);
                RequestDeferredRebuild();
            }
        };
    }

    /// <summary>Index of the pane entry under a point in the pane's space.</summary>
    private int CategoryIndexAtPoint(Windows.Foundation.Point point)
    {
        for (var i = 0; i < CategoryNav.MenuItems.Count; i++)
        {
            if (CategoryNav.MenuItems[i] is not NavigationViewItem item)
            {
                continue;
            }

            var origin = item.TransformToVisual(CategoryNav)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            var bounds = new Windows.Foundation.Rect(origin, item.RenderSize);
            if (bounds.Contains(point))
            {
                return i;
            }
        }
        return -1;
    }
}
