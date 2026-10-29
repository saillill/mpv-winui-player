using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// Quick-control panel lifecycle (open / language refresh). The section
    /// builders live in the QuickControlPanel.* partials; this file only
    /// wires the host and composes the tab shell.
    /// </summary>
    public sealed partial class PlayerControl
    {
        private bool _panelBuilt;

        /// <summary>The page stack the tab strip swaps between.
        ///
        /// This grid is also the panel's height container. The Pivot it
        /// replaced carried the fixed height itself and scrolled each page
        /// inside that box; a SelectorBar is only the strip, so the height that
        /// kept the three tabs from resizing each other has to live here, on
        /// the thing that actually holds the pages.
        /// </summary>
        private Grid? _panelPageHost;

        /// <summary>Panel geometry. The roomy size is deliberate: one fixed
        /// height keeps the three tabs from resizing each other, and because the
        /// popup is allowed to leave the window this size no longer has to be
        /// negotiated with the window it happens to be opened from.</summary>
        private const double PanelWidth = 560;
        private const double PanelHeight = 460;
        private const double PanelMinHeight = 320;

        /// <summary>Height of the panel's chrome: the border plus the tab strip.</summary>
        private const double PanelChromeHeight = 40;

        /// <summary>
        /// Gap between the anchor and the panel, in DIPs. Shared with the other
        /// player popups so they all sit the same distance off their button.
        /// </summary>
        private const double PopupGap = 8;

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
        /// Two height defects used to live here. The tab host was built at a
        /// fixed 440, but every open recomputed it from the space above the
        /// button -- and the first open runs before the host exists, so only the
        /// second and later opens saw the recomputation, which is why the panel
        /// filled the window on the second open. The presenter was then capped
        /// to the space above the button, which is what clipped a tall panel in
        /// a short window.
        ///
        /// The geometry is fixed now: the panel is bounded by the window height
        /// only so a very short window stays usable, and because the popup is
        /// explicitly not constrained to the window, a panel taller or wider
        /// than the space around the button still renders in full.
        /// </summary>
        private void OpenControlPanel(FrameworkElement anchor)
        {
            EnsureControlPanel();
            if (_panelPageHost is null)
            {
                return;
            }

            var window = anchor.XamlRoot?.Size ?? default;
            var available = window.Height > 0 ? window.Height - 16 : PanelHeight;
            var panelHeight = Math.Min(PanelHeight, Math.Max(PanelMinHeight, available));

            ControlPanelHost.Width = PanelWidth;
            _panelPageHost.Height = Math.Max(200, panelHeight - PanelChromeHeight);

            // Let the framework place it. Popup.DesiredPlacement plus a
            // PlacementTarget is the same anchoring a Flyout uses, and it is
            // what the first version of this got wrong: it computed absolute
            // window coordinates by hand and assigned them to
            // HorizontalOffset/VerticalOffset, which put the panel under the
            // window instead of above the button. The offsets below are a gap
            // relative to the anchor, not a position, so there is no coordinate
            // origin left to get wrong.
            // Top, not TopEdgeAlignedLeft: the panel hangs off the middle of its
            // button, the way the volume strip, the track selector and the More
            // menu already do. PlacementFor bends that to an edge-aligned
            // placement only when centring would push the panel off the display.
            ControlPanelPopup.PlacementTarget = anchor;
            ControlPanelPopup.DesiredPlacement = PlacementFor(anchor, PanelWidth);
            ControlPanelPopup.HorizontalOffset = 0;
            ControlPanelPopup.VerticalOffset = -PopupGap;
            ControlPanelPopup.IsOpen = true;
        }

        /// <summary>
        /// Shows the track selector above <paramref name="anchor"/>.
        ///
        /// The same anchoring and the same escape as the control panel: a Popup
        /// with ShouldConstrainToRootBounds off, positioned by the framework from
        /// its PlacementTarget. As a Flyout the selector was held inside the
        /// window and its list was cut off at the window edge.
        /// </summary>
        private void OpenTrackSelector(FrameworkElement anchor)
        {
            TrackSelectionPopup.PlacementTarget = anchor;
            TrackSelectionPopup.DesiredPlacement = PlacementFor(anchor, TrackSelectorWidth);
            TrackSelectionPopup.HorizontalOffset = 0;
            TrackSelectionPopup.VerticalOffset = -PopupGap;
            TrackSelectionPopup.IsOpen = true;
        }

        /// <summary>Size cap of the track selector, matching its XAML MaxWidth.</summary>
        private const double TrackSelectorWidth = 560;

        /// <summary>
        /// Picks the horizontal placement that keeps a popup on the display.
        ///
        /// <c>Top</c> centres the popup on its button, which is the house style.
        /// But these popups are deliberately <b>not</b> constrained to the window
        /// (that is the whole point of ShouldConstrainToRootBounds), so nothing
        /// pulls one back when centring it would run it off the edge of the
        /// screen -- the framework only constrains what it owns. When the centred
        /// box would not fit, the popup's edge is aligned to the button's edge
        /// instead: still directly above the button, just not centred on it.
        /// That is a bend, not a fallback -- it keeps every pixel on screen.
        ///
        /// The work area is read from the window's own DisplayArea, so a
        /// multi-monitor setup with different sizes is handled per window, and
        /// the taskbar is excluded because WorkArea is not Bounds.
        /// </summary>
        private static PopupPlacementMode PlacementFor(FrameworkElement anchor, double popupWidth)
        {
            if (anchor.XamlRoot?.Content is not UIElement root)
            {
                return PopupPlacementMode.Top;
            }
            if (App.Window?.AppWindow is not { } appWindow)
            {
                return PopupPlacementMode.Top;
            }

            // Physical pixels in, device-independent pixels out: the layout works
            // in DIPs and the display is described in raw pixels.
            var scale = anchor.XamlRoot.RasterizationScale;
            if (scale <= 0)
            {
                scale = 1;
            }

            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            if (area is null)
            {
                return PopupPlacementMode.Top;
            }

            // The usable strip, expressed relative to the window's own origin so
            // it can be compared with a point measured inside the XamlRoot.
            var work = area.WorkArea;
            var usableLeft = (work.X - appWindow.Position.X) / scale;
            var usableRight = (work.X + work.Width - appWindow.Position.X) / scale;

            var anchorLeft = anchor
                .TransformToVisual(root)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).X;
            var centred = anchorLeft + ((anchor.ActualWidth - popupWidth) / 2);

            if (centred < usableLeft)
            {
                return PopupPlacementMode.TopEdgeAlignedLeft;
            }
            if (centred + popupWidth > usableRight)
            {
                return PopupPlacementMode.TopEdgeAlignedRight;
            }
            return PopupPlacementMode.Top;
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

            // A SelectorBar is only the tab strip. It has no content host, so
            // the three pages are stacked in one grid cell and swapped by
            // Visibility from the SelectionChanged handler below.
            var bar = new SelectorBar
            {
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var pageHost = new Grid
            {
                // Fixed height keeps the panel the same size across the
                // audio/video/subtitle tabs. OpenControlPanel sets the value
                // actually used, from the panel geometry, before the popup is
                // shown; this is only the pre-open default.
                Height = PanelHeight - PanelChromeHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            _panelPageHost = pageHost;
            var pages = new (string Text, string Glyph, Action<StackPanel> Build)[]
            {
                (lang.SettingsCategoryAudio, "\uF472", root => ControlPanelHost.BuildAudio(root)),
                (lang.SettingsCategoryVideo, "\uF84D", root => ControlPanelHost.BuildVideo(root)),
                (lang.SettingsCategorySubtitles, "\uEBCD", root => ControlPanelHost.BuildSubtitles(root)),
            };
            var items = new List<SelectorBarItem>(pages.Length);
            var scrollers = new List<ScrollViewer>(pages.Length);
            foreach (var (text, glyph, build) in pages)
            {
                // Cards keep their natural height. The old filler-stretch
                // made the last card huge and hid its content (giant empty
                // EQ/moving/buttons cards) - removed. Each tab scrolls inside
                // the fixed-height page host below (audio's cards exceed
                // 400px).
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
                    // Only the selected page is laid out, so the three
                    // scrollers cannot each grab the host's height.
                    Visibility = Visibility.Collapsed,
                };
                scrollers.Add(scroller);
                pageHost.Children.Add(scroller);

                // Text plus Icon, nothing else. The item template in WinUI
                // 2.3.9 presents exactly PART_IconVisual (bound to Icon) and
                // PART_TextVisual (bound to Text); Child sits behind a bare
                // "Placeholder for Child UIElement" comment and is never
                // presented, so a composite header put there would render an
                // empty tab and raise nothing anywhere.
                var item = new SelectorBarItem
                {
                    Text = text,
                    FontSize = 12,
                    Icon = new FontIcon
                    {
                        Glyph = glyph,
                        FontSize = 16,
                        FontFamily = QuickControlPanel.PanelIconFont,
                    },
                };
                // The header is an icon plus text. WinUI leaves an item
                // unnamed whenever the caption is not a plain string on a
                // template part it owns, and this app measured exactly that
                // shape before the track selector's four tabs were named by
                // hand. Naming it here keeps the reads-the-same guarantee.
                AutomationProperties.SetName(item, text);
                items.Add(item);
                bar.Items.Add(item);
            }

            // Subscribed before the initial selection so the handler, not a
            // second hand-written line, is what decides which page is up.
            // Both orders end in the same state, and this one leaves a single
            // place that maps "selected item" to "visible page".
            bar.SelectionChanged += (sender, _) =>
            {
                var selected = sender.SelectedItem;
                for (var i = 0; i < items.Count; i++)
                {
                    scrollers[i].Visibility = ReferenceEquals(items[i], selected)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };
            // The first page is the default, matching the Pivot's own
            // selection of its first item. Setting it can be a no-op until the
            // bar is templated, so the fallback is stated here rather than
            // inferred: the audio page is what the panel opens on.
            scrollers[0].Visibility = Visibility.Visible;
            bar.SelectedItem = items[0];

            ControlPanelHost.ContentPanel.Children.Add(bar);
            ControlPanelHost.ContentPanel.Children.Add(pageHost);
        }
    }
}
