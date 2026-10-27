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

using mpv_winui.Infrastructure.Motion;

using mpv_winui.Modules.Settings;
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

        /// <summary>

        /// Applies the user's control-bar skin.

        ///

        /// Three states, and the default is "no style" - the control draws its own

        /// template from PlayerControl.xaml. The two alternates are whole

        /// ControlTemplates in their own resource dictionaries, which is why this

        /// assigns a Style rather than setting properties.

        ///

        /// Idempotent: assigning the same Style twice is a no-op in WinUI, and this

        /// runs once per setting change rather than per frame.

        /// </summary>

        internal void ApplyPlayerStyle()

        {

            var style = AppContext.AppSetting.PlayerStyle;



            switch (style)

            {

                case AppSettings.PlayerStyle_Center:

                {

                    Style = Application.Current.Resources["CenterPlayerControlStyle"] as Style;

                    break;

                }



                case AppSettings.PlayerStyle_Compact:

                {

                    Style = Application.Current.Resources["CompactPlayerControlStyle"] as Style;

                    break;

                }



                default:

                {

                    Style = null;

                    break;

                }

            }

        }



        /// <summary>
        /// The coalesced position, for consumers that need the value rather than
        /// just "it moved" - the music page's lyric highlight is the only one.
        /// Separate from OnPositionChanged so existing subscribers are untouched.
        /// </summary>
        public event Action<double>? PositionTicked;



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

        private readonly DispatcherTimer _positionUpdateTimer;

        private readonly object _positionLock = new();

        private (double Position, double Duration)? _pendingPosition;

        private double _lastPolledPosition = double.NaN;

        private double _lastPolledDuration = double.NaN;

        private bool _hasError = false;

        private bool _isBuffering = false;

        private bool _isInScrubMode = false;

        private bool _isDragging = false;

        private bool _isHoveringProgress = false;

        private long _suppressBufferingUntil;

        private bool _sourceLoaded = false;

        private double[]? _cachedChapterTimes;

        private double _cachedChapterDuration = double.NaN;

        private double _lastChapterMarkWidth = double.NaN;

        private SolidColorBrush? _chapterTickBrush;



        private mpv_winrt.MpvPlayer? _mediaPlayer;



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



        /// <summary>
        /// True once a file is loaded. While false the whole bar is inert.
        ///
        /// The bar is the transport for what is playing, so with nothing playing
        /// there is no valid action on it - and it used to be fully interactive
        /// anyway, which let a click issue a seek or a volume change into an idle
        /// mpv and quietly change a setting that would apply to the NEXT file.
        /// Reported as "with no video loaded, every button in the bottom bar
        /// should be greyed out and not interactive".
        ///
        /// IsHitTestVisible rather than IsEnabled: a Grid has no enable state of
        /// its own to propagate, and IsEnabled on the container would grey the
        /// glyphs twice (once for the container, once for each button) - the bar
        /// would read as broken rather than as waiting. Opacity does the dimming
        /// once, and hit-testing is what actually stops the click.
        /// </summary>
        public void SetTransportEnabled(bool enabled)
        {
            if (_transportEnabled == enabled)
            {
                return;
            }

            _transportEnabled = enabled;

            ControlPanelGrid.IsHitTestVisible = enabled;

            ApplyTransportRestOpacity();
        }

        /// <summary>
        /// The opacity the bar sits at when nothing is animating.
        ///
        /// Every path that resets <c>ControlPanelGrid.Opacity</c> after an
        /// interrupted fade must come through here rather than writing 1. They used
        /// to write 1 directly, and three of them run immediately after
        /// <see cref="SetTransportEnabled"/> during startup: the reveal animation's
        /// completion wrote 1 over the 0.4 that marks the bar disabled, so the grey
        /// state existed for one frame and then vanished. The bar was still inert -
        /// only the visual state was lost, which is exactly how it was reported:
        /// the grey "全没用了".
        /// </summary>
        private void ApplyTransportRestOpacity()
        {
            ControlPanelGrid.Opacity = _transportEnabled ? 1.0 : DisabledTransportOpacity;
        }

        /// <summary>How far the bar dims while there is nothing for it to drive.</summary>
        private const double DisabledTransportOpacity = 0.4;

        private bool _transportEnabled = true;

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

            UpdateTimeToggleAccessibility();



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

            ToolTipService.SetToolTip(RepeatButton, AppContext.AppLang.MoreRepeat);

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

            if (key == nameof(AppContext.AppSetting.PlayerStyle))

            {

                // The skin is a Style on this control, so it is applied here rather

                // than by the page: the settings window has no reference to the

                // page, and routing it through the page would be a second path for

                // one setting.

                DispatcherQueue.TryEnqueue(ApplyPlayerStyle);

            }

            else if (key == nameof(AppContext.AppSetting.VolumeMax))

            {

                VolumeSlider.Maximum = Math.Max(100, Convert.ToInt32(value));

            }

            else if (key == nameof(AppContext.AppSetting.WindowPiP))

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



        // Right-side time: click toggles between total duration and the

        // remaining time (minus-prefixed), like mainstream players.

        private bool _showRemainingTime;



        private void UpdateTimeTexts(double position, double duration)

        {

            TimeElapsedElement.Text = FormatTime(position);

            TimeRemainingElement.Text = _showRemainingTime && duration > 0

                ? "-" + FormatTime(Math.Max(0, duration - position))

                : FormatTime(duration);

            CompactTimeText.Text = $"{FormatCompactTime(position)}/{FormatCompactTime(duration)}";

        }



        private void TimeRemainingElement_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)

        {

            _showRemainingTime = !_showRemainingTime;

            UpdateTimeToggleAccessibility();

            UpdateTimeTexts(MediaPlayer?.Position() ?? 0.0, MediaPlayer?.Duration() ?? 0.0);

            e.Handled = true;

        }



        private void UpdateTimeToggleAccessibility()

        {

            var hint = _showRemainingTime ? AppContext.AppLang.TimeShowTotal : AppContext.AppLang.TimeShowRemaining;

            ToolTipService.SetToolTip(TimeRemainingElement, hint);

            AutomationProperties.SetName(TimeRemainingElement, hint);

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





        public mpv_winrt.MpvPlayer? MediaPlayer

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

                        UpdatePlaybackRateUI(value.PlaybackSpeed());

                        // The slider must reach every level volume-max allows,

                        // otherwise hotkey/wheel volume above 100 displays as

                        // a pinned 100 on the slider.

                        VolumeSlider.Maximum = Math.Max(100, AppContext.AppSetting.VolumeMax);

                        VolumeSlider.Value2 = _mediaPlayer?.Volume() ?? 50;



                        // Initialize time/progress from the current media state.

                        // The position timer only refreshes while playing, so a

                        // control created while paused (e.g. the PiP window)

                        // would otherwise keep the placeholder "00.00/00.00".

                        UpdateProgressSliderValue(value.Position(), value.Duration());

                        UpdateTimeTexts(value.Position(), value.Duration());

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



        /// <summary>
        /// Raised on any transport interaction, before the interaction's own
        /// meaning runs.
        ///
        /// The page uses it for one thing: while the library is up over a running
        /// video, the first press of ANY transport button returns to the video
        /// and then does what it was asked. Driving the bar from the browse
        /// screen is otherwise a blind interaction - you are looking at a grid
        /// while seeking a picture you cannot see. Reported as "if there is video
        /// loaded, clicking any button on the bar should go back to the video page
        /// first and then do the thing".
        ///
        /// Raised from the control rather than wired per button in the page:
        /// there are a dozen buttons, three of them are rebuilt into different
        /// CommandBars by the layout engine, and a handler list that has to be
        /// kept in step with them is how a new button silently escapes.
        /// </summary>
        public event EventHandler? TransportInteraction;

        /// <summary>
        /// The bar's single interaction funnel.
        ///
        /// PointerPressed rather than each button's Click: the press is what
        /// should bring the video back, and awaiting a Click would mean the
        /// button had already acted on a screen the user cannot see. Handled is
        /// deliberately left false so the press still reaches the button that was
        /// actually hit.
        /// </summary>
        private void ControlPanelGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            TransportInteraction?.Invoke(this, EventArgs.Empty);
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

                VolumeSlider.Value2 = player.Volume();

                UpdateProgressSliderValue(player.Position(), player.Duration());

                UpdateTimeTexts(player.Position(), player.Duration());

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

            _overlayIdleTimer.Stop();

            _overlayIdleTimer.Tick -= OverlayIdleTimer_Tick;

            _hideDelayTimer.Stop();

            _hideDelayTimer.Tick -= HideDelayTimer_Tick;



            RemoveEventListeners();

        }



        private void AddEventListeners()

        {

            _mediaPlayer?.FileLoaded += MediaPlayer_MediaOpened;

            // FileFailed, not PlaybackFailed. The latter is the pre-c49b461
            // name for this event and the component has not raised it since
            // that refactor renamed the raise to m_fileFailedEvent; the C#
            // side kept subscribing to the old name, so a file that failed to
            // load never reached MediaPlayer_MediaFailed and the Error visual
            // state was unreachable. The arg type differs in name only --
            // both runtimeclasses carry one String Message.
            _mediaPlayer?.FileFailed += MediaPlayer_MediaFailed;

            _mediaPlayer?.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            _mediaPlayer?.SeekStarted += PlaybackSession_SeekingStarted;

            _mediaPlayer?.PlaybackRestarted += PlaybackSession_SeekingEnded;

            _mediaPlayer?.VolumeChanged += PlaybackSession_VolumeChanged;

            // No Seeked subscription: the component declares that event but has
            // not raised it since c49b461 renamed the raise to
            // m_seekStartedEvent. Subscribing was a no-op that read as a live
            // handler -- the seek-finished state is already covered by
            // PlaybackRestarted (PlaybackSession_SeekingEnded clears buffering
            // and refreshes the UI). tools/check-winrt-events.py fails the build
            // if this is ever re-added without a raise behind it.

            _mediaPlayer?.LoopFileChanged += MediaPlayer_RepeatStateChanged;

            _mediaPlayer?.LoopPlaylistChanged += MediaPlayer_RepeatStateChanged;

            _mediaPlayer?.ShuffleChanged += MediaPlayer_ShuffleEnabledChanged;

            _mediaPlayer?.PositionChanged += PlaybackSession_PositionChanged;

            _mediaPlayer?.SpeedChanged += PlaybackSession_SpeedChanged;

        }



        private void RemoveEventListeners()

        {

            _mediaPlayer?.FileLoaded -= MediaPlayer_MediaOpened;

            _mediaPlayer?.FileFailed -= MediaPlayer_MediaFailed;

            _mediaPlayer?.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;

            _mediaPlayer?.SeekStarted -= PlaybackSession_SeekingStarted;

            _mediaPlayer?.PlaybackRestarted -= PlaybackSession_SeekingEnded;

            _mediaPlayer?.VolumeChanged -= PlaybackSession_VolumeChanged;

            _mediaPlayer?.LoopFileChanged -= MediaPlayer_RepeatStateChanged;

            _mediaPlayer?.LoopPlaylistChanged -= MediaPlayer_RepeatStateChanged;

            _mediaPlayer?.ShuffleChanged -= MediaPlayer_ShuffleEnabledChanged;

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

                _mediaPlayer?.SetAspectRatio(ar);

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

                TrackSelectorControl.LoadVideoTracks(_mediaPlayer?.GetVideoTracks() ?? []);

                TrackSelectorControl.VideoTrackSelected += TrackSelectorControl_VideoTrackSelected;

            }

            catch (Exception ex)

            {

                Debug.WriteLine($"TrackSelectionButton_Click video error: {ex.Message}");

            }



            try

            {

                TrackSelectorControl.SubtitleTrackSelected -= TrackSelectorControl_SubtitleTrackSelected;

                TrackSelectorControl.LoadSubtitleTracks(_mediaPlayer?.GetSubtitleTracks() ?? [], AppContext.AppLang.Off);

                TrackSelectorControl.SubtitleTrackSelected += TrackSelectorControl_SubtitleTrackSelected;

            }

            catch (Exception ex)

            {

                Debug.WriteLine($"TrackSelectionButton_Click sub error: {ex.Message}");

            }



            try

            {

                TrackSelectorControl.AudioTrackSelected -= TrackSelectorControl_AudioTrackSelected;

                TrackSelectorControl.LoadAudioTracks(_mediaPlayer?.GetAudioTracks() ?? []);

                TrackSelectorControl.AudioTrackSelected += TrackSelectorControl_AudioTrackSelected;

            }

            catch (Exception ex)

            {

                Debug.WriteLine($"TrackSelectionButton_Click audio error: {ex.Message}");

            }



            try

            {

                TrackSelectorControl.SecondSubTrackSelected -= TrackSelectorControl_SecondSubTrackSelected;

                // The second-subtitle picker is a second slot over the same
                // native subtitle track list; mpv has no separate list for it.
                TrackSelectorControl.LoadSecondSubtitleTracks(_mediaPlayer?.GetSubtitleTracks() ?? [], _mediaPlayer?.CurrentSecondSubtitleTrack() ?? -1, AppContext.AppLang.Off);

                TrackSelectorControl.SecondSubTrackSelected += TrackSelectorControl_SecondSubTrackSelected;

                TrackSelectorControl.SetSecondSubVisibility(true);

            }

            catch (Exception ex)

            {

                Debug.WriteLine($"TrackSelectionButton_Click second-sub error: {ex.Message}");

            }

            // The button no longer carries a Flyout, so the popup is opened here
            // instead of by the framework -- and it is opened after the lists
            // above have been filled, not before.
            OpenTrackSelector(TrackSelectionButton);

        }



        private void TrackSelectorControl_VideoTrackSelected(object? sender, int trackIndex)

        {

            _mediaPlayer?.CurrentVideoTrack(trackIndex);

        }



        private void TrackSelectorControl_SubtitleTrackSelected(object? sender, int trackIndex)

        {

            _mediaPlayer?.CurrentSubtitleTrack(trackIndex);

        }



        private void TrackSelectorControl_AudioTrackSelected(object? sender, int trackIndex)

        {

            _mediaPlayer?.CurrentAudioTrack(trackIndex);

        }



        private void TrackSelectorControl_SecondSubTrackSelected(object? sender, int trackIndex)

        {

            _mediaPlayer?.CurrentSecondSubtitleTrack(trackIndex);

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

                _mediaPlayer.PlaybackSpeed(speed);

            }

        }



        private void PlaybackSession_SpeedChanged(SpeedChangedEventArgs args)

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

            SeekRelative(10);

        }



        /// <summary>
        /// Relative seek in seconds. The native <c>Position()</c> getter returns
        /// <c>double?</c> while the setter overload takes a non-nullable <c>double</c>,
        /// so the null case is folded to 0 here rather than at every call site.
        /// </summary>
        private void SeekRelative(double delta)
        {
            if (_mediaPlayer is null)
            {
                return;
            }

            var target = (_mediaPlayer.Position()) + delta;
            _mediaPlayer.Position(target < 0 ? 0 : target);
        }

        public void Backward()

        {

            SeekRelative(-10);

        }



        private void PlaybackSession_PlaybackStateChanged(mpv_winrt.PlaybackStateChangedEventArgs args)

        {

            DispatcherQueue.RunAsync(() =>

            {

                if (!(MediaPlayer?.IsPaused() ?? true))

                {

                    _positionUpdateTimer.Stop();

                    lock (_positionLock)

                    {

                        _pendingPosition = null;

                    }

                }



                // The parameter is "is the playback paused"; passing the negated
                // value made PlayState/PauseState swap, so the button showed a
                // play glyph while playing and a pause glyph while paused.
                UpdatePlayPauseUI(MediaPlayer?.IsPaused() ?? true, true);

            });

        }



        private void MediaPlayer_MediaOpened()

        {

            _hasError = false;

            _sourceLoaded = true;

            DispatcherQueue.RunAsync(() =>

            {

                InvalidateChapterCache();

                UpdateChapterMarks(force: true);

                var duration = MediaPlayer?.Duration() ?? 0.0;

                UpdateProgressSliderValue(0, duration);

                if (duration > 0)

                {

                    ApplyAdaptiveSliderStep(duration);

                }



                UpdatePlaybackStatusUI(false);


                UpdateVolumeUI(false);

            });

        }



        // MediaPlayer_Seeked was here. It became unreachable when c49b461 renamed
        // the native raise from m_seekedEvent to m_seekStartedEvent, and it was
        // kept for one round "as a record of what it did" -- which was a mistake:
        // a dead method is not documentation, and its presence suggested the
        // seek-finished path was handled there when it is handled by
        // PlaybackSession_SeekingEnded. Deleted, with the reasoning recorded in
        // the subscription site above and in tools/check-winrt-events.py.



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



        private void MediaPlayer_MediaFailed(FileFailedEventArgs args)

        {

            _hasError = true;

            _sourceLoaded = false;



            DispatcherQueue.RunAsync(() =>

            {

                ErrorTextBlock.Text = args.Message;

                UpdatePlaybackStatusUI(true);

            });

        }



        private void PlaybackSession_SeekingStarted()

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



        private void PlaybackSession_SeekingEnded()

        {

            _isBuffering = false;

            DispatcherQueue.RunAsync(() => { UpdatePlaybackStatusUI(true); });

        }



        private void PlaybackSession_VolumeChanged(VolumeChangedEventArgs args)

        {

            DispatcherQueue.RunAsync(() =>

            {

                VolumeSlider.Value2 = (int)args.Volume;

                UpdateVolumeUI(true);

            });

        }



        private void MediaPlayer_RepeatStateChanged()

        {

            DispatcherQueue.RunAsync(UpdatePlaybackModeUI);

        }



        private void MediaPlayer_ShuffleEnabledChanged()

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



            if (!MediaPlayer.IsPaused())

            {

                MediaPlayer?.Pause();

            }

            else

            {

                MediaPlayer?.Play();

            }

        }



        /// <summary>True while the compact-bar volume flyout is open: the

        /// overlay bar must not retract under a user who is dragging its

        /// slider (pointer events land on the popup, not this control).</summary>

        private bool _volumeFlyoutOpen;



        private void OnMuteClick(object sender, RoutedEventArgs e)

        {

            if (MediaPlayer == null)

            {

                return;

            }



            // The PiP compact bar always shows the horizontal slider, so this

            // flyout is only reached when the main window's volume strip is

            // hidden through the control-bar customization.

            if (VolumeSliderContainer.Visibility != Visibility.Visible)

            {

                var control = new VolumeFlyoutControl(MediaPlayer);

                // Same chrome as the track selector and the More menu
                // (MpvPlayerPopupPresenterStyle). The strip is the only consumer
                // that adds size bounds of its own: it is a fixed 56x176 control,
                // and left to itself the presenter shrink-wraps it and cuts the
                // thumb and the foot of the track off.

                var presenterStyle = new Style(typeof(FlyoutPresenter))

                {

                    BasedOn = (Style)Application.Current.Resources["MpvPlayerPopupPresenterStyle"],

                };

                presenterStyle.Setters.Add(new Setter(FlyoutPresenter.PaddingProperty, new Thickness(4)));

                presenterStyle.Setters.Add(new Setter(FlyoutPresenter.MinWidthProperty, 0d));

                // The strip is a fixed 56x176. Left to itself the presenter
                // shrink-wraps it and the thumb and the foot of the track are
                // cut off, so give it the strip's own height as both a floor and
                // a generous ceiling -- the earlier version handed the presenter
                // numbers derived from what happened to fit, which is how the
                // strip ended up smaller than its contents.
                presenterStyle.Setters.Add(new Setter(FlyoutPresenter.MinHeightProperty, 168d));
                presenterStyle.Setters.Add(new Setter(FlyoutPresenter.MaxHeightProperty, 240d));

                var flyout = new Flyout

                {

                    Content = control,

                    Placement = FlyoutPlacementMode.Top,

                    FlyoutPresenterStyle = presenterStyle,

                };

                _volumeFlyoutOpen = true;

                flyout.Closed += (_, _) => _volumeFlyoutOpen = false;

                // Centred on the button, so the strip reads as belonging to the
                // icon that opened it. This used to be nudged right by the
                // button's width plus 12, to keep a left-edge button's flyout
                // inside the window; that nudge is exactly what put the strip
                // visibly out of line with its own button in a portrait window.
                // The framework already moves a flyout back inside the window
                // when centring it would overhang, so the nudge was guarding
                // against something that does not happen.

                flyout.ShowAt(VolumeMuteButton, new FlyoutShowOptions

                {

                    Placement = FlyoutPlacementMode.Top,

                });

                return;

            }



            MediaPlayer.IsMuted(!MediaPlayer.IsMuted());

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
                //
                // mpv keeps the two repeat states in separate flags;
                // GetRepeatState/SetRepeatState (MpvPlayerExtensions) are the
                // tri-state view over them.
                if (player.Shuffle())
                {
                    player.SetShuffle(false);
                    player.SetRepeatState(RepeatState.None);
                }
                else
                {
                    switch (player.GetRepeatState())
                    {
                        case RepeatState.None:
                            player.SetRepeatState(RepeatState.All);
                            break;

                        case RepeatState.All:
                            player.SetRepeatState(RepeatState.One);
                            break;

                        default:
                            player.SetShuffle(true);
                            player.SetRepeatState(RepeatState.None);
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

                // Never seek onto the very end.
                //
                // Setting time-pos to the file's duration is indistinguishable to
                // mpv from reaching EOF, and with a playlist loaded that advances
                // to the next item - so dragging the scrubber to the right edge
                // started the next video. Reported as "dragging to the end should
                // not jump to the next video; only a natural EOF may".
                //
                // Half a second short is close enough to be invisible (the last
                // frame is still shown) and far enough that mpv does not treat it
                // as the end of file. A natural end-of-file still advances,
                // because that path never goes through this method.
                var duration = MediaPlayer?.Duration() ?? 0.0;

                var requested = e.NewValue;

                if (duration > 0 && requested >= duration - 0.05)

                {

                    requested = Math.Max(0, duration - 0.5);

                }

                MediaPlayer?.Position(requested);

                UpdateTimeTexts(requested, duration);

                if (AppContext.AppSetting.EnableVideoPreview && ProgressSlider.Maximum > 0)

                {

                    UpdatePreview(e.NewValue / ProgressSlider.Maximum);

                }

            }

        }



        private void OnVolumeSliderValueChanged(object sender, double value)

        {

            MediaPlayer?.Volume(value);

        }



        private void PlaybackSession_PositionChanged(PositionChangedEventArgs args)

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

            PositionTicked?.Invoke(position);

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

            if (MediaPlayer?.Shuffle() == true)
            {
                stateName = "ModeShuffle";
                label = lang.MoreShuffle;
            }
            else
            {
                switch (MediaPlayer?.GetRepeatState())
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

            // The one place every play/pause path converges: the button, the

            // keyboard shortcut, the media key, and playback starting by itself.

            // Hooking the click handler instead would have missed three of those.

            MotionBehaviors.SpringPop(PlayPauseButton);

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

            if (MediaPlayer?.IsMuted() == true)

            {

                VisualStateManager.GoToState(this, "MuteState", useTransitions);

            }

            else

            {

                var volume = MediaPlayer?.Volume();

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

            if (_mediaPlayer?.Volume() is double value && sender is UIElement element)

            {

                var delta = e.GetCurrentPoint(element).Properties.MouseWheelDelta;



                if (delta > 0)

                {

                    _mediaPlayer?.Volume(Math.Min(value + 2, 100));

                }

                else if (delta < 0)

                {

                    _mediaPlayer?.Volume(Math.Max(value - 2, 0));

                }

            }

        }

    }

}

