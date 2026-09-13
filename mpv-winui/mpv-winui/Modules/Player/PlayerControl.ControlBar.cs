using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winrt;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Control-bar composition: PiP bar switching, layout/zones application,
    /// width-adaptive states and the narrow-window overflow menu.
    /// </summary>
    public sealed partial class PlayerControl
    {
            private void OnPiPClick(object sender, RoutedEventArgs e)
            {
                AppContext.AppSetting.WindowPiP = !AppContext.AppSetting.WindowPiP;
                AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.WindowPiP), AppContext.AppSetting.WindowPiP);
                UpdatePiPButton();
            }
    
            private void UpdatePiPButton()
            {
                PiPButton.IsChecked = AppContext.AppSetting.WindowPiP;
                // Fluent PiP glyphs: E97E = enter PiP, E981 = exit PiP.
                PiPSymbol.Glyph = AppContext.AppSetting.WindowPiP ? "\uE981" : "\uE97E";
            }
    
            /// <summary>
            /// Hides the main control bar while the dedicated PiP window is shown.
            /// The PiP window has its own compact controls.
            /// </summary>
            public void UpdatePiPBar()
            {
                var pip = AppContext.AppSetting.WindowPiP;
                if (_isPiPHost)
                {
                    // The PiP window hosts this control bar, so it stays visible
                    // even while WindowPiP is enabled.
                    StopPanelAnimations();
                    ControlPanelGrid.Visibility = Visibility.Visible;
                    _controlPanelIsVisible = true;
                    return;
                }
                if (pip)
                {
                    StopPanelAnimations();
                    ControlPanelGrid.Visibility = Visibility.Collapsed;
                    _controlPanelIsVisible = false;
                }
            }
    
            /// <summary>Applies control-bar layout and hidden-icon preferences from the settings.</summary>
            public void ApplyControlBarStyle()
            {
                var layout = _compactMode ? "modernx" : ControlBarLayoutGrammar.Normalize(AppContext.AppSetting.ControlBarLayout);
                ApplyControlBarOrder(layout);
    
                if (_compactMode)
                {
                    // PiP: volume button + always-visible horizontal slider
                    // on the far left (the same strip as the main status
                    // bar — no popup), transport centered, subtitle toggle
                    // on the far right.
                    _currentSegment = 0;
                    var pipRight = PiPRightToggle is { } toggle
                        ? new ICommandBarElement[] { BuildPiPRightItem(toggle) }
                        : [];
                    ApplyBarOrders(
                        LeftCommandBar,
                        MiddleCommandBar,
                        RightCommandBar,
                        [VolumeMuteButton, VolumeSliderContainer, CompactTimeContainer],
                        [SkipBackwardButton, PlayPauseButton, SkipForwardButton],
                        pipRight);
                    SetHidden(true, PreviousTrackButton, NextTrackButton, RepeatButton,
                               TrackSelectionButton, ShuffleButton, PlaybackRateButton,
                               ZoomButton, PiPButton, FullWindowButton, FullScreenButton);
                    SetHidden(false, VolumeSliderContainer);
                    TimeTextGrid.Visibility = Visibility.Collapsed;
                    CompactTimeContainer.Visibility = Visibility.Visible;
                    UpdateTimeTexts(MediaPlayer?.Position ?? 0, MediaPlayer?.Duration ?? 0);
                    return;
                }
    
                TimeTextGrid.Visibility = Visibility.Visible;
                CompactTimeContainer.Visibility = Visibility.Collapsed;
    
                var hiddenValue = layout == "modernx"
                    ? AppContext.AppSetting.ControlBarHiddenIconsModernX
                    : AppContext.AppSetting.ControlBarHiddenIconsClassic;
                var hidden = ControlBarLayoutGrammar.ParseHiddenIcons(hiddenValue);

                // Single decision point: the user's hidden-icon preference and
                // the width tier both feed ApplyControlBarVisibility. Rebuilding
                // the bars is ApplyControlBarOrder's job (it is skipped when
                // nothing moved), so this only sets Visibility.
                ApplyControlBarVisibility(ControlBarAdaptiveLayout.TierFor(ActualWidth), hidden);
            }

            /// <summary>
            /// Control id -> the elements it controls. Ids match the ones the
            /// settings channel uses ("volume", "tracks", ...) plus ids for the
            /// fixed transport, so one table drives both writers.
            /// </summary>
            private IEnumerable<(string Id, FrameworkElement[] Elements)> AdaptiveControlMap()
            {
                yield return ("play", [PlayPauseButton]);
                yield return ("previous", [PreviousTrackButton]);
                yield return ("next", [NextTrackButton]);
                yield return ("skip-back", [SkipBackwardButton]);
                yield return ("skip-forward", [SkipForwardButton]);
                yield return ("rate", [PlaybackRateButton]);
                yield return ("random", [ShuffleButton]);
                yield return (ControlBarAdaptiveLayout.VolumeId, [VolumeMuteButton]);
                yield return (ControlBarAdaptiveLayout.VolumeSliderId, [VolumeSliderContainer]);
                yield return ("tracks", [TrackSelectionButton]);
                yield return ("panel", [ControlPanelButton]);
                yield return ("aspect", [ZoomButton]);
                yield return ("pip", [PiPButton]);
                yield return ("fullwindow", [FullWindowButton]);
                yield return ("fullscreen", [FullScreenButton]);
            }

            /// <summary>
            /// Applies the resolved visibility to every control-bar button and
            /// shows the overflow button whenever something is hidden (by the
            /// user or by the width tier) so no action is ever unreachable.
            /// </summary>
            private void ApplyControlBarVisibility(ControlBarTier tier, IReadOnlySet<string> userHidden)
            {
                foreach (var (id, elements) in AdaptiveControlMap())
                {
                    var visible = ControlBarAdaptiveLayout.IsVisible(id, tier, userHidden);

                    // The settings channel only knows "volume" and hides the
                    // mute button and its slider together; the tier treats the
                    // slider separately, so merge the two here.
                    if (visible
                        && string.Equals(id, ControlBarAdaptiveLayout.VolumeSliderId, StringComparison.OrdinalIgnoreCase)
                        && userHidden.Contains(ControlBarAdaptiveLayout.VolumeId))
                    {
                        visible = false;
                    }

                    SetHidden(!visible, elements);
                }

                // Hysteresis-free: the overflow button appears as soon as the
                // width tier (or the user) hides anything at all.
                var anythingHidden = tier != ControlBarTier.Wide || userHidden.Count > 0;
                SetHidden(!anythingHidden, MoreButton);
            }

            /// <summary>
            /// True when this control is hosted in the dedicated PiP window. The
            /// bar switches to the centered layout and shows only volume plus the
            /// transport buttons.
            /// </summary>
            public bool IsPiPHost
            {
                get => _isPiPHost;
                set
                {
                    if (_isPiPHost == value)
                    {
                        return;
                    }
                    _isPiPHost = value;
                    _compactMode = value;
                    ApplyControlBarStyle();
                    UpdatePiPBar();
                    if (value)
                    {
                        DispatcherQueue.TryEnqueue(() => SetOverlayMode(true));
                    }
                    else
                    {
                        SetOverlayMode(false);
                    }
                }
            }
    
            /// <summary>PiP-only toggle hosted at the compact bar's far right (subtitle switch).</summary>
            public ToggleButton? PiPRightToggle
            {
                get;
                set;
            }
    
            /// <summary>Invoked with the new checked state when the PiP subtitle toggle is clicked.</summary>
            public Action<bool>? PiPRightToggleAction
            {
                get;
                set;
            }
    
            /// <summary>
            /// Enables the gradient mask behind the control bar (fullscreen and
            /// PiP overlay). While active, the bar appears when the pointer is
            /// over the mask and retracts when it leaves.
            /// </summary>
    
            /// <summary>
            /// 原版 keeps the upstream control order. 居中 reorders the buttons to
            /// match ModernX: tracks and volume on the left edge, previous/skip/
            /// play/skip/next centered, window controls on the right edge. The
            /// command bars sit in star columns so the middle cluster is centered
            /// between the two edges.
            /// </summary>
            private void ApplyControlBarOrder(string layout)
            {
                // The settings canvas assigns every movable button to the left or
                // right frame (zone 0/2) and orders it inside that frame; the
                // transport group stays fixed. Volume maps to two controls that
                // share the "volume" id and zone.
                bool modernx = layout == "modernx";
                var custom = ControlBarLayoutGrammar.ParseCustomOrder(modernx
                    ? AppContext.AppSetting.ControlBarCustomOrderModernX
                    : AppContext.AppSetting.ControlBarCustomOrderClassic);
                var zones = ControlBarLayoutGrammar.ParseZones(modernx
                    ? AppContext.AppSetting.ControlBarZonesModernX
                    : AppContext.AppSetting.ControlBarZonesClassic);
    
                (string Id, ICommandBarElement Element)[] catalog =
                [
                    ("volume", VolumeMuteButton),
                    ("volume", VolumeSliderContainer),
                    ("tracks", TrackSelectionButton),
                    ("random", ShuffleButton),
                    ("panel", ControlPanelButton),
                    ("aspect", ZoomButton),
                    ("pip", PiPButton),
                    ("fullwindow", FullWindowButton),
                    ("fullscreen", FullScreenButton),
                ];
    
                int DefaultZone(string id) => layout == "modernx"
                    ? (ControlBarIconCatalog.ModernXRight.Contains(id) ? 2 : 0)
                    : (ControlBarIconCatalog.ClassicLeft.Contains(id) ? 0 : 2);
    
                var leftMovable = new List<(string, ICommandBarElement)>();
                var rightMovable = new List<(string, ICommandBarElement)>();
                foreach (var entry in catalog)
                {
                    var zone = zones.TryGetValue(entry.Id, out var saved) ? saved : DefaultZone(entry.Id);
                    (zone == 0 ? leftMovable : rightMovable).Add(entry);
                }
    
                ICommandBarElement[] left, middle, right;
                if (layout == "modernx")
                {
                    left = ControlBarLayoutEngine.ReorderMovable(leftMovable.ToArray(), custom);
                    middle =
                    [
                        PreviousTrackButton, SkipBackwardButton,
                        PlayPauseButton, SkipForwardButton,
                        NextTrackButton,
                    ];
                    right = [.. ControlBarLayoutEngine.ReorderMovable(rightMovable.ToArray(), custom), MoreButton];
                }
                else
                {
                    left =
                    [
                        PlayPauseButton, PreviousTrackButton, NextTrackButton, SkipBackwardButton,
                        SkipForwardButton,
                        .. ControlBarLayoutEngine.ReorderMovable(leftMovable.ToArray(), custom),
                    ];
                    middle = [];
                    right = [.. ControlBarLayoutEngine.ReorderMovable(rightMovable.ToArray(), custom), MoreButton];
                }
    
                ApplyBarOrders(LeftCommandBar, MiddleCommandBar, RightCommandBar, left, middle, right);
            }
    
            private static void ApplyBarOrders(
                CommandBar left,
                CommandBar middle,
                CommandBar right,
                ICommandBarElement[] leftDesired,
                ICommandBarElement[] middleDesired,
                ICommandBarElement[] rightDesired)
            {
                // The PiP subtitle toggle is defined in PiPWindow.xaml and gets
                // reparented here; make sure it is visible once it joins the bar.
                foreach (var element in rightDesired)
                {
                    if (element is AppBarElementContainer { Content: ToggleButton toggle })
                    {
                        toggle.Visibility = Visibility.Visible;
                    }
                }
    
                // Rebuild from the canonical lists on every apply. The previous
                // implementation only re-added elements that were already present
                // in the bars, so after the PiP compact pass stripped a subset of
                // buttons, later applies silently dropped the missing buttons and
                // the control bar could end up empty (progress bar + times only).
                //
                // When the desired set is unchanged (same button objects, same
                // order), skip the rebuild entirely: PrimaryCommands clears + adds
                // cause visual churn on every settings/language change even though
                // nothing moved. Visibility is a separate channel, so a pure
                // show/hide change still works without rebuilding.
                if (SameElements(left.PrimaryCommands, leftDesired)
                    && SameElements(middle.PrimaryCommands, middleDesired)
                    && SameElements(right.PrimaryCommands, rightDesired))
                {
                    return;
                }
    
                left.PrimaryCommands.Clear();
                middle.PrimaryCommands.Clear();
                right.PrimaryCommands.Clear();
    
                foreach (var element in leftDesired)
                {
                    left.PrimaryCommands.Add(element);
                }
                foreach (var element in middleDesired)
                {
                    middle.PrimaryCommands.Add(element);
                }
                foreach (var element in rightDesired)
                {
                    right.PrimaryCommands.Add(element);
                }
            }
    
            private AppBarButton BuildPiPRightItem(ToggleButton source)
            {
                // A ToggleButton that has already been realized in the XAML tree
                // cannot be reparented into the command bar (Content setter throws
                // E_INVALIDARG), so build a fresh button each time and forward the
                // click through PiPRightToggleAction. The active state tints the
                // glyph with the accent color instead of the AppBarToggleButton
                // checked pill, which paints the whole 40px slot.
                var icon = source.Content as FontIcon;
                var buttonIcon = new FontIcon
                {
                    Glyph = icon?.Glyph ?? "\uF2E3",
                    FontSize = 19,
                    FontFamily = new FontFamily(IconFonts.FluentSystemIconsUri),
                };
                var activeBrush = (Brush)RootGrid.Resources["PiPSubtitleActiveBrush"];
                var button = new AppBarButton
                {
                    Icon = buttonIcon,
                    Style = (Style)RootGrid.Resources["AppBarButtonStyle"],
                    Visibility = Visibility.Visible,
                };
                AutomationProperties.SetName(button, AppContext.AppLang.Subtitles);
                ToolTipService.SetToolTip(button, AppContext.AppLang.Subtitles);
                var active = source.IsChecked == true;
                SetPiPSubtitleTint(buttonIcon, activeBrush, active);
                button.Click += (_, _) =>
                {
                    active = !active;
                    SetPiPSubtitleTint(buttonIcon, activeBrush, active);
                    if (PiPRightToggleAction is { } action)
                    {
                        action(active);
                    }
                };
                return button;
            }

            /// <summary>Tints the active glyph with the accent; when off, the
            /// local value must be cleared (not set to null) so the theme
            /// style's foreground applies again — a local null would override
            /// it and render the glyph near-invisible black.</summary>
            private static void SetPiPSubtitleTint(FontIcon icon, Brush activeBrush, bool active)
            {
                if (active)
                {
                    icon.Foreground = activeBrush;
                }
                else
                {
                    icon.ClearValue(FontIcon.ForegroundProperty);
                }
            }
    
            private static bool SameElements(IList<ICommandBarElement> current, ICommandBarElement[] desired)
            {
                if (current.Count != desired.Length)
                {
                    return false;
                }
                for (int i = 0; i < desired.Length; i++)
                {
                    if (!ReferenceEquals(current[i], desired[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
    
            private static void SetHidden(bool hide, params FrameworkElement[] elements)
            {
                foreach (var element in elements)
                {
                    if (element is not null)
                    {
                        element.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
                    }
                }
            }
    
            /// <summary>Ids the user explicitly hid in the control-bar settings.</summary>
            private static HashSet<string> CurrentHiddenIconIds()
            {
                var layout = ControlBarLayoutGrammar.Normalize(AppContext.AppSetting.ControlBarLayout);
                var value = layout == "modernx"
                    ? AppContext.AppSetting.ControlBarHiddenIconsModernX
                    : AppContext.AppSetting.ControlBarHiddenIconsClassic;
                return ControlBarLayoutGrammar.ParseHiddenIcons(value);
            }
    
            /// <summary>
            /// Rebuilds the narrow-window overflow menu from the controls the
            /// width-adaptive state currently hides (Medium/Compact/Narrow).
            /// User-hidden icons stay hidden; the menu only restores
            /// width-collapsed actions so no function is lost when the window
            /// shrinks. The bar itself is never rebuilt here (see AGENTS.md).
            /// </summary>
            private void MoreFlyout_Opening(object? sender, object e)
            {
                MoreFlyout.Items.Clear();
                var userHidden = CurrentHiddenIconIds();
    
                AddOverflowItem(AppContext.AppLang.MoreSkipBackward, SkipBackwardButton, Backward, userHidden);
                AddOverflowItem(AppContext.AppLang.MoreSkipForward, SkipForwardButton, Forward, userHidden);
                AddOverflowItem(AppContext.AppLang.MoreShuffle, ShuffleButton, () => OnPlaybackModeClick(null, null), userHidden, "random");
    
                if (PlaybackRateButton.Visibility != Visibility.Visible)
                {
                    MoreFlyout.Items.Add(BuildPlaybackRateSubmenu());
                }
    
                if (ZoomButton.Visibility != Visibility.Visible && !userHidden.Contains("aspect"))
                {
                    MoreFlyout.Items.Add(BuildZoomSubmenu());
                }
    
                AddOverflowItem(AppContext.AppLang.MorePreviousTrack, PreviousTrackButton, () => PreviousTrackButton_Click(null, null), userHidden);
                AddOverflowItem(AppContext.AppLang.MoreNextTrack, NextTrackButton, () => NextTrackButton_Click(null, null), userHidden);
                AddOverflowItem(AppContext.AppLang.MoreFullWindow, FullWindowButton, () => ToggleFullWindow(), userHidden, "fullwindow");
                AddOverflowItem(AppContext.AppLang.MoreFullScreen, FullScreenButton, () => ToggleFullScreen(), userHidden, "fullscreen");

                // Controls the width tiers collapse at Narrow and below. Without
                // these entries a portrait/narrow window would lose them
                // entirely: they used to be visible at every width.
                AddOverflowItem(AppContext.AppLang.ControlBarIconTracks, TrackSelectionButton, ShowTracksFromMore, userHidden, "tracks");
                AddOverflowItem(AppContext.AppLang.ControlBarIconPanel, ControlPanelButton, ShowControlPanelFromMore, userHidden, "panel");
                AddOverflowItem(AppContext.AppLang.ControlBarIconPiP, PiPButton, () => OnPiPClick(this, new RoutedEventArgs()), userHidden, "pip");
            }

            /// <summary>Loads the track lists and then opens the track flyout anchored on the overflow button.</summary>
            private void ShowTracksFromMore()
            {
                // The handler only fills the track lists; the flyout itself is
                // declared on the button, so anchor it on the overflow button.
                TrackSelectionButton_Click(TrackSelectionButton, new RoutedEventArgs());
                ShowFromMore(TrackSelectionFlyout);
            }

            /// <summary>Opens the control-panel flyout anchored on the overflow button.</summary>
            private void ShowControlPanelFromMore()
            {
                ShowFromMore(ControlPanelFlyout);
            }

            /// <summary>
            /// A flyout cannot be shown while the MenuFlyout that triggered it is
            /// still open, so close the menu first and open the flyout on the
            /// next dispatcher pass, anchored on the overflow button.
            /// </summary>
            private void ShowFromMore(FlyoutBase flyout)
            {
                MoreFlyout.Hide();
                DispatcherQueue.TryEnqueue(() => flyout.ShowAt(MoreButton));
            }
    
            private void AddOverflowItem(
                string label,
                FrameworkElement sourceButton,
                Action action,
                IReadOnlySet<string> userHidden,
                string? hiddenId = null)
            {
                if (sourceButton.Visibility == Visibility.Visible)
                {
                    return;
                }
                if (hiddenId is not null && userHidden.Contains(hiddenId))
                {
                    return;
                }
    
                var item = new MenuFlyoutItem { Text = label };
                item.Click += (_, _) => action();
                MoreFlyout.Items.Add(item);
            }
    
            /// <summary>Clones the standard rate flyout into a submenu item.</summary>
            private MenuFlyoutSubItem BuildPlaybackRateSubmenu()
            {
                var submenu = new MenuFlyoutSubItem { Text = AppContext.AppLang.MorePlaybackRate };
                foreach (var item in PlaybackRateFlyout.Items)
                {
                    if (item is MenuFlyoutItem source)
                    {
                        var clone = new MenuFlyoutItem { Text = source.Text, Tag = source.Tag };
                        clone.Click += PlaybackRateFlyout_MenuFlyoutItem_Click;
                        submenu.Items.Add(clone);
                    }
                }
                return submenu;
            }
    
            private MenuFlyoutSubItem BuildZoomSubmenu()
            {
                var submenu = new MenuFlyoutSubItem { Text = AppContext.AppLang.MoreZoom };
                AddZoomOptions(submenu.Items, ZoomSelectionMenu_Click);
                return submenu;
            }
    
        private void UpdateToolbarVisibility(double w)
            {
                if (_isPiPHost)
                {
                    // PiP hosts the compact centered bar; the adaptive tiers
                    // would collapse the transport buttons at small widths.
                    if (_currentSegment != 0)
                    {
                        _currentSegment = 0;
                    }
                    return;
                }

                var tier = ControlBarAdaptiveLayout.TierFor(w);
                if ((int)tier == _currentSegment)
                {
                    return;
                }

                _currentSegment = (int)tier;
                ApplyControlBarVisibility(tier, CurrentHiddenIconIds());
            }

            /// <summary>Re-applies the width-adaptive state after a layout mode change.</summary>
            public void RefreshAdaptiveState()
            {
                UpdateToolbarVisibility(ActualWidth);
            }
    
    }
}
