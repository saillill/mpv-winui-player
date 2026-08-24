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
                    // PiP: volume on the far left (flyout slider), transport
                    // centered, subtitle toggle on the far right.
                    _currentSegment = 0;
                    VisualStateManager.GoToState(this, "Wide", false);
                    var pipRight = PiPRightToggle is { } toggle
                        ? new ICommandBarElement[] { BuildPiPRightItem(toggle) }
                        : [];
                    ApplyBarOrders(
                        LeftCommandBar,
                        MiddleCommandBar,
                        RightCommandBar,
                        [VolumeMuteButton, CompactTimeContainer],
                        [SkipBackwardButton, PlayPauseButton, SkipForwardButton],
                        pipRight);
                    SetHidden(true, PreviousTrackButton, NextTrackButton, RepeatButton,
                               TrackSelectionButton, ShuffleButton, PlaybackRateButton,
                               ZoomButton, PiPButton, FullWindowButton, FullScreenButton,
                               VolumeSliderContainer);
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
    
                // Restore the buttons the PiP compact pass collapses.
                SetHidden(false,
                    PreviousTrackButton, NextTrackButton, RepeatButton,
                    TrackSelectionButton, ShuffleButton, PlaybackRateButton,
                    ZoomButton, PiPButton, FullWindowButton, FullScreenButton,
                    VolumeMuteButton, VolumeSliderContainer);
    
                // Playback controls are always shown and cannot be hidden.
                SetHidden(hidden.Contains("volume"), VolumeMuteButton, VolumeSliderContainer);
                SetHidden(hidden.Contains("tracks"), TrackSelectionButton);
                SetHidden(hidden.Contains("random"), ShuffleButton);
                SetHidden(hidden.Contains("panel"), ControlPanelButton);
                SetHidden(hidden.Contains("aspect"), ZoomButton);
                SetHidden(hidden.Contains("fullwindow"), FullWindowButton);
                SetHidden(hidden.Contains("fullscreen"), FullScreenButton);
                SetHidden(hidden.Contains("pip"), PiPButton);
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
    
            private AppBarToggleButton BuildPiPRightItem(ToggleButton source)
            {
                // A ToggleButton that has already been realized in the XAML tree
                // cannot be reparented into the command bar (Content setter throws
                // E_INVALIDARG), so build a fresh AppBarToggleButton each time and
                // forward the click through PiPRightToggleAction. Using the same
                // AppBarToggleButton style as the rest of the bar keeps the CC
                // icon at the same height and gives it the checked accent state.
                var icon = source.Content as FontIcon;
                var toggle = new AppBarToggleButton
                {
                    Icon = new FontIcon
                    {
                        Glyph = icon?.Glyph ?? "\uF2E3",
                        FontSize = 16,
                        FontFamily = new FontFamily(IconFonts.FluentSystemIconsUri),
                    },
                    IsChecked = source.IsChecked,
                    Style = (Style)RootGrid.Resources["AppBarToggleButtonStyle"],
                    Visibility = Visibility.Visible,
                };
                AutomationProperties.SetName(toggle, AppContext.AppLang.Subtitles);
                ToolTipService.SetToolTip(toggle, AppContext.AppLang.Subtitles);
                if (PiPRightToggleAction is { } action)
                {
                    toggle.Click += (_, _) => action(toggle.IsChecked == true);
                }
                return toggle;
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
                    // PiP hosts the compact centered bar; the adaptive states would
                    // collapse the transport buttons at small widths.
                    if (_currentSegment != 0)
                    {
                        _currentSegment = 0;
                        VisualStateManager.GoToState(this, "Wide", false);
                    }
                    return;
                }
    
                int newSegment = w >= 700 ? 0 : w >= 500 ? 1 : w >= 280 ? 2 : 3;
                if (newSegment == _currentSegment)
                {
                    return;
                }
    
                _currentSegment = newSegment;
    
                string name = newSegment switch
                {
                    0 => "Wide",
                    1 => "Medium",
                    2 => "Compact",
                    _ => "Narrow"
                };
                VisualStateManager.GoToState(this, name, false);
            }
    
            /// <summary>Re-applies the width-adaptive state after a layout mode change.</summary>
            public void RefreshAdaptiveState()
            {
                UpdateToolbarVisibility(ActualWidth);
            }
    
    }
}
