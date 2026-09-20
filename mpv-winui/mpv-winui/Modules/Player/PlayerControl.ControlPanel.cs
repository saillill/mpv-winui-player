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

        /// <summary>Panel geometry. The roomy size is deliberate: one fixed
        /// height keeps the three tabs from resizing each other, and because the
        /// popup is allowed to leave the window this size no longer has to be
        /// negotiated with the window it happens to be opened from.</summary>
        private const double PanelWidth = 440;
        private const double PanelHeight = 560;
        private const double PanelMinHeight = 320;

        /// <summary>Height of the panel's chrome: the border plus the tab strip.</summary>
        private const double PanelChromeHeight = 40;

        private void OnLanguageChanged()
        {
            // The quick-control panel captures AppLang strings when built;
            // drop it so the next open rebuilds with the new language (B5).
            _panelBuilt = false;
            if (ControlPanelPopup.IsOpen)
            {
                EnsureControlPanel();
            }
        }

        private void ControlPanelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControlPanelPopup.IsOpen)
            {
                ControlPanelPopup.IsOpen = false;
                return;
            }
            OpenControlPanel(ControlPanelButton);
        }

        private void ControlPanelPopup_Opened(object sender, object e)
        {
            EnsureControlPanel();
        }

        /// <summary>
        /// Shows the panel above <paramref name="anchor"/>.
        ///
        /// Two things used to go wrong here, and both were about height. The
        /// pivot was built at a fixed 440, but every open recomputed it from the
        /// space above the button -- and the first open runs before the pivot
        /// exists, so only the second and later opens saw the recomputation.
        /// That is why the panel filled the window on the second open. The
        /// presenter was then capped to the space above the button, which is
        /// what clipped a tall panel in a short window.
        ///
        /// The geometry is fixed now: the panel is capped by the window height
        /// only so a very short window stays usable, and because the popup is
        /// explicitly not constrained to the window, a panel taller or wider
        /// than the space around the button still renders in full.
        /// </summary>
        private void OpenControlPanel(FrameworkElement anchor)
        {
            EnsureControlPanel();
            if (_panelPivot is null)
            {
                return;
            }

            var window = anchor.XamlRoot?.Size ?? default;
            var available = window.Height > 0 ? window.Height - 16 : PanelHeight;
            var panelHeight = Math.Min(PanelHeight, Math.Max(PanelMinHeight, available));

            ControlPanelHost.Width = PanelWidth;
            _panelPivot.Height = Math.Max(200, panelHeight - PanelChromeHeight);

            var root = anchor.XamlRoot?.Content as UIElement;
            var origin = root is null
                ? new Windows.Foundation.Point(0, 0)
                : anchor.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));

            // Sit above the button with the left edges roughly in line. A
            // negative offset is meaningful now -- it is how a panel taller
            // than the space above the button keeps its top instead of losing
            // it -- so it is only pulled back when the panel does fit.
            var top = origin.Y - panelHeight - 8;
            if (top < 0 && panelHeight + 16 <= window.Height)
            {
                top = 8;
            }

            ControlPanelPopup.HorizontalOffset = Math.Max(0, origin.X - 8);
            ControlPanelPopup.VerticalOffset = top;
            ControlPanelPopup.IsOpen = true;
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
                // Fixed height keeps the panel the same size across the
                // audio/video/subtitle tabs. OpenControlPanel sets the value
                // actually used, from the panel geometry, before the popup is
                // shown; this is only the pre-open default.
                Height = PanelHeight - PanelChromeHeight,
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
