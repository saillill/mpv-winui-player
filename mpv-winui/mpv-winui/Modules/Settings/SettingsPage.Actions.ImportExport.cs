using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage
{
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
            _actionStatus = AppContext.AppLang.SettingsConfigExported;
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Export config failed");
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
            _actionStatus = AppContext.AppLang.SettingsConfigImported;
            Frame?.BackStack.Clear();
            Frame?.Navigate(typeof(SettingsPage));
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Import config failed");
        }
    }

    /// <summary>
    /// Opens the deployed mpv.conf in whatever the user associates with .conf.
    ///
    /// This is the raw escape hatch the settings window promises: mpv.conf is
    /// the authority, so tens of options that have no row here are still
    /// reachable, and a hand-written line always beats both the managed block
    /// and everything the UI does at runtime.
    ///
    /// The file is opened through the shell rather than launched as a storage
    /// item: the whole point is editing it as text, and LaunchFileAsync would
    /// hand it to whatever app "opens" it, which for a .conf is often nothing
    /// or a browser instead of an editor.
    /// </summary>
    private void FireAndForgetOpenMpvConf()
    {
        _ = OpenMpvConfAsync();
    }

    private async Task OpenMpvConfAsync()
    {
        try
        {
            if (!ManagedMpvConfig.MpvConfExists)
            {
                // Nothing to edit. Saying so beats an explorer stub that would
                // leave the user staring at an empty folder.
                _actionStatus = AppContext.AppLang.SettingsOpenMpvConfMissing;
                return;
            }

            await Task.Run(() => Process.Start(new ProcessStartInfo
            {
                FileName = ManagedMpvConfig.MpvConfPath,
                UseShellExecute = true,
            }));

            // Re-read what the file now names, so rows the user has just taken
            // over pick up their "overridden by mpv.conf" marker without
            // waiting for a restart.
            MpvConfOverrides.Refresh();
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Open mpv.conf failed");
        }
    }
}
