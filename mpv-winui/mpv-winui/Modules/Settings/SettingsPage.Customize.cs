using Microsoft.UI.Xaml;
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
            // Store only. A drag already left the ListView in the new order, so
            // rebuilding here would tear the list down and repaint it right
            // under the user's cursor — several times per drag, because a
            // reorder can commit more than once. The stored order is what the
            // next rebuild (or a mode exit) reads back.
            StoreOrder(keys);
        };

        // 2nd-level (section / column) editing. Captions are localized, so they
        // are resolved to a stable id before being stored. User-created folders
        // have no caption in AppLang, so they resolve by their own name.
        OptionsControl.SectionMoveRequested += (caption, delta) =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                MoveSection(id, delta);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.SectionHideRequested += caption =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                SetSectionHidden(id, true);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.SectionDeleteRequested += caption =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                DeleteSection(id);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.MoveRowRequested += (optionKey, sectionId) =>
        {
            MoveRowToSection(optionKey, sectionId);
            RequestDeferredRebuild();
        };

        OptionsControl.CreateSectionRequested += async () => await CreateSectionInteractiveAsync();

        OptionsControl.RenameRowRequested += async (optionKey, current) => await RenameRowInteractiveAsync(optionKey, current);

        OptionsControl.RenameSectionRequested += async (sectionId, current) => await RenameSectionInteractiveAsync(sectionId, current);

        OptionsControl.AddAdvancedRequested += async () => await AddAdvancedOptionAsync();

        OptionsControl.EditAdvancedRequested += async optionKey => await EditAdvancedOptionAsync(optionKey);

        // Clipboard-style row operations, so one card's customization can be
        // applied to another without retyping it.
        OptionsControl.CopyRowRequested += CopyRowCustomization;
        OptionsControl.PasteRowRequested += PasteRowCustomization;
        OptionsControl.DuplicateRowRequested += DuplicateRow;

        // The right pane of the two-pane view: a card dropped into it joins the
        // folder that pane is showing.
        CustomizeOptionsControl.PaneJoinRequested += (optionKey, sectionId) =>
        {
            MoveRowToSection(optionKey, sectionId);
            RequestDeferredRebuild();
        };

        // The pane's cards carry the same edit menu, so it subscribes too.
        CustomizeOptionsControl.CopyRowRequested += CopyRowCustomization;
        CustomizeOptionsControl.PasteRowRequested += PasteRowCustomization;
        CustomizeOptionsControl.DuplicateRowRequested += DuplicateRow;

        UpdateCustomizeToggleText();
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
    /// Resolves a section caption to its stable id, covering the folders the
    /// user created themselves (which have no AppLang caption to look up).
    /// </summary>
    private string? ResolveSectionId(string? caption)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return null;
        }

        if (SettingsSectionIds.IdFor(caption) is { } builtIn)
        {
            return builtIn;
        }

        return _layout.CustomSections
            .FirstOrDefault(s => string.Equals(s.Name, caption, StringComparison.Ordinal))
            ?.Id;
    }

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

    /// <summary>
    /// Asks for a name and creates a 2nd-level folder.
    ///
    /// The two-pane view knows the category from the tree node that was
    /// right-clicked rather than from the page's current category, so the
    /// caller may pass it explicitly; browsing mode passes null and falls back
    /// to whatever category the page is showing.
    /// </summary>
    private async Task CreateSectionInteractiveAsync(string? categoryKey)
    {
        categoryKey ??= CurrentCategoryKey;
        if (categoryKey is null)
        {
            return;
        }

        var lang = AppContext.AppLang;
        var input = new TextBox
        {
            PlaceholderText = lang.CustomizeNewSection,
            Header = lang.CustomizeNewSection,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeNewSection,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    input,
                    new TextBlock
                    {
                        Text = lang.CustomizeNewSectionHint,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    },
                },
            },
            PrimaryButtonText = lang.CustomizeNewSection,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        CreateSection(categoryKey, input.Text);
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
        var builtIn = SettingsSectionIds.CaptionFor(sectionId) is not null;
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

        // The footer is a different set of jobs while customizing, so the
        // browsing buttons step aside rather than sitting next to controls that
        // mean something else. Without this swap the customize buttons stay
        // collapsed and the footer still offers only the two reset actions.
        BrowseActions.Visibility = Visibility.Collapsed;
        CustomizeActions.Visibility = Visibility.Visible;

        CustomizeUndoButton.Content = lang.CustomizeUndo;
        CustomizeRedoButton.Content = lang.CustomizeRedo;
        CustomizeDiscardButton.Content = lang.CustomizeDiscardSession;
        CustomizeApplyButtonText.Text = lang.ApplyAndExit;
        CustomizeExitButton.Content = lang.CustomizeExit;

        ToolTipService.SetToolTip(CustomizeUndoButton, lang.CustomizeUndoTip);
        ToolTipService.SetToolTip(CustomizeRedoButton, lang.CustomizeRedoTip);
        ToolTipService.SetToolTip(CustomizeDiscardButton, lang.CustomizeResetSessionTip);
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

    // ===== advanced editor: add / edit an option backed by an mpv key =====

    /// <summary>
    /// Opens the advanced editor for a new option, pre-filled from the current
    /// category so the new row lands where the user is looking.
    /// </summary>
    private async Task AddAdvancedOptionAsync()
    {
        if (CurrentCategoryKey is not { } categoryKey)
        {
            // Silence here is what read as "the button does nothing". Say why
            // instead: there is genuinely nowhere to put the row.
            ShowCustomizeNotice(AppContext.AppLang.CustomizeNoSelectionBody, caution: true);
            return;
        }

        var result = await ShowAdvancedOptionDialogAsync(null, categoryKey);
        if (result is null)
        {
            return;
        }

        // Half of these dialogs are opened from a folder node, where the row
        // belongs in that folder rather than loose in the category.
        var sectionId = _customizeMode && _treeSelectedIsSection ? _treeSelectedKey : null;

        PushCustomizeEdit();

        // Ids are derived from the mpv key so re-adding the same key updates
        // the existing row instead of stacking a second one on top of it.
        var id = AdvancedOptionId(result.MpvKey);
        _layout.Added.RemoveAll(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        _layout.Added.Add(new CustomOption
        {
            Id = id,
            CategoryKey = categoryKey,
            Section = sectionId,
            Label = result.Label,
            Description = string.IsNullOrEmpty(result.Description) ? null : result.Description,
            MpvKey = result.MpvKey,
            Kind = result.Kind,
            Choices = result.Choices,
            Value = result.DefaultValue,
        });

        if (!_layout.Order.Contains(id))
        {
            _layout.Order.Add(id);
        }

        SaveLayout();
        CommitCustomizeEdit();
        ShowCustomizeNotice(AppContext.AppLang.CustomizeSavedNotice);
        RequestDeferredRebuild();
    }

    /// <summary>Opens the advanced editor for an existing row.</summary>
    private async Task EditAdvancedOptionAsync(string optionKey)
    {
        if (Settings.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal)) is not { } option)
        {
            return;
        }

        if (CurrentCategoryKey is not { } categoryKey)
        {
            // An edit keeps the row where it already lives, so the pane's
            // selection is only needed for a built-in row being promoted.
            categoryKey = _layout.FindAdded(optionKey)?.CategoryKey ?? string.Empty;
        }

        if (string.IsNullOrEmpty(categoryKey))
        {
            ShowCustomizeNotice(AppContext.AppLang.CustomizeNoSelectionBody, caution: true);
            return;
        }

        var existing = _layout.FindAdded(optionKey);
        var result = await ShowAdvancedOptionDialogAsync(existing, categoryKey, option.Label);
        if (result is null)
        {
            return;
        }

        PushCustomizeEdit();

        if (existing is null)
        {
            // A built-in row being promoted to a custom one: carry the override
            // across rather than orphaning it under the old key.
            var id = AdvancedOptionId(result.MpvKey);
            _layout.Added.Add(new CustomOption
            {
                Id = id,
                CategoryKey = categoryKey,
                Label = result.Label,
                Description = string.IsNullOrEmpty(result.Description) ? null : result.Description,
                MpvKey = result.MpvKey,
                Kind = result.Kind,
                Choices = result.Choices,
                Value = result.DefaultValue,
            });
        }
        else
        {
            existing.Label = result.Label;
            existing.Description = string.IsNullOrEmpty(result.Description) ? null : result.Description;
            existing.MpvKey = result.MpvKey;
            existing.Kind = result.Kind;
            existing.Choices = result.Choices;
            existing.Value = result.DefaultValue;
        }

        SaveLayout();
        CommitCustomizeEdit();
        ShowCustomizeNotice(AppContext.AppLang.CustomizeSavedNotice);
        RequestDeferredRebuild();
    }

    /// <summary>Stable id for a hand-added option, derived from its mpv key.</summary>
    private static string AdvancedOptionId(string mpvKey) => "custom:" + mpvKey;

    /// <summary>What the advanced editor collected, already validated.</summary>
    private sealed record AdvancedOptionInput(
        string Label,
        string Description,
        string MpvKey,
        string Kind,
        List<string> Choices,
        string DefaultValue);

    /// <summary>
    /// The advanced editor: name, mpv option, value type, default and (for a
    /// dropdown) the choices — with validation that runs on the dialog's own
    /// primary button, so an invalid row can never reach the store.
    /// </summary>
    private async Task<AdvancedOptionInput?> ShowAdvancedOptionDialogAsync(
        CustomOption? existing,
        string categoryKey,
        string? fallbackLabel = null)
    {
        var lang = AppContext.AppLang;
        var isNew = existing is null;

        var nameBox = new TextBox
        {
            Text = existing?.Label ?? fallbackLabel ?? string.Empty,
            Header = lang.CustomizeFieldName,
            PlaceholderText = lang.CustomizeAddLabelHint,
            MaxLength = MaxLabelLength,
        };

        var descriptionBox = new TextBox
        {
            Text = existing?.Description ?? string.Empty,
            Header = lang.CustomizeFieldDescription,
            PlaceholderText = lang.CustomizeFieldDescriptionHint,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = MaxDescriptionLength,
        };

        var keyBox = new TextBox
        {
            Text = existing?.MpvKey ?? string.Empty,
            Header = lang.CustomizeFieldMpvKey,
            PlaceholderText = lang.CustomizeFieldMpvKeyPlaceholder,
        };

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
            SelectedIndex = existing is null
                ? 0
                : Array.IndexOf(CustomOptionKinds.All, existing.Kind) is var i && i >= 0 ? i : 0,
        };

        var defaultBox = new TextBox
        {
            Text = existing?.Value ?? string.Empty,
            Header = lang.CustomizeFieldDefault,
            PlaceholderText = lang.CustomizeFieldRawHint,
        };

        var choicesBox = new TextBox
        {
            Text = existing is null ? string.Empty : string.Join(",", existing.Choices),
            Header = lang.CustomizeFieldChoices,
            PlaceholderText = lang.CustomizeAddChoicesHint,
        };

        // The choices box only matters for a dropdown; hide it otherwise so the
        // dialog does not ask for input it will ignore.
        void SyncChoicesVisibility()
        {
            var isChoice = KindFromIndex(kindBox.SelectedIndex) == CustomOptionKinds.Choice;
            choicesBox.Visibility = isChoice ? Visibility.Visible : Visibility.Collapsed;
        }

        kindBox.SelectionChanged += (_, _) => SyncChoicesVisibility();
        SyncChoicesVisibility();

        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isNew ? lang.CustomizeAddAdvanced : lang.CustomizeEditAdvanced,
            Content = new StackPanel
            {
                Spacing = 12,
                MinWidth = 460,
                Children =
                {
                    nameBox,
                    descriptionBox,
                    keyBox,
                    kindBox,
                    defaultBox,
                    choicesBox,
                    new TextBlock
                    {
                        Text = lang.CustomizeAdvancedHint,
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

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var kind = KindFromIndex(kindBox.SelectedIndex);

            if (Validate(MpvKeyPattern, keyBox.Text.Trim(), kind, choicesBox.Text, defaultBox.Text, nameBox.Text)
                is { } error)
            {
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var mpvKey = keyBox.Text.Trim();
        var kind2 = KindFromIndex(kindBox.SelectedIndex);
        var choices = ParseChoices(choicesBox.Text);
        var name = nameBox.Text.Trim();

        return new AdvancedOptionInput(
            string.IsNullOrEmpty(name) ? mpvKey : name,
            descriptionBox.Text.Trim(),
            mpvKey,
            kind2,
            kind2 == CustomOptionKinds.Choice ? choices : [],
            defaultBox.Text.Trim());
    }

    /// <summary>mpv option names: letters, digits and the separators mpv itself uses.</summary>
    private static readonly System.Text.RegularExpressions.Regex MpvKeyPattern =
        new(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string KindFromIndex(int index) =>
        index >= 0 && index < CustomOptionKinds.All.Length ? CustomOptionKinds.All[index] : CustomOptionKinds.Text;

    private static List<string> ParseChoices(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Validates the advanced editor's fields and returns a message to show, or
    /// null when everything is acceptable. Kept separate from the dialog so the
    /// rules are testable and stated in one place.
    /// </summary>
    private string? Validate(
        System.Text.RegularExpressions.Regex keyPattern,
        string mpvKey,
        string kind,
        string choicesText,
        string defaultValue,
        string label)
    {
        var lang = AppContext.AppLang;

        if (string.IsNullOrEmpty(mpvKey))
        {
            return lang.CustomizeErrorKeyRequired;
        }

        if (!keyPattern.IsMatch(mpvKey))
        {
            return lang.CustomizeErrorKeyInvalid;
        }

        // Two rows editing one mpv option would fight over the same value.
        var duplicate = _layout.Added.FirstOrDefault(
            a => string.Equals(a.MpvKey, mpvKey, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            return string.Format(lang.CustomizeErrorKeyDuplicate, mpvKey);
        }

        if (label.Length > MaxLabelLength)
        {
            return string.Format(lang.CustomizeErrorLabelTooLong, MaxLabelLength);
        }

        var value = defaultValue.Trim();

        if (kind == CustomOptionKinds.Choice)
        {
            var choices = ParseChoices(choicesText);
            if (choices.Count < 2)
            {
                return lang.CustomizeErrorChoicesTooFew;
            }

            if (!string.IsNullOrEmpty(value)
                && !choices.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return lang.CustomizeErrorDefaultNotInChoices;
            }
        }
        else if (kind == CustomOptionKinds.Boolean && !string.IsNullOrEmpty(value))
        {
            // mpv accepts a family of spellings for booleans; anything else is a
            // typo that would silently fail at `set` time.
            string[] accepted = ["yes", "no", "true", "false", "1", "0"];
            if (!accepted.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return lang.CustomizeErrorBooleanDefault;
            }
        }

        return null;
    }
}
