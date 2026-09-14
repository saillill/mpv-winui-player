using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// The customize mode's shell: the collapsible sidebar, its "add" menu, the
/// scope notice and the apply / undo / redo / discard / exit footer.
///
/// The mode deliberately has its own footer. Browsing and customizing are two
/// different jobs: browsing wants "reset this category / reset everything",
/// while customizing wants a draft boundary (apply or throw the session away)
/// plus step-by-step undo. Sharing one row of buttons served neither.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>Draft/undo stack for the customize session.</summary>
    private readonly LayoutDraft _draft = new();

    /// <summary>Whether the folder-tree sidebar is collapsed to its arrow rail.</summary>
    private bool _sidebarCollapsed;

    /// <summary>
    /// The layout as it was when the mode opened. "Discard" restores this, and
    /// the same snapshot decides whether leaving needs a confirmation.
    /// </summary>
    private SettingsLayout? _sessionBaseline;

    /// <summary>What the footer status line is currently reporting.</summary>
    private enum CustomizeStatus
    {
        None,
        Dirty,
        Applied,
        Discarded,
    }

    /// <summary>
    /// Opens the customize session: snapshots the layout as the discard point
    /// and switches the footer over to the draft action group.
    /// </summary>
    private void BeginCustomizeSession()
    {
        _draft.Begin(_layout);
        _sessionBaseline = SettingsLayoutStore.Load();
        BrowseActions.Visibility = Visibility.Collapsed;
        CustomizeActions.Visibility = Visibility.Visible;
        UpdateCustomizeFooter();
    }

    /// <summary>Closes the session and returns the footer to the browsing actions.</summary>
    private void EndCustomizeSession()
    {
        _sessionBaseline = null;
        BrowseActions.Visibility = Visibility.Visible;
        CustomizeActions.Visibility = Visibility.Collapsed;
        SaveStatusText.Text = string.Empty;
    }

    /// <summary>
    /// Records one edit for undo. Call immediately before mutating
    /// <see cref="_layout"/>; pair it with <see cref="CommitCustomizeEdit"/>.
    /// </summary>
    private void PushCustomizeEdit() => _draft.Push(_layout);

    /// <summary>Records the post-edit state and refreshes the footer.</summary>
    private void CommitCustomizeEdit()
    {
        _draft.Commit(_layout);
        UpdateCustomizeFooter();
    }

    /// <summary>Refreshes the footer captions and the undo/redo enabled state.</summary>
    private void UpdateCustomizeFooter()
    {
        var lang = AppContext.AppLang;
        CustomizeApplyButtonText.Text = lang.ApplyAndExit;
        CustomizeDiscardButton.Content = lang.Discard;
        CustomizeExitButton.Content = lang.Close;
        ToolTipService.SetToolTip(CustomizeApplyButton, lang.ApplyAndExit);
        ToolTipService.SetToolTip(CustomizeUndoButton, lang.CustomizeUndoTip);
        ToolTipService.SetToolTip(CustomizeRedoButton, lang.CustomizeRedoTip);
        ToolTipService.SetToolTip(CustomizeDiscardButton, lang.CustomizeResetSessionTip);

        CustomizeUndoButton.IsEnabled = _draft.CanUndo;
        CustomizeRedoButton.IsEnabled = _draft.CanRedo;
        CustomizeDiscardButton.IsEnabled = _draft.IsDirty;
    }

    /// <summary>Shows (or clears) the unsaved-changes line in the footer.</summary>
    private void ShowCustomizeStatus(CustomizeStatus status)
    {
        var lang = AppContext.AppLang;
        switch (status)
        {
            case CustomizeStatus.Dirty:
                SaveStatusText.Text = lang.CustomizeDirtyNotice;
                break;
            case CustomizeStatus.Applied:
                SaveStatusText.Text = lang.CustomizeSavedNotice;
                break;
            case CustomizeStatus.Discarded:
                SaveStatusText.Text = lang.CustomizeDiscardedNotice;
                break;
            default:
                SaveStatusText.Text = string.Empty;
                return;
        }

        SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            status == CustomizeStatus.Dirty
                ? "SystemFillColorCautionBrush"
                : "SystemFillColorSuccessBrush"];
    }

    /// <summary>
    /// One-off footer message with a severity tint. Used where an action is
    /// refused or produces no visible change: a Windows-settings-style line
    /// beats a button that silently does nothing.
    /// </summary>
    private void ShowCustomizeNotice(string message, bool caution = false)
    {
        SaveStatusText.Text = message;
        SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            caution ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush"];
    }

    // ===== footer actions =====

    private void CustomizeUndo_Click(object sender, RoutedEventArgs e)
    {
        if (!_draft.Undo(_layout))
        {
            return;
        }

        SaveLayout();
        RebuildLocalizedContent();
        UpdateCustomizeFooter();
        ShowCustomizeStatus(_draft.IsDirty ? CustomizeStatus.Dirty : CustomizeStatus.None);
    }

    private void CustomizeRedo_Click(object sender, RoutedEventArgs e)
    {
        if (!_draft.Redo(_layout))
        {
            return;
        }

        SaveLayout();
        RebuildLocalizedContent();
        UpdateCustomizeFooter();
        ShowCustomizeStatus(_draft.IsDirty ? CustomizeStatus.Dirty : CustomizeStatus.None);
    }

    private void CustomizeApply_Click(object sender, RoutedEventArgs e)
    {
        SaveLayout();
        _draft.AcceptBaseline();
        _sessionBaseline = SettingsLayoutStore.Load();
        UpdateCustomizeFooter();
        ShowCustomizeStatus(CustomizeStatus.Applied);
        ExitCustomizeMode();
    }

    private void CustomizeDiscard_Click(object sender, RoutedEventArgs e)
    {
        if (!_draft.Discard(_layout))
        {
            return;
        }

        SaveLayout();
        RebuildLocalizedContent();
        UpdateCustomizeFooter();
        ShowCustomizeStatus(CustomizeStatus.Discarded);
    }

    /// <summary>
    /// Leaves the mode. With unsaved changes this asks first and offers to
    /// apply them, discard them, or stay; the dialog is the "save?" card the
    /// exit path needs.
    /// </summary>
    private async void CustomizeExit_Click(object sender, RoutedEventArgs e)
    {
        if (_draft.IsDirty)
        {
            var lang = AppContext.AppLang;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = lang.CustomizeExitTitle,
                Content = new TextBlock { Text = lang.CustomizeExitBody, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = lang.CustomizeExitApply,
                SecondaryButtonText = lang.CustomizeExitDiscard,
                CloseButtonText = lang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                SaveLayout();
                _draft.AcceptBaseline();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                _draft.Discard(_layout);
                SaveLayout();
            }
            else
            {
                return;
            }
        }

        ExitCustomizeMode();
    }

    /// <summary>Flips the page back to browsing and re-renders it.</summary>
    private void ExitCustomizeMode()
    {
        _customizeMode = false;
        CustomizeToggle.IsChecked = false;
        OptionsControl.CustomizeMode = false;
        CustomizeOptionsControl.CustomizeMode = false;
        CustomizeRoot.Visibility = Visibility.Collapsed;
        BrowseHost.Visibility = Visibility.Visible;
        CategoryNav.IsPaneVisible = true;
        EndCustomizeSession();
        RestoreBrowseChrome();
        RebuildLocalizedContent();
        UpdateCustomizeToggleText();
    }

    // ===== sidebar collapse =====

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e) => SetSidebarCollapsed(true);

    private void ExpandSidebar_Click(object sender, RoutedEventArgs e) => SetSidebarCollapsed(false);

    /// <summary>
    /// Collapses the tree to the arrow rail on the far left. The column keeps
    /// its width so the cards on the right do not reflow on every toggle.
    /// </summary>
    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;
        SidebarPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ExpandSidebarButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = collapsed
            ? new GridLength(44)
            : new GridLength(272);
    }

    // ===== sidebar "add" menu =====

    /// <summary>
    /// The sidebar's add button: a menu rather than one action, because the
    /// thing a user wants to add here can be a folder, a new option or a whole
    /// top-level category.
    /// </summary>
    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var lang = AppContext.AppLang;
        var flyout = new MenuFlyout();

        var addOption = new MenuFlyoutItem
        {
            Text = lang.CustomizeAddIconOption,
            Icon = new FontIcon { Glyph = "\uE710" },
        };
        addOption.Click += async (_, _) => await AddAdvancedOptionAsync();
        flyout.Items.Add(addOption);

        var addFolder = new MenuFlyoutItem
        {
            Text = lang.CustomizeAddFolderOption,
            Icon = new FontIcon { Glyph = "\uE8F4" },
        };
        addFolder.Click += async (_, _) => await CreateSectionInteractiveAsync();
        flyout.Items.Add(addFolder);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var addCategory = new MenuFlyoutItem
        {
            Text = lang.CustomizeAddCategoryOption,
            Icon = new FontIcon { Glyph = "\uE8B7" },
        };
        addCategory.Click += async (_, _) => await CreateCategoryInteractiveAsync();
        flyout.Items.Add(addCategory);

        // Anchored to the button rather than opened contextually, so the menu
        // appears where the click was.
        flyout.ShowAt(AddCategoryButton);
    }

    /// <summary>
    /// Asks for a name and creates a top-level sidebar category the user can
    /// then fill with folders and cards.
    /// </summary>
    private async Task CreateCategoryInteractiveAsync()
    {
        var lang = AppContext.AppLang;
        var nameBox = new TextBox
        {
            Header = lang.CustomizeNewCategory,
            PlaceholderText = lang.CustomizeNewCategory,
        };

        // The icon is picked from the same Fluent set the built-in categories
        // use, so a custom category does not look like a foreign object.
        var iconChoices = new ComboBox
        {
            Header = lang.CustomizeNewCategoryIcon,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var (iconGlyph, iconName) in CategoryIconChoices())
        {
            iconChoices.Items.Add(new ComboBoxItem
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = iconGlyph, FontSize = 14 },
                        new TextBlock { Text = iconName, VerticalAlignment = VerticalAlignment.Center },
                    },
                },
                Tag = iconGlyph,
            });
        }
        iconChoices.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeNewCategory,
            Content = new StackPanel
            {
                Spacing = 12,
                MinWidth = 360,
                Children =
                {
                    nameBox,
                    iconChoices,
                    new TextBlock
                    {
                        Text = lang.CustomizeNewCategoryHint,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
            PrimaryButtonText = lang.CustomizeAddTopLevel,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var name = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var glyph = (iconChoices.SelectedItem as ComboBoxItem)?.Tag as string ?? "\uE8B7";
        PushCustomizeEdit();
        CreateCategory(name, glyph);
        CommitCustomizeEdit();
        RebuildLocalizedContent();
    }

    /// <summary>
    /// The icons a custom category can take. Kept to the Fluent glyphs that
    /// render correctly in Segoe Fluent Icons, the font the built-in sidebar
    /// categories already use.
    /// </summary>
    private static List<(string Glyph, string Name)> CategoryIconChoices() =>
    [
        ("\uE8B7", "Folder"),
        ("\uE713", "Settings"),
        ("\uE714", "Video"),
        ("\uE767", "Audio"),
        ("\uED1F", "Subtitles"),
        ("\uE774", "Network"),
        ("\uE765", "Keyboard"),
        ("\uE722", "Camera"),
        ("\uE946", "Info"),
        ("\uE8A4", "Library"),
        ("\uE7C3", "Page"),
        ("\uE787", "Calendar"),
    ];

    // ===== tree node inline edit =====

    /// <summary>
    /// The pencil on a custom folder node. It opens the same rename dialog as
    /// the context menu, so there is one code path for one operation.
    /// </summary>
    private async void EditTreeNode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TreeViewNode node
            || node.Content is not TreeViewNodeContent { SectionId: { } id, IsSection: true })
        {
            return;
        }

        await RenameSectionInteractiveAsync(id, node.Content is TreeViewNodeContent content ? content.Text : string.Empty);
    }

    // ===== card clipboard: copy / paste / duplicate =====

    /// <summary>
    /// One card's customization, held in memory for pasting onto another. Only
    /// the presentation is carried (name, description, mpv key, value type,
    /// choices); the row's identity and folder are the target's business.
    /// </summary>
    private sealed class CopiedRow
    {
        public string Label { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? MpvKey { get; init; }
        public string? MpvValue { get; init; }
    }

    private CopiedRow? _copiedRow;

    /// <summary>Copies a row's customization so it can be pasted onto another.</summary>
    private void CopyRowCustomization(string optionKey)
    {
        var option = Settings.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal));
        if (option is null)
        {
            return;
        }

        var entry = _layout.Entries.TryGetValue(optionKey, out var e) ? e : null;
        _copiedRow = new CopiedRow
        {
            Label = LabelOverrides.ResolveLabel(entry, ActiveLanguageKey) ?? option.Label ?? string.Empty,
            Description = LabelOverrides.ResolveDescription(entry, ActiveLanguageKey) ?? option.Description,
            MpvKey = entry?.MpvKey,
            MpvValue = entry?.MpvValue,
        };

        ShowCustomizeStatus(CustomizeStatus.None);
        SaveStatusText.Text = AppContext.AppLang.CustomizeItemCopied;
        SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
    }

    /// <summary>
    /// Applies the copied customization to another row. The label and
    /// description become that row's overrides; the raw mpv key/value are only
    /// carried over when the source had them, so pasting a built-in row does
    /// not blank the target's own binding.
    /// </summary>
    private void PasteRowCustomization(string optionKey)
    {
        var option = Settings.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal));
        if (option is null)
        {
            return;
        }

        if (_copiedRow is null)
        {
            SaveStatusText.Text = AppContext.AppLang.CustomizePasteEmpty;
            SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
            return;
        }

        PushCustomizeEdit();
        var entry = _layout.EntryFor(optionKey);
        entry.Label = string.IsNullOrEmpty(_copiedRow.Label)
            || string.Equals(_copiedRow.Label, option.Label, StringComparison.Ordinal)
                ? null
                : _copiedRow.Label;
        entry.Description = _copiedRow.Description;
        entry.Scope = RenameScopes.Current;
        entry.Language = ActiveLanguageKey;
        if (_copiedRow.MpvKey is not null)
        {
            entry.MpvKey = _copiedRow.MpvKey;
        }
        if (_copiedRow.MpvValue is not null)
        {
            entry.MpvValue = _copiedRow.MpvValue;
        }
        _layout.Prune(optionKey);

        SaveLayout();
        CommitCustomizeEdit();
        RequestDeferredRebuild();
    }

    /// <summary>
    /// Duplicates a hand-added row under a fresh id. Only a row the user added
    /// can be duplicated: a built-in row is one the app owns, and a second copy
    /// of it would have no distinct key to store under.
    /// </summary>
    private void DuplicateRow(string optionKey)
    {
        if (_layout.FindAdded(optionKey) is not { } source)
        {
            SaveStatusText.Text = AppContext.AppLang.CustomizeInvalidValueTitle;
            SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
            return;
        }

        PushCustomizeEdit();
        var id = $"{source.Id}:copy{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        _layout.Added.Add(new CustomOption
        {
            Id = id,
            CategoryKey = source.CategoryKey,
            Section = source.Section,
            Label = source.Label + " (2)",
            Description = source.Description,
            MpvKey = source.MpvKey,
            Kind = source.Kind,
            Choices = [.. source.Choices],
            Value = source.Value,
        });
        if (!_layout.Order.Contains(id))
        {
            _layout.Order.Add(id);
        }

        SaveLayout();
        CommitCustomizeEdit();
        RequestDeferredRebuild();
    }
}
