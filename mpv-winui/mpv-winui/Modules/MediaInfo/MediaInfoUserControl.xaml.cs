using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace mpv_winui.Modules.MediaInfo
{
    /// <summary>
    /// The media-info window's body.
    ///
    /// Upstream rendered the native MediaInfo dump as one Consolas TextBlock,
    /// which read like a log file rather than a Windows settings page. The
    /// report is now parsed into sections of label/value rows and rendered as
    /// WinUI cards, with the captions coming from AppLang like the rest of the
    /// app.</summary>
    public sealed partial class MediaInfoUserControl : UserControl
    {
        private static readonly Logger _logger = LogManager.GetLogger("MediaInfo");

        private readonly Task<string?>? _infoTask;
        private IReadOnlyList<MediaInfoSection> _sections = [];

        public MediaInfoUserControl(string? path)
        {
            this.InitializeComponent();

            ApplyLocalizedChrome();
            UpdateLoading(true);

            if (!string.IsNullOrEmpty(path))
            {
                _infoTask = ReadMediaInfoAsync(path);
            }
        }

        /// <summary>Fills the window's fixed chrome from the current language.</summary>
        private void ApplyLocalizedChrome()
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            ScopeBar.Title = lang.MediaInfoTitle;
            ScopeBar.Message = lang.ViewMediaInfo + " · " + lang.MediaInfoPoweredBy;
            LoadingText.Text = lang.MediaInfoLoading;
            PoweredByLink.Content = "MediaInfoLib";
            CopyAllText.Text = lang.MediaInfoCopyAll;
            ToolTipService.SetToolTip(CopyAllButton, lang.MediaInfoCopyAll);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_infoTask is { } task)
            {
                try
                {
                    ShowReport(await task);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MediaInfo read failed");
                    ShowError(ex.Message);
                }
            }
            else
            {
                ShowEmpty();
            }

            UpdateLoading(false);
        }

        private static Task<string?> ReadMediaInfoAsync(string path)
        {
            return Task.Run(() =>
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Reading media info, path={Path}", path);
                }

                using var mediaInfo = new NativeMediaInfo();
                return mediaInfo.Read(path);
            });
        }

        private void ShowReport(string? text)
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            _sections = MediaInfoReport.Parse(text, MediaInfoReport.AppLangStrings.FromCurrent());
            SectionsList.ItemsSource = _sections;

            // A file MediaInfo could open but not describe still deserves an
            // explanation rather than an empty pane.
            if (_sections.Count == 0)
            {
                ShowState(InfoBarSeverity.Informational, lang.MediaInfoFailed);
                CopyAllButton.IsEnabled = false;
            }
            else
            {
                StateBar.IsOpen = false;
                CopyAllButton.IsEnabled = true;
            }
        }

        private void ShowEmpty()
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            ShowState(InfoBarSeverity.Informational, lang.MediaInfoNoFile);
            CopyAllButton.IsEnabled = false;
        }

        private void ShowError(string message)
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            ShowState(InfoBarSeverity.Error, $"{lang.MediaInfoFailed}\n{message}");
            CopyAllButton.IsEnabled = false;
        }

        private void ShowState(InfoBarSeverity severity, string message)
        {
            StateBar.Severity = severity;
            StateBar.Message = message;
            StateBar.IsOpen = true;
        }

        private void UpdateLoading(bool show)
        {
            LoadingRing.IsActive = show;
            LoadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            LoadingText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CopySection_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is MediaInfoSection section)
            {
                CopyToClipboard(section.ToPlainText());
            }
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_sections.Count == 0)
            {
                return;
            }

            var text = string.Join(
                Environment.NewLine + Environment.NewLine,
                _sections.Select(s => $"{s.DisplayName}{Environment.NewLine}{s.ToPlainText()}"));
            CopyToClipboard(text);
        }

        /// <summary>
        /// Copies through a DataPackage (the WinUI 3 clipboard surface) and
        /// reports the outcome in the scope bar, so a failed copy is not
        /// silently swallowed.
        /// </summary>
        private void CopyToClipboard(string text)
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            try
            {
                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
                ScopeBar.Title = lang.MediaInfoCopied;
                ScopeBar.Severity = InfoBarSeverity.Success;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Copying media info to the clipboard failed");
                ScopeBar.Title = lang.MediaInfoFailed;
                ScopeBar.Severity = InfoBarSeverity.Error;
            }
        }
    }
}
