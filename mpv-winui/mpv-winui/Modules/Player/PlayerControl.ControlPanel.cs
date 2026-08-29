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
        private Pivot? _panelPivot;

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

        /// <summary>
        /// XAML popups clip at the window edge instead of repositioning, and
        /// the panel's 440-DIP pivot plus bar do not fit a small or high-DPI
        /// window (maximized at 175% scale leaves ~425 DIP above the bar) —
        /// the panel top was silently cut off. Build the presenter style per
        /// open with a MaxHeight capped to the space actually available above
        /// the button (Opening runs before the presenter is created); pages
        /// that no longer fit scroll inside instead of clipping, and the
        /// height stays identical across the three tabs.
        /// </summary>
        private void ControlPanelFlyout_Opening(object sender, object e)
        {
            var available = AvailableAbove(ControlPanelButton);
            if (_panelPivot is not null)
            {
                _panelPivot.Height = Math.Max(300, available - 8);
            }
            ControlPanelFlyout.FlyoutPresenterStyle = BuildPresenterStyle(
                (Style)Application.Current.Resources["AcrylicFlyoutPresenterStyle"],
                available, 560);
        }

        /// <summary>Same window-edge clip protection for the track selector.</summary>
        private void TrackSelectionFlyout_Opening(object sender, object e)
        {
            TrackSelectionFlyout.FlyoutPresenterStyle = BuildPresenterStyle(
                (Style)Application.Current.Resources["AcrylicFlyoutPresenterStyle"],
                AvailableAbove(TrackSelectionButton), 480);
        }

        private static Style BuildPresenterStyle(Style basedOn, double maxHeight, double maxWidth) => new(typeof(FlyoutPresenter))
        {
            BasedOn = basedOn,
            Setters =
            {
                new Setter(FlyoutPresenter.PaddingProperty, new Thickness(0)),
                new Setter(FlyoutPresenter.MaxWidthProperty, maxWidth),
                new Setter(FlyoutPresenter.MaxHeightProperty, maxHeight),
            },
        };

        /// <summary>DIPs from the window content top to the element top, minus a margin.</summary>
        private static double AvailableAbove(FrameworkElement anchor)
        {
            if (anchor.XamlRoot?.Content is UIElement contentRoot)
            {
                return Math.Max(320, anchor.TransformToVisual(contentRoot).TransformPoint(new Windows.Foundation.Point(0, 0)).Y - 12);
            }
            return 480;
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
            _panelPivot = pivot;
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
