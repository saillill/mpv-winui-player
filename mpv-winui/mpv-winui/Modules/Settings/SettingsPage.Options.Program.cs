using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
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

public sealed partial class SettingsPage
{
    private List<Option> BuildProgramBehaviorOptions()
    {
        var program = AppContext.AppLang.SettingsCategoryProgram;
        var input = AppContext.AppLang.SettingsCategoryInput;
        var testing = AppContext.AppLang.SettingsCategoryTesting;
        var window = AppContext.AppLang.SettingsCategoryWindow;
        var sProgramTesting = AppContext.AppLang.SectionProgramTesting;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Program Behavior =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeType),
                Label = lang.AppSettingTheme,
                Category = program,
                Description = lang.SettingsHelpTheme,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice(AppSettings.ThemeType_Auto, lang.ThemeAuto),
                    new OptionChoice(AppSettings.ThemeType_Light, lang.ThemeLightName),
                    new OptionChoice(AppSettings.ThemeType_Dark, lang.ThemeDarkName),
                    new OptionChoice(AppSettings.ThemeType_Custom, lang.ThemeCustomName),
                ],
                Getter = () => AppContext.AppSetting.ThemeType,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeType = (string)v;
                    UpdateTheme((string)v);
                    RefreshWarningsAndEnabled();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackdropType),
                Label = lang.Backdrop,
                Category = program,
                Description = lang.SettingsHelpBackdrop,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice(AppSettings.BackdropType_Acrylic, lang.OptionValueBackdropAcrylic),
                    new OptionChoice(AppSettings.BackdropType_Mica, lang.OptionValueBackdropMica),
                ],
                Getter = () => AppContext.AppSetting.BackdropType,
                Setter = v =>
                {
                    AppContext.AppSetting.BackdropType = (string)v;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.BackdropType), v);
                    RefreshWarningsAndEnabled();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeAccentColor),
                Label = lang.SettingsThemeAccentColor,
                Category = program,
                Description = lang.SettingsHelpThemeAccentColor,
                Type = OptionType.Color,
                Getter = () => AppContext.AppSetting.ThemeAccentColor,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeAccentColor = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeAccentColor), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeOpacity),
                Label = lang.SettingsThemeOpacity,
                Category = program,
                Description = lang.SettingsHelpThemeOpacity,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ThemeOpacity,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeOpacity = Convert.ToInt32(v);
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeOpacity), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeLuminosity),
                Label = lang.SettingsThemeLuminosity,
                Category = program,
                Description = lang.SettingsHelpThemeLuminosity,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ThemeLuminosity,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeLuminosity = Convert.ToInt32(v);
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeLuminosity), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.UiFont),
                Label = lang.SettingsUiFont,
                Category = program,
                Type = OptionType.StringList,
                Choices = SubtitleFontChoices(lang),
                Getter = () => AppContext.AppSetting.UiFont,
                Setter = v =>
                {
                    AppContext.AppSetting.UiFont = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.UiFont), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ControlBarLayout),
                Label = lang.SettingsControlBarLayout,
                Category = program,
                Description = lang.SettingsHelpControlBarLayout,
                Type = OptionType.Layout,
                LayoutChoices =
                [
                    new OptionLayoutChoice("classic", lang.OptionValueControlBarClassic),
                    new OptionLayoutChoice("modernx", lang.OptionValueControlBarModernX),
                ],
                Getter = () => NormalizeControlBarLayout(AppContext.AppSetting.ControlBarLayout),
                Setter = v =>
                {
                    AppContext.AppSetting.ControlBarLayout = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarLayout), v);
                },
                CheckItemsProviderForStyle = style => BuildControlBarIconItems(
                    style == "modernx"
                        ? nameof(AppSettings.ControlBarHiddenIconsModernX)
                        : nameof(AppSettings.ControlBarHiddenIconsClassic)),
                CheckChanged = (_, value, isChecked, target) => ApplyControlBarIcon(value, isChecked, target),
            },

            new Option
            {
                Key = "ControlBarCustomOrderAction",
                Label = lang.SettingsControlBarCustomOrder,
                Category = program,
                Description = lang.SettingsHelpControlBarCustomOrder,
                Type = OptionType.Action,
                ActionKind = OptionActionKind.Button,
                ActionLabel = lang.SettingsControlBarCustomOrder,
                ActionHandler = opt => { _ = ShowControlBarOrderDialogAsync(); },
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestMpvCommandLog),
                Label = lang.SettingsTestMpvCommandLog,
                Category = testing,
                Section = sProgramTesting,
                Description = lang.SettingsHelpTestMpvCommandLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TestMpvCommandLog,
                Setter = v => AppContext.AppSetting.TestMpvCommandLog = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestOsdMessage),
                Label = lang.SettingsTestOsdMessage,
                Category = testing,
                Section = sProgramTesting,
                Description = lang.SettingsHelpTestOsdMessage,
                // One-shot test: fire only when the toggle is turned on, so
                // switching it off again does not re-trigger the OSD.
                Type = OptionType.Boolean,
                Getter = () => false,
                Setter = v =>
                {
                    if (v is true)
                    {
                        AppContext.SendMpvCommand("show-text \"mpv-winui OSD test\"");
                    }
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestSignal),
                Label = lang.SettingsTestSignal,
                Category = testing,
                Section = sProgramTesting,
                Type = OptionType.StringList,
                AllowCustom = false,
                Choices =
                [
                    new OptionChoice("off", lang.OptionValueTestSignalOff),
                    new OptionChoice("testsrc2", lang.OptionValueTestSignalVideo),
                    new OptionChoice("sine", lang.OptionValueTestSignalAudio),
                ],
                Getter = () => AppContext.AppSetting.TestSignal,
                Setter = v =>
                {
                    AppContext.AppSetting.TestSignal = (string)v!;
                    var cmd = (string)v! switch
                    {
                        "testsrc2" => "loadfile \"lavfi://testsrc2=size=1280x720:rate=60\"",
                        "sine" => "loadfile \"lavfi://sine=frequency=1000:duration=60\"",
                        _ => null,
                    };
                    if (cmd is not null)
                    {
                        AppContext.SendMpvCommand(cmd);
                    }
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableDebugLog),
                Label = lang.DebugLog,
                Category = testing,
                Description = lang.SettingsHelpDebugLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableDebugLog,
                Setter = v =>
                {
                    AppContext.AppSetting.EnableDebugLog = (bool)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.EnableDebugLog), v);
                }
            },

            new Option
            {
                Key = "FileAssociationCheckList",
                Label = lang.SettingsAssociateFiles,
                Category = program,
                Description = lang.SettingsHelpFileAssociations,
                Type = OptionType.CheckList,
                CheckExpandLabel = lang.Expand,
                CheckCollapseLabel = lang.Collapse,
                CheckApplyLabel = lang.Apply,
                CheckItems = BuildAssociationItems(),
                CheckChanged = (_, value, isChecked, _) => UpdateAssociationSelection(value, isChecked),
                CheckApplyHandler = _ => ApplyAssociations(),
            },

            new Option
            {
                Key = "ActionUnassociateFiles",
                Label = lang.SettingsUnassociateFiles,
                Category = program,
                Description = lang.SettingsHelpUnassociateFiles,
                Type = OptionType.Action,
                ActionKind = OptionActionKind.Button,
                ActionLabel = lang.SettingsUnassociateFiles,
                ActionHandler = _ => UnassociateFiles(),
                ActionStatus = () => _actionStatus,
            },

            new Option
            {
                Key = "ActionExportConfig",
                Label = lang.SettingsExportConfig,
                Category = program,
                Description = lang.SettingsHelpExportConfig,
                Type = OptionType.Action,
                ActionKind = OptionActionKind.Button,
                ActionLabel = lang.SettingsExportConfig,
                ActionHandler = _ => FireAndForgetExport(),
                ActionStatus = () => _actionStatus,
            },

            new Option
            {
                Key = "ActionImportConfig",
                Label = lang.SettingsImportConfig,
                Category = program,
                Description = lang.SettingsHelpImportConfig,
                Type = OptionType.Action,
                ActionKind = OptionActionKind.Button,
                ActionLabel = lang.SettingsImportConfig,
                ActionHandler = _ => FireAndForgetImport(),
                ActionStatus = () => _actionStatus,
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AlwaysOnTop),
                Label = lang.SettingsAlwaysOnTop,
                Category = window,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AlwaysOnTop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AlwaysOnTop), AppContext.AppSetting.AlwaysOnTop = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InputIme),
                Label = lang.SettingsInputIme,
                Category = input,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.InputIme,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InputIme), AppContext.AppSetting.InputIme = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = lang.SettingLanguages,
                Category = program,
                Description = lang.SettingsHelpLanguage,
                Type = OptionType.StringList,
                Choices = AppContext.AvailableLanguages()
                    .Select(code => new OptionChoice(code, AppLang.NativeLanguageName(code)))
                    .ToList(),
                Getter = () =>
                {
                    var lang = AppContext.AppSetting.CurrentLanguage;
                    return string.IsNullOrEmpty(lang) ? "en-US" : lang;
                },
                Setter = v =>
                {
                    var newLang = (string)v!;
                    var current = AppContext.AppSetting.CurrentLanguage;
                    if (string.IsNullOrEmpty(current)) current = "en-US";
                    if (current == newLang) return; // 控件初始化回填不视为用户改动
                    AppContext.SwitchLanguage(newLang);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CheckForUpdates),
                Label = lang.SettingsCheckForUpdates,
                Category = program,
                Description = lang.SettingsCheckForUpdatesDescription,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CheckForUpdates,
                Setter = v => AppContext.AppSetting.CheckForUpdates = (bool)v!
            },

        ];
    }

    private async System.Threading.Tasks.Task ShowControlBarOrderDialogAsync()
    {
        // Reorderable buttons (ids from BuildControlBarIconItems). The
        // transport group (play/prev/next/skips) is fixed and shown as
        // locked cells at the top of the canvas.
        var orderable = new List<string>
        {
            "volume", "tracks", "random", "speed", "aspect",
            "fullwindow", "fullscreen", "pip",
        };
        var saved = AppContext.AppSetting.ControlBarCustomOrder
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(orderable.Contains)
            .ToList();
        // Keep any saved entries first, then append the rest in default order.
        foreach (var id in orderable)
        {
            if (!saved.Contains(id, StringComparer.Ordinal))
            {
                saved.Add(id);
            }
        }

        var lang = AppContext.AppLang;
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["volume"] = lang.ControlBarIconVolume,
            ["tracks"] = lang.ControlBarIconTracks,
            ["random"] = lang.ControlBarIconRandom,
            ["speed"] = lang.ControlBarIconSpeed,
            ["aspect"] = lang.ControlBarIconAspect,
            ["fullwindow"] = lang.ControlBarIconFullWindow,
            ["fullscreen"] = lang.ControlBarIconFullScreen,
            ["pip"] = lang.ControlBarIconPiP,
        };

        var list = new ListBox { MinHeight = 240, MaxHeight = 280 };
        void RefreshList()
        {
            list.Items.Clear();
            for (var i = 0; i < saved.Count; i++)
            {
                list.Items.Add($"{i + 1}. {labels[saved[i]]}");
            }
        }
        RefreshList();

        var up = new Button { Content = "↑" };
        var down = new Button { Content = "↓" };
        up.Click += (_, _) => MoveOrderItem(-1);
        down.Click += (_, _) => MoveOrderItem(1);
        void MoveOrderItem(int delta)
        {
            var index = list.SelectedIndex;
            var target = index + delta;
            if (index < 0 || target < 0 || target >= saved.Count)
            {
                return;
            }
            (saved[target], saved[index]) = (saved[index], saved[target]);
            RefreshList();
            list.SelectedIndex = target;
        }

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = lang.SettingsHelpControlBarCustomOrder, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Border
        {
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.25 },
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = lang.SettingsControlBarFixedGroup, TextWrapping = TextWrapping.Wrap },
        });
        panel.Children.Add(list);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        var resetOrderButton = new Button { Content = lang.SettingsControlBarResetOrder };
        resetOrderButton.Click += (_, _) =>
        {
            saved.Clear();
            saved.AddRange(orderable);
            RefreshList();
        };
        buttons.Children.Add(resetOrderButton);
        panel.Children.Add(buttons);

        var dialog = new ContentDialog
        {
            Title = lang.SettingsControlBarCustomOrder,
            Content = panel,
            PrimaryButtonText = lang.Save,
            CloseButtonText = lang.Cancel,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppContext.AppSetting.ControlBarCustomOrder = string.Join(',', saved);
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ControlBarCustomOrder), string.Join(',', saved));
        }
    }
}
