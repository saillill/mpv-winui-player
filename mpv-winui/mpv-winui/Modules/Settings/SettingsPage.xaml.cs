using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
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

    /// <summary>Sentinel for the consolidated "More settings" second-level page.</summary>
    private const string AdvancedOverviewKey = "__more_settings__";
    private const string GroupSectionPrefix = "__group__";

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

    public SettingsPage()
    {
        InitializeComponent();
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        WarmDeviceChoices();
        LoadSearchHistory();
        RebuildLocalizedContent();
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
    private readonly Dictionary<string, string> _optionSearchTextCache = new(StringComparer.Ordinal);

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
            var categoryCount = Math.Min(CategoryOrder.Count, CategoryKeys.Length);
            for (var i = 0; i < categoryCount; i++)
            {
                var label = CategoryOrder[i];
                if (!Settings.Any(o => o.Category == label))
                {
                    continue;
                }
                Categories.Add(label);
                ActiveCategoryKeys.Add(CategoryKeys[i]);
            }
            RebuildSearchIndex();
            RebuildNavigationItems(selectedKey);
            ResetButton.Content = AppContext.AppLang.ResetCurrentCategory;
            ResetAllButton.Content = AppContext.AppLang.ResetAllSettings;
            if (SearchBox is not null)
            {
                SearchBox.PlaceholderText = AppContext.AppLang.SearchPlaceholder;
                AutomationProperties.SetName(SearchBox, AppContext.AppLang.Search);
            }
            var backTip = AppContext.AppLang.CommonBack;            ToolTipService.SetToolTip(BreadcrumbBackButton, backTip);
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
        _optionSearchTextCache.Clear();

        foreach (var category in Categories)
        {
            _categoryAliasCache[category] = CategorySearchAliases(category)
                .Where(alias => !string.IsNullOrEmpty(alias))
                .ToArray();
        }

        foreach (var option in Settings)
        {
            var text = string.Join(
                "\n",
                option.Label,
                option.Description ?? string.Empty,
                option.Category,
                string.Join("\n", GetCategoryAliases(option.Category)));
            _optionSearchTextCache[option.Key] = text;
        }
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

    private string? CurrentCategoryKey =>
        CategoryNav.SelectedItem is NavigationViewItem item ? item.Tag as string : null;

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
                    Glyph = CategoryGlyphs[Array.IndexOf(CategoryKeys, key)],
                    FontFamily = CreateCategoryIconFont(),
                },
            };
            if (string.Equals(key, selectedKey, StringComparison.Ordinal))
            {
                selectedItem = item;
            }
            CategoryNav.MenuItems.Add(item);
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
            mainWindow.UpdateCurrentTheme();
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
        if (!_rebuildingContent && !string.IsNullOrEmpty(SearchBox.Text))
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

        var query = SearchBox.Text?.Trim() ?? string.Empty;
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
        // Searching leaves the section drill-down: the global result list
        // spans sections, so a stale _selectedSection would filter it away.
        _selectedSection = null;
        // Global results: every matching option across categories is shown in
        // one flat list for now; grouping/highlighting arrives with the
        // search-results view in the next stage.
        var categoryMatches = Categories
            .Where(c => FuzzyMatch(query, c))
            .ToList();
        var optionMatches = Settings
            .Where(o => FuzzyMatchOption(query, o))
            .ToList();
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
        if (args.SelectedItem is string category && Categories.Contains(category))
        {
            SearchBox.Text = string.Empty;
            SelectCategory(category);
        }
    }

    internal void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
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
        // first hit.
        var option = Settings.FirstOrDefault(o => FuzzyMatchOption(query, o));
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

    private bool FuzzyMatchOption(string query, Option option)
    {
        if (_optionSearchTextCache.TryGetValue(option.Key, out var searchText))
        {
            return ContainsFuzzy(query, searchText);
        }

        // Index not built yet (e.g. mid-rebuild): fall back to the direct
        // fields so search never drops a result.
        return FuzzyMatch(query, option.Label)
            || (option.Description is not null && FuzzyMatch(query, option.Description));
    }

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
            OptionsControl.OptionList = Settings.Where(o => FuzzyMatchOption(query, o)).ToList();
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
        SectionsHost.Visibility = showOverview ? Visibility.Visible : Visibility.Collapsed;
        OptionsControl.Visibility = Visibility.Visible;

        if (showOverview)
        {
            // The overview also carries the trail — category name only.
            BreadcrumbBar.Visibility = Visibility.Visible;
            BreadcrumbCategoryLink.Visibility = Visibility.Collapsed;
            BreadcrumbSeparator.Visibility = Visibility.Collapsed;
            BreadcrumbSection.Text = selected ?? string.Empty;

            var primaryOptions = categoryOptions.Where(o => !o.AdvancedSection).ToList();
            var advancedSections = sections.Where(s => s.Advanced).ToList();

            OptionsControl.Visibility = primaryOptions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            OptionsControl.OptionList = primaryOptions;
            SectionsHost.ItemsSource = advancedSections.Count > 0
                ? BuildGroupCards(advancedSections)
                : Array.Empty<FrameworkElement>();
            return;
        }

        if (_selectedSection?.StartsWith(GroupSectionPrefix, StringComparison.Ordinal) == true)
        {
            var groupName = _selectedSection[GroupSectionPrefix.Length..];
            var groupSections = SectionSummaries(categoryOptions)
                .Where(s => s.Advanced && SectionGroupOf(s.Label) == groupName)
                .ToList();

            BreadcrumbBar.Visibility = Visibility.Visible;
            // Breadcrumb: category is a live link back to the overview;
            // the trailing segment names this functional group.
            BreadcrumbCategoryLink.Content = selected;
            BreadcrumbCategoryLink.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbSeparator.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbSection.Text = groupName;

            // The group page is itself a menu of its sub-sections. The user
            // drills one more level to see that section's actual options.
            OptionsControl.Visibility = Visibility.Collapsed;
            SectionsHost.Visibility = Visibility.Visible;
            SectionsHost.ItemsSource = groupSections.Select(s => BuildSectionCard(s.Label)).ToList();
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

    /// <summary>Card that opens the consolidated second-level page for all
    /// advanced/complex options in the current category.</summary>
    private FrameworkElement BuildMoreSettingsCard(string label)
    {
        var card = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 72,
            Padding = new Thickness(16, 16, 16, 16),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 8),
            Content = new Grid { ColumnSpacing = 18 },
        };
        if (card.Content is Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new FontIcon
            {
                Glyph = "\uE713",
                FontSize = 20,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = label, FontSize = 14 });
            textStack.Children.Add(new TextBlock
            {
                Text = AppContext.AppLang.AdvancedSettings,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var chevron = new FontIcon
            {
                Glyph = "\uE76C",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
        }
        AutomationProperties.SetName(card, $"{label}, {AppContext.AppLang.AdvancedSettings}");
        card.Click += (_, _) =>
        {
            _selectedSection = AdvancedOverviewKey;
            UpdateOptions();
        };
        return card;
    }

    private List<FrameworkElement> BuildGroupCards(
        IReadOnlyList<(string Label, int Count, bool Advanced)> advancedSections)
    {
        var groups = new List<(string Label, List<string> Sections)>();
        foreach (var section in advancedSections)
        {
            var group = SectionGroupOf(section.Label);
            if (groups.Count == 0 || groups[^1].Label != group)
            {
                groups.Add((group, new List<string>()));
            }
            groups[^1].Sections.Add(section.Label);
        }

        return groups
            .Select(g => BuildGroupCard(g.Label, SectionGroupIcon(g.Label)))
            .ToList();
    }

    private FrameworkElement BuildGroupCard(string groupLabel, string iconGlyph)
    {
        var card = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 72,
            Padding = new Thickness(16, 16, 16, 16),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 8),
            Content = new Grid { ColumnSpacing = 18 },
        };
        if (card.Content is Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new FontIcon
            {
                Glyph = iconGlyph,
                FontSize = 20,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = groupLabel, FontSize = 14 });
            textStack.Children.Add(new TextBlock
            {
                Text = AppContext.AppLang.AdvancedSettings,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var chevron = new FontIcon
            {
                Glyph = "\uE76C",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
        }
        AutomationProperties.SetName(card, $"{groupLabel}, {AppContext.AppLang.AdvancedSettings}");
        card.Click += (_, _) =>
        {
            _selectedSection = GroupSectionPrefix + groupLabel;
            UpdateOptions();
        };
        return card;
    }

    private string SectionGroupOf(string sectionLabel)
    {
        var lang = AppContext.AppLang;
        if (sectionLabel == lang.SectionProgramLanguageLog
            || sectionLabel == lang.SectionProgramConfig
            || sectionLabel == lang.SectionProgramTesting
            || sectionLabel == lang.SectionProgramAssociations)
            return lang.SettingsGroupProgramConfigFiles;
        if (sectionLabel == lang.SectionReversePlayback
            || sectionLabel == lang.SectionPlaybackSeeking
            || sectionLabel == lang.SectionPlaybackSeekPreview)
            return lang.SettingsGroupPlaybackNavigation;
        if (sectionLabel == lang.SectionWatchLaterResume
            || sectionLabel == lang.SectionWatchLaterStorage
            || sectionLabel == lang.SectionDemuxerPlaylist)
            return lang.SettingsGroupFilesPlaylists;
        if (sectionLabel == lang.SectionVideoDecode
            || sectionLabel == lang.SectionVideoSync)
            return lang.SettingsGroupVideoDecodeSync;
        if (sectionLabel == lang.SectionVideoImage
            || sectionLabel == lang.SectionVideoFilters)
            return lang.SettingsGroupVideoImageFilters;
        if (sectionLabel == lang.SectionGpuScaling
            || sectionLabel == lang.SectionGpuInterpolation
            || sectionLabel == lang.SectionGpuShaders
            || sectionLabel == lang.SectionGpuBackground
            || sectionLabel == lang.SectionGpuD3d11)
            return lang.SettingsGroupGpuRendering;
        if (sectionLabel == lang.SectionToneMapping
            || sectionLabel == lang.SectionTargetColorspace
            || sectionLabel == lang.SectionColorManagement)
            return lang.SettingsGroupColorHdr;
        if (sectionLabel == lang.SectionAudioOutput
            || sectionLabel == lang.SectionAudioVolume)
            return lang.SettingsGroupAudioOutput;
        if (sectionLabel == lang.SectionAudioExternal
            || sectionLabel == lang.SectionAudioCoverArt)
            return lang.SettingsGroupAudioExternalCover;
        if (sectionLabel == lang.SectionSubtitleText
            || sectionLabel == lang.SectionSubtitleStyle
            || sectionLabel == lang.SectionSubtitlePosition
            || sectionLabel == lang.SectionSubtitleBehavior)
            return lang.SettingsGroupSubtitleText;
        if (sectionLabel == lang.SectionSubtitleAss)
            return lang.SettingsGroupSubtitleAss;
        if (sectionLabel == lang.SectionSubtitleImage)
            return lang.SettingsGroupSubtitleImage;
        if (sectionLabel == lang.SectionWindow)
            return lang.SettingsGroupWindowBehavior;
        if (sectionLabel == lang.SectionWindowPiP)
            return lang.SettingsGroupWindowPip;
        if (sectionLabel == lang.SectionNetworkYtdlp)
            return lang.SettingsGroupNetworkYtdlp;
        if (sectionLabel == lang.SectionNetworkHttp
            || sectionLabel == lang.SectionNetworkCurl)
            return lang.SettingsGroupNetworkHttpCurl;
        if (sectionLabel == lang.SectionCache
            || sectionLabel == lang.SectionDemuxerBuffering)
            return lang.SettingsGroupCache;
        if (sectionLabel == lang.SectionOsdAppearance
            || sectionLabel == lang.SectionOsdBehavior
            || sectionLabel == lang.SectionOsdPosition)
            return lang.SettingsGroupOsdAppearance;
        if (sectionLabel == lang.SectionOsdMetadata)
            return lang.SettingsGroupOsdMetadata;
        if (sectionLabel == lang.SectionScreenshotLocation)
            return lang.SettingsGroupScreenshotLocation;
        if (sectionLabel == lang.SectionScreenshotQuality)
            return lang.SettingsGroupScreenshotQuality;
        return AppContext.AppLang.MoreSettings;
    }

    private static string SectionGroupIcon(string groupLabel)
    {
        return groupLabel switch
        {
            _ when groupLabel == AppContext.AppLang.SettingsGroupProgramConfigFiles => "\uE838",
            _ when groupLabel == AppContext.AppLang.SettingsGroupPlaybackNavigation => "\uE786",
            _ when groupLabel == AppContext.AppLang.SettingsGroupFilesPlaylists => "\uE8FD",
            _ when groupLabel == AppContext.AppLang.SettingsGroupNetworkCache => "\uE774",
            _ when groupLabel == AppContext.AppLang.SettingsGroupVideoDecodeSync => "\uE9D9",
            _ when groupLabel == AppContext.AppLang.SettingsGroupVideoImageFilters => "\uE7F4",
            _ when groupLabel == AppContext.AppLang.SettingsGroupGpuRendering => "\uE771",
            _ when groupLabel == AppContext.AppLang.SettingsGroupColorHdr => "\uE790",
            _ when groupLabel == AppContext.AppLang.SettingsGroupAudioOutput => "\uE767",
            _ when groupLabel == AppContext.AppLang.SettingsGroupAudioExternalCover => "\uEB9F",
            _ when groupLabel == AppContext.AppLang.SettingsGroupSubtitleText => "\uE90B",
            _ when groupLabel == AppContext.AppLang.SettingsGroupSubtitleAss => "\uE8D2",
            _ when groupLabel == AppContext.AppLang.SettingsGroupSubtitleImage => "\uE91B",
            _ when groupLabel == AppContext.AppLang.SettingsGroupWindowBehavior => "\uE8A4",
            _ when groupLabel == AppContext.AppLang.SettingsGroupWindowPip => "\uEE49",
            _ when groupLabel == AppContext.AppLang.SettingsGroupNetworkYtdlp => "\uE717",
            _ when groupLabel == AppContext.AppLang.SettingsGroupNetworkHttpCurl => "\uE702",
            _ when groupLabel == AppContext.AppLang.SettingsGroupCache => "\uE74E",
            _ when groupLabel == AppContext.AppLang.SettingsGroupOsdAppearance => "\uE932",
            _ when groupLabel == AppContext.AppLang.SettingsGroupOsdMetadata => "\uEA8F",
            _ when groupLabel == AppContext.AppLang.SettingsGroupScreenshotLocation => "\uE8B5",
            _ when groupLabel == AppContext.AppLang.SettingsGroupScreenshotQuality => "\uE740",
            _ => "\uE713",
        };
    }

    /// <summary>Windows-Settings-like navigation card for one section: a
    /// leading glyph, title with a secondary description line, chevron.</summary>
    private FrameworkElement BuildSectionCard(string label)
    {
        var (icon, description) = SectionMeta(label);
        var card = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 72,
            Padding = new Thickness(16, 16, 16, 16),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 8),
            Content = new Grid { ColumnSpacing = 18 },
        };
        if (card.Content is Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new FontIcon
            {
                Glyph = icon,
                FontSize = 20,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = label, FontSize = 14 });
            if (!string.IsNullOrEmpty(description))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                });
            }
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var chevron = new FontIcon
            {
                Glyph = "\uE76C",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
        }
        AutomationProperties.SetName(card, string.IsNullOrEmpty(description) ? label : $"{label}, {description}");
        card.Click += (_, _) =>
        {
            _selectedSection = label;
            UpdateOptions();
        };
        return card;
    }

    /// <summary>Icon glyph and description line for a section card, keyed by
    /// the localized section label so no extra identifier has to travel with
    /// Option. Unlisted labels fall back to a generic folder card.</summary>
    private static (string Icon, string Description) SectionMeta(string label)
    {
        var lang = AppContext.AppLang;
        return label switch
        {
            var l when l == lang.SectionProgramInterface => ("\uE771", lang.SectionDescProgramInterface),
            var l when l == lang.SectionProgramLanguageLog => ("\uE8C1", lang.SectionDescProgramLanguageLog),
            var l when l == lang.SectionProgramConfig => ("\uECC5", lang.SectionDescProgramConfig),
            var l when l == lang.SectionProgramAssociations => ("\uE8E5", lang.SectionDescProgramAssociations),
            var l when l == lang.SectionProgramTesting => ("\uE9D9", lang.SectionDescProgramTesting),
            var l when l == lang.SectionPlayback => ("\uE102", lang.SectionDescPlayback),
            var l when l == lang.SectionPlaybackSeeking => ("\uE786", lang.SectionDescPlaybackSeeking),
            var l when l == lang.SectionPlaybackSeekPreview => ("\uE720", lang.SectionDescPlaybackSeekPreview),
            var l when l == lang.SectionReversePlayback => ("\uE8AB", lang.SectionDescReversePlayback),
            var l when l == lang.SectionWatchLaterResume => ("\uE8E6", lang.SectionDescWatchLaterResume),
            var l when l == lang.SectionWatchLaterStorage => ("\uE8B7", lang.SectionDescWatchLaterStorage),
            var l when l == lang.SectionVideoDecode => ("\uE714", lang.SectionDescVideoDecode),
            var l when l == lang.SectionVideoImage => ("\uE7F4", lang.SectionDescVideoImage),
            var l when l == lang.SectionVideoFilters => ("\uE70F", lang.SectionDescVideoFilters),
            var l when l == lang.SectionVideoSync => ("\uE895", lang.SectionDescVideoSync),
            var l when l == lang.SectionGpuScaling => ("\uE71D", lang.SectionDescGpuScaling),
            var l when l == lang.SectionGpuInterpolation => ("\uEDD5", lang.SectionDescGpuInterpolation),
            var l when l == lang.SectionColorManagement => ("\uE790", lang.SectionDescColorManagement),
            var l when l == lang.SectionGpuShaders => ("\uE9D2", lang.SectionDescGpuShaders),
            var l when l == lang.SectionGpuBackground => ("\uE756", lang.SectionDescGpuBackground),
            var l when l == lang.SectionToneMapping => ("\uE706", lang.SectionDescToneMapping),
            var l when l == lang.SectionTargetColorspace => ("\uE914", lang.SectionDescTargetColorspace),
            var l when l == lang.SectionTrackLanguage => ("\uE7F8", lang.SectionDescTrackLanguage),
            var l when l == lang.SectionDemuxerBuffering => ("\uEC4E", lang.SectionDescDemuxerBuffering),
            var l when l == lang.SectionDemuxerPlaylist => ("\uE8FD", lang.SectionDescDemuxerPlaylist),
            var l when l == lang.SectionCache => ("\uE74E", lang.SectionDescCache),
            var l when l == lang.SectionNetworkHttp => ("\uE702", lang.SectionDescNetworkHttp),
            var l when l == lang.SectionNetworkCurl => ("\uE8EA", lang.SectionDescNetworkCurl),
            var l when l == lang.SectionNetworkYtdlp => ("\uE717", lang.SectionDescNetworkYtdlp),
            var l when l == lang.SectionAudioVolume => ("\uE76E", lang.SectionDescAudioVolume),
            var l when l == lang.SectionAudioOutput => ("\uE7F6", lang.SectionDescAudioOutput),
            var l when l == lang.SectionAudioExternal => ("\uE8D6", lang.SectionDescAudioExternal),
            var l when l == lang.SectionAudioCoverArt => ("\uEB9F", lang.SectionDescAudioCoverArt),
            var l when l == lang.SectionSubtitleBehavior => ("\uE7DE", lang.SectionDescSubtitleBehavior),
            var l when l == lang.SectionSubtitleText => ("\uE90B", lang.SectionDescSubtitleText),
            var l when l == lang.SectionSubtitleStyle => ("\uE8D2", lang.SectionDescSubtitleStyle),
            var l when l == lang.SectionSubtitlePosition => ("\uE787", lang.SectionDescSubtitlePosition),
            var l when l == lang.SectionSubtitleAss => ("\uE7DE", lang.SectionDescSubtitleAss),
            var l when l == lang.SectionSubtitleImage => ("\uE91B", lang.SectionDescSubtitleImage),
            var l when l == lang.SectionTrackSelection => ("\uE142", lang.SectionDescTrackSelection),
            var l when l == lang.SectionTrackFallback => ("\uE72E", lang.SectionDescTrackFallback),
            var l when l == lang.SectionOsd => ("\uE932", lang.SectionDescOsd),
            var l when l == lang.SectionOsdBehavior => ("\uE7EE", lang.SectionDescOsdBehavior),
            var l when l == lang.SectionOsdAppearance => ("\uE8D2", lang.SectionDescOsdAppearance),
            var l when l == lang.SectionOsdPosition => ("\uE787", lang.SectionDescOsdPosition),
            var l when l == lang.SectionOsdMetadata => ("\uEA8F", lang.SectionDescOsdMetadata),
            var l when l == lang.SectionScreenshotLocation => ("\uE8B5", lang.SectionDescScreenshotLocation),
            var l when l == lang.SectionScreenshotQuality => ("\uE740", lang.SectionDescScreenshotQuality),
            var l when l == lang.SectionWindow => ("\uE745", lang.SectionDescWindow),
            var l when l == lang.SectionWindowPiP => ("\uEE49", lang.SectionDescWindowPiP),
            _ => ("\uE8B7", string.Empty),
        };
    }

    }
