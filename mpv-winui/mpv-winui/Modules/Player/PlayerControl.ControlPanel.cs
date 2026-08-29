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
                // audio/video/subtitle tabs. 440 gives all three pages
                // enough room to render without their inner scrollbars
                // (audio's EQ card is the tallest) while staying inside
                // the flyout presenter's 480 cap.
                // User preference: roomy panel with fixed size so tabs
                // never resize the flyout.
                Height = 440,
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
                // EQ/移动/按钮 cards) - removed. Each tab scrolls inside the
                // fixed-height pivot below (audio's cards exceed 400px).
                var cards = new StackPanel
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                build(cards);
                var scroller = new ScrollViewer
                {
                    Content = cards,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalScrollMode = ScrollMode.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollMode = ScrollMode.Auto,
                    Padding = new Thickness(0, 0, 4, 0),
                };
                pivot.Items.Add(new PivotItem { Header = header, Content = scroller });
            }
            ControlPanelHost.ContentPanel.Children.Add(pivot);
        }
    }
}
