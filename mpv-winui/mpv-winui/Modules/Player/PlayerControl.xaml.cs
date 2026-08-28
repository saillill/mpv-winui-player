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
        private readonly object _positionLock = new();
        private (double Position, double Duration)? _pendingPosition;
        private double _lastPolledPosition = double.NaN;
        private double _lastPolledDuration = double.NaN;
        private bool _hasError = false;
        private bool _isBuffering = false;
        private bool _isInScrubMode = false;
        private bool _isDragging = false;
        private long _suppressBufferingUntil;
        private bool _sourceLoaded = false;
        private double[]? _cachedChapterTimes;
        private double _cachedChapterDuration = double.NaN;
        private double _lastChapterMarkWidth = double.NaN;
        private SolidColorBrush? _chapterTickBrush;

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

            // Coalescing timer for event-driven time-pos updates: the native
            // observation can fire at frame rate, so the UI applies the
            // latest value at most ~10 times per second.
            _positionUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
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
            ToolTipService.SetToolTip(VolumeMuteButton, AppContext.AppLang.PiPMute);
            ToolTipService.SetToolTip(VolumeSlider, AppContext.AppLang.ControlBarIconVolume);
            ToolTipService.SetToolTip(PlaybackRateButton, AppContext.AppLang.MorePlaybackRate);
            ToolTipService.SetToolTip(TrackSelectionButton, AppContext.AppLang.ControlBarIconTracks);
            ToolTipService.SetToolTip(ZoomButton, AppContext.AppLang.MoreZoom);
            ToolTipService.SetToolTip(FullWindowButton, AppContext.AppLang.MoreFullWindow);
            ToolTipService.SetToolTip(FullScreenButton, AppContext.AppLang.MoreFullScreen);
            ToolTipService.SetToolTip(ControlPanelButton, AppContext.AppLang.ControlBarIconPanel);
            AutomationProperties.SetName(ControlPanelButton, AppContext.AppLang.ControlBarIconPanel);
            ToolTipService.SetToolTip(MoreButton, AppContext.AppLang.ControlBarIconMore);

            // Flyout labels that XAML holds as English placeholders.
            ToolTipService.SetToolTip(AbLoopButton, AppContext.AppLang.ControlBarIconAbLoop);
            AutomationProperties.SetName(AbLoopButton, AppContext.AppLang.ControlBarIconAbLoop);
            ToolTipService.SetToolTip(EqualizerButton, AppContext.AppLang.PanelEqualizer);
            AutomationProperties.SetName(EqualizerButton, AppContext.AppLang.PanelEqualizer);
            EqualizerTitle.Text = AppContext.AppLang.PanelEqualizer;
            EqualizerResetButton.Content = AppContext.AppLang.Reset;
            EqualizerOffButton.Content = AppContext.AppLang.Off;
            CustomRateItem.Text = AppContext.AppLang.CustomRate;
            ToolTipService.SetToolTip(DelayButton, AppContext.AppLang.PanelDelay);
            AutomationProperties.SetName(DelayButton, AppContext.AppLang.PanelDelay);
            DelayTitle.Text = AppContext.AppLang.PanelDelay;
            AudioDelayLabel.Text = AppContext.AppLang.SettingsAudioDelay;
            SubDelayLabel.Text = AppContext.AppLang.SettingsSubDelay;
            AutomationProperties.SetName(AudioDelaySlider, AppContext.AppLang.SettingsAudioDelay);
            AutomationProperties.SetName(SubDelaySlider, AppContext.AppLang.SettingsSubDelay);
            DelayResetButton.Content = AppContext.AppLang.Reset;

            // Keyboard/screen-reader names (XAML holds English placeholders).
            AutomationProperties.SetName(ProgressSlider, AppContext.AppLang.ControlBarIconPlayback);
            AutomationProperties.SetName(PlayPauseButton, AppContext.AppLang.Play);
            AutomationProperties.SetName(PreviousTrackButton, AppContext.AppLang.MorePreviousTrack);
            AutomationProperties.SetName(NextTrackButton, AppContext.AppLang.MoreNextTrack);
            AutomationProperties.SetName(SkipBackwardButton, AppContext.AppLang.MoreSkipBackward);
            AutomationProperties.SetName(SkipForwardButton, AppContext.AppLang.MoreSkipForward);
            AutomationProperties.SetName(ShuffleButton, AppContext.AppLang.MoreShuffle);
            // RepeatButton is hidden (mode button reuses it) but stays in
            // the tree; give it a localized name for screen readers.
            AutomationProperties.SetName(RepeatButton, AppContext.AppLang.MoreRepeat);
            AutomationProperties.SetName(VolumeMuteButton, AppContext.AppLang.PiPMute);
            AutomationProperties.SetName(VolumeSlider, AppContext.AppLang.ControlBarIconVolume);
            AutomationProperties.SetName(PlaybackRateButton, AppContext.AppLang.MorePlaybackRate);
            AutomationProperties.SetName(TrackSelectionButton, AppContext.AppLang.ControlBarIconTracks);
            AutomationProperties.SetName(ZoomButton, AppContext.AppLang.MoreZoom);
            AutomationProperties.SetName(PiPButton, AppContext.AppLang.SettingsPiP);
            AutomationProperties.SetName(FullWindowButton, AppContext.AppLang.MoreFullWindow);
            AutomationProperties.SetName(FullScreenButton, AppContext.AppLang.MoreFullScreen);
            AutomationProperties.SetName(MoreButton, AppContext.AppLang.ControlBarIconMore);
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

                        UpdatePlaybackModeUI();
                        UpdatePlaybackRateUI(value.PlaybackRate);
                        VolumeSlider.Value2 = _mediaPlayer?.Volume ?? 50;

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
            AppContext.LanguageChanged += OnLanguageChanged;
            PlayPauseButton.Click += OnPlayPauseClick;
            SkipBackwardButton.Click += SkipBackwardButton_Click;
            SkipForwardButton.Click += SkipForwardButton_Click;
            VolumeMuteButton.Click += OnMuteClick;
            FullScreenButton.Click += OnFullScreenClick;
            FullWindowButton.Click += FullWindowButton_Click;
            ShuffleButton.Click += OnPlaybackModeClick;
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
            _overlayIdleTimer.Tick += OverlayIdleTimer_Tick;

            UpdateToolbarVisibility(ActualWidth);

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
            AppContext.LanguageChanged -= OnLanguageChanged;
            PlayPauseButton.Click -= OnPlayPauseClick;
            SkipBackwardButton.Click -= SkipBackwardButton_Click;
            SkipForwardButton.Click -= SkipForwardButton_Click;
            VolumeMuteButton.Click -= OnMuteClick;
            FullScreenButton.Click -= OnFullScreenClick;
            FullWindowButton.Click -= FullWindowButton_Click;
            ShuffleButton.Click -= OnPlaybackModeClick;
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
            _mediaPlayer?.SeekingStarted += PlaybackSession_SeekingStarted;
            _mediaPlayer?.SeekingEnded += PlaybackSession_SeekingEnded;
            _mediaPlayer?.VolumeChanged += PlaybackSession_VolumeChanged;
            _mediaPlayer?.Seeked += MediaPlayer_Seeked;
            _mediaPlayer?.RepeatStateChanged += MediaPlayer_RepeatStateChanged;
            _mediaPlayer?.ShuffleEnabledChanged += MediaPlayer_ShuffleEnabledChanged;
            _mediaPlayer?.PositionChanged += PlaybackSession_PositionChanged;
            _mediaPlayer?.SpeedChanged += PlaybackSession_SpeedChanged;
        }

        private void RemoveEventListeners()
        {
            _mediaPlayer?.MediaOpened -= MediaPlayer_MediaOpened;
            _mediaPlayer?.MediaFailed -= MediaPlayer_MediaFailed;
            _mediaPlayer?.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            _mediaPlayer?.SeekingStarted -= PlaybackSession_SeekingStarted;
            _mediaPlayer?.SeekingEnded -= PlaybackSession_SeekingEnded;
            _mediaPlayer?.VolumeChanged -= PlaybackSession_VolumeChanged;
            _mediaPlayer?.Seeked -= MediaPlayer_Seeked;
            _mediaPlayer?.RepeatStateChanged -= MediaPlayer_RepeatStateChanged;
            _mediaPlayer?.ShuffleEnabledChanged -= MediaPlayer_ShuffleEnabledChanged;
            _mediaPlayer?.PositionChanged -= PlaybackSession_PositionChanged;
            _mediaPlayer?.SpeedChanged -= PlaybackSession_SpeedChanged;
        }

        private void NextTrackButton_Click(object? sender, RoutedEventArgs? e)
        {
            _mediaPlayer?.PlaylistNext();
        }

        private void PreviousTrackButton_Click(object? sender, RoutedEventArgs? e)
        {
            _mediaPlayer?.PlaylistPrevious();
        }

        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomSelectionFlyout.Items.Clear();
            AddZoomOptions(ZoomSelectionFlyout.Items, ZoomSelectionMenu_Click);

            if (sender is MenuFlyoutItem)
            {
                ZoomSelectionFlyout.ShowAt(ZoomButton);
            }
        }

        /// <summary>Single source of the aspect-ratio options shared by
        /// the zoom flyout and the narrow-window overflow submenu.</summary>
        internal static void AddZoomOptions(IList<MenuFlyoutItemBase> items, RoutedEventHandler onClick)
        {
            foreach (var (label, tag) in new[]
                     {
                         (AppContext.AppLang.MoreZoomAuto, "no"),
                         ("4:3", "4:3"),
                         ("16:9", "16:9"),
                         ("16:10", "16:10"),
                     })
            {
                var item = new MenuFlyoutItem { Text = label, Tag = tag };
                item.Click += onClick;
                items.Add(item);
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

        private void PlaybackSession_SpeedChanged(MpvMediaPlayer sender, SpeedChangedEventArgs args)
        {
            DispatcherQueue.RunAsync(() => UpdatePlaybackRateUI(args.Speed));
        }

        /// <summary>
        /// Marks the matching rate preset in the flyout and shows the current
        /// speed in the button tooltip; keeps the UI in sync when speed is
        /// changed from mpv commands (menu/input.conf) as well as the flyout.
        /// </summary>
        private void UpdatePlaybackRateUI(double speed)
        {
            foreach (var item in PlaybackRateFlyout.Items)
            {
                if (item is ToggleMenuFlyoutItem toggle
                    && item.Tag is string tag
                    && tag != "custom"
                    && double.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var preset))
                {
                    toggle.IsChecked = Math.Abs(preset - speed) < 0.001;
                }
            }
            var label = speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            ToolTipService.SetToolTip(PlaybackRateButton, $"{AppContext.AppLang.MorePlaybackRate} — {label}x");
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

        private void PlaybackSession_PlaybackStateChanged(MpvMediaPlayer sender, bool args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (args)
                {
                    _positionUpdateTimer.Stop();
                    lock (_positionLock)
                    {
                        _pendingPosition = null;
                    }
                }

                UpdatePlayPauseUI(args, true);
            });
        }

        private void MediaPlayer_MediaOpened(MpvMediaPlayer sender, object? args)
        {
            _hasError = false;
            _sourceLoaded = true;
            DispatcherQueue.RunAsync(() =>
            {
                InvalidateChapterCache();
                UpdateChapterMarks(force: true);
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

        private void MediaPlayer_Seeked(MpvMediaPlayer sender, object? args)
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

        private void MediaPlayer_MediaFailed(MpvMediaPlayer sender, string? args)
        {
            _hasError = true;
            _sourceLoaded = false;

            DispatcherQueue.RunAsync(() =>
            {
                ErrorTextBlock.Text = args;
                UpdatePlaybackStatusUI(true);
            });
        }

        private void PlaybackSession_SeekingStarted(MpvMediaPlayer sender, object? args)
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

        private void PlaybackSession_SeekingEnded(MpvMediaPlayer sender, object? args)
        {
            _isBuffering = false;
            DispatcherQueue.RunAsync(() => { UpdatePlaybackStatusUI(true); });
        }

        private void PlaybackSession_VolumeChanged(MpvMediaPlayer sender, int volume)
        {
            DispatcherQueue.RunAsync(() =>
            {
                VolumeSlider.Value2 = volume;
                UpdateVolumeUI(true);
            });
        }

        private void MediaPlayer_RepeatStateChanged(MpvMediaPlayer sender, RepeatState state)
        {
            DispatcherQueue.RunAsync(UpdatePlaybackModeUI);
        }

        private void MediaPlayer_ShuffleEnabledChanged(MpvMediaPlayer sender, bool enabled)
        {
            DispatcherQueue.RunAsync(UpdatePlaybackModeUI);
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
                var flyout = new Flyout
                {
                    Content = control,
                    Placement = FlyoutPlacementMode.Top,
                    FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter))
                    {
                        Setters =
                        {
                            new Setter(FlyoutPresenter.PaddingProperty, new Thickness(4)),
                        },
                    },
                };
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

        private void OnPlaybackModeClick(object? sender, RoutedEventArgs? e)
        {
            if (MediaPlayer is { } player)
            {
                // Cycle: no repeat -> sequential -> single loop -> shuffle.
                // "No repeat" runs the playlist in order and stops at the
                // end; "sequential" loops the list in order; "single loop"
                // repeats the current file; shuffle hands ordering to the
                // patched mpv shuffle state.
                if (player.ShuffleEnabled)
                {
                    player.ShuffleEnabled = false;
                    player.RepeatState = RepeatState.None;
                }
                else
                {
                    switch (player.RepeatState)
                    {
                        case RepeatState.None:
                            player.RepeatState = RepeatState.All;
                            break;
                        case RepeatState.All:
                            player.RepeatState = RepeatState.One;
                            break;
                        default:
                            player.ShuffleEnabled = true;
                            player.RepeatState = RepeatState.None;
                            break;
                    }
                }
                UpdatePlaybackModeUI();
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

        private void PlaybackSession_PositionChanged(MpvMediaPlayer sender, PositionChangedEventArgs args)
        {
            // Runs on the native mpv event thread; store the latest snapshot
            // and let the UI-thread coalescing timer apply it.
            lock (_positionLock)
            {
                _pendingPosition = (args.Position, args.Duration);
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_positionUpdateTimer.IsEnabled)
                {
                    _positionUpdateTimer.Start();
                }
            });
        }

        private void OnPositionUpdateTimerTick(object? sender, object e)
        {
            _positionUpdateTimer.Stop();
            (double Position, double Duration)? pending;
            lock (_positionLock)
            {
                pending = _pendingPosition;
                _pendingPosition = null;
            }
            if (pending is not { } value)
            {
                return;
            }

            var position = value.Position;
            var duration = value.Duration;
            if (Math.Abs(position - _lastPolledPosition) < 0.001
                && Math.Abs(duration - _lastPolledDuration) < 0.001)
            {
                return;
            }

            _lastPolledPosition = position;
            _lastPolledDuration = duration;
            UpdateProgressSliderValue(position, duration);
            UpdateTimeTexts(position, duration);
            OnPositionChanged?.Invoke();
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

        private void UpdatePlaybackModeUI()
        {
            var lang = mpv_winui.AppContext.AppLang;
            string stateName;
            string label;
            if (MediaPlayer?.ShuffleEnabled == true)
            {
                stateName = "ModeShuffle";
                label = lang.MoreShuffle;
            }
            else
            {
                switch (MediaPlayer?.RepeatState)
                {
                    case RepeatState.All:
                        stateName = "ModeSequence";
                        label = lang.ControlBarModeSequence;
                        break;
                    case RepeatState.One:
                        stateName = "ModeRepeatOne";
                        label = lang.ControlBarModeRepeatOne;
                        break;
                    default:
                        stateName = "ModeNoRepeat";
                        label = lang.ControlBarModeNoRepeat;
                        break;
                }
            }

            VisualStateManager.GoToState(this, stateName, false);
            ToolTipService.SetToolTip(ShuffleButton, label);
            AutomationProperties.SetName(ShuffleButton, label);
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
                    default:
                        // Medium and high volume share one glyph (the bundled
                        // font has no distinct fourth step), so both ranges
                        // land in VolumeState2.
                        VisualStateManager.GoToState(this, "VolumeState2", useTransitions);
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
