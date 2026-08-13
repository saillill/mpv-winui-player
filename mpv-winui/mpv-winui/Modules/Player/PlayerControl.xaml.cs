using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
    public sealed partial class PlayerControl : UserControl
    {

        public delegate bool FullScreenRequestHandler();
        public delegate bool FullWindowRequestHandler();
        public delegate void OnPanelVisibleChangedHandler(bool hide);
        public delegate void OnPositionChangedHandler();

        public event FullScreenRequestHandler? OnFullScreenRequest;
        public event FullWindowRequestHandler? OnFullWindowRequest;
        public event OnPanelVisibleChangedHandler? OnPanelVisibleChanged;
        public event OnPositionChangedHandler? OnPositionChanged;

        public event EventHandler<(double HoverSec, double RelativeX, double RelativeY)>? PreviewUpdateRequested;
        public event EventHandler? PreviewClearRequested;

        private bool _controlPanelIsVisible = true;
        private bool _compactMode;
        private bool _isPiPHost;
        private bool _overlayMode;
        private readonly DispatcherTimer _hideDelayTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
        private readonly DispatcherTimer _overlayIdleTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
        private Point _lastOverlayActivity = new(double.NaN, double.NaN);
        private const double OverlayMoveThreshold = 5.0;
        private bool _panelAnimationShow;
        private bool _panelAnimating;
        private readonly DispatcherTimer _panelAnimationTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private long _panelAnimationStart;
        private Compositor? _panelCompositor;
        private Visual? _panelGridVisual;
        private Visual? _panelGradientVisual;

        private readonly DispatcherTimer _positionUpdateTimer;
        private bool _hasError = false;
        private bool _isBuffering = false;
        private bool _isInScrubMode = false;
        private bool _isDragging = false;
        private long _suppressBufferingUntil;
        private bool _sourceLoaded = false;

        private MpvMediaPlayer? _mediaPlayer;

        public static readonly DependencyProperty FullWindowButtonVisibilityProperty = DependencyProperty.Register("FullWindowButtonVisibility", typeof(Visibility), typeof(PlayerControl), new PropertyMetadata(Visibility.Collapsed, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        {
            var control = (PlayerControl)d;
            control.FullWindowButton.Visibility = (Visibility)e.NewValue;
        }));

        public static readonly DependencyProperty NextTrackButtonVisibilityProperty = DependencyProperty.Register("NextTrackButtonVisibility", typeof(Visibility), typeof(PlayerControl), new PropertyMetadata(Visibility.Collapsed, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        {
            var control = (PlayerControl)d;
            control.NextTrackButton.Visibility = (Visibility)e.NewValue;
        }));

        public static readonly DependencyProperty IsNextTrackButtonEnabledProperty = DependencyProperty.Register("IsNextTrackButtonEnabled", typeof(bool), typeof(PlayerControl), new PropertyMetadata(true, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        {
            var control = (PlayerControl)d;
            control.NextTrackButton.IsEnabled = (bool)e.NewValue;
        }));

        public static readonly DependencyProperty PreviousTrackButtonVisibilityProperty = DependencyProperty.Register("PreviousTrackButtonVisibility", typeof(Visibility), typeof(PlayerControl), new PropertyMetadata(Visibility.Collapsed, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        {
            var control = (PlayerControl)d;
            control.PreviousTrackButton.Visibility = (Visibility)e.NewValue;
        }));

        public static readonly DependencyProperty IsPreviousTrackButtonEnabledProperty = DependencyProperty.Register("IsPreviousTrackButtonEnabled", typeof(bool), typeof(PlayerControl), new PropertyMetadata(true, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        {
            var control = (PlayerControl)d;
            control.PreviousTrackButton.IsEnabled = (bool)e.NewValue;
        }));

        public PlayerControl()
        {
            this.InitializeComponent();
            ApplyLocalizedStrings();
            ApplyControlBarStyle();
            UpdatePiPBar();
            this.Loaded += PlayerControl_Loaded;
            this.Unloaded += PlayerControl_Unloaded;

            _positionUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        }

        public void ApplyLocalizedStrings()
        {
            ToolTipService.SetToolTip(PiPButton, AppContext.AppLang.SettingsPiP);
            ToolTipService.SetToolTip(PlayPauseButton, AppContext.AppLang.Play);
            ToolTipService.SetToolTip(PreviousTrackButton, AppContext.AppLang.MorePreviousTrack);
            ToolTipService.SetToolTip(NextTrackButton, AppContext.AppLang.MoreNextTrack);
            ToolTipService.SetToolTip(SkipBackwardButton, AppContext.AppLang.MoreSkipBackward);
            ToolTipService.SetToolTip(SkipForwardButton, AppContext.AppLang.MoreSkipForward);
            ToolTipService.SetToolTip(ShuffleButton, AppContext.AppLang.MoreShuffle);
            ToolTipService.SetToolTip(RepeatButton, AppContext.AppLang.MoreRepeat);
            ToolTipService.SetToolTip(VolumeMuteButton, AppContext.AppLang.PiPMute);
            ToolTipService.SetToolTip(VolumeSlider, AppContext.AppLang.ControlBarIconVolume);
            ToolTipService.SetToolTip(PlaybackRateButton, AppContext.AppLang.MorePlaybackRate);
            ToolTipService.SetToolTip(TrackSelectionButton, AppContext.AppLang.ControlBarIconTracks);
            ToolTipService.SetToolTip(ZoomButton, AppContext.AppLang.MoreZoom);
            ToolTipService.SetToolTip(FullWindowButton, AppContext.AppLang.MoreFullWindow);
            ToolTipService.SetToolTip(FullScreenButton, AppContext.AppLang.MoreFullScreen);
            ToolTipService.SetToolTip(ControlPanelButton, AppContext.AppLang.ControlBarIconPanel);

            // Keyboard/screen-reader names (XAML holds English placeholders).
            AutomationProperties.SetName(ProgressSlider, AppContext.AppLang.ControlBarIconPlayback);
            AutomationProperties.SetName(PlayPauseButton, AppContext.AppLang.Play);
            AutomationProperties.SetName(PreviousTrackButton, AppContext.AppLang.MorePreviousTrack);
            AutomationProperties.SetName(NextTrackButton, AppContext.AppLang.MoreNextTrack);
            AutomationProperties.SetName(SkipBackwardButton, AppContext.AppLang.MoreSkipBackward);
            AutomationProperties.SetName(SkipForwardButton, AppContext.AppLang.MoreSkipForward);
            AutomationProperties.SetName(ShuffleButton, AppContext.AppLang.MoreShuffle);
            AutomationProperties.SetName(RepeatButton, AppContext.AppLang.MoreRepeat);
            AutomationProperties.SetName(VolumeMuteButton, AppContext.AppLang.PiPMute);
            AutomationProperties.SetName(VolumeSlider, AppContext.AppLang.ControlBarIconVolume);
            AutomationProperties.SetName(PlaybackRateButton, AppContext.AppLang.MorePlaybackRate);
            AutomationProperties.SetName(TrackSelectionButton, AppContext.AppLang.ControlBarIconTracks);
            AutomationProperties.SetName(ZoomButton, AppContext.AppLang.MoreZoom);
            AutomationProperties.SetName(PiPButton, AppContext.AppLang.SettingsPiP);
            AutomationProperties.SetName(FullWindowButton, AppContext.AppLang.MoreFullWindow);
            AutomationProperties.SetName(FullScreenButton, AppContext.AppLang.MoreFullScreen);
        }

        private void OnAppSettingChanged(string key, object? value)
        {
            if (key == nameof(AppContext.AppSetting.WindowPiP))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdatePiPButton();
                    UpdatePiPBar();
                });
            }
            else if (key == nameof(AppContext.AppSetting.ControlBarLayout)
                || key == nameof(AppContext.AppSetting.ControlBarHiddenIconsClassic)
                || key == nameof(AppContext.AppSetting.ControlBarHiddenIconsModernX)
                || key == nameof(AppContext.AppSetting.ControlBarCustomOrderClassic)
                || key == nameof(AppContext.AppSetting.ControlBarCustomOrderModernX)
                || key == nameof(AppContext.AppSetting.ControlBarZonesClassic)
                || key == nameof(AppContext.AppSetting.ControlBarZonesModernX))
            {
                DispatcherQueue.TryEnqueue(ApplyControlBarStyle);
            }
        }

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
            var layout = _compactMode ? "modernx" : NormalizeControlBarLayout(AppContext.AppSetting.ControlBarLayout);
            ApplyControlBarOrder(layout);

            if (_compactMode)
            {
                // PiP: time on the left, transport centered, volume on the right.
                _currentSegment = 0;
                VisualStateManager.GoToState(this, "Wide", false);
                ApplyBarOrders(
                    LeftCommandBar,
                    MiddleCommandBar,
                    RightCommandBar,
                    [],
                    [SkipBackwardButton, PlayPauseButton, SkipForwardButton],
                    [VolumeMuteButton, VolumeSliderContainer]);
                SetHidden(true, PreviousTrackButton, NextTrackButton, RepeatButton,
                           TrackSelectionButton, ShuffleButton, PlaybackRateButton,
                           ZoomButton, PiPButton, FullWindowButton, FullScreenButton);
                TimeTextGrid.Visibility = Visibility.Collapsed;
                CompactTimeText.Visibility = Visibility.Visible;
                UpdateTimeTexts(MediaPlayer?.Position ?? 0, MediaPlayer?.Duration ?? 0);
                return;
            }

            TimeTextGrid.Visibility = Visibility.Visible;
            CompactTimeText.Visibility = Visibility.Collapsed;

            var hiddenValue = layout == "modernx"
                ? AppContext.AppSetting.ControlBarHiddenIconsModernX
                : AppContext.AppSetting.ControlBarHiddenIconsClassic;
            var hidden = new HashSet<string>(
                hiddenValue?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                StringComparer.OrdinalIgnoreCase);

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

        /// <summary>
        /// Enables the gradient mask behind the control bar (fullscreen and
        /// PiP overlay). While active, the bar appears when the pointer is
        /// over the mask and retracts when it leaves.
        /// </summary>
        public void SetOverlayMode(bool overlay)
        {
            if (_overlayMode == overlay)
            {
                return;
            }
            _overlayMode = overlay;

            if (overlay)
            {
                _lastOverlayActivity = new(double.NaN, double.NaN);
                // Fullscreen mask: the gradient shell extends above the bar so
                // the video fades into the controls, matching the mpv-lazy /
                // ModernX bottom fade (120px, ~90% black at the base).
                ControlPanelGradient.Height = 120;
                // PiP-style overlay: no solid panel box, only the gradient
                // fade. The dark element theme keeps glyphs white so they
                // stay readable on the gradient without a visible border.
                ControlPanelGrid.Background = null;
                ControlPanelGrid.RequestedTheme = ElementTheme.Dark;
                ControlPanelGradient.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop { Offset = 0, Color = Color.FromArgb(0, 0, 0, 0) },
                        new GradientStop { Offset = 1, Color = Color.FromArgb(230, 0, 0, 0) },
                    },
                };
                ControlPanelGradient.Opacity = 1;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                ControlPanelGrid.Visibility = Visibility.Visible;
                _controlPanelIsVisible = true;
                ShowControlPanel();
                RestartOverlayIdleTimer();
            }
            else
            {
                _lastOverlayActivity = new(double.NaN, double.NaN);
                // Windowed mode: the shell hugs the bar content (Auto) so no
                // extra background strip remains between the video and the
                // controls.
                ControlPanelGradient.Height = double.NaN;
                ControlPanelGradient.Background = null;
                ControlPanelGradient.Opacity = 1;
                ControlPanelGrid.Background = null;
                ControlPanelGrid.RequestedTheme = ElementTheme.Default;
                _overlayIdleTimer.Stop();
                // Reset any mid-animation state: leaving overlay while the
                // bar was fading out used to leave it at partial opacity or
                // collapsed, which the user saw as an incompletely expanded
                // windowed bar after double-click fullscreen.
                StopPanelAnimations();
                ControlPanelGradient.Visibility = Visibility.Visible;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                ControlPanelGrid.Visibility = Visibility.Visible;
                _controlPanelIsVisible = true;
                ShowControlPanel();
            }
        }

        /// <summary>
        /// Activity-based overlay bar: any pointer movement over the player
        /// (video, mask or bar) expands the bar and restarts the idle timer;
        /// when the mouse stops moving, the bar retracts.
        /// </summary>
        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_overlayMode)
            {
                return;
            }

            NotifyOverlayPointerActivity(e.GetCurrentPoint(RootGrid).Position);
        }

        /// <summary>Called on pointer activity from the video area as well.</summary>
        public void NotifyOverlayPointerActivity(Point position)
        {
            if (!_overlayMode)
            {
                return;
            }

            // Ignore sub-threshold jitter / duplicate events: they used to
            // restart the idle timer repeatedly, so the bar stayed visible for
            // many seconds after the mouse actually stopped moving.
            if (!double.IsNaN(_lastOverlayActivity.X)
                && Math.Abs(position.X - _lastOverlayActivity.X) < OverlayMoveThreshold
                && Math.Abs(position.Y - _lastOverlayActivity.Y) < OverlayMoveThreshold)
            {
                return;
            }

            _lastOverlayActivity = position;
            ShowControlPanel();
            RestartOverlayIdleTimer();
        }

        private void RestartOverlayIdleTimer()
        {
            _overlayIdleTimer.Stop();
            _overlayIdleTimer.Start();
        }

        private void OverlayIdleTimer_Tick(object? sender, object e)
        {
            _overlayIdleTimer.Stop();
            if (_overlayMode && (_controlPanelIsVisible || _panelAnimating))
            {
                StartPanelAnimation(false);
                OnPanelVisibleChanged?.Invoke(true);
            }
        }

        private void HideDelayTimer_Tick(object? sender, object e)
        {
            _hideDelayTimer.Stop();
            if (_overlayMode && (_controlPanelIsVisible || _panelAnimating))
            {
                StartPanelAnimation(false);
                OnPanelVisibleChanged?.Invoke(true);
            }
        }

        // While a slider owns focus its arrow keys are the slider's own input;
        // the keyboard hook (MpvPlayerPage_Input) checks UiFocusInSlider to
        // avoid forwarding them to mpv as well (double seek).
        private void ProgressSlider_GotFocus(object sender, RoutedEventArgs e) => mpv_winui.AppContext.UiFocusInSlider = true;
        private void ProgressSlider_LostFocus(object sender, RoutedEventArgs e) => mpv_winui.AppContext.UiFocusInSlider = false;
        private void VolumeSlider_GotFocus(object sender, RoutedEventArgs e) => mpv_winui.AppContext.UiFocusInSlider = true;
        private void VolumeSlider_LostFocus(object sender, RoutedEventArgs e) => mpv_winui.AppContext.UiFocusInSlider = false;

        private void UpdateTimeTexts(double position, double duration)
        {
            TimeElapsedElement.Text = FormatTime(position);
            TimeRemainingElement.Text = FormatTime(duration);
            CompactTimeText.Text = $"{FormatCompactTime(position)}/{FormatCompactTime(duration)}";
        }

        private static string FormatCompactTime(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:D2}.{ts.Minutes:D2}.{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}.{ts.Seconds:D2}";
        }

        /// <summary>
        /// 鍘熺増 keeps the upstream control order. 灞呬腑 reorders the buttons to
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
            var custom = ParseCustomOrder();
            var zones = ParseZones(layout);

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
                left = ReorderMovable(leftMovable.ToArray(), custom);
                middle =
                [
                    PreviousTrackButton, SkipBackwardButton,
                    PlayPauseButton, SkipForwardButton,
                    NextTrackButton,
                ];
                right = ReorderMovable(rightMovable.ToArray(), custom);
            }
            else
            {
                left =
                [
                    PlayPauseButton, PreviousTrackButton, NextTrackButton, SkipBackwardButton,
                    SkipForwardButton,
                    .. ReorderMovable(leftMovable.ToArray(), custom),
                ];
                middle = [];
                right = ReorderMovable(rightMovable.ToArray(), custom);
            }

            ApplyBarOrders(LeftCommandBar, MiddleCommandBar, RightCommandBar, left, middle, right);
        }

        /// <summary>Parses the persisted per-id zone overrides ("id:0,id:2").</summary>
        private static Dictionary<string, int> ParseZones(string layout)
        {
            var zones = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var setting = layout == "modernx"
                ? AppContext.AppSetting.ControlBarZonesModernX
                : AppContext.AppSetting.ControlBarZonesClassic;
            foreach (var token in (setting ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon >= token.Length - 1)
                {
                    continue;
                }
                if (int.TryParse(token[(colon + 1)..], out var zone) && (zone == 0 || zone == 2))
                {
                    zones[token[..colon]] = zone;
                }
            }
            return zones;
        }

        /// <summary>
        /// Parses the custom order of the active layout style into the allowed
        /// canvas ids. 原版 and 居中 keep separate orders so editing one style
        /// never reorders the other.
        /// </summary>
        private static List<string> ParseCustomOrder()
        {
            var allowed = new[] { "volume", "tracks", "random", "panel", "aspect", "fullwindow", "fullscreen", "pip" };
            var layout = NormalizeControlBarLayout(AppContext.AppSetting.ControlBarLayout);
            var order = layout == "modernx"
                ? AppContext.AppSetting.ControlBarCustomOrderModernX
                : AppContext.AppSetting.ControlBarCustomOrderClassic;
            return order
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => allowed.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Reorders a command bar's canvas buttons to the custom order. Only
        /// ids present in this partition are affected; the rest keep their
        /// default relative order. "volume" maps to two controls (mute + slider).
        /// </summary>
        private static ICommandBarElement[] ReorderMovable((string Id, ICommandBarElement Element)[] defaults, IReadOnlyList<string> custom)
        {
            var result = new List<ICommandBarElement>(defaults.Length);
            var remaining = new HashSet<string>(defaults.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var id in custom)
            {
                if (!remaining.Remove(id))
                {
                    continue;
                }
                foreach (var (did, el) in defaults)
                {
                    if (string.Equals(did, id, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(el);
                    }
                }
            }
            // ids not mentioned in the custom order keep their default position.
            foreach (var (id, el) in defaults)
            {
                if (remaining.Contains(id))
                {
                    result.Add(el);
                }
            }
            return result.ToArray();
        }

        private static void ApplyBarOrders(
            CommandBar left,
            CommandBar middle,
            CommandBar right,
            ICommandBarElement[] leftDesired,
            ICommandBarElement[] middleDesired,
            ICommandBarElement[] rightDesired)
        {
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

        private static string NormalizeControlBarLayout(string? value)
        {
            return value switch
            {
                "modernx" or "center" or "right" => "modernx",
                _ => "classic",
            };
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

        public MpvMediaPlayer? MediaPlayer
        {
            get
            {
                return _mediaPlayer;
            }
            set
            {
                if (_mediaPlayer != value)
                {
                    RemoveEventListeners();

                    _mediaPlayer = value;

                    if (null != value)
                    {
                        AddEventListeners();

                        UpdateShuffleButtonUI();
                        UpdateRepeatButtonUI();
                        VolumeSlider.Value2 = _mediaPlayer?.Volume ?? 50; //TODO

                        // Initialize time/progress from the current media state.
                        // The position timer only refreshes while playing, so a
                        // control created while paused (e.g. the PiP window)
                        // would otherwise keep the placeholder "00.00/00.00".
                        UpdateProgressSliderValue(value.Position, value.Duration);
                        UpdateTimeTexts(value.Position, value.Duration);
                        UpdateChapterMarks();
                    }
                }
            }
        }

        public Visibility FullWindowButtonVisibility
        {
            get
            {
                return (Visibility)GetValue(FullWindowButtonVisibilityProperty);
            }
            set
            {
                SetValue(FullWindowButtonVisibilityProperty, value);
            }
        }

        public Visibility NextTrackButtonVisibility
        {
            get
            {
                return (Visibility)GetValue(NextTrackButtonVisibilityProperty);
            }
            set
            {
                SetValue(NextTrackButtonVisibilityProperty, value);
            }
        }

        public bool IsNextTrackButtonEnabled
        {
            get
            {
                return (bool)GetValue(IsNextTrackButtonEnabledProperty);
            }
            set
            {
                SetValue(IsNextTrackButtonEnabledProperty, value);
            }
        }

        public Visibility PreviousTrackButtonVisibility
        {
            get
            {
                return (Visibility)GetValue(PreviousTrackButtonVisibilityProperty);
            }
            set
            {
                SetValue(PreviousTrackButtonVisibilityProperty, value);
            }
        }

        public bool IsPreviousTrackButtonEnabled
        {
            get
            {
                return (bool)GetValue(IsPreviousTrackButtonEnabledProperty);
            }
            set
            {
                SetValue(IsPreviousTrackButtonEnabledProperty, value);
            }
        }

        private void PlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Re-hook media events on every load: Unloaded removes them, so a
            // reload cycle (fullscreen presenter switch, window hide/restore,
            // navigation) would otherwise leave the bar dead (volume/repeat/
            // shuffle/duration/seek updates stop). Remove-then-add keeps the
            // subscription exactly once even when the MediaPlayer setter has
            // already subscribed.
            RemoveEventListeners();
            AddEventListeners();

            RootGrid.PointerMoved += RootGrid_PointerMoved;
            PiPButton.Click += OnPiPClick;
            AppContext.SettingChanged += OnAppSettingChanged;
            PlayPauseButton.Click += OnPlayPauseClick;
            SkipBackwardButton.Click += SkipBackwardButton_Click;
            SkipForwardButton.Click += SkipForwardButton_Click;
            VolumeMuteButton.Click += OnMuteClick;
            FullScreenButton.Click += OnFullScreenClick;
            FullWindowButton.Click += FullWindowButton_Click;
            RepeatButton.Click += OnRepeatClick;
            ShuffleButton.Click += OnShuffleClick;
            TrackSelectionButton.Click += TrackSelectionButton_Click;
            ZoomButton.Click += ZoomButton_Click;
            PreviousTrackButton.Click += PreviousTrackButton_Click;
            NextTrackButton.Click += NextTrackButton_Click;

            foreach (var item in PlaybackRateFlyout.Items)
            {
                if (item is MenuFlyoutItem menuFlyoutItem)
                {
                    menuFlyoutItem.Click += PlaybackRateFlyout_MenuFlyoutItem_Click;
                }
            }

            TimeElapsedElement.Text = "00:00";
            TimeRemainingElement.Text = "00:00";
            CompactTimeText.Text = "00.00/00.00";
            ProgressSlider.ValueChanged += OnPositionSliderValueChanged;
            ProgressSlider.PointerPressed += ProgressSlider_PointerPressed;
            ProgressSlider.PointerReleased += ProgressSlider_PointerReleased;
            ProgressSlider.Tapped += ProgressSlider_Tapped;
            DelayFlyout.Opened += DelayFlyout_Opened;
            ProgressSlider.GotFocus += ProgressSlider_GotFocus;
            ProgressSlider.LostFocus += ProgressSlider_LostFocus;
            VolumeSlider.GotFocus += VolumeSlider_GotFocus;
            VolumeSlider.LostFocus += VolumeSlider_LostFocus;
            if (AppContext.AppSetting.EnableVideoPreview)
            {
                ProgressSlider.PointerEntered += ProgressSlider_PointerEntered;
                ProgressSlider.PointerMoved += ProgressSlider_PointerMoved;
                ProgressSlider.PointerExited += ProgressSlider_PointerExited;
            }

            VolumeSlider.ValueChanged2 += OnVolumeSliderValueChanged;

            _positionUpdateTimer.Tick += OnPositionUpdateTimerTick;
            _positionUpdateTimer.Start();
            _overlayIdleTimer.Tick += OverlayIdleTimer_Tick;

            UpdateToolbarVisibility(ActualWidth);
            //UpdatePlaybackStatusUI(false);
            //UpdatePlayPauseUI(false);
            //UpdateVolumeUI(false);
            //UpdateCompactUI(false);
            //UpdateFullScreenUI();
            //UpdateRepeatButtonUI();

            this.SizeChanged += PlayerControl_SizeChanged;

            if (_mediaPlayer is { } player)
            {
                VolumeSlider.Value2 = player.Volume;
                UpdateProgressSliderValue(player.Position, player.Duration);
                UpdateTimeTexts(player.Position, player.Duration);
            }
        }

        private void PlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            PiPButton.Click -= OnPiPClick;
            RootGrid.PointerMoved -= RootGrid_PointerMoved;
            AppContext.SettingChanged -= OnAppSettingChanged;
            PlayPauseButton.Click -= OnPlayPauseClick;
            SkipBackwardButton.Click -= SkipBackwardButton_Click;
            SkipForwardButton.Click -= SkipForwardButton_Click;
            VolumeMuteButton.Click -= OnMuteClick;
            FullScreenButton.Click -= OnFullScreenClick;
            FullWindowButton.Click -= FullWindowButton_Click;
            RepeatButton.Click -= OnRepeatClick;
            ShuffleButton.Click -= OnShuffleClick;
            foreach (var item in PlaybackRateFlyout.Items)
            {
                if (item is MenuFlyoutItem menuFlyoutItem)
                {
                    menuFlyoutItem.Click -= PlaybackRateFlyout_MenuFlyoutItem_Click;
                }
            }

            ProgressSlider.ValueChanged -= OnPositionSliderValueChanged;
            ProgressSlider.PointerPressed -= ProgressSlider_PointerPressed;
            ProgressSlider.PointerReleased -= ProgressSlider_PointerReleased;
            ProgressSlider.Tapped -= ProgressSlider_Tapped;
            DelayFlyout.Opened -= DelayFlyout_Opened;
            ProgressSlider.GotFocus -= ProgressSlider_GotFocus;
            ProgressSlider.LostFocus -= ProgressSlider_LostFocus;
            VolumeSlider.GotFocus -= VolumeSlider_GotFocus;
            VolumeSlider.LostFocus -= VolumeSlider_LostFocus;
            ProgressSlider.PointerEntered -= ProgressSlider_PointerEntered;
            ProgressSlider.PointerMoved -= ProgressSlider_PointerMoved;
            ProgressSlider.PointerExited -= ProgressSlider_PointerExited;

            VolumeSlider.ValueChanged2 -= OnVolumeSliderValueChanged;

            SizeChanged -= PlayerControl_SizeChanged;
            _positionUpdateTimer.Stop();
            _positionUpdateTimer.Tick -= OnPositionUpdateTimerTick;
            _panelAnimationTimer.Stop();
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _overlayIdleTimer.Stop();
            _overlayIdleTimer.Tick -= OverlayIdleTimer_Tick;
            _hideDelayTimer.Stop();
            _hideDelayTimer.Tick -= HideDelayTimer_Tick;

            RemoveEventListeners();
        }

        private void AddEventListeners()
        {
            _mediaPlayer?.MediaOpened += MediaPlayer_MediaOpened;
            _mediaPlayer?.MediaFailed += MediaPlayer_MediaFailed;
            _mediaPlayer?.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            _mediaPlayer?.BufferingStarted += PlaybackSession_BufferingStarted;
            _mediaPlayer?.BufferingEnded += PlaybackSession_BufferingEnded;
            _mediaPlayer?.NaturalDurationChanged += PlaybackSession_NaturalDurationChanged;
            _mediaPlayer?.VolumeChangedChanged += PlaybackSession_VolumeChangedChanged;
            _mediaPlayer?.Seeked += MediaPlayer_Seeked;
            _mediaPlayer?.RepeatStateChanged += MediaPlayer_RepeatStateChanged;
            _mediaPlayer?.ShuffleEnabledChanged += MediaPlayer_ShuffleEnabledChanged;
        }

        private void RemoveEventListeners()
        {
            _mediaPlayer?.MediaOpened -= MediaPlayer_MediaOpened;
            _mediaPlayer?.MediaFailed -= MediaPlayer_MediaFailed;
            _mediaPlayer?.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            _mediaPlayer?.BufferingStarted -= PlaybackSession_BufferingStarted;
            _mediaPlayer?.BufferingEnded -= PlaybackSession_BufferingEnded;
            _mediaPlayer?.NaturalDurationChanged -= PlaybackSession_NaturalDurationChanged;
            _mediaPlayer?.VolumeChangedChanged -= PlaybackSession_VolumeChangedChanged;
            _mediaPlayer?.Seeked -= MediaPlayer_Seeked;
            _mediaPlayer?.RepeatStateChanged -= MediaPlayer_RepeatStateChanged;
            _mediaPlayer?.ShuffleEnabledChanged -= MediaPlayer_ShuffleEnabledChanged;
        }

        private void NextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.PlaylistNext();
        }

        private void PreviousTrackButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.PlaylistPrevious();
        }

        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomSelectionFlyout.Items.Clear();

            var item = new MenuFlyoutItem() { Text = AppContext.AppLang.MoreZoomAuto, Tag = "no", };
            item.Click += ZoomSelectionMenu_Click;
            ZoomSelectionFlyout.Items.Add(item);

            item = new MenuFlyoutItem() { Text = "4:3", Tag = "4:3", };
            item.Click += ZoomSelectionMenu_Click;
            ZoomSelectionFlyout.Items.Add(item);

            item = new MenuFlyoutItem() { Text = "16:9", Tag = "16:9", };
            item.Click += ZoomSelectionMenu_Click;
            ZoomSelectionFlyout.Items.Add(item);

            item = new MenuFlyoutItem() { Text = "16:10", Tag = "16:10", };
            item.Click += ZoomSelectionMenu_Click;
            ZoomSelectionFlyout.Items.Add(item);

            if (sender is MenuFlyoutItem)
            {
                ZoomSelectionFlyout.ShowAt(ZoomButton);
            }
        }

        private void ZoomSelectionMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItemBase item && item.Tag is string ar)
            {
                _mediaPlayer?.AspectRatio = ar;
            }
        }

        private void TrackSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null)
            {
                return;
            }

            try
            {
                TrackSelectorControl.VideoTrackSelected -= TrackSelectorControl_VideoTrackSelected;
                TrackSelectorControl.LoadVideoTracks(_mediaPlayer?.VideoTracks() ?? []);
                TrackSelectorControl.VideoTrackSelected += TrackSelectorControl_VideoTrackSelected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackSelectionButton_Click video error: {ex.Message}");
            }

            try
            {
                TrackSelectorControl.SubtitleTrackSelected -= TrackSelectorControl_SubtitleTrackSelected;
                TrackSelectorControl.LoadSubtitleTracks(_mediaPlayer?.SubtitleTracks() ?? [], AppContext.AppLang.Off);
                TrackSelectorControl.SubtitleTrackSelected += TrackSelectorControl_SubtitleTrackSelected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackSelectionButton_Click sub error: {ex.Message}");
            }

            try
            {
                TrackSelectorControl.AudioTrackSelected -= TrackSelectorControl_AudioTrackSelected;
                TrackSelectorControl.LoadAudioTracks(_mediaPlayer?.AudioTracks() ?? []);
                TrackSelectorControl.AudioTrackSelected += TrackSelectorControl_AudioTrackSelected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackSelectionButton_Click audio error: {ex.Message}");
            }

            try
            {
                TrackSelectorControl.SecondSubTrackSelected -= TrackSelectorControl_SecondSubTrackSelected;
                TrackSelectorControl.LoadSecondSubtitleTracks(_mediaPlayer?.SecondSubtitleTracks() ?? [], AppContext.AppLang.Off);
                TrackSelectorControl.SecondSubTrackSelected += TrackSelectorControl_SecondSubTrackSelected;
                TrackSelectorControl.SetSecondSubVisibility(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackSelectionButton_Click second-sub error: {ex.Message}");
            }
        }

        private void TrackSelectorControl_VideoTrackSelected(object? sender, int trackIndex)
        {
            _mediaPlayer?.CurrentVideoTrack = trackIndex;
        }

        private void TrackSelectorControl_SubtitleTrackSelected(object? sender, int trackIndex)
        {
            _mediaPlayer?.CurrentSubtitleTrack = trackIndex;
        }

        private void TrackSelectorControl_AudioTrackSelected(object? sender, int trackIndex)
        {
            _mediaPlayer?.CurrentAudioTrack = trackIndex;
        }

        private void TrackSelectorControl_SecondSubTrackSelected(object? sender, int trackIndex)
        {
            _mediaPlayer?.CurrentSecondSubtitleTrack = trackIndex;
        }

        private void PlaybackRateFlyout_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null || sender is not MenuFlyoutItem item)
            {
                return;
            }
            if (item.Tag is string tag && tag == "custom")
            {
                CustomRateItem_Click(sender, e);
                return;
            }
            if (double.TryParse(item.Tag?.ToString(), out double speed))
            {
                _mediaPlayer.PlaybackRate = speed;
            }
        }


        private void SkipForwardButton_Click(object sender, RoutedEventArgs e)
        {
            Forward();
        }

        private void SkipBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            Backward();
        }

        public void Forward()
        {
            _mediaPlayer?.Position += 10;
        }

        public void Backward()
        {
            _mediaPlayer?.Position -= 10;
        }

        private async void PlaybackSession_PlaybackStateChanged(MpvMediaPlayer sender, bool args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (args)
                {
                    _positionUpdateTimer.Stop();
                }
                else
                {
                    _positionUpdateTimer.Start();
                }

                UpdatePlayPauseUI(args, true);
            });
        }

        private async void MediaPlayer_MediaOpened(MpvMediaPlayer sender, object? args)
        {
            _hasError = false;
            _sourceLoaded = true;
            DispatcherQueue.RunAsync(() =>
            {
                UpdateProgressSliderValue(0, sender.Duration);
                if (sender.Duration > 0)
                {
                    ApplyAdaptiveSliderStep(sender.Duration);
                }

                UpdatePlaybackStatusUI(false);
                //UpdatePlayPauseUI(false);
                UpdateVolumeUI(false);
            });
        }

        private async void MediaPlayer_Seeked(MpvMediaPlayer sender, object? args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                _isBuffering = false;
                UpdatePlaybackStatusUI(true);
                UpdateTimeTexts(sender?.Position ?? 0, sender?.Duration ?? 0);
            });
        }

        private void ApplyAdaptiveSliderStep(double durationSeconds)
        {
            if (durationSeconds <= 0)
            {
                return;
            }

            var small = Math.Max(10, durationSeconds / 40.0);
            var large = Math.Max(10, durationSeconds / 20.0);

            small = Math.Round(small);
            large = Math.Round(large);

            ProgressSlider.SmallChange = small;
            ProgressSlider.LargeChange = large;
            ProgressSlider.StepFrequency = 1;
        }

        private async void MediaPlayer_MediaFailed(MpvMediaPlayer sender, string? args)
        {
            _hasError = true;
            _sourceLoaded = false;

            DispatcherQueue.RunAsync(() =>
            {
                ErrorTextBlock.Text = args;
                UpdatePlaybackStatusUI(true);
            });
        }

        private async void PlaybackSession_BufferingStarted(MpvMediaPlayer sender, object? args)
        {
            // Local files briefly enter mpv's buffering state when seeking;
            // do not flash the loading strip for user-initiated scrubs.
            if (_isDragging || Environment.TickCount64 < _suppressBufferingUntil)
            {
                return;
            }
            _isBuffering = true;
            DispatcherQueue.RunAsync(() => { UpdatePlaybackStatusUI(true); });
        }

        private async void PlaybackSession_BufferingEnded(MpvMediaPlayer sender, object? args)
        {
            _isBuffering = false;
            DispatcherQueue.RunAsync(() => { UpdatePlaybackStatusUI(true); });
        }

        private async void PlaybackSession_NaturalDurationChanged(MpvMediaPlayer sender, object? args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (sender.Duration > 0)
                {
                    UpdateProgressSliderValue(null, sender.Duration);
                }
            });
        }

        private async void PlaybackSession_VolumeChangedChanged(MpvMediaPlayer sender, int volume)
        {
            DispatcherQueue.RunAsync(() =>
            {
                VolumeSlider.Value2 = volume;
                UpdateVolumeUI(true);
            });
        }

        private void MediaPlayer_RepeatStateChanged(MpvMediaPlayer sender, RepeatState state)
        {
            DispatcherQueue.RunAsync(UpdateRepeatButtonUI);
        }

        private void MediaPlayer_ShuffleEnabledChanged(MpvMediaPlayer sender, bool enabled)
        {
            DispatcherQueue.RunAsync(UpdateShuffleButtonUI);
        }

        private void OnPlayPauseClick(object sender, RoutedEventArgs e)
        {
            TogglePlay();
        }

        public void TogglePlay()
        {
            if (MediaPlayer == null)
            {
                return;
            }

            if (MediaPlayer.Playing)
            {
                MediaPlayer?.Pause();
            }
            else
            {
                MediaPlayer?.Play();
            }
        }

        private void OnMuteClick(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer == null)
            {
                return;
            }

            if (VolumeSliderContainer.Visibility != Visibility.Visible)
            {
                var control = new VolumeFlyoutControl(MediaPlayer);
                var flyout = new Flyout { Content = control };
                flyout.ShowAt(VolumeMuteButton);
                return;
            }

            MediaPlayer.IsMuted = !MediaPlayer.IsMuted;
            UpdateVolumeUI(true);
        }

        private void FullWindowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullWindow();
        }

        public bool ToggleFullWindow()
        {
            var full = OnFullWindowRequest?.Invoke();
            if (null != full)
            {
                UpdateFullWindowUI(full.Value);
                return full.Value;
            }

            return false;
        }

        private void OnFullScreenClick(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        public void ToggleFullScreen()
        {
            OnFullScreenRequest?.Invoke();
        }

        public void UpdateFullScreen(bool enabled)
        {
            UpdateFullScreenUI(enabled);
        }

        private void OnRepeatClick(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer is { } player)
            {
                player.RepeatState = player.RepeatState switch
                {
                    RepeatState.All => RepeatState.One,
                    RepeatState.One => RepeatState.None,
                    _ => RepeatState.All,
                };
                UpdateRepeatButtonUI();
            }
        }

        private void OnShuffleClick(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer is { } player)
            {
                if (player.ShuffleEnabled)
                {
                    player.ShuffleEnabled = false;
                }
                else
                {
                    player.ShuffleEnabled = true;

                    //TODO 
                    player.PlaylistShuffle();
                }
                UpdateShuffleButtonUI();
            }
        }

        private void OnPositionSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (MediaPlayer == null)
            {
                return;
            }

            if (!_isInScrubMode)
            {
                MediaPlayer?.Position = e.NewValue;
                UpdateTimeTexts(e.NewValue, MediaPlayer?.Duration ?? 0);
                if (AppContext.AppSetting.EnableVideoPreview && ProgressSlider.Maximum > 0)
                {
                    UpdatePreview(e.NewValue / ProgressSlider.Maximum);
                }
            }
        }

        private void OnVolumeSliderValueChanged(object sender, double value)
        {
            MediaPlayer?.Volume = value;
        }

        private void OnPositionUpdateTimerTick(object? sender, object e)
        {
            if (MediaPlayer?.Playing == true)
            {
                UpdateProgressSliderValue(MediaPlayer?.Position);
                UpdateTimeTexts(MediaPlayer?.Position ?? 0, MediaPlayer?.Duration ?? 0);
                OnPositionChanged?.Invoke();
            }
        }

        private void UpdateProgressSliderValue(double? value, double? max = null)
        {
            _isInScrubMode = true;
            if (null != value)
            {
                ProgressSlider.Value = value ?? 0;
            }

            if (null != max)
            {
                ProgressSlider.Maximum = max ?? 0;
            }

            _isInScrubMode = false;
        }

        private void AbLoopButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer?.ToggleAbLoop();
            UpdateAbLoopMarks();
        }

        // ===== Unified control panel =====
        private bool _panelBuilt;
        private bool _panelUpdating;
        private readonly List<Slider> _panelEqSliders = [];
        private Slider? _panelVolumeSlider;
        private Slider? _panelBrightnessSlider;
        private Slider? _panelContrastSlider;
        private Slider? _panelSaturationSlider;
        private Slider? _panelHueSlider;
        private ToggleButton? _panelEqOffToggle;
        private ComboBox? _panelAudioDeviceBox;
        private ComboBox? _panelFontBox;
        private TextBlock? _panelAbTimes;

        private void ControlPanelFlyout_Opened(object sender, object e)
        {
            EnsureControlPanel();
            SyncPanelValues();
        }

        private void EnsureControlPanel()
        {
            if (_panelBuilt)
            {
                return;
            }
            _panelBuilt = true;

            var lang = AppContext.AppLang;
            ControlPanelRoot.Children.Clear();

            var title = new Grid();
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.Children.Add(new TextBlock
            {
                Text = lang.ControlBarIconPanel,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var close = new Button
            {
                Padding = new Thickness(2),
                Background = null,
                BorderThickness = new Thickness(0),
                Foreground = mpv_winui.Modules.Common.View.ThemeResource.Brush(this, "TextFillColorSecondaryBrush"),
                Content = new FontIcon { Glyph = "\uE711", FontSize = 12 },
            };
            close.Click += (_, _) => ControlPanelFlyout.Hide();
            Grid.SetColumn(close, 1);
            title.Children.Add(close);
            ControlPanelRoot.Children.Add(title);

            var pivot = new Pivot
            {
                MinHeight = 320,
                IsHeaderItemsCarouselEnabled = false,
                IsTabStop = false,
            };
            var pages = new (string Text, string Glyph, Action<StackPanel> Build)[]
            {
                (lang.SettingsCategoryAudio, "\uE8B1", BuildPanelAudio),
                (lang.SettingsCategoryVideo, "\uE790", BuildPanelVideo),
                (lang.SettingsCategorySubtitles, "\uED1F", BuildPanelSubtitles),
                (lang.SettingsCategoryPlayback, "\uE768", BuildPanelPlayback),
            };
            foreach (var (text, glyph, build) in pages)
            {
                var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
                header.Children.Add(new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                var content = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
                build(content);
                pivot.Items.Add(new PivotItem { Header = header, Content = content });
            }
            ControlPanelRoot.Children.Add(pivot);
        }

        private Grid PanelSection(string labelText, params UIElement[] controls)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = labelText, VerticalAlignment = VerticalAlignment.Center });
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            foreach (var control in controls)
            {
                right.Children.Add(control);
            }
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return grid;
        }

        private Button PanelResetButton(string property, Slider slider)
        {
            var button = new Button { Content = AppContext.AppLang.Reset, Padding = new Thickness(6, 3, 6, 3), MinWidth = 0 };
            button.Click += (_, _) =>
            {
                _panelUpdating = true;
                try
                {
                    slider.Value = 0;
                }
                finally
                {
                    _panelUpdating = false;
                }
                MediaPlayer?.Command("set", property, "0");
            };
            return button;
        }

        private static StackPanel PanelEqColumn(string labelText, Slider slider)
        {
            var column = new StackPanel
            {
                Spacing = 4,
                Width = 34,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            slider.Orientation = Orientation.Vertical;
            slider.Height = 150;
            slider.Width = 30;
            slider.HorizontalAlignment = HorizontalAlignment.Center;
            column.Children.Add(slider);
            column.Children.Add(new TextBlock
            {
                Text = labelText,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return column;
        }

        private Slider PanelPropertySlider(string property, double min, double max, double step)
        {
            var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = step };
            slider.ValueChanged += (_, _) =>
            {
                if (_panelUpdating)
                {
                    return;
                }
                MediaPlayer?.Command("set", property, slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            };
            return slider;
        }

        private void BuildPanelAudio(StackPanel root)
        {
            var lang = AppContext.AppLang;

            _panelEqOffToggle = new ToggleButton
            {
                Content = $"{lang.PanelEqualizer} {lang.Off}",
                IsChecked = true,
                MinWidth = 0,
                Padding = new Thickness(6, 3, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _panelEqOffToggle.Checked += (_, _) =>
            {
                if (!_panelUpdating)
                {
                    MediaPlayer?.Command("set", "af", "");
                }
            };
            _panelEqOffToggle.Unchecked += (_, _) =>
            {
                if (!_panelUpdating)
                {
                    ApplyEqualizer();
                }
            };

            _panelAudioDeviceBox = new ComboBox
            {
                MinWidth = 160,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = lang.SettingsAudioDevice,
            };
            var devices = AppContext.GetAudioDevices?.Invoke();
            if (devices is not null)
            {
                foreach (var device in devices)
                {
                    var label = string.IsNullOrWhiteSpace(device.Description) ? device.Name : device.Description;
                    _panelAudioDeviceBox.Items.Add(new ComboBoxItem { Content = label, Tag = device.Name });
                }
            }
            _panelAudioDeviceBox.SelectionChanged += (_, _) =>
            {
                if (_panelUpdating || _panelAudioDeviceBox.SelectedItem is not ComboBoxItem { Tag: string name })
                {
                    return;
                }
                MediaPlayer?.Command("set", "audio-device", name);
            };

            var presetButton = new Button { Content = lang.PanelPreset, Padding = new Thickness(6, 3, 6, 3), MinWidth = 0 };
            presetButton.Flyout = BuildPanelPresetFlyout();

            var topRow = new Grid { ColumnSpacing = 8 };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.Children.Add(_panelEqOffToggle);
            Grid.SetColumn(_panelAudioDeviceBox, 1);
            topRow.Children.Add(_panelAudioDeviceBox);
            Grid.SetColumn(presetButton, 2);
            topRow.Children.Add(presetButton);
            root.Children.Add(topRow);

            var bandLabels = new[] { "60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K" };
            var bandRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            for (var i = 0; i < bandLabels.Length; i++)
            {
                var slider = new Slider { Minimum = -12, Maximum = 12, StepFrequency = 0.5, Value = _eqGains[i] };
                var index = i;
                slider.ValueChanged += (_, _) =>
                {
                    _eqGains[index] = slider.Value;
                    if (!_panelUpdating)
                    {
                        ApplyEqualizer();
                    }
                };
                _panelEqSliders.Add(slider);
                bandRow.Children.Add(PanelEqColumn(bandLabels[i], slider));
            }

            _panelVolumeSlider = PanelPropertySlider("volume", 0, 150, 1);
            _panelVolumeSlider.Orientation = Orientation.Vertical;
            _panelVolumeSlider.Height = 210;
            _panelVolumeSlider.Width = 44;
            _panelVolumeSlider.HorizontalAlignment = HorizontalAlignment.Center;
            var volumeColumn = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            volumeColumn.Children.Add(new TextBlock
            {
                Text = lang.PanelMasterVolume,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            volumeColumn.Children.Add(_panelVolumeSlider);

            var audioGrid = new Grid { ColumnSpacing = 10 };
            audioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            audioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            audioGrid.Children.Add(bandRow);
            Grid.SetColumn(volumeColumn, 1);
            audioGrid.Children.Add(volumeColumn);
            root.Children.Add(audioGrid);
        }

        private MenuFlyout BuildPanelPresetFlyout()
        {
            var flyout = new MenuFlyout();
            var presets = new (string Name, double[] Gains)[]
            {
                ("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
                ("Bass", [6, 5, 4, 2, 0, 0, 0, 0, 0, 0]),
                ("Vocal", [0, -2, 0, 3, 4, 4, 3, 2, 0, 0]),
                ("Treble", [0, 0, 0, 0, 0, 1, 2, 3, 4, 5]),
            };
            foreach (var preset in presets)
            {
                var item = new MenuFlyoutItem { Text = preset.Name, Tag = preset.Gains };
                item.Click += (_, _) => ApplyPanelPreset((double[])item.Tag);
                flyout.Items.Add(item);
            }
            return flyout;
        }

        private void ApplyPanelPreset(double[] gains)
        {
            for (var i = 0; i < _eqGains.Count && i < gains.Length; i++)
            {
                _eqGains[i] = gains[i];
            }
            _panelUpdating = true;
            try
            {
                for (var i = 0; i < _panelEqSliders.Count && i < gains.Length; i++)
                {
                    _panelEqSliders[i].Value = gains[i];
                }
            }
            finally
            {
                _panelUpdating = false;
            }
            if (_panelEqOffToggle is { } toggle)
            {
                if (toggle.IsChecked == true)
                {
                    toggle.IsChecked = false; // the Unchecked handler applies the curve
                }
                else
                {
                    ApplyEqualizer();
                }
            }
        }

        private void BuildPanelVideo(StackPanel root)
        {
            var lang = AppContext.AppLang;
            _panelBrightnessSlider = PanelPropertySlider("brightness", -100, 100, 1);
            _panelContrastSlider = PanelPropertySlider("contrast", -100, 100, 1);
            _panelSaturationSlider = PanelPropertySlider("saturation", -100, 100, 1);
            _panelHueSlider = PanelPropertySlider("hue", -100, 100, 1);

            root.Children.Add(PanelSection(lang.PanelBrightness, _panelBrightnessSlider, PanelResetButton("brightness", _panelBrightnessSlider)));
            root.Children.Add(PanelSection(lang.PanelContrast, _panelContrastSlider, PanelResetButton("contrast", _panelContrastSlider)));
            root.Children.Add(PanelSection(lang.PanelSaturation, _panelSaturationSlider, PanelResetButton("saturation", _panelSaturationSlider)));
            root.Children.Add(PanelSection(lang.PanelHue, _panelHueSlider, PanelResetButton("hue", _panelHueSlider)));

            var sharp = new CheckBox { Content = lang.PanelSharpen, MinWidth = 0 };
            sharp.Checked += (_, _) => MediaPlayer?.Command("set", "vf", "lavfi=[unsharp=5:5:1.0]");
            sharp.Unchecked += (_, _) => MediaPlayer?.Command("set", "vf", "");
            var blur = new CheckBox { Content = lang.PanelBlur, MinWidth = 0 };
            blur.Checked += (_, _) => MediaPlayer?.Command("set", "vf", "lavfi=[gblur=sigma=1.0]");
            blur.Unchecked += (_, _) => MediaPlayer?.Command("set", "vf", "");
            var post = new CheckBox { Content = lang.PanelPost, MinWidth = 0 };
            post.Checked += (_, _) => MediaPlayer?.Command("set", "deband", "yes");
            post.Unchecked += (_, _) => MediaPlayer?.Command("set", "deband", "no");

            var capture = new Button
            {
                Padding = new Thickness(6, 3, 6, 3),
                MinWidth = 0,
                Content = new FontIcon { Glyph = "\uE722", FontSize = 16 },
            };
            ToolTipService.SetToolTip(capture, lang.PanelCapture);
            capture.Click += (_, _) => MediaPlayer?.Command("screenshot");

            var bottom = new Grid { ColumnSpacing = 8 };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var toggles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
            toggles.Children.Add(sharp);
            toggles.Children.Add(blur);
            toggles.Children.Add(post);
            bottom.Children.Add(toggles);
            Grid.SetColumn(capture, 1);
            bottom.Children.Add(capture);
            root.Children.Add(bottom);
        }

        private void BuildPanelSubtitles(StackPanel root)
        {
            var lang = AppContext.AppLang;

            _panelFontBox = new ComboBox
            {
                IsEditable = true,
                MinWidth = 150,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Text = "Segoe UI",
                ItemsSource = new[]
                {
                    "sans-serif", "Segoe UI", "Microsoft YaHei", "SimSun", "DengXian",
                    "SimHei", "Consolas", "Source Han Sans SC", "LXGW WenKai Mono Lite",
                },
            };
            _panelFontBox.SelectionChanged += (_, _) =>
            {
                if (_panelUpdating)
                {
                    return;
                }
                var font = (_panelFontBox.SelectedItem as string) ?? _panelFontBox.Text;
                if (!string.IsNullOrWhiteSpace(font))
                {
                    MediaPlayer?.Command("set", "sub-font", font);
                }
            };

            var sizeBox = new NumberBox
            {
                Minimum = 1,
                Maximum = 200,
                Value = AppContext.AppSetting.SubFontSize,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 110,
            };
            sizeBox.ValueChanged += (_, _) =>
            {
                if (_panelUpdating || double.IsNaN(sizeBox.Value))
                {
                    return;
                }
                var value = (int)Math.Round(sizeBox.Value);
                AppContext.AppSetting.SubFontSize = value;
                MediaPlayer?.Command("set", "sub-font-size", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            };

            var fontRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            fontRow.Children.Add(_panelFontBox);
            fontRow.Children.Add(sizeBox);
            root.Children.Add(PanelSection(lang.SettingsSubFont, fontRow));

            var moves = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var up = new Button { Content = lang.PanelMoveUp, Padding = new Thickness(6, 3, 6, 3) };
            up.Click += (_, _) => MediaPlayer?.Command("add", "sub-pos", "-1");
            var down = new Button { Content = lang.PanelMoveDown, Padding = new Thickness(6, 3, 6, 3) };
            down.Click += (_, _) => MediaPlayer?.Command("add", "sub-pos", "1");
            var left = new Button { Content = lang.PanelMoveLeft, Padding = new Thickness(6, 3, 6, 3) };
            left.Click += (_, _) => MediaPlayer?.Command("add", "sub-margin-x", "-5");
            var right = new Button { Content = lang.PanelMoveRight, Padding = new Thickness(6, 3, 6, 3) };
            right.Click += (_, _) => MediaPlayer?.Command("add", "sub-margin-x", "5");
            moves.Children.Add(up);
            moves.Children.Add(down);
            moves.Children.Add(left);
            moves.Children.Add(right);
            root.Children.Add(PanelSection(lang.PanelMove, moves));

            var syncRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var slower = new Button { Content = lang.PanelSlower, Padding = new Thickness(6, 3, 6, 3) };
            slower.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "0.25");
            var normal = new Button { Content = lang.PanelNormal, Padding = new Thickness(6, 3, 6, 3) };
            normal.Click += (_, _) => MediaPlayer?.Command("set", "sub-delay", "0");
            var faster = new Button { Content = lang.PanelFaster, Padding = new Thickness(6, 3, 6, 3) };
            faster.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "-0.25");
            syncRow.Children.Add(slower);
            syncRow.Children.Add(normal);
            syncRow.Children.Add(faster);
            root.Children.Add(PanelSection(lang.PanelSync, syncRow));
        }

        private void BuildPanelPlayback(StackPanel root)
        {
            var lang = AppContext.AppLang;

            var seeks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var seekDefs = new (string Label, int Delta)[]
            {
                ("\uE72B\uE72B 1min", -60),
                ("\uE72B\uE72B 5sec", -5),
                ("5sec \uE72A\uE72A", 5),
                ("1min \uE72A\uE72A", 60),
            };
            foreach (var (label, delta) in seekDefs)
            {
                var button = new Button { Content = label, Padding = new Thickness(6, 3, 6, 3) };
                var offset = delta;
                button.Click += (_, _) =>
                {
                    if (MediaPlayer is not { } player)
                    {
                        return;
                    }
                    var target = player.Position + offset;
                    player.Command("seek", target.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "absolute");
                };
                seeks.Children.Add(button);
            }
            root.Children.Add(PanelSection(lang.PanelSeek, seeks));

            var speeds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var slower = new Button { Content = lang.PanelSlower, Padding = new Thickness(8, 3, 8, 3) };
            slower.Click += (_, _) => MediaPlayer?.Command("add", "speed", "-0.1");
            var normal = new Button { Content = lang.PanelNormal, Padding = new Thickness(8, 3, 8, 3) };
            normal.Click += (_, _) => MediaPlayer?.Command("set", "speed", "1");
            var faster = new Button { Content = lang.PanelFaster, Padding = new Thickness(8, 3, 8, 3) };
            faster.Click += (_, _) => MediaPlayer?.Command("add", "speed", "0.1");
            speeds.Children.Add(slower);
            speeds.Children.Add(normal);
            speeds.Children.Add(faster);
            root.Children.Add(PanelSection(lang.PanelSpeed, speeds));

            var aButton = new Button { Content = "A", MinWidth = 44, Padding = new Thickness(6, 3, 6, 3) };
            aButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-a");
                SyncPanelAbLoop();
            };
            var bButton = new Button { Content = "B", MinWidth = 44, Padding = new Thickness(6, 3, 6, 3) };
            bButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-b");
                SyncPanelAbLoop();
            };
            var resetButton = new Button { Content = lang.Reset, MinWidth = 56, Padding = new Thickness(6, 3, 6, 3) };
            resetButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-a", "no");
                MediaPlayer?.Command("ab-loop-b", "no");
                SyncPanelAbLoop();
            };
            _panelAbTimes = new TextBlock
            {
                Text = "00:00:00 ~ 00:00:00",
                MinWidth = 170,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };

            var repeatRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            repeatRow.Children.Add(aButton);
            repeatRow.Children.Add(_panelAbTimes);
            repeatRow.Children.Add(bButton);
            repeatRow.Children.Add(resetButton);
            root.Children.Add(PanelSection(lang.MoreRepeat, repeatRow));
        }

        private void SyncPanelValues()
        {
            if (MediaPlayer is not { } player)
            {
                return;
            }

            _panelUpdating = true;
            try
            {
                _panelVolumeSlider!.Value = player.Volume;
                SyncPanelAbLoop();
            }
            finally
            {
                _panelUpdating = false;
            }
        }

        private void SyncPanelAbLoop()
        {
            if (_panelAbTimes is null)
            {
                return;
            }
            var a = MediaPlayer?.AbLoopA ?? 0;
            var b = MediaPlayer?.AbLoopB ?? 0;
            _panelAbTimes.Text = $"{FormatPanelTime(a)} ~ {FormatPanelTime(b)}";
        }

        private static string FormatPanelTime(double seconds)
        {
            if (seconds <= 0)
            {
                return "00:00:00";
            }
            var t = TimeSpan.FromSeconds(seconds);
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        // ===== Equalizer =====
        private static readonly string[] EqualizerBands =
        [
            "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k",
        ];

        private readonly List<double> _eqGains = new(10) { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private void EqualizerFlyout_Opened(object sender, object e)
        {
            BuildEqualizerBands();
        }

        private void BuildEqualizerBands()
        {
            EqualizerBandsPanel.Items.Clear();
            for (int i = 0; i < EqualizerBands.Length; i++)
            {
                var band = EqualizerBands[i];
                var slider = new Slider
                {
                    Minimum = -12,
                    Maximum = 12,
                    Value = _eqGains[i],
                    Width = 160,
                    StepFrequency = 0.5,
                };
                int index = i;
                slider.ValueChanged += (_, _) =>
                {
                    _eqGains[index] = slider.Value;
                    ApplyEqualizer();
                };
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock { Text = band, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(slider, 1);
                row.Children.Add(label);
                row.Children.Add(slider);
                EqualizerBandsPanel.Items.Add(row);
            }
        }

        private void ApplyEqualizer()
        {
            if (MediaPlayer is null)
            {
                return;
            }
            // superequalizer gains order: 10 bands (31Hz..16kHz), where the
            // first value is the "bass" band and last the "treble".
            var gains = string.Join(":", _eqGains.Select(g => g.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));
            MediaPlayer.Command(["set", "af", $"lavfi=[superequalizer@eq:{gains}]"]);
        }

        private void EqualizerReset_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _eqGains.Count; i++)
            {
                _eqGains[i] = 0;
            }
            BuildEqualizerBands();
            MediaPlayer?.Command(["set", "af", ""]);
        }

        private void EqualizerOff_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer?.Command(["set", "af", ""]);
        }


        // ===== Audio / subtitle delay =====
        private bool _delaySliderUpdating;

        private void DelayFlyout_Opened(object sender, object e)
        {
            _delaySliderUpdating = true;
            AudioDelaySlider.Value = AppContext.AppSetting.AudioDelay;
            SubDelaySlider.Value = AppContext.AppSetting.SubDelay;
            _delaySliderUpdating = false;
        }

        private void AudioDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_delaySliderUpdating)
            {
                return;
            }
            var value = Math.Round(e.NewValue, 1);
            AppContext.AppSetting.AudioDelay = value;
            AppContext.SendMpvCommand($"no-osd set audio-delay {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        private void SubDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_delaySliderUpdating)
            {
                return;
            }
            var value = Math.Round(e.NewValue, 1);
            AppContext.AppSetting.SubDelay = value;
            AppContext.SendMpvCommand($"no-osd set sub-delay {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        private void DelayReset_Click(object sender, RoutedEventArgs e)
        {
            AudioDelaySlider.Value = 0;
            SubDelaySlider.Value = 0;
        }

        // ===== Custom playback rate =====
        private async void CustomRateItem_Click(object sender, RoutedEventArgs e)
        {
            var box = new TextBox { PlaceholderText = "e.g. 1.3 or 16" };
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.CustomRate,
                Content = box,
                PrimaryButtonText = "OK",
                CloseButtonText = AppContext.AppLang.Cancel,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary
                && double.TryParse(box.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rate)
                && rate > 0 && rate <= 100)
            {
                MediaPlayer?.Command(["set", "speed", rate.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)]);
            }
        }

        /// <summary>Positions the A/B markers on the progress bar from mpv's ab-loop properties.</summary>
        private void UpdateAbLoopMarks()
        {
            var duration = MediaPlayer?.Duration ?? 0;
            var a = MediaPlayer?.AbLoopA ?? -1;
            var b = MediaPlayer?.AbLoopB ?? -1;
            var width = ProgressSlider.ActualWidth;
            if (width <= 0)
            {
                return;
            }

            if (a > 0 && duration > 0)
            {
                AbLoopMarkA.Visibility = Visibility.Visible;
                Canvas.SetLeft(AbLoopMarkA, a / duration * width);
            }
            else
            {
                AbLoopMarkA.Visibility = Visibility.Collapsed;
            }

            if (b > 0 && duration > 0)
            {
                AbLoopMarkB.Visibility = Visibility.Visible;
                Canvas.SetLeft(AbLoopMarkB, b / duration * width);
            }
            else
            {
                AbLoopMarkB.Visibility = Visibility.Collapsed;
            }
        }

        private string FormatTime(double second)
        {
            var ts = TimeSpan.FromSeconds(second);
            return ts.TotalHours switch
            {
                >= 1 => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}",
                _ => $"{ts.Minutes:D2}:{ts.Seconds:D2}"
            };
        }

        public bool IsVisible()
        {
            return _controlPanelIsVisible;
        }

        public void ShowControlPanel()
        {
            if (_overlayMode)
            {
                _hideDelayTimer.Stop();
                // Re-show even while a hide animation is still running: the
                // visible flag only flips when the animation completes, so
                // checking it alone would swallow the re-show and make the bar
                // pop out only after the hide finished (felt like flicker).
                if (!_controlPanelIsVisible || (_panelAnimating && !_panelAnimationShow))
                {
                    StartPanelAnimation(true);
                    OnPanelVisibleChanged?.Invoke(false);
                }
                return;
            }

            if (!_controlPanelIsVisible)
            {
                StopPanelAnimations();
                ControlPanelGradient.Visibility = Visibility.Visible;
                ControlPanelGrid.Opacity = 0;
                TranslateVertical.Y = 48;
                ControlPanelGrid.Visibility = Visibility.Visible;
                _controlPanelIsVisible = true;

                var storyboard = new Storyboard
                {
                    Duration = TimeSpan.FromMilliseconds(180),
                };
                AddPanelAnimation(storyboard, "Opacity", 0, 1, 180);
                AddPanelAnimation(storyboard, "(UIElement.RenderTransform).(TranslateTransform.Y)", 48, 0, 180);
                storyboard.Begin();
                _showStoryboard = storyboard;
            }

            OnPanelVisibleChanged?.Invoke(false);
        }

        public void HideControlPanel()
        {
            if (_overlayMode)
            {
                // Debounce: a transient exit (pointer on the mask edge) must
                // not start and restart the retract animation repeatedly.
                _hideDelayTimer.Tick -= HideDelayTimer_Tick;
                _hideDelayTimer.Tick += HideDelayTimer_Tick;
                _hideDelayTimer.Start();
                return;
            }

            if (_controlPanelIsVisible)
            {
                StopPanelAnimations();
                _controlPanelIsVisible = false;

                var storyboard = new Storyboard
                {
                    Duration = TimeSpan.FromMilliseconds(150),
                };
                AddPanelAnimation(storyboard, "Opacity", 1, 0, 150);
                AddPanelAnimation(storyboard, "(UIElement.RenderTransform).(TranslateTransform.Y)", 0, 48, 150);
                storyboard.Completed += (_, _) =>
                {
                    if (!_controlPanelIsVisible)
                    {
                        // Collapse the whole 120px host border, not just the
                        // grid: the Auto-sized page row would otherwise keep a
                        // white (window background) strip below the video
                        // after the bar hides.
                        ControlPanelGradient.Visibility = Visibility.Collapsed;
                        ControlPanelGrid.Visibility = Visibility.Collapsed;
                        ControlPanelGrid.Opacity = 1;
                        TranslateVertical.Y = 0;
                    }
                    _hideStoryboard = null;
                };
                storyboard.Begin();
                _hideStoryboard = storyboard;
            }

            OnPanelVisibleChanged?.Invoke(true);
        }

        private Storyboard? _showStoryboard;
        private Storyboard? _hideStoryboard;

        private void StopPanelAnimations()
        {
            _panelGridVisual?.StopAnimation("Opacity");
            _panelGradientVisual?.StopAnimation("Opacity");
            if (_panelGridVisual is not null)
            {
                _panelGridVisual.Opacity = 1f;
            }
            if (_panelGradientVisual is not null)
            {
                _panelGradientVisual.Opacity = 1f;
            }
            _panelAnimationTimer.Stop();
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _showStoryboard?.Stop();
            _showStoryboard = null;
            _hideStoryboard?.Stop();
            _hideStoryboard = null;
            _panelAnimating = false;
        }

        /// <summary>
        /// Overlay tween: the main window uses a XAML Storyboard on the render
        /// transform. PiP (a second top-level window) cannot use a Storyboard
        /// (crashes the compositor) and must not animate the composition
        /// `Offset` (layout owns it, which strands the bar after zoom), so it
        /// uses a short DispatcherTimer tween on the XAML values instead.
        /// </summary>
        private void StartPanelAnimation(bool show)
        {
            if (_panelAnimating && _panelAnimationShow == show)
            {
                return;
            }

            _panelAnimating = true;
            _panelAnimationShow = show;

            if (!_isPiPHost)
            {
                StartMainWindowPanelAnimation(show);
                return;
            }

            StartPiPPanelAnimation(show);
        }

        /// <summary>
        /// Main-window overlay tween: XAML Storyboard on the render transform.
        /// TranslateTransform is layout-independent, so a resize/zoom while the
        /// bar is hidden cannot leave it stranded above the bottom edge (the
        /// composition Visual.Offset path snaps back to a stale layout value
        /// when the grid is Collapsed).
        /// </summary>
        private void StartMainWindowPanelAnimation(bool show)
        {
            // Stop only the in-flight storyboard. Do not call
            // StopPanelAnimations() here: it clears _panelAnimating, which
            // defeats the same-direction re-entry guard in StartPanelAnimation
            // and lets every pointer move restart the pop-out animation
            // (the bar visibly twitches).
            _showStoryboard?.Stop();
            _showStoryboard = null;
            _hideStoryboard?.Stop();
            _hideStoryboard = null;

            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }

            ControlPanelGradient.Opacity = 1;
            ControlPanelGrid.Opacity = 1;
            TranslateVertical.Y = 0;

            var storyboard = new Storyboard
            {
                Duration = TimeSpan.FromMilliseconds(180),
            };
            AddPanelAnimation(storyboard, "Opacity", show ? 0 : 1, show ? 1 : 0, 180, ControlPanelGrid);
            AddPanelAnimation(storyboard, "Opacity", show ? 0 : 1, show ? 1 : 0, 180, ControlPanelGradient);
            AddPanelAnimation(storyboard, "(UIElement.RenderTransform).(TranslateTransform.Y)", show ? 48 : 0, show ? 0 : 48, 180, ControlPanelGrid);
            storyboard.Completed += (_, _) => PanelAnimationCompleted(show);
            storyboard.Begin();

            if (show)
            {
                _showStoryboard = storyboard;
            }
            else
            {
                _hideStoryboard = storyboard;
            }
        }

        private void StartPiPPanelAnimation(bool show)
        {
            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }

            // Composition opacity multiplies the XAML opacity, so keep the
            // XAML base at 1 and animate only the composition value; the 16ms
            // timer drives just the slide (TranslateTransform), which is
            // layout-independent. This is smoother than tweening three XAML
            // properties per timer tick.
            ControlPanelGradient.Opacity = 1;
            ControlPanelGrid.Opacity = 1;
            TranslateVertical.Y = show ? 48 : 0;

            EnsurePanelVisuals();
            if (_panelCompositor is not null && _panelGridVisual is not null && _panelGradientVisual is not null)
            {
                _panelGridVisual.StopAnimation("Opacity");
                _panelGradientVisual.StopAnimation("Opacity");

                var ease = _panelCompositor.CreateCubicBezierEasingFunction(
                    new System.Numerics.Vector2(0.215f, 0.61f),
                    new System.Numerics.Vector2(0.355f, 1f));
                var opacity = _panelCompositor.CreateScalarKeyFrameAnimation();
                opacity.Duration = TimeSpan.FromMilliseconds(180);
                opacity.InsertKeyFrame(0f, show ? 0f : 1f);
                opacity.InsertKeyFrame(1f, show ? 1f : 0f, ease);
                _panelGridVisual.StartAnimation("Opacity", opacity);
                _panelGradientVisual.StartAnimation("Opacity", opacity);
            }

            _panelAnimationStart = Environment.TickCount64;
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _panelAnimationTimer.Tick += PanelAnimationTick;
            _panelAnimationTimer.Start();
        }

        private void PanelAnimationTick(object? sender, object e)
        {
            const double durationMs = 180;
            var elapsed = Environment.TickCount64 - _panelAnimationStart;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic

            TranslateVertical.Y = _panelAnimationShow ? 48 * (1 - eased) : 48 * eased;

            if (t >= 1)
            {
                _panelAnimationTimer.Stop();
                PanelAnimationCompleted(_panelAnimationShow);
            }
        }

        private void EnsurePanelVisuals()
        {
            if (_panelGridVisual is null)
            {
                _panelGridVisual = ElementCompositionPreview.GetElementVisual(ControlPanelGrid);
                _panelGradientVisual = ElementCompositionPreview.GetElementVisual(ControlPanelGradient);
                _panelCompositor = _panelGridVisual.Compositor;
            }
        }

        private void PanelAnimationCompleted(bool show)
        {
            _panelAnimating = false;
            _panelGridVisual?.StopAnimation("Opacity");
            _panelGradientVisual?.StopAnimation("Opacity");
            if (_panelGridVisual is not null)
            {
                _panelGridVisual.Opacity = 1f;
            }
            if (_panelGradientVisual is not null)
            {
                _panelGradientVisual.Opacity = 1f;
            }

            if (show)
            {
                ControlPanelGradient.Opacity = 1;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                _controlPanelIsVisible = true;
            }
            else
            {
                ControlPanelGrid.Visibility = Visibility.Collapsed;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                // Keep the gradient mounted and hit-testable. WinUI skips
                // hit-testing for elements at exactly zero opacity, so
                // leave a barely visible floor value.
                ControlPanelGradient.Opacity = 0.01;
                _controlPanelIsVisible = false;
            }
        }

        private void AddPanelAnimation(
            Storyboard storyboard,
            string property,
            double from,
            double to,
            double milliseconds,
            FrameworkElement? target = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, target ?? ControlPanelGrid);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        private void AppBarElementContainer_GotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource != sender)
            {
                return;
            }

            if (sender is AppBarElementContainer { Content: Panel control })
            {
                foreach (var item in control.Children)
                {
                    if (item is Control cc)
                    {
                        cc.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

        private void UpdateFullScreenUI(bool enabled)
        {
            if (enabled)
            {
                VisualStateManager.GoToState(this, "FullScreenState", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "NonFullScreenState", false);
            }
        }
        private void UpdateFullWindowUI(bool enabled)
        {
            if (enabled)
            {
                VisualStateManager.GoToState(this, "FullWindowState", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "NonFullWindowState", false);
            }
        }

        private void UpdateRepeatButtonUI()
        {
            var stateName = MediaPlayer?.RepeatState switch
            {
                RepeatState.One => "RepeatOneState",
                RepeatState.None => "RepeatNoneState",
                _ => "RepeatAllState",
            };

            VisualStateManager.GoToState(this, stateName, false);
        }

        private void UpdateShuffleButtonUI()
        {
            var stateName = MediaPlayer?.ShuffleEnabled switch
            {
                true => "ShuffleState",
                _ => "ShuffleNoneState",
            };

            VisualStateManager.GoToState(this, stateName, false);
        }

        private void UpdatePlaybackStatusUI(bool useTransitions)
        {
            if (_hasError)
            {
                VisualStateManager.GoToState(this, "Error", useTransitions);
            }
            else if (_isBuffering)
            {
                VisualStateManager.GoToState(this, "Buffering", useTransitions);
            }
            else if (!_sourceLoaded)
            {
                VisualStateManager.GoToState(this, "MediaLoading", useTransitions);
            }
            else
            {
                VisualStateManager.GoToState(this, "Normal", useTransitions);
            }
        }

        private void UpdatePlayPauseUI(bool isPaused, bool useTransitions)
        {
            if (isPaused)
            {
                VisualStateManager.GoToState(this, "PlayState", useTransitions);
            }
            else
            {
                VisualStateManager.GoToState(this, "PauseState", useTransitions);
            }
        }

        private void UpdateVolumeUI(bool useTransitions)
        {
            if (MediaPlayer?.IsMuted == true)
            {
                VisualStateManager.GoToState(this, "MuteState", useTransitions);
            }
            else
            {
                var volume = MediaPlayer?.Volume;
                switch (volume)
                {
                    case < 0.01:
                        VisualStateManager.GoToState(this, "VolumeState0", useTransitions);
                        break;
                    case < 34:
                        VisualStateManager.GoToState(this, "VolumeState1", useTransitions);
                        break;
                    case < 67:
                        VisualStateManager.GoToState(this, "VolumeState2", useTransitions);
                        break;
                    default:
                        VisualStateManager.GoToState(this, "VolumeState3", useTransitions);
                        break;
                }
            }
        }

        private int _currentSegment = -1; // 0=wide, 1=medium, 2=compact, 3=narrow
        private void PlayerControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateToolbarVisibility(e.NewSize.Width);
            UpdateChapterMarks();
        }

        /// <summary>Draws thin tick marks on the progress bar at chapter starts.</summary>
        private void UpdateChapterMarks()
        {
            ChapterMarksCanvas.Children.Clear();
            var duration = MediaPlayer?.Duration ?? 0;
            var width = ProgressSlider.ActualWidth;
            if (width <= 0 || duration <= 0 || MediaPlayer?.Chapters() is not { Count: > 0 } chapters)
            {
                return;
            }
            // Skip the first chapter (time 0) so the bar start stays clean.
            foreach (var chapter in chapters)
            {
                if (chapter.Time <= 0)
                {
                    continue;
                }
                var tick = new Border
                {
                    Width = 1,
                    Height = 14,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                };
                Canvas.SetLeft(tick, chapter.Time / duration * width);
                ChapterMarksCanvas.Children.Add(tick);
            }
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

        private void ProgressSlider_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = true;
        }

        private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = true;
            _suppressBufferingUntil = Environment.TickCount64 + 2000;
            if (AppContext.AppSetting.EnableVideoPreview && ProgressSlider.Maximum > 0)
            {
                UpdatePreview(ProgressSlider.Value / ProgressSlider.Maximum);
            }
        }

        private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            _suppressBufferingUntil = Environment.TickCount64 + 500;
        }

        private void ProgressSlider_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Click anywhere on the track to seek there (the thumb drag path
            // sets the same value, so clicking the thumb is harmless).
            if (ProgressSlider.Maximum <= 0)
            {
                return;
            }
            var point = e.GetPosition(ProgressSlider);
            if (ProgressSlider.ActualWidth <= 0)
            {
                return;
            }
            var ratio = Math.Clamp(point.X / ProgressSlider.ActualWidth, 0, 1);
            ProgressSlider.Value = ratio * ProgressSlider.Maximum;
        }

        private void ProgressSlider_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                UpdatePreview(e);
            }
        }

        private void ProgressSlider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            ClearPreview();
        }

        private void UpdatePreview(PointerRoutedEventArgs e)
        {
            if (MediaPlayer == null || MediaPlayer.Duration <= 0)
            {
                return;
            }

            var point = e.GetCurrentPoint(ProgressSlider);
            UpdatePreview(point.Position.X / ProgressSlider.ActualWidth);
        }

        private void UpdatePreview(double fraction)
        {
            if (MediaPlayer == null || MediaPlayer.Duration <= 0)
            {
                return;
            }

            fraction = Math.Clamp(fraction, 0, 1);
            var hoverSec = fraction * MediaPlayer.Duration;
            var controlPoint = ProgressSlider.TransformToVisual(this).TransformPoint(new Point(fraction * ProgressSlider.ActualWidth, 0));

            PreviewUpdateRequested?.Invoke(this, (hoverSec, controlPoint.X, controlPoint.Y));
        }

        private void ClearPreview()
        {
            PreviewClearRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Attaches or detaches the progress-bar preview hooks (live toggle support).</summary>
        public void EnablePreviewEvents(bool enabled)
        {
            if (enabled)
            {
                ProgressSlider.PointerEntered += ProgressSlider_PointerEntered;
                ProgressSlider.PointerMoved += ProgressSlider_PointerMoved;
                ProgressSlider.PointerExited += ProgressSlider_PointerExited;
            }
            else
            {
                ProgressSlider.PointerEntered -= ProgressSlider_PointerEntered;
                ProgressSlider.PointerMoved -= ProgressSlider_PointerMoved;
                ProgressSlider.PointerExited -= ProgressSlider_PointerExited;
                _isDragging = false;
                ClearPreview();
            }
        }

        private void VolumeMuteButton_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (_mediaPlayer?.Volume is double value && sender is UIElement element)
            {
                var delta = e.GetCurrentPoint(element).Properties.MouseWheelDelta;

                if (delta > 0)
                {
                    _mediaPlayer?.Volume = Math.Min(value + 2, 100);
                }
                else if (delta < 0)
                {
                    _mediaPlayer?.Volume = Math.Max(value - 2, 0);
                }
            }
        }
    }
}
