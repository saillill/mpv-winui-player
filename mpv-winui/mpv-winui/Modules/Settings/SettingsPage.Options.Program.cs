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
        var shortcuts = AppContext.AppLang.SettingsCategoryShortcuts;
        var osd = AppContext.AppLang.SettingsCategoryOsd;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
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
                    new OptionChoice(AppSettings.BackdropType_Mica, lang.OptionValueBackdropMica),
                    new OptionChoice(AppSettings.BackdropType_Acrylic, lang.OptionValueBackdropAcrylic),
                    new OptionChoice(AppSettings.BackdropType_None, lang.OptionValueBackdropNone),
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
                Key = nameof(AppContext.AppSetting.UiFont),
                Description = lang.SettingsHelpUiFont,
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
                Key = nameof(AppContext.AppSetting.TestMpvCommandLog),
                Label = lang.SettingsTestMpvCommandLog,
                // Developer/diagnostic option, grouped under Program.
                Category = program,
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
                Category = osd,
                Section = sProgramTesting,
                Description = lang.SettingsHelpTestOsdMessage,
                // One-shot test is an action, not a toggle: a button keeps the
                // control semantics honest (the old Boolean switch always read
                // "off" and only fired on the rising edge).
                Type = OptionType.Action,
                ActionKind = OptionActionKind.Button,
                ActionLabel = lang.SettingsTestOsdMessage,
                ActionHandler = _ => AppContext.SendMpvCommand("show-text \"mpv-winui OSD test\"")
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestSignal),
                Description = lang.SettingsHelpTestSignal,
                Label = lang.SettingsTestSignal,
                Category = playback,
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
                Category = program,
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
                Description = lang.SettingsHelpAlwaysOnTop,
                Label = lang.SettingsAlwaysOnTop,
                Category = window,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AlwaysOnTop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AlwaysOnTop), AppContext.AppSetting.AlwaysOnTop = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InputIme),
                Description = lang.SettingsHelpInputIme,
                Label = lang.SettingsInputIme,
                Category = program,
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
}
