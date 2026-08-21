using Microsoft.UI.Xaml;
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
    public List<string> CategoryOrder { get; } = [];
    private string _actionStatus = string.Empty;
    private int _resetStatusGeneration;

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
        _rebuildingContent = true;
        try
        {
            CategoryOrder.Clear();
            var options = BuildSettings();
            Settings.Clear();
            Settings.AddRange(options);
            Categories.Clear();
            Categories.AddRange(CategoryOrder.Where(c => Settings.Any(o => o.Category == c)));
            RebuildSearchIndex();
            RebuildNavigationItems(selectedKey);
            ResetButton.Content = AppContext.AppLang.ResetCurrentCategory;
            ResetAllButton.Content = AppContext.AppLang.ResetAllSettings;
            SearchBox.PlaceholderText = AppContext.AppLang.Search;
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
            var index = Array.IndexOf(CategoryKeys, key);
            return index >= 0 && index < Categories.Count ? Categories[index] : null;
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
        for (var i = 0; i < Categories.Count && i < CategoryKeys.Length; i++)
        {
            var item = new NavigationViewItem
            {
                Content = Categories[i],
                Tag = CategoryKeys[i],
                Icon = new FontIcon
                {
                    Glyph = CategoryGlyphs[i],
                    FontFamily = CreateCategoryIconFont(),
                },
            };
            if (string.Equals(CategoryKeys[i], selectedKey, StringComparison.Ordinal))
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
        UpdateOptions();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
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
        SearchBox.ItemsSource = categoryMatches.Count > 0
            ? categoryMatches
            : optionMatches.Select(o => o.Category).Distinct().ToList();
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string category && Categories.Contains(category))
        {
            SearchBox.Text = string.Empty;
            SelectCategory(category);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
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
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(query))
        {
            // Global search results span categories, so "reset current
            // category" has no visible target until the query is cleared.
            ResetButton.IsEnabled = false;
            OptionsControl.OptionList = Settings.Where(o => FuzzyMatchOption(query, o)).ToList();
            return;
        }

        ResetButton.IsEnabled = true;
        var selected = CurrentCategory;
        OptionsControl.OptionList = selected is null
            ? Settings
            : Settings.Where(o => o.Category == selected).ToList();
    }

    }
