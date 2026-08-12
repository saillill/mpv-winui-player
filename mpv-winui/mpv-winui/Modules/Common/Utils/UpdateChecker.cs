using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.AppModel;
using NLog;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace mpv_winui.Modules.Common.Utils;

/// <summary>
/// Checks the GitHub Releases API for a newer version once per app session.
/// Silent by design: network failures or parse errors are logged, never
/// shown, and the user is only prompted when a real newer release exists.
/// </summary>
public static class UpdateChecker
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(UpdateChecker));

    private const string ReleasesUrl = "https://api.github.com/repos/saillill/mpv-winui-player/releases/latest";

    private static bool _checked;

    public static bool IsEnabled => AppContext.AppSetting.CheckForUpdates;

    public static async Task CheckForUpdatesAsync(XamlRoot? xamlRoot)
    {
        if (_checked || !IsEnabled)
        {
            return;
        }
        _checked = true;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("mpv-winui-player");
            client.Timeout = TimeSpan.FromSeconds(8);
            var json = await client.GetStringAsync(ReleasesUrl);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl)
                ? tagEl.GetString()
                : null;
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            var latest = ParseVersion(tag);
            var current = ParseVersion(PackageHelper.AppVersion);
            if (latest is null || current is null || latest <= current)
            {
                return;
            }

            var url = doc.RootElement.TryGetProperty("html_url", out var urlEl)
                ? urlEl.GetString()
                : $"https://github.com/saillill/mpv-winui-player/releases/tag/{tag}";
            _logger.Info("update available: {} (current {})", tag, PackageHelper.AppVersion);

            if (xamlRoot is null)
            {
                return;
            }
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.UpdateAvailableTitle,
                Content = AppContext.AppLang.UpdateAvailableMessage + $" {tag}",
                PrimaryButtonText = AppContext.AppLang.UpdateAvailableOpen,
                CloseButtonText = AppContext.AppLang.UpdateAvailableLater,
                XamlRoot = xamlRoot,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri(url));
            }
        }
        catch (Exception ex)
        {
            // Silent: update checking must never disturb playback.
            _logger.Debug("update check skipped: {}", ex.Message);
        }
    }

    private static Version? ParseVersion(string text)
    {
        var digits = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray())
            .Trim('.');
        return Version.TryParse(digits, out var v) ? v : null;
    }
}
