using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    /// <summary>Navigation state used to keep the selected category and scroll position after a reset or language switch.</summary>
    public sealed record NavigationState(string? Category, double Offset);

    public SettingsPage()
    {
        InitializeComponent();
        var options = BuildSettings();
        Settings.AddRange(options);
        Categories.AddRange(CategoryOrder.Where(c => Settings.Any(o => o.Category == c)));
        CategoryList.ItemsSource = Categories;
        CategoryList.SelectedIndex = 0;
        ResetButton.Content = AppContext.AppLang.ResetCurrentCategory;
        ResetAllButton.Content = AppContext.AppLang.ResetAllSettings;
        SearchBox.PlaceholderText = AppContext.AppLang.Search;
        LoadSearchHistory();
        UpdateOptions();
        RefreshWarningsAndEnabled();
    }

    private const int MaxSearchHistory = 8;

    private void LoadSearchHistory()
    {
        var history = AppContext.AppSetting.SettingsSearchHistory
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(MaxSearchHistory)
            .ToList();
        if (history.Count > 0)
        {
            SearchBox.ItemsSource = history;
        }
    }

    private void RememberSearchQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }
        var history = AppContext.AppSetting.SettingsSearchHistory
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.Equals(x, query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        history.Insert(0, query);
        AppContext.AppSetting.SettingsSearchHistory = string.Join(",", history.Take(MaxSearchHistory));
    }

    /// <summary>The currently selected category (used before navigating away).</summary>
    public string? CurrentCategory => CategoryList.SelectedItem as string;

    /// <summary>The current vertical scroll offset of the options list.</summary>
    public double CurrentScrollOffset => OptionsControl.GetScrollOffset();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not NavigationState state)
        {
            return;
        }

        if (!string.IsNullOrEmpty(state.Category) && Categories.Contains(state.Category))
        {
            CategoryList.SelectedItem = state.Category;
        }

        if (state.Offset > 0)
        {
            var offset = state.Offset;
            DispatcherQueue.TryEnqueue(() => OptionsControl.SetScrollOffset(offset));
        }
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
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
        Frame?.BackStack.Clear();
        Frame?.Navigate(typeof(SettingsPage), new NavigationState(category, CurrentScrollOffset));
    }

    private async void OnResetAllClick(object sender, RoutedEventArgs e)
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

        var category = CurrentCategory;
        var offset = CurrentScrollOffset;
        AppContext.AppSetting.ResetAll();
        ResetShortcuts();
        UnassociateFiles();
        ApplyAfterReset();
        ShowResetStatus(AppContext.AppLang.SettingsResetAllDone);
        Frame?.BackStack.Clear();
        Frame?.Navigate(typeof(SettingsPage), new NavigationState(category, offset));
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

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOptions();

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = SearchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            CategoryList.ItemsSource = Categories;
            SearchBox.ItemsSource = null;
        }
        else
        {
            var matches = Categories
                .Where(c => FuzzyMatch(query, c))
                .ToList();
            CategoryList.ItemsSource = matches;
            SearchBox.ItemsSource = matches.Count > 0 ? matches : null;
        }

        if (CategoryList.Items.Count > 0)
        {
            CategoryList.SelectedIndex = 0;
        }
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string category && Categories.Contains(category))
        {
            CategoryList.SelectedItem = category;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is string suggested && Categories.Contains(suggested))
        {
            CategoryList.SelectedItem = suggested;
            RememberSearchQuery(suggested);
            return;
        }

        var query = sender.Text?.Trim() ?? string.Empty;
        var match = Categories.FirstOrDefault(c => FuzzyMatch(query, c));
        if (match is not null)
        {
            CategoryList.SelectedItem = match;
            RememberSearchQuery(query);
        }
    }

    private static bool FuzzyMatch(string query, string target)
    {
        if (ContainsFuzzy(query, target))
        {
            return true;
        }

        foreach (var alias in CategorySearchAliases(target))
        {
            if (ContainsFuzzy(query, alias))
            {
                return true;
            }
        }
        return false;
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
        var selected = CategoryList.SelectedItem as string;
        OptionsControl.OptionList = selected is null
            ? string.IsNullOrWhiteSpace(SearchBox.Text) ? Settings : []
            : Settings.Where(o => o.Category == selected).ToList();
    }

    }
