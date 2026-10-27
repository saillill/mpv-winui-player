using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;
using mpv_winui.Modules.Common.View;
using System;
using System.IO;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// The playing page's playlist side panel.
    ///
    /// The panel is the official player's shape: a playlist that comes IN over
    /// the picture from the right edge, briefly, and goes away again - not a
    /// column that permanently takes width away from the video. It is also the
    /// shape this app already uses for the navigation pane, which is why the
    /// surface, the header treatment and the scrim are deliberately the same
    /// ones: two panels that behave alike should look alike.
    ///
    /// It is an OVERLAY over both states rather than part of the library: it
    /// works while the video is on screen (which is when you want it) and it also
    /// works over the queue section, because it is not the section - it is a
    /// separate surface that happens to read the same collection.
    ///
    /// One collection, two on-screen readers. The queue section and this panel
    /// both bind to PlaylistItems, so there is no second list to keep in step -
    /// which is what the old three-column playlist could never promise.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        private bool _playlistPanelOpen;

        /// <summary>Opens or closes the pop-out playlist panel.</summary>
        internal void TogglePlaylistPanel()
        {
            if (_playlistPanelOpen)
            {
                ClosePlaylistPanel();
            }
            else
            {
                OpenPlaylistPanel();
            }
        }

        private void OpenPlaylistPanel()
        {
            // Anchored to the button that opened it, offset so the panel's right
            // edge lines up with the button rather than its left - the panel is
            // wide and the button is at the right end of the menu row, so
            // left-aligning it would push the panel off the window.
            PlaylistPanelPopup.XamlRoot ??= XamlRoot;

            PlaylistPanelPopup.IsOpen = true;

            _playlistPanelOpen = true;

            PositionPlaylistPanel();
        }

        /// <summary>The tool key that opens and closes the panel.</summary>
        private void PlaylistPanelButton_Click(object sender, RoutedEventArgs e) =>
            TogglePlaylistPanel();

        private void PlaylistPanelCloseButton_Click(object sender, RoutedEventArgs e) =>
            ClosePlaylistPanel();

        private void ClosePlaylistPanel()
        {
            PlaylistPanelPopup.IsOpen = false;

            _playlistPanelOpen = false;
        }

        /// <summary>
        /// Places the popup under the playlist key, right-aligned to it.
        ///
        /// PointToWindow coordinates are window-relative and the popup is
        /// unconstrained, so this has to clamp to the display's work area itself -
        /// nothing else will pull it back on screen. Same rule the control panel
        /// and track selector follow.
        /// </summary>
        private void PositionPlaylistPanel()
        {
            if (PlaylistPanelButton.XamlRoot is not { } root)
            {
                return;
            }

            var origin = PlaylistPanelButton
                .TransformToVisual(root.Content)
                .TransformPoint(new Point(0, PlaylistPanelButton.ActualHeight));

            var width = PlaylistPanelSurface.Width;

            var x = origin.X + PlaylistPanelButton.ActualWidth - width;

            var y = origin.Y;

            // Keep it on the display: the popup is unconstrained by design, so
            // this is the only thing standing between a narrow window and a panel
            // hanging in space.
            var area = DisplayArea.GetFromWindowId(
                App.Window!.AppWindow.Id,
                DisplayAreaFallback.Primary).WorkArea;

            var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1;

            var minX = (area.X - App.Window.AppWindow.Position.X) / scale;
            var maxX = (area.X + area.Width - App.Window.AppWindow.Position.X) / scale - width;

            x = Math.Clamp(x, minX, Math.Max(minX, maxX));

            PlaylistPanelPopup.HorizontalOffset = x;

            PlaylistPanelPopup.VerticalOffset = y;
        }

        /// <summary>
        /// One playback-level message, replacing whatever was showing.
        ///
        /// One InfoBar instance in the page rather than one per kind of message:
        /// these are all "something you just did did not fully work", they never
        /// need answering, and the newest is the only one worth reading. The
        /// caller supplies severity, title and body so the wording stays at the
        /// call site that knows what happened.
        /// </summary>
        private void ShowPlaybackInfoBar(InfoBarSeverity severity, string title, string message)
        {
            PlaybackInfoBar.Severity = severity;

            PlaybackInfoBar.Title = title;

            PlaybackInfoBar.Message = message;

            PlaybackInfoBar.IsOpen = true;
        }

        /// <summary>
        /// The screenshot key. Routed through the same mpv command the menu's
        /// Screenshot item issues, so the two entry points cannot take different
        /// screenshots.
        /// </summary>
        /// <summary>
        /// The screenshot key: a flyout of the four ways mpv can take one, then
        /// the file it wrote.
        ///
        /// mpv's screenshot command takes mode flags (subtitles / video), and the
        /// app used to issue the bare "screenshot" command with no way to choose -
        /// so "without subtitles" existed in the menu but not on this key, and the
        /// user never learned where the file went. Both are fixed here: the flyout
        /// is the choice, the InfoBar is the answer.
        /// </summary>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e) =>
            ShowScreenshotMenu();

        private void ShowScreenshotMenu()
        {
            var lang = AppContext.AppLang;

            var flyout = new MenuFlyout();

            void Add(string text, string mode)
            {
                var item = new MenuFlyoutItem { Text = text };

                item.Click += (_, _) => TakeScreenshot(mode);

                flyout.Items.Add(item);
            }

            Add(lang.ScreenshotWithSubtitles, "subtitles");
            Add(lang.ScreenshotWithoutSubtitles, "video");
            flyout.Items.Add(new MenuFlyoutSeparator());
            Add(lang.ScreenshotWindow, "window");
            Add(lang.ScreenshotVideoFrame, "video-window");

            flyout.ShowAt(ScreenshotButton);
        }

        /// <summary>
        /// Issues the screenshot and reports where it landed.
        ///
        /// The path is read back from mpv rather than guessed: screenshot-directory
        /// and screenshot-template are user settings, and a template can contain
        /// %-expansions, so only mpv knows the filename. It is appended to
        /// recently-used so the newest screenshot is the one reported.
        /// </summary>
        private async void TakeScreenshot(string mode)
        {
            var lang = AppContext.AppLang;

            try
            {
                // The app names the file, mpv writes it.
                //
                // Reading the path back is not possible: the native player has no
                // generic property getter, and even if it had one, the template
                // expands %-sequences that only mpv can resolve. Naming the file
                // here makes the destination knowable without guessing, which is
                // what the "show where it was saved" request needs.
                var directory = AppContext.AppSetting.ScreenshotDirectory;

                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "Screenshots");
                }

                Directory.CreateDirectory(directory);

                var fileName = $"MPV-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.TickCount64 % 100000}.png";

                var path = Path.Combine(directory, fileName);

                await _mediaPlayer.CommandArgsAsync(["screenshot-to-file", path, mode]);

                ShowPlaybackInfoBar(
                    InfoBarSeverity.Success,
                    lang.ScreenshotSucceededTitle,
                    string.Format(lang.ScreenshotSucceededDetail, path));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "screenshot failed, mode={}", mode);

                ShowPlaybackInfoBar(
                    InfoBarSeverity.Error,
                    lang.ScreenshotFailedTitle,
                    lang.ScreenshotFailedDetail);
            }
        }

        /// <summary>
        /// The media-info key: mpv's OWN stats overlay, not a window of ours.
        ///
        /// stats.lua ships with mpv, is already in the config directory, and
        /// renders the property list over the picture with the same bindings the
        /// command line uses. The app drew its own window instead, which meant a
        /// second, partial copy of the same information - and the request is
        /// explicit: "call mpv's built-in information rather than a self-drawn
        /// window".
        ///
        /// script-binding stats/display-stats-toggle rather than load-stats-overlay:
        /// the overlay script is deliberately not auto-loaded in this config (it
        /// would draw on top of the app's own chrome at every start), so the
        /// binding is how it is asked for on demand. The binding is what the
        /// overlay's own "i" key is wired to.
        /// </summary>
        private void MediaInfoButton_Click(object sender, RoutedEventArgs e) =>
            _ = _mediaPlayer.CommandArgsAsync(["script-binding", "stats/display-stats-toggle"]);

        /// <summary>
        /// Shows the three tool keys exactly while a file is up.
        ///
        /// Driven from the same place the video/library switch happens, so the
        /// keys cannot be on screen over the library - where "screenshot this
        /// grid" and "media info for nothing" are both meaningless.
        /// </summary>
        private void UpdatePlaybackTools(bool playing)
        {
            PlaybackTools.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The three keys' icons and labels. Assigned here rather than in markup
        /// because FluentIcons hands out a fresh Geometry per call and a shared
        /// one fails with E_INVALIDARG on the second use.
        /// </summary>
        private void ApplyPlaybackToolStrings()
        {
            var lang = AppContext.AppLang;

            ScreenshotIcon.Data = FluentIcons.Geometry(FluentIcons.Camera);

            MediaInfoIcon.Data = FluentIcons.Geometry(FluentIcons.Info);

            // The same glyph the library's playlists/queue rows use, so the
            // panel key and the list it opens read as one thing - which is what
            // "the playlist icon does not match the home playlist icon" was.
            PlaylistPanelIcon.Data = FluentIcons.Geometry(FluentIcons.List);

            ToolTipService.SetToolTip(ScreenshotButton, lang.FileScreenshot);

            ToolTipService.SetToolTip(MediaInfoButton, lang.ViewMediaInfo);

            ToolTipService.SetToolTip(PlaylistPanelButton, lang.Playlist);

            AutomationProperties.SetName(ScreenshotButton, lang.FileScreenshot);

            AutomationProperties.SetName(MediaInfoButton, lang.ViewMediaInfo);

            AutomationProperties.SetName(PlaylistPanelButton, lang.Playlist);
        }

        /// <summary>
        /// The panel's own strings. Called from ApplyLocalizedStrings so a
        /// language change reaches it like every other label.
        /// </summary>
        private void ApplyPlaylistPanelStrings()
        {
            var lang = AppContext.AppLang;

            PlaylistPanelTitle.Text = lang.Playlist;

            ToolTipService.SetToolTip(PlaylistPanelCloseButton, lang.Close);

            AutomationProperties.SetName(PlaylistPanelCloseButton, lang.Close);

            AutomationProperties.SetName(PlaylistPanelView, lang.Playlist);
        }

        private void PlaylistPanelView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PlaylistItem { } item)
            {
                _mediaPlayer.PlaylistPlayIndex(item.Index);
            }
        }

        private void PlaylistPanelView_ContextRequested(UIElement sender, ContextRequestedEventArgs args) =>
            // Same menu, same implementation as the queue section's rows: the two
            // views are two windows onto one list, so an action must not exist in
            // one and not the other.
            ShowPlaylistContextMenu(PlaylistPanelView, args);
    }
}
