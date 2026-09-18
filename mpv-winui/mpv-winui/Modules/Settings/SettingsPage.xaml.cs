using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage : Page
{

    public List<Option> Settings { get; } = [];
    public List<string> Categories { get; } = [];

    /// <summary>Stable keys parallel to <see cref="Categories"/>: same length,
    /// same order. Pairing by index into <see cref="CategoryKeys"/> instead
    /// would shift whenever an empty category is filtered out.</summary>
    private List<string> ActiveCategoryKeys { get; } = [];

    public List<string> CategoryOrder { get; } = [];
    private string _actionStatus = string.Empty;
    private int _resetStatusGeneration;

    /// <summary>
    /// Section currently drilled into (localized label), null while the
    /// category shows its section overview. Reset on category/language
    /// switches: section labels change with the language.
    /// </summary>
    private string? _selectedSection;

    /// <summary>Stable category keys, parallel to the localized category order.</summary>
    private static readonly string[] CategoryKeys =
    [
        "program", "playback", "video", "audio", "subtitles",
        "window", "network", "shortcuts", "osd", "screenshot",
    ];

    /// <summary>Fluent glyphs in the same order as <see cref="CategoryKeys"/>.</summary>
    // Segoe Fluent Icons codepoints (system icon font). The sidebar must not
    // mix these with the bundled FluentSystemIcons-Regular.ttf: the same
    // codepoints map to different glyphs there (e.g. E946 is "Code", which
    // made the OSD icon render as a pile of code characters).
    private static readonly string[] CategoryGlyphs =
    [
        "\uE713", "\uE768", "\uE714", "\uE767", "\uED1F",
        "\uE8A4", "\uE774", "\uE765", "\uE946", "\uE722",
    ];

    /// <summary>
    /// Creates the Segoe Fluent Icons font on the calling (UI) thread. It
    /// must not live in the static initializer: WarmDeviceChoices can trigger
    /// the SettingsPage cctor on a background thread, and WinUI FontFamily is
    /// thread-affine (settings crashed with 0x8001010E on open).
    /// </summary>
    private static FontFamily CreateCategoryIconFont() => new("Segoe Fluent Icons");

    private readonly bool _isUnpackaged;

    public SettingsPage()
    {
        _isUnpackaged = !PackageHelper.IsPackaged;
        InitializeComponent();
        InitCustomizeMode();
        OptionsControl.SectionCardClicked += OnSectionCardClicked;
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        WarmDeviceChoices();
        LoadSearchHistory();
        RebuildLocalizedContent();
    }

    /// <summary>A folded section's card was opened: drill in.
    ///
    /// The card carries the stable id, while drill-down keys on the localized
    /// caption, so the id is resolved here -- the reverse of how rows on the
    /// option list store both.</summary>
    private void OnSectionCardClicked(string sectionId)
    {
        var caption = Settings.Where(o => o.SectionId == sectionId)
            .Select(o => o.Section)
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        if (caption is null)
        {
            return;
        }

        _selectedSection = caption;
        UpdateOptions();
    }

    /// <summary>Debounces keystroke-level search filtering (audit A4).</summary>
    private readonly DispatcherTimer _searchDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250),
    };
    private string _pendingSearchQuery = string.Empty;

    /// <summary>Prebuilt search index (audit A4): category aliases and the
    /// flattened searchable text of every option are computed once per
    /// settings rebuild instead of per keystroke burst.</summary>
    private readonly Dictionary<string, string[]> _categoryAliasCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OptionSearchEntry> _optionSearchIndex = new(StringComparer.Ordinal);

    private void SearchDebounceTimer_Tick(object? sender, object e)
    {
        _searchDebounceTimer.Stop();
        ApplySearchQuery(_pendingSearchQuery);
    }

    /// <summary>
    /// Rebuilds the option tree, category pane and localized labels in place.
    /// Used at construction, after language switches and after resets, so the
    /// page instance (search text, selection, scroll) survives.
    /// </summary>
    private void RebuildLocalizedContent()
    {
        // Section captions are localized, and the caption -> stable-id map is
        // cached: a language switch changes every caption, so the cache has to
        // go before the tree is rebuilt.
        SettingsSections.Invalidate();
        var selectedKey = CurrentCategoryKey;
        var offset = OptionsControl.GetScrollOffset();
        // Section labels are localized; a drilled-in section cannot survive
        // a rebuild (language switch / reset) — fall back to the overview.
        _selectedSection = null;
        _rebuildingContent = true;
        try
        {
            CategoryOrder.Clear();
            var options = BuildSettings();
            Settings.Clear();
            Settings.AddRange(options);
            Categories.Clear();
            ActiveCategoryKeys.Clear();
            _displayedCategoryKeys.Clear();
            var categoryCount = Math.Min(CategoryOrder.Count, CategoryKeys.Length);

            // Pair each stable key with its localized label first: the sidebar
            // customization reorders and hides pairs, and the two public lists
            // have to stay index-aligned.
            //
            // The empty-category gate compares against the caption the rows were
            // built with, but the pair carries the caption to *show*, which is
            // the user's own name when they renamed the category. The rows are
            // re-labelled to match right after, so one string still identifies a
            // category for everything downstream.
            var pairs = new List<(string Key, string Label)>(categoryCount);
            for (var i = 0; i < categoryCount; i++)
            {
                var label = CategoryOrder[i];
                if (!Settings.Any(o => o.Category == label))
                {
                    continue;
                }
                pairs.Add((CategoryKeys[i], CategoryCaptionForKey(CategoryKeys[i]) ?? label));
            }

            RenameCategoryRows(pairs);

            // Categories the user created sit alongside the built-in ones. They
            // start empty, so they are appended after the layout pass: the
            // built-in filter above drops a category with no options, which
            // would also drop a freshly made one.
            foreach (var category in _layout.CustomCategories)
            {
                pairs.Add((category.Id, category.DisplayFor(ActiveLanguageKey)));
            }

            ApplyCategoryLayout(pairs);

            foreach (var pair in pairs)
            {
                Categories.Add(pair.Label);
                ActiveCategoryKeys.Add(pair.Key);
                _displayedCategoryKeys.Add(pair.Key);
            }
            RebuildSearchIndex();
            RebuildNavigationItems(selectedKey);
            ResetButton.Content = AppContext.AppLang.ResetCurrentCategory;
            ResetAllButton.Content = AppContext.AppLang.ResetAllSettings;
            UpdateCustomizeToggleText();
            if (_customizeMode)
            {
                ApplyCustomizeChrome();
                UpdateCustomizeFooter();
                ShowCustomizeStatus(_draft.IsDirty ? CustomizeStatus.Dirty : CustomizeStatus.None);
            }
            if (SearchBox is not null)
            {
                SearchBox.PlaceholderText = AppContext.AppLang.SearchPlaceholder;
                AutomationProperties.SetName(SearchBox, AppContext.AppLang.Search);
            }
            var backTip = AppContext.AppLang.CommonBack;
            ToolTipService.SetToolTip(BreadcrumbBackButton, backTip);
            AutomationProperties.SetName(BreadcrumbBackButton, backTip);
            RefreshWarningsAndEnabled();
            UpdateOptions();
        }
        finally
        {
            _rebuildingContent = false;
        }
        if (offset > 0)
        {
            var target = offset;
            DispatcherQueue.TryEnqueue(() => OptionsControl.SetScrollOffset(target));
        }
    }

    private void RebuildSearchIndex()
    {
        _categoryAliasCache.Clear();
        _optionSearchIndex.Clear();

        foreach (var category in Categories)
        {
            _categoryAliasCache[category] = CategorySearchAliases(category)
                .Where(alias => !string.IsNullOrEmpty(alias))
                .ToArray();
        }

        foreach (var option in Settings)
        {
            _optionSearchIndex[option.Key] = BuildSearchEntry(option);
        }
    }

    /// <summary>
    /// One option's searchable fields, split into tiers so that a match can be
    /// ranked. <see cref="Text"/> is the flat concatenation used by the cheap
    /// membership test; the tiers decide ordering.
    /// </summary>
    private sealed record OptionSearchEntry(
        string Label,
        string Description,
        string MpvName,
        string Context,
        string Values,
        string Text);

    /// <summary>
    /// Builds an option's search entry.
    ///
    /// The mpv option name is the term people actually arrive with: anyone
    /// carrying settings over from an mpv.conf, or reading the manual, knows
    /// <c>sub-font-size</c> rather than whatever this UI labels it in their
    /// language. Leaving it out made search useless precisely for the users
    /// who knew what they wanted.
    ///
    /// Choice values and labels land in their own low-ranked tier so that
    /// "auto" or "vulkan" finds every switch accepting them, while a row whose
    /// *name* matches still sorts above one that merely offers the value.
    /// </summary>
    private OptionSearchEntry BuildSearchEntry(Option option)
    {
        var context = string.Join(
            "\n",
            option.Category,
            string.Join("\n", GetCategoryAliases(option.Category)),
            option.Section ?? string.Empty);

        var lines = new List<string>(64);
        foreach (var choice in option.Choices ?? SafeInvoke(option.ChoicesProvider) ?? [])
        {
            lines.Add(choice.Value);
            lines.Add(choice.Label);
        }

        foreach (var item in option.CheckItems ?? [])
        {
            lines.Add(item.Value);
            lines.Add(item.Label);
        }

        var values = string.Join("\n", lines);
        var mpvName = MpvSettings.ToMpvOptionName(option.Key) ?? string.Empty;
        var label = option.Label;
        var description = option.Description ?? string.Empty;

        // Shortcut keys are generated from input.conf and carry the binding
        // as their Key ("Shortcut:Space"), so that text belongs in the index:
        // searching "space" should find the Space binding.
        var text = string.Join("\n", label, description, mpvName, context, values);
        if (option.Key.StartsWith("Shortcut:", StringComparison.Ordinal))
        {
            text = string.Join("\n", text, option.Key);
        }

        return new OptionSearchEntry(label, description, mpvName, context, values, text);
    }

    /// <summary>A lazy choice provider may touch the disk or the registry;
    /// search must never fail because one row is expensive to expand.</summary>
    private static IList<OptionChoice>? SafeInvoke(Func<IList<OptionChoice>>? provider)
    {
        if (provider is null)
        {
            return null;
        }

        try
        {
            return provider();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "Search index: a choice provider threw while building the index");
            return null;
        }
    }

    /// <summary>
    /// Lower is better. Ranking is what keeps indexing values from flooding
    /// the results: tier 4 only wins when nothing else matched.
    /// </summary>
    private const int RankLabelPrefix = 0;
    private const int RankLabelContains = 1;
    private const int RankMpvName = 2;
    private const int RankDescription = 3;
    private const int RankContext = 4;
    private const int RankValues = 5;
    private const int RankFuzzy = 6;
    private const int RankNone = int.MaxValue;

    private int RankOptionCore(string query, OptionSearchEntry entry)
    {
        if (entry.Label.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankLabelPrefix;
        }

        if (entry.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankLabelContains;
        }

        if (entry.MpvName.Length > 0 && entry.MpvName.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankMpvName;
        }

        if (entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankDescription;
        }

        if (entry.Context.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankContext;
        }

        if (entry.Values.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankValues;
        }

        return RankNone;
    }

    /// <summary>Best tier this option matches on, falling back to subsequence
    /// matching so the typos and partials people actually type still hit.</summary>
    private int RankOption(string query, OptionSearchEntry entry)
    {
        var exact = RankOptionCore(query, entry);
        return exact != RankNone ? exact : (ContainsFuzzy(query, entry.Text) ? RankFuzzy : RankNone);
    }

    private IReadOnlyList<string> GetCategoryAliases(string category)
    {
        return _categoryAliasCache.TryGetValue(category, out var aliases)
            ? aliases
            : Array.Empty<string>();
    }

    private const int MaxSearchHistory = 8;

    private void LoadSearchHistory() => RestoreSearchHistorySuggestions();

    /// <summary>Refills the suggestion list with the saved search history
    /// (used at construction and whenever an active query is cleared).</summary>
    private void RestoreSearchHistorySuggestions()
    {
        // The constructor path runs before the window hands over the search box.
        if (SearchBox is null)
        {
            return;
        }
        var history = AppContext.AppSetting.SettingsSearchHistory
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DecodeHistoryEntry)
            .Take(MaxSearchHistory)
            .ToList();
        SearchBox.ItemsSource = history.Count > 0 ? history : null;
    }

    private void RememberSearchQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }
        var history = AppContext.AppSetting.SettingsSearchHistory
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DecodeHistoryEntry)
            .Where(x => !string.Equals(x, query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        history.Insert(0, query);
        AppContext.AppSetting.SettingsSearchHistory = string.Join(",", history.Take(MaxSearchHistory).Select(EncodeHistoryEntry));
    }

    /// <summary>Base64url-encodes one history entry (commas/percent safe, audit A8).</summary>
    private static string EncodeHistoryEntry(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string DecodeHistoryEntry(string value)
    {
        try
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
        catch (FormatException)
        {
            // Legacy histories are stored raw/escaped; keep parsing them.
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                return value;
            }
        }
    }

    /// <summary>The currently selected category (localized label).</summary>
    public string? CurrentCategory
    {
        get
        {
            var key = CurrentCategoryKey;
            if (key is null)
            {
                return null;
            }
            var index = ActiveCategoryKeys.IndexOf(key);
            return index >= 0 ? Categories[index] : null;
        }
    }

    /// <summary>
    /// Stable key of the category a new row would go into. While customizing,
    /// the NavigationView pane is hidden and the folder tree is what the user
    /// is actually navigating, so the tree's selection wins. Falling back to
    /// the pane there is what made "new option" a no-op: the tree had moved on
    /// while the pane still pointed at whatever it held when it was hidden.
    /// </summary>
    private string? CurrentCategoryKey
    {
        get
        {
            if (_customizeMode && ResolveTreeCategoryKey() is { } fromTree)
            {
                return fromTree;
            }

            return CategoryNav.SelectedItem is NavigationViewItem item ? item.Tag as string : null;
        }
    }

    /// <summary>
    /// The category the tree selection belongs to. A selected folder resolves
    /// to its parent category, so adding while a folder is open files the new
    /// row where the user is looking rather than silently doing nothing.
    /// </summary>
    private string? ResolveTreeCategoryKey()
    {
        if (FolderTree.SelectedNode?.Content is not TreeViewNodeContent content)
        {
            return null;
        }

        if (!content.IsSection)
        {
            return content.CategoryKey;
        }

        // A folder node carries no category of its own; look up its parent.
        foreach (var root in FolderTree.RootNodes)
        {
            foreach (var child in root.Children)
            {
                if (ReferenceEquals(child, FolderTree.SelectedNode))
                {
                    return (root.Content as TreeViewNodeContent)?.CategoryKey;
                }
            }
        }

        return null;
    }

    /// <summary>The current vertical scroll offset of the options list.</summary>
    public double CurrentScrollOffset => OptionsControl.GetScrollOffset();

    /// <summary>Refreshes the page after a language switch without recreating it.</summary>
    public void OnLanguageChanged()
    {
        RebuildLocalizedContent();
    }

    private void RebuildNavigationItems(string? selectedKey)
    {
        CategoryNav.MenuItems.Clear();
        NavigationViewItem? selectedItem = null;
        for (var i = 0; i < Categories.Count && i < ActiveCategoryKeys.Count; i++)
        {
            var key = ActiveCategoryKeys[i];
            var item = new NavigationViewItem
            {
                Content = Categories[i],
                Tag = key,
                Icon = new FontIcon
                {
                    Glyph = GlyphForCategoryKey(key),
                    FontFamily = CreateCategoryIconFont(),
                },
            };
            if (string.Equals(key, selectedKey, StringComparison.Ordinal))
            {
                selectedItem = item;
            }

            // In customize mode the pane entries carry their own edit menu
            // (move up/down, hide), and can be dragged onto each other to
            // reorder, so the sidebar is customizable too.
            ApplyCategoryEditMenu(item, key, i);
            AttachCategoryDrag(item, i);

            CategoryNav.MenuItems.Add(item);
        }

        // A hidden category or section must be recoverable, otherwise hiding is
        // a one-way trip. The restore entry only exists while customizing.
        CategoryNav.FooterMenuItems.Clear();
        if (_customizeMode && (_layout.HiddenCategories.Count > 0 || _layout.HiddenSections.Count > 0))
        {
            var restore = new NavigationViewItem
            {
                Content = AppContext.AppLang.CustomizeRestoreCategories,
                Icon = new FontIcon { Glyph = "\uE72C" },
            };
            restore.Tapped += (_, _) =>
            {
                RestoreHiddenCategories();
                RestoreHiddenSections();
                RebuildLocalizedContent();
            };
            CategoryNav.FooterMenuItems.Add(restore);
        }

        CategoryNav.SelectedItem = selectedItem
            ?? (CategoryNav.MenuItems.Count > 0 ? CategoryNav.MenuItems[0] : null);
    }

    private void SelectCategory(string category)
    {
        var index = Categories.IndexOf(category);
        if (index >= 0 && index < CategoryNav.MenuItems.Count)
        {
            CategoryNav.SelectedItem = CategoryNav.MenuItems[index];
        }
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.Reset,
                Content = AppContext.AppLang.SettingsResetConfirm,
                XamlRoot = XamlRoot,
                PrimaryButtonText = AppContext.AppLang.Reset,
                CloseButtonText = AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var category = CurrentCategory;
            var keys = Settings
                .Where(o => o.Category == category)
                .Select(o => o.Key)
                .Where(k => !k.StartsWith("Shortcut:", StringComparison.Ordinal)
                    && k is not ("ShortcutCapture" or "ShortcutReset"
                        or "FileAssociationCheckList" or "ActionUnassociateFiles"
                        or "ActionExportConfig" or "ActionImportConfig"))
                .ToList();
            AppContext.AppSetting.ResetKeys(keys);
            ApplyAfterReset();
            ShowResetStatus(AppContext.AppLang.SettingsResetDone);
            RebuildLocalizedContent();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "reset category failed");
        }
    }

    private async void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.ResetAllSettings,
                Content = AppContext.AppLang.SettingsResetAllConfirm,
                XamlRoot = XamlRoot,
                PrimaryButtonText = AppContext.AppLang.Reset,
                CloseButtonText = AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            AppContext.AppSetting.ResetAll();
            ResetShortcuts();
            UnassociateFiles();
            ApplyAfterReset();
            ShowResetStatus(AppContext.AppLang.SettingsResetAllDone);
            RebuildLocalizedContent();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "reset all settings failed");
        }
    }

    private void ApplyAfterReset()
    {
        MpvSettings.ApplyAll(cmd => AppContext.SendMpvCommand(cmd));
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.UpdateTheme();
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.BackdropType), AppContext.AppSetting.BackdropType);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.EnableDebugLog), AppContext.AppSetting.EnableDebugLog);
        }
    }

    private void ShowResetStatus(string text)
    {
        SaveStatusText.Text = text;
        SaveStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        var generation = ++_resetStatusGeneration;
        _ = ClearResetStatusAsync(generation);
    }

    private async System.Threading.Tasks.Task ClearResetStatusAsync(int generation)
    {
        await System.Threading.Tasks.Task.Delay(3000);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation == _resetStatusGeneration)
            {
                SaveStatusText.Text = string.Empty;
            }
        });
    }

    /// <summary>True while <see cref="RebuildLocalizedContent"/> re-selects the
    /// category programmatically; the selection-changed handler must not treat
    /// that as a user click and clear the active search.</summary>
    private bool _rebuildingContent;

    private void CategoryNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // A user click on a category ends an active search: the list switches
        // to that category, so what is shown matches the footer's reset target.
        if (!_rebuildingContent && !string.IsNullOrEmpty(SearchBox?.Text))
        {
            _searchDebounceTimer.Stop();
            _pendingSearchQuery = string.Empty;
            SearchBox.Text = string.Empty;
            RestoreSearchHistorySuggestions();
        }
        // Category switches always land on the section overview (section
        // labels are localized, so a drilled-in section cannot survive).
        _selectedSection = null;
        UpdateOptions();
    }

    private void BreadcrumbBack_Click(object sender, RoutedEventArgs e)
    {
        _selectedSection = null;
        UpdateOptions();
    }

    private void BreadcrumbCategory_Click(object sender, RoutedEventArgs e)
    {
        _selectedSection = null;
        UpdateOptions();
    }

    /// <summary>The search box lives in the settings window's top bar;
    /// SettingsWindow hands it over before navigation so all search
    /// behaviour (history, debounce, suggestions) stays on this page.</summary>
    internal AutoSuggestBox? SearchBox { get; private set; }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // The constructor rebuilds content before the box exists (the frame
        // instantiates the page by type); the top-bar box arrives here, and
        // the bits the constructor skipped get applied now.
        if (SearchBox is null && e.Parameter is AutoSuggestBox box)
        {
            SearchBox = box;
            SearchBox.PlaceholderText = AppContext.AppLang.SearchPlaceholder;
            AutomationProperties.SetName(SearchBox, AppContext.AppLang.Search);
            RestoreSearchHistorySuggestions();
        }
    }

    internal void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            _searchDebounceTimer.Stop();
            _pendingSearchQuery = string.Empty;
            RestoreSearchHistorySuggestions();
            UpdateOptions();
            return;
        }

        // Debounce: wait for the user to pause before scanning the tree.
        _pendingSearchQuery = query;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void ApplySearchQuery(string query)
    {
        // The search box belongs to the window's top bar, so it can be absent
        // while this page is still coming up; without it there is no query to
        // show results for.
        if (SearchBox is null)
        {
            return;
        }

        // Searching leaves the section drill-down: the global result list
        // spans sections, so a stale _selectedSection would filter it away.
        _selectedSection = null;
        // Global results: every matching option across categories is shown in
        // one flat list for now; grouping/highlighting arrives with the
        // search-results view in the next stage.
        var categoryMatches = Categories
            .Where(c => FuzzyMatch(query, c))
            .ToList();
        // Ranked, then stable by page order: search now indexes choice values
        // and mpv option names, which multiplies the hits for a common query.
        // Ordering by tier keeps "auto" from burying the rows actually named
        // "Auto" under every switch that merely accepts auto.
        var optionMatches = RankedSearchMatches(query);
        OptionsControl.OptionList = optionMatches;
        SectionsHost.Visibility = Visibility.Collapsed;
        BreadcrumbBar.Visibility = Visibility.Collapsed;
        OptionsControl.Visibility = Visibility.Visible;
        SearchBox.ItemsSource = categoryMatches.Count > 0
            ? categoryMatches
            : optionMatches.Select(o => o.Category).Distinct().ToList();
    }

    internal void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        // The search box belongs to the window's top bar and arrives as a
        // navigation parameter, so it can still be missing here.
        if (SearchBox is null)
        {
            return;
        }

        if (args.SelectedItem is string category && Categories.Contains(category))
        {
            SearchBox.Text = string.Empty;
            SelectCategory(category);
        }
    }

    internal void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (SearchBox is null)
        {
            return;
        }

        _searchDebounceTimer.Stop();

        if (args.ChosenSuggestion is string suggested && Categories.Contains(suggested))
        {
            SearchBox.Text = string.Empty;
            SelectCategory(suggested);
            RememberSearchQuery(suggested);
            return;
        }

        var query = sender.Text?.Trim() ?? string.Empty;
        var match = Categories.FirstOrDefault(c => FuzzyMatch(query, c));
        if (match is not null)
        {
            SearchBox.Text = string.Empty;
            SelectCategory(match);
            RememberSearchQuery(query);
            return;
        }

        // Option-level query: keep the global results list and scroll to the
        // best hit -- the same ranking the list uses, so Enter always lands on
        // the row the user sees first rather than the earliest in page order.
        var option = TopSearchMatch(query);
        if (option is not null)
        {
            ApplySearchQuery(query);
            RememberSearchQuery(query);
            DispatcherQueue.TryEnqueue(() => OptionsControl.ScrollToOption(option.Key));
        }
    }

    private bool FuzzyMatch(string query, string category)
    {
        if (ContainsFuzzy(query, category))
        {
            return true;
        }

        foreach (var alias in GetCategoryAliases(category))
        {
            if (ContainsFuzzy(query, alias))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Every option matching the query, best tier first then in page
    /// order. Both entry points (live typing and state restore) funnel through
    /// this so they cannot drift into ranking results differently.</summary>
    private List<Option> RankedSearchMatches(string query) =>
        Settings
            .Select(o => (Option: o, Rank: RankOption(query, o)))
            .Where(x => x.Rank != RankNone)
            .OrderBy(x => x.Rank)
            .Select(x => x.Option)
            .ToList();

    /// <summary>The single best match, or null. Enter-to-scroll uses this so
    /// it lands on the row the user sees at the top of the list.</summary>
    private Option? TopSearchMatch(string query) => RankedSearchMatches(query).FirstOrDefault();

    /// <summary>Rank for an option the index has no entry for yet (a rebuild
    /// caught mid-flight); keeping hits alive matters more than ranking them
    /// precisely, and the index catches up on the next rebuild.</summary>
    private int RankOptionFallback(string query, Option option) =>
        Math.Min(
            ContainsFuzzy(query, option.Label) ? RankLabelContains : RankNone,
            option.Description is not null && ContainsFuzzy(query, option.Description)
                ? RankDescription
                : RankNone);

    private int RankOption(string query, Option option) =>
        _optionSearchIndex.TryGetValue(option.Key, out var entry)
            ? RankOption(query, entry)
            : RankOptionFallback(query, option);

    private static bool ContainsFuzzy(string query, string target)
    {
        if (target.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var targetIndex = 0;
        foreach (var queryChar in query)
        {
            var matched = false;
            while (targetIndex < target.Length)
            {
                if (char.ToLowerInvariant(target[targetIndex]) == char.ToLowerInvariant(queryChar))
                {
                    targetIndex++;
                    matched = true;
                    break;
                }
                targetIndex++;
            }
            if (!matched)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns extra search spellings for a category: pinyin for Chinese,
    /// romaji for Japanese and romanization for Korean, plus English terms
    /// so the search box stays useful regardless of the UI language.
    /// </summary>
    private static IEnumerable<string> CategorySearchAliases(string category)
    {
        var language = string.IsNullOrWhiteSpace(AppContext.AppSetting.CurrentLanguage)
            ? "en-US"
            : AppContext.AppSetting.CurrentLanguage;

        if (language == "zh-CN")
        {
            yield return PinyinSpelling(category);
            yield return PinyinInitials(category);
        }

        if (RomajiAliases.TryGetValue(language, out var aliases)
            && aliases.TryGetValue(category, out var alias)
            && !string.IsNullOrEmpty(alias))
        {
            yield return alias;
        }
    }

    /// <summary>Full pinyin (no tone marks) for every Chinese character in the string.</summary>
    private static string PinyinSpelling(string text)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (PinyinTable.TryGetValue(ch, out var syllable))
            {
                builder.Append(syllable);
            }
            else if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    /// <summary>First letter of each syllable, e.g. 快捷键 → kjj.</summary>
    private static string PinyinInitials(string text)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (PinyinTable.TryGetValue(ch, out var syllable))
            {
                builder.Append(syllable[0]);
            }
            else if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static readonly Dictionary<char, string> PinyinTable = new()
    {
        ['桌'] = "zhuo", ['面'] = "mian", ['播'] = "bo", ['放'] = "fang",
        ['轨'] = "gui", ['道'] = "dao", ['选'] = "xuan", ['择'] = "ze",
        ['记'] = "ji", ['忆'] = "yi", ['视'] = "shi", ['频'] = "pin",
        ['音'] = "yin", ['字'] = "zi", ['幕'] = "mu", ['窗'] = "chuang",
        ['口'] = "kou", ['解'] = "jie", ['封'] = "feng", ['装'] = "zhuang",
        ['缓'] = "huan", ['存'] = "cun", ['网'] = "wang", ['络'] = "luo",
        ['输'] = "shu", ['入'] = "ru", ['快'] = "kuai", ['捷'] = "jie",
        ['键'] = "jian", ['截'] = "jie", ['屏'] = "ping", ['测'] = "ce",
        ['试'] = "shi", ['渲'] = "xuan", ['染'] = "ran", ['器'] = "qi",
        ['项'] = "xiang", ['同'] = "tong", ['步'] = "bu", ['程'] = "cheng",
        ['序'] = "xu", ['稍'] = "shao", ['后'] = "hou", ['观'] = "guan",
        ['看'] = "kan",
    };

    /// <summary>Romaji/romanized spellings for categories in non-Latin UI languages.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> RomajiAliases = new(StringComparer.Ordinal)
    {
        ["ja-JP"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["デスクトップ"] = "desukutoppu desktop",
            ["再生"] = "saisei playback",
            ["トラック選択"] = "torakkusentaku track selection",
            ["後で見る"] = "atodemiru watch later",
            ["映像"] = "eizou video",
            ["音声"] = "onsei audio",
            ["字幕"] = "jimaku subtitle",
            ["ウィンドウ"] = "uindou window",
            ["デマルチプレクサ"] = "demaruchipurekusa demuxer",
            ["キャッシュ"] = "kyasshu cache",
            ["ネットワーク"] = "nettowaaku network",
            ["入力"] = "nyuuryoku input",
            ["ショートカット"] = "shaatokatto shortcut",
            ["スクリーンショット"] = "sukuriinshotto screenshot",
            ["テスト"] = "tesuto test",
            ["GPU レンダラーオプション"] = "gpu renderaa opushon gpu renderer",
            ["ビデオ同期"] = "bideo douki video sync",
        },
        ["ko-KR"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["데스크톱"] = "deseukeutop desktop",
            ["재생"] = "jaesaeng playback",
            ["트랙 선택"] = "teuraek seontaek track selection",
            ["나중에 보기"] = "najunge bogi watch later",
            ["비디오"] = "bidio video",
            ["오디오"] = "odio audio",
            ["자막"] = "jamak subtitle",
            ["창"] = "chang window",
            ["디먹서"] = "dimeokseo demuxer",
            ["캐시"] = "kaesi cache",
            ["네트워크"] = "neteuwokeu network",
            ["입력"] = "imnyeok input",
            ["단축키"] = "danchukki shortcut",
            ["스크린샷"] = "seukeurinsyat screenshot",
            ["테스트"] = "teseuteu test",
            ["GPU 렌더러 옵션"] = "gpu rendeo opyeon gpu renderer",
            ["비디오 동기화"] = "bidio donggihwa video sync",
        },
    };

    private void UpdateOptions()
    {
        // Customize mode owns the layout: the bookmark-manager surface (folder
        // tree + the selected node's cards) replaces the browsing view. The
        // tree is rebuilt here so its labels follow the language and its node
        // set follows the current option tree.
        if (_customizeMode)
        {
            // UpdateOptions runs on every rebuild — the constructor, a language
            // switch, a reset, the customize toggle — and the browsing branch
            // below re-shows BreadcrumbBar unconditionally. Re-assert the mode's
            // own visibility here so a rebuild triggered while customizing
            // cannot leave the trail sitting on top of the tree.
            BrowseHost.Visibility = Visibility.Collapsed;
            CustomizeRoot.Visibility = Visibility.Visible;
            BreadcrumbBar.Visibility = Visibility.Collapsed;
            SectionsHost.Visibility = Visibility.Collapsed;
            ResetButton.IsEnabled = true;
            BuildCustomizeTree();
            UpdateCustomizePane();

            // Both hosts must lay out before the cards can be measured; the
            // pane is filled here and its containers are realised on the next
            // pass.
            CustomizeRoot.UpdateLayout();
            return;
        }

        // Can run from the constructor (the navigation's initial
        // SelectionChanged) before the top-bar search box is handed over.
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(query))
        {
            // Global search results span categories and sections, so the
            // drill-down state and "reset current category" have no visible
            // target until the query is cleared.
            ResetButton.IsEnabled = false;
            SectionsHost.Visibility = Visibility.Collapsed;
            BreadcrumbBar.Visibility = Visibility.Collapsed;
            OptionsControl.Visibility = Visibility.Visible;

            // Results span categories, so no caption is "the page itself" --
            // every section header is useful context here.
            OptionsControl.CategoryCaption = null;
            OptionsControl.OptionList = RankedSearchMatches(query);
            return;
        }

        ResetButton.IsEnabled = true;
        var selected = CurrentCategory;
        var categoryOptions = selected is null
            ? Settings
            : Settings.Where(o => o.Category == selected).ToList();
        var sections = SectionSummaries(categoryOptions);

        // Windows-Settings flow: common options live directly on the
        // category overview; advanced/complex sections stay behind entry
        // cards. Categories with a single section go straight to their
        // options.
        var showOverview = _selectedSection is null && sections.Count > 1;
        SectionsHost.Visibility = Visibility.Collapsed;
        OptionsControl.Visibility = Visibility.Visible;

        // Tells the list which caption is "the page itself", so it can drop a
        // section header that would otherwise repeat it (Playback inside
        // Playback). Set on every branch that assigns an option list.
        OptionsControl.CategoryCaption = selected;

        if (showOverview)
        {
            // The overview also carries the trail — category name only.
            BreadcrumbBar.Visibility = Visibility.Visible;
            BreadcrumbCategoryLink.Visibility = Visibility.Collapsed;
            BreadcrumbSeparator.Visibility = Visibility.Collapsed;
            BreadcrumbSection.Text = selected ?? string.Empty;

            // Folded sections do NOT all sit at the bottom. They are emitted as
            // cards at the position their section occupies, so the page reads in
            // section order end to end: a card for section 3 appears above the
            // rows of section 8 instead of trailing the whole category. That
            // ordering is the one reason for the exercise -- everything landing
            // at the end is what made a category look like "common stuff, then
            // a dumping ground".
            var advancedCards = BuildSectionCardItems(sections.Where(s => s.Advanced).ToList());
            var advancedIds = new HashSet<string>(
                advancedCards.Select(c => c.SectionId!).Where(id => id is not null),
                StringComparer.Ordinal);

            OptionsControl.SetSectionCards(advancedCards);
            OptionsControl.CollapsedSectionIds = advancedIds;
            OptionsControl.OptionList = categoryOptions;
            return;
        }


        if (_selectedSection is not null)
        {
            BreadcrumbBar.Visibility = Visibility.Visible;
            // Windows-settings breadcrumb: the category segment is a live
            // button jumping back to the section overview; only the trailing
            // section is plain text.
            BreadcrumbCategoryLink.Content = selected;
            BreadcrumbCategoryLink.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbSeparator.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbSection.Text = _selectedSection ?? string.Empty;
            OptionsControl.OptionList = categoryOptions
                .Where(o => o.Section == _selectedSection)
                .ToList();
            return;
        }

        // Single-section category or the section overview: show the trail
        // with just the category name — nothing above it to link back to.
        BreadcrumbBar.Visibility = Visibility.Visible;
        BreadcrumbCategoryLink.Visibility = Visibility.Collapsed;
        BreadcrumbSeparator.Visibility = Visibility.Collapsed;
        BreadcrumbSection.Text = selected ?? string.Empty;
        OptionsControl.OptionList = categoryOptions;
    }

    /// <summary>Ordered summaries of a category's sections; options arrive
    /// pre-clustered so first-seen order is the page order.</summary>
    private static List<(string Label, int Count, bool Advanced)> SectionSummaries(IEnumerable<Option> categoryOptions) =>
        categoryOptions
            .Where(o => !string.IsNullOrEmpty(o.Section))
            .GroupBy(o => o.Section!, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count(), g.First().AdvancedSection))
            .ToList();


    /// <summary>One drill-in model per folded section.
    ///
    /// These replace the entry cards that used to be built as controls and
    /// stacked below every option row. A model is emitted into the list at the
    /// position its section occupies, so the card inherits its place from the
    /// section order instead of being appended after everything else.
    ///
    /// Each carries the resolution live reconcile currently needs: the glyph
    /// and count line are resolved here, once, from the section catalog.</summary>
    private List<SectionCardItem> BuildSectionCardItems(
        IReadOnlyList<(string Label, int Count, bool Advanced)> foldedSections)
    {
        var cards = new List<SectionCardItem>(foldedSections.Count);
        foreach (var section in foldedSections)
        {
            var id = SettingsSections.IdFor(section.Label);
            if (id is null)
            {
                continue;
            }

            var (icon, description) = SettingsSections.MetaFor(section.Label);
            cards.Add(new SectionCardItem
            {
                Caption = section.Label,
                SectionId = id,
                Icon = icon,
                Description = description,
                Count = section.Count,
                CountText = section.Count == 1
                    ? AppContext.AppLang.CardOptionCountOne
                    : string.Format(
                        System.Globalization.CultureInfo.CurrentUICulture,
                        AppContext.AppLang.CardOptionCount,
                        section.Count),
            });
        }

        return cards;
    }
}