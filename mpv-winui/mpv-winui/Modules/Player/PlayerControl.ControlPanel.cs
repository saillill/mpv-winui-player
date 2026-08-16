using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Quick-control panel lifecycle (open / language refresh). The section
    /// builders live in the QuickControlPanel.* partials; this file only
    /// wires the host and composes the Pivot shell.
    /// </summary>
    public sealed partial class PlayerControl
    {
        private bool _panelBuilt;

        private void OnLanguageChanged()
        {
            // The quick-control panel captures AppLang strings when built;
            // drop it so the next open rebuilds with the new language (B5).
            _panelBuilt = false;
            if (ControlPanelFlyout.IsOpen)
            {
                EnsureControlPanel();
            }
        }

        private void ControlPanelFlyout_Opened(object sender, object e)
        {
            EnsureControlPanel();
        }

        private void EnsureControlPanel()
        {
            if (_panelBuilt)
            {
                return;
            }
            _panelBuilt = true;

            // Wire the host once per build; the section builders read these
            // on demand (mpv commands, equalizer state, gain list).
            ControlPanelHost.MediaPlayer = _mediaPlayer;
            ControlPanelHost.ApplyEqualizer = ApplyEqualizer;
            ControlPanelHost.EqGains = _eqGains;

            var lang = AppContext.AppLang;
            ControlPanelHost.ContentPanel.Children.Clear();

            var pivot = new Pivot
            {
                // Fixed height keeps the flyout the same size across the
                // audio/video/subtitle tabs; content taller than this scrolls
                // inside the ScrollViewer instead of resizing the popup.
                // Sized to the video page's natural height (4 slider rows +
                // two button rows) so video fills the panel with no large
                // blank area; still fixed so switching tabs never resizes
                // the flyout.
                Height = 360,
                Padding = new Thickness(0),
                IsHeaderItemsCarouselEnabled = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var pages = new (string Text, string Glyph, Action<StackPanel> Build)[]
            {
                (lang.SettingsCategoryAudio, "\uF472", root => ControlPanelHost.BuildAudio(root)),
                (lang.SettingsCategoryVideo, "\uF84D", root => ControlPanelHost.BuildVideo(root)),
                (lang.SettingsCategorySubtitles, "\uEBCD", root => ControlPanelHost.BuildSubtitles(root)),
            };
            foreach (var (text, glyph, build) in pages)
            {
                var header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                header.Children.Add(new FontIcon
                {
                    Glyph = glyph,
                    FontSize = 16,
                    FontFamily = QuickControlPanel.PanelIconFont,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                header.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                // Cards keep their natural height. The old filler-stretch
                // made the last card huge and hid its content (giant empty
                // EQ/移动/按钮 cards) - removed.
                var cards = new StackPanel
                {
                    // Keep every tab at the same visible height so switching
                    // audio/video/subtitles does not resize the flyout.
                    MinHeight = 320,
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                build(cards);
                // Never let a card stretch to fill leftover page height
                // (that was what inflated the EQ/移动/按钮 cards).
                foreach (var child in cards.Children)
                {
                    if (child is FrameworkElement fe)
                    {
                        fe.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                pivot.Items.Add(new PivotItem { Header = header, Content = cards });
            }
            ControlPanelHost.ContentPanel.Children.Add(pivot);
        }
    }
}
