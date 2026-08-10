using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Diagnostics;
using System.Collections.Generic;
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
        private readonly DispatcherTimer _panelAnimationTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly DispatcherTimer _hideDelayTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        private readonly DispatcherTimer _overlayIdleTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
        private Point _lastOverlayActivity = new(double.NaN, double.NaN);
        private const double OverlayMoveThreshold = 5.0;
        private long _panelAnimationStart;
        private bool _panelAnimationShow;
        private bool _panelAnimating;

        private readonly DispatcherTimer _positionUpdateTimer;
        private bool _hasError = false;
        private bool _isBuffering = false;
        private bool _isInScrubMode = false;
        private bool _isDragging = false;
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
            ToolTipService.SetToolTip(PiPPlayPauseButton, AppContext.AppLang.Play);
            ToolTipService.SetToolTip(PiPCloseButton, AppContext.AppLang.Close);
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
                || key == nameof(AppContext.AppSetting.ControlBarHiddenIconsModernX))
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
            // Two-state pin (ModernX style): E718 = unpinned, E840 = pinned.
            PiPSymbol.Glyph = AppContext.AppSetting.WindowPiP ? "\uE840" : "\uE718";
        }

        private void OnPiPCloseClick(object sender, RoutedEventArgs e)
        {
            AppContext.AppSetting.WindowPiP = false;
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.WindowPiP), false);
            UpdatePiPButton();
            UpdatePiPBar();
        }

        /// <summary>
        /// Hides the main control bar while the dedicated PiP window is shown.
        /// The in-window PiP bar is kept for compatibility but never displayed;
        /// the PiP window has its own compact controls.
        /// </summary>
        public void UpdatePiPBar()
        {
            var pip = AppContext.AppSetting.WindowPiP;
            PiPBar.Visibility = Visibility.Collapsed;
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
            SetHidden(hidden.Contains("speed"), PlaybackRateButton);
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
                // ModernX bottom fade (140px, ~90% black at the base).
                ControlPanelGradient.Height = 140;
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
            if (_overlayMode && _controlPanelIsVisible)
            {
                StartPanelAnimation(false);
                OnPanelVisibleChanged?.Invoke(true);
            }
        }

        private void HideDelayTimer_Tick(object? sender, object e)
        {
            _hideDelayTimer.Stop();
            if (_overlayMode && _controlPanelIsVisible)
            {
                StartPanelAnimation(false);
                OnPanelVisibleChanged?.Invoke(true);
            }
        }

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
            ICommandBarElement[] left, middle, right;
            if (layout == "modernx")
            {
                left =
                [
                    TrackSelectionButton, ShuffleButton, RepeatButton, PlaybackRateButton,
                    VolumeMuteButton, VolumeSliderContainer,
                ];
                middle =
                [
                    PreviousTrackButton, SkipBackwardButton,
                    PlayPauseButton, SkipForwardButton,
                    NextTrackButton,
                ];
                right =
                [
                    ZoomButton, PiPButton, FullWindowButton, FullScreenButton,
                ];
            }
            else
            {
                left =
                [
                    PlayPauseButton, PreviousTrackButton, NextTrackButton, SkipBackwardButton,
                    SkipForwardButton, ShuffleButton, RepeatButton,
                ];
                middle = [];
                right =
                [
                    VolumeMuteButton, VolumeSliderContainer, PlaybackRateButton,
                    TrackSelectionButton, ZoomButton, PiPButton,
                    FullWindowButton, FullScreenButton,
                ];
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
            // Rebuild from the canonical lists on every apply. The previous
            // implementation only re-added elements that were already present
            // in the bars, so after the PiP compact pass stripped a subset of
            // buttons, later applies silently dropped the missing buttons and
            // the control bar could end up empty (progress bar + times only).
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
            RootGrid.PointerMoved += RootGrid_PointerMoved;
            PiPButton.Click += OnPiPClick;
            PiPPlayPauseButton.Click += OnPlayPauseClick;
            PiPCloseButton.Click += OnPiPCloseClick;
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
            PiPPlayPauseButton.Click -= OnPlayPauseClick;
            PiPCloseButton.Click -= OnPiPCloseClick;
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
            ProgressSlider.PointerEntered -= ProgressSlider_PointerEntered;
            ProgressSlider.PointerMoved -= ProgressSlider_PointerMoved;
            ProgressSlider.PointerExited -= ProgressSlider_PointerExited;

            VolumeSlider.ValueChanged2 -= OnVolumeSliderValueChanged;

            SizeChanged -= PlayerControl_SizeChanged;
            _positionUpdateTimer.Stop();
            _positionUpdateTimer.Tick -= OnPositionUpdateTimerTick;
            _overlayIdleTimer.Stop();
            _overlayIdleTimer.Tick -= OverlayIdleTimer_Tick;

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
            _mediaPlayer?.SeekingStarted += MediaPlayer_SeekingStarted;
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
            _mediaPlayer?.SeekingStarted -= MediaPlayer_SeekingStarted;
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
            if (_mediaPlayer != null && sender is MenuFlyoutItem item && double.TryParse(item.Tag.ToString(), out double speed))
            {
                _mediaPlayer?.PlaybackRate = speed;
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

        private async void MediaPlayer_SeekingStarted(MpvMediaPlayer sender, object? args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                _isBuffering = true;
                UpdatePlaybackStatusUI(true);
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
                if (!_controlPanelIsVisible)
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
            _panelAnimationTimer.Stop();
            _showStoryboard?.Stop();
            _showStoryboard = null;
            _hideStoryboard?.Stop();
            _hideStoryboard = null;
        }

        /// <summary>
        /// Manual tween for overlay mode (fullscreen/PiP). A Storyboard in a
        /// second top-level window crashes the XAML compositor, so the slide
        /// and fade are driven by a dispatcher timer instead.
        /// </summary>
        private void StartPanelAnimation(bool show)
        {
            if (_panelAnimating && _panelAnimationShow == show)
            {
                return;
            }

            _panelAnimating = true;
            _panelAnimationShow = show;
            _panelAnimationStart = Environment.TickCount64;
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _panelAnimationTimer.Tick += PanelAnimationTick;
            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }
            _panelAnimationTimer.Start();
        }

        private void PanelAnimationTick(object? sender, object e)
        {
            const double durationMs = 180;
            var elapsed = Environment.TickCount64 - _panelAnimationStart;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic

            if (_panelAnimationShow)
            {
                ControlPanelGradient.Opacity = eased;
                ControlPanelGrid.Opacity = eased;
                TranslateVertical.Y = 48 * (1 - eased);
            }
            else
            {
                ControlPanelGradient.Opacity = 1 - eased;
                ControlPanelGrid.Opacity = 1 - eased;
                TranslateVertical.Y = 48 * eased;
            }

            if (t >= 1)
            {
                _panelAnimationTimer.Stop();
                _panelAnimating = false;
                if (_panelAnimationShow)
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
        }

        private void AddPanelAnimation(
            Storyboard storyboard,
            string property,
            double from,
            double to,
            double milliseconds)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, ControlPanelGrid);
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
            PiPPlayPauseSymbol.Glyph = isPaused ? "\uF5B0" : "\uF8AE";
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
            if (AppContext.AppSetting.EnableVideoPreview && ProgressSlider.Maximum > 0)
            {
                UpdatePreview(ProgressSlider.Value / ProgressSlider.Maximum);
            }
        }

        private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
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
