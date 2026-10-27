using mpv_winui.Infrastructure.Controls;
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
using mpv_winui.Infrastructure.Common.View;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Domains.Settings.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// Control-bar composition: PiP bar switching, layout/zones application,
    /// width-adaptive states and the narrow-window overflow menu.
    /// </summary>
    public sealed partial class PlayerControl
    {
            /// <summary>Current per-button slot width, or 0 before the first apply.</summary>
            private double _barPitch;

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
                    UpdateTimeTexts(MediaPlayer?.Position() ?? 0, MediaPlayer?.Duration() ?? 0);
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
                ApplyControlBarVisibility(TierForWidth(ActualWidth), hidden);
            }

            /// <summary>
            /// The tier for a given bar width. The bar's own ActualWidth is the
            /// window's width, but its ActualHeight is the bar's own height, so
            /// the orientation half of the rule has to be read from the XamlRoot.
            /// </summary>
            private ControlBarTier TierForWidth(double width)
            {
                var size = XamlRoot?.Size;
                return ControlBarAdaptiveLayout.TierFor(width, size?.Height ?? 0);
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
            /// <summary>
            /// True when the current track is audio, so the bar can drop the keys
            /// that only mean something with a picture.
            ///
            /// Zoom, aspect and PiP all act on the video frame. With an audio file
            /// they are either inert or - worse - they resize a window that has
            /// nothing to show, which is the "remove the video-only buttons that
            /// have no value on the music page" request. Kept as a flag on the
            /// control rather than a second visibility table so it composes with
            /// the tier rules instead of overriding them.
            /// </summary>
            public bool IsAudioOnly
            {
                get => _audioOnly;
                set
                {
                    if (_audioOnly == value)
                    {
                        return;
                    }

                    _audioOnly = value;

                    ApplyControlBarStyle();
                }
            }

            private bool _audioOnly;

            /// <summary>Ids that only make sense with a video frame.</summary>
            private static readonly string[] VideoOnlyIds = ["aspect", "pip", "fullwindow"];

            private void ApplyControlBarVisibility(ControlBarTier tier, IReadOnlySet<string> userHidden)
            {
                foreach (var (id, elements) in AdaptiveControlMap())
                {
                    var visible = ControlBarAdaptiveLayout.IsVisible(id, tier, userHidden);

                    // Audio drops the frame-only keys whatever the tier says.
                    if (visible && _audioOnly && VideoOnlyIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        visible = false;
                    }

                    // The settings channel only knows "volume" and hides the
                    // mute button and its slider together; the tier treats the
                    // slider separately, so merge the two here.
                    if (visible
                        && string.Equals(id, ControlBarAdaptiveLayout.VolumeSliderId, StringComparison.OrdinalIgnoreCase)
                        && userHidden.Contains(ControlBarAdaptiveLayout.VolumeId))
                    {
                        visible = false;
                    }

                    // The PlayerStyle skin, a third input beside the width tier and the
                    // user's hidden-icon preference.
                    //
                    // This is what `SettingsHelpPlayerStyle` promises: "Compact narrows the
                    // bar and drops the volume slider". Before this, PlayerStyle reached only
                    // `ApplyPlayerStyle`, which assigns a Style whose single setter is
                    // Template -- and WinUI never applies a Template setter to a UserControl,
                    // so all three skins rendered the same inline tree and the setting had no
                    // visible effect at all (instrumented: styleSet=True while Template
                    // stayed null).
                    //
                    // Only the volume slider is dropped: that is the one claim in the string
                    // that names a control, so it is the one that can be asserted.
                    if (visible
                        && ControlBarAdaptiveLayout.SkinHides(AppContext.AppSetting.PlayerStyle, id))
                    {
                        visible = false;
                    }

                    SetHidden(!visible, elements);
                }

                // The More button exists only to reach what the bar cannot show,
                // so its visibility is the menu's own "is there anything to
                // offer" question, asked of the same table the menu is built
                // from. It used to be shown whenever the tier was below Wide,
                // which is not that question: at the narrower tiers every
                // surviving control can still be on the bar, and a control the
                // user hid on purpose is never offered back -- in both cases the
                // button appeared and opened an empty menu.
                SetHidden(!ControlBarAdaptiveLayout.HasOverflowContent(tier, userHidden), MoreButton);

                // Three controls a side plus the transport is more than the
                // default 40px pitch fits in a portrait window; tighten it
                // rather than dropping controls to make room.
                ApplyBarPitch(ControlBarAdaptiveLayout.IsTight(tier));
            }

            /// <summary>
            /// Sets the per-button pitch. The command bars lay their items out
            /// edge to edge, so an item's width is what the pitch really is;
            /// narrowing it tightens the row without touching the glyph size or
            /// the hit target's height.
            ///
            /// These two numbers are AppBarButtonStyle's own width and a step
            /// below it, and that relationship is the point. The style used to
            /// declare 62 while this method wrote 40, so 40 is what the bar
            /// actually showed - which is why measuring the built app reported a
            /// 40 DIP pitch against a 62 DIP style, and why "the buttons are too
            /// small" could not be fixed by editing the style alone. Whatever
            /// the style says, this is the value that lands.
            ///
            /// PlayPauseButton is deliberately absent from the loop below: it is
            /// the one control meant to be larger than its neighbours, and
            /// equalising it here would cancel that.
            /// </summary>
            private void ApplyBarPitch(bool tight)
            {
                // 44 = the style's own button, and the official player's own
                // transport pitch; 36 is the widest step down that still keeps
                // ten slots inside a 340 window.
                const double Wide = 44;
                const double Tight = 36;

                var width = tight ? Tight : Wide;
                if (_barPitch == width)
                {
                    return;
                }
                _barPitch = width;

                foreach (var button in PitchControlledButtons())
                {
                    button.Width = width;
                    button.MinWidth = 0;
                }
            }

            /// <summary>
            /// Buttons whose slot width the pitch rule owns. PlayPauseButton is
            /// absent on purpose: it keeps MpvTransportPlayButtonStyle's own 56,
            /// because a play button the same size as everything beside it is
            /// the one thing the official player's bar never does.
            /// </summary>
            private IEnumerable<FrameworkElement> PitchControlledButtons()
            {
                yield return PreviousTrackButton;
                yield return NextTrackButton;
                yield return SkipBackwardButton;
                yield return SkipForwardButton;
                yield return ShuffleButton;
                yield return PlaybackRateButton;
                yield return TrackSelectionButton;
                yield return ControlPanelButton;
                yield return ZoomButton;
                yield return PiPButton;
                yield return FullWindowButton;
                yield return FullScreenButton;
                yield return VolumeMuteButton;
                yield return MoreButton;
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
                    right = [MoreButton, .. ControlBarLayoutEngine.ReorderMovable(rightMovable.ToArray(), custom)];
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
                    right = [MoreButton, .. ControlBarLayoutEngine.ReorderMovable(rightMovable.ToArray(), custom)];
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
            /// width-adaptive state currently hides. User-hidden icons stay
            /// hidden; the menu only restores width-collapsed actions so no
            /// function is lost when the window shrinks. The bar itself is never
            /// rebuilt here: only the menu is, so a resize never loses a hidden icon.
            ///
            /// The entries and their "respects a user hide" flags come from
            /// ControlBarAdaptiveLayout.OverflowEntries — the same table
            /// ApplyControlBarVisibility decides the More button from — so the
            /// button cannot open a menu this loop leaves empty.
            /// </summary>
            private void MoreFlyout_Opening(object? sender, object e)
            {
                MoreFlyout.Items.Clear();
                var userHidden = CurrentHiddenIconIds();

                foreach (var (id, respectsUserHide) in ControlBarAdaptiveLayout.OverflowEntries)
                {
                    var ui = OverflowUiFor(id);
                    if (ui is null)
                    {
                        continue;
                    }
                    var (button, label, invoke, submenu) = ui.Value;
                    if (!OffersInOverflow(button, userHidden, respectsUserHide ? id : null))
                    {
                        continue;
                    }
                    if (submenu is not null)
                    {
                        MoreFlyout.Items.Add(submenu());
                        continue;
                    }
                    var item = new MenuFlyoutItem { Text = label() };
                    item.Click += (_, _) => invoke?.Invoke();
                    MoreFlyout.Items.Add(item);
                }
            }

            /// <summary>
            /// One rule for "does the overflow menu offer this control right now".
            /// The menu reads it directly, and
            /// ControlBarAdaptiveLayout.HasOverflowContent applies the same rule to
            /// the same table to decide the More button's visibility. A control
            /// already on the bar is not offered again; one the user hid through
            /// the settings is not offered back, because the menu never undoes a
            /// deliberate choice.
            /// </summary>
            private static bool OffersInOverflow(
                FrameworkElement sourceButton,
                IReadOnlySet<string> userHidden,
                string? hiddenId) =>
                sourceButton.Visibility != Visibility.Visible
                && (hiddenId is null || !userHidden.Contains(hiddenId));

            /// <summary>
            /// What the overflow menu needs in order to render one entry, keyed by
            /// the ids in ControlBarAdaptiveLayout.OverflowEntries. The label is a
            /// delegate rather than a captured string, so a language change is
            /// picked up on the next open without rebuilding anything.
            /// </summary>
            private (FrameworkElement Button, Func<string> Label, Action? Invoke, Func<MenuFlyoutSubItem>? Submenu)? OverflowUiFor(string id) =>
                id switch
                {
                    "skip-back" => (SkipBackwardButton, () => AppContext.AppLang.MoreSkipBackward, Backward, null),
                    "skip-forward" => (SkipForwardButton, () => AppContext.AppLang.MoreSkipForward, Forward, null),
                    "random" => (ShuffleButton, () => AppContext.AppLang.MoreShuffle, () => OnPlaybackModeClick(null, null), null),
                    "rate" => (PlaybackRateButton, () => AppContext.AppLang.MorePlaybackRate, null, BuildPlaybackRateSubmenu),
                    "aspect" => (ZoomButton, () => AppContext.AppLang.MoreZoom, null, BuildZoomSubmenu),
                    "previous" => (PreviousTrackButton, () => AppContext.AppLang.MorePreviousTrack, () => PreviousTrackButton_Click(null, null), null),
                    "next" => (NextTrackButton, () => AppContext.AppLang.MoreNextTrack, () => NextTrackButton_Click(null, null), null),
                    "fullwindow" => (FullWindowButton, () => AppContext.AppLang.MoreFullWindow, () => ToggleFullWindow(), null),
                    _ => null,
                };

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
    
            /// <summary>Clones the zoom flyout into a submenu item, so the aspect
            /// control stays reachable at the tiers that take its button off the
            /// bar (Minimal).</summary>
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

                var tier = TierForWidth(w);
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

            /// <summary>
            /// Converts a point expressed relative to the control bar into the
            /// coordinate space of <paramref name="target"/> (the video panel).
            /// Used to place the seek-bar hover thumbnail over the video surface.
            /// </summary>
            public Point TransformSliderPoint(UIElement target, double x, double y)
            {
                return TransformToVisual(target).TransformPoint(new Point(x, y));
            }

            /// <summary>Flips the overlay control panel between shown and hidden.</summary>
            public void ToggleControlPanel()
            {
                if (_controlPanelIsVisible)
                {
                    HideControlPanel();
                }
                else
                {
                    ShowControlPanel();
                }
            }
    }
}
