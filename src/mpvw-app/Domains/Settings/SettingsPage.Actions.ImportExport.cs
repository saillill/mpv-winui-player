using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Platform.Activation;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Lexicon.Language;
using mpv_winui.Domains.MpvConf;
using mpv_winui.Domains.Playback;
using mpv_winui.Domains.Settings.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Domains.Settings;

public sealed partial class SettingsPage
{
    /// <summary>
    /// The in-app mpv.conf editor, kept so a second click brings the open window
    /// forward and re-reads the file instead of stacking a duplicate window on
    /// the same file. Cleared when it closes.
    /// </summary>
    private static MpvConfTextWindow? _mpvConfTextWindow;

    private void FireAndForgetExport()
    {
        _ = ExportConfigAsync();
    }

    private void FireAndForgetImport()
    {
        _ = ImportConfigAsync();
    }

    private async System.Threading.Tasks.Task ExportConfigAsync()
    {
        try
        {
            var owner = SettingsWindow.Instance?.AppWindow.Id ?? App.Window!.AppWindow.Id;
            var filePicker = new FileSavePicker(owner)
            {
                SuggestedFileName = "mpv-winui-settings.conf",
            };
            filePicker.FileTypeChoices["Settings"] = new List<string> { ".conf" };
            var file = await filePicker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var builder = new System.Text.StringBuilder();
            foreach (var entry in AppContext.AppSetting.ExportAll())
            {
                // InvariantCulture: export must round-trip through the
                // InvariantCulture-based import regardless of the user's
                // number format (comma-decimal regions would otherwise emit
                // "0,5" which fails to parse back on import).
                var text = entry.Value switch
                {
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => entry.Value?.ToString() ?? string.Empty,
                };
                builder.Append(entry.Key)
                    .Append('=')
                    .AppendLine(text);
            }
            await File.WriteAllTextAsync(file.Path, builder.ToString());
            // The handler that called us returned before the picker resolved,
            // so the control read ActionStatus while it was still empty. Setting
            // the status notifies the row, which is what makes the line appear
            // at all; without it the row reported nothing whether the export
            // succeeded or failed.
            SetActionStatus("ActionExportConfig", AppContext.AppLang.SettingsConfigExported);
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Export config failed");
            SetActionStatus("ActionExportConfig", AppContext.AppLang.SettingsConfigExportFailed);
        }
    }

    private async System.Threading.Tasks.Task ImportConfigAsync()
    {
        try
        {
            var owner = SettingsWindow.Instance?.AppWindow.Id ?? App.Window!.AppWindow.Id;
            var filePicker = new FileOpenPicker(owner);
            filePicker.FileTypeFilter.Add(".conf");
            filePicker.FileTypeFilter.Add("*");
            var file = await filePicker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var line in await File.ReadAllLinesAsync(file.Path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                var equals = trimmed.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..];
            }

            AppContext.AppSetting.ImportAll(values);

            // Importing writes settings the whole page renders from, so the
            // page is rebuilt rather than patched. That means:
            //   * the result line cannot live in this instance's dictionary --
            //     the next page gets a fresh one -- so it travels as part of
            //     the navigation parameter;
            //   * the search box has to travel too, because it is owned by the
            //     window and the new page would otherwise come up with
            //     SearchBox == null, which silently kills search (every query
            //     reads SearchBox?.Text and gives up when it is null).
            var status = AppContext.AppLang.SettingsConfigImported;
            Frame?.BackStack.Clear();
            Frame?.Navigate(typeof(SettingsPage), new SettingsNavigationArgs(
                SearchBox,
                "ActionImportConfig",
                status));
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Import config failed");
            // Nothing was rebuilt, so the line goes on the current instance.
            SetActionStatus("ActionImportConfig", AppContext.AppLang.SettingsConfigImportFailed);
        }
    }

    /// <summary>
    /// Opens mpv.conf in the app's own editor.
    ///
    /// This is the raw escape hatch the settings window promises: mpv.conf is
    /// the authority, so tens of options that have no row here are still
    /// reachable, and a hand-written line always beats both the managed block
    /// and everything the UI does at runtime.
    /// </summary>
    private void FireAndForgetOpenMpvConfEditor()
    {
        try
        {
            if (!ManagedMpvConfig.MpvConfExists)
            {
                // Nothing to edit. Saying so beats a window on an empty file,
                // which reads as the editor having failed to load.
                SetActionStatus("ActionOpenMpvConf", AppContext.AppLang.SettingsOpenMpvConfMissing);
                return;
            }

            if (_mpvConfTextWindow is null)
            {
                _mpvConfTextWindow = new MpvConfTextWindow();
                _mpvConfTextWindow.Closed += (_, _) =>
                {
                    _mpvConfTextWindow = null;

                    // The editor writes the file directly, so the page has to
                    // re-read what the file now names or every row keeps the
                    // "overridden by mpv.conf" verdict it had on open.
                    //
                    // No result line here, deliberately. Closing an editor that
                    // saves as it goes is not an action whose outcome needs
                    // announcing, and a green "saved" on the row said the same
                    // thing the file already says.
                    MpvConfOverrides.Refresh();
                };
            }
            else
            {
                // Already open: bring it forward on the file's current contents
                // rather than leaving a stale copy behind the settings window.
                _mpvConfTextWindow.ReloadFromDisk();
            }

            _mpvConfTextWindow.Activate();
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "open mpv.conf editor failed");
            SetActionStatus("ActionOpenMpvConf", AppContext.AppLang.SettingsOpenMpvConfMissing);
        }
    }

    /// <summary>
    /// Shows mpv.conf in Explorer with the file itself selected.
    ///
    /// Selecting rather than merely opening the folder: the user asked for this
    /// to know where the file lives, and a folder listing of a thousand files
    /// does not answer that.
    /// </summary>
    private void FireAndForgetRevealMpvConf()
    {
        try
        {
            var path = ManagedMpvConfig.MpvConfPath;
            if (!File.Exists(path))
            {
                SetActionStatus("ActionOpenMpvConf", AppContext.AppLang.SettingsOpenMpvConfMissing);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true,
            });
            SetActionStatus("ActionOpenMpvConf", string.Empty);
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "reveal mpv.conf failed");
        }
    }
}
