using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winrt;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Automation;

namespace mpv_winui.Domains.Playback
{
    public class TrackItem(int index, string label)
    {
        public string Label { get; set; } = label;
        public int Index { get; set; } = index;
    }

    public sealed partial class PlayerTrackSelectorControl : UserControl
    {
        public ObservableCollection<TrackItem> VideoItems { get; } = [];
        public ObservableCollection<TrackItem> SubtitleItems { get; } = [];
        public ObservableCollection<TrackItem> AudioItems { get; } = [];
        public ObservableCollection<TrackItem> SecondSubItems { get; } = [];

        public event EventHandler<int>? VideoTrackSelected;
        public event EventHandler<int>? SubtitleTrackSelected;
        public event EventHandler<int>? AudioTrackSelected;
        public event EventHandler<int>? SecondSubTrackSelected;

        // The tab captions are localized, and each one carries the selected
        // index of its own list. A SelectorBarItem presents exactly two
        // elements (Icon and Text), so the caption and the index have to be
        // one string -- the Pivot could spend a third TextBlock on the number.
        private string _subtitleTitle = string.Empty;
        private string _secondSubtitleTitle = string.Empty;
        private string _videoTitle = string.Empty;
        private string _audioTitle = string.Empty;

        public PlayerTrackSelectorControl()
        {
            InitializeComponent();
            ApplyLocalizedStrings();
            Loaded += TrackSelectorControl_Loaded;
            Unloaded += TrackSelectorControl_Unloaded;
        }

        private void TrackSelectorControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Idempotent: Loaded can fire again on every flyout open.
            AppContext.LanguageChanged -= ApplyLocalizedStrings;
            AppContext.LanguageChanged += ApplyLocalizedStrings;
            ApplyLocalizedStrings();
        }

        private void TrackSelectorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            AppContext.LanguageChanged -= ApplyLocalizedStrings;
        }

        /// <summary>
        /// The four tab captions were XAML text literals ("Subtile",
        /// "Subtile Sec", "Video", "Audio"), so they stayed English in every
        /// language. They reuse the keys the quick-control panel already labels
        /// its Audio/Video/Subtitles tabs with (PlayerControl.ControlPanel), so
        /// both tab strips read identically in all nine languages.
        /// </summary>
        private void ApplyLocalizedStrings()
        {
            var lang = AppContext.AppLang;
            _subtitleTitle = lang.SettingsCategorySubtitles;
            _secondSubtitleTitle = lang.SecondSubtitle;
            _videoTitle = lang.SettingsCategoryVideo;
            _audioTitle = lang.SettingsCategoryAudio;
            RefreshTabCaptions();
        }

        /// <summary>
        /// Rewrites all four tab labels as "caption + selected index".
        ///
        /// The index is the list's raw <c>SelectedIndex</c>, with no +1 -- the
        /// same number the Pivot header showed, including "-1" when nothing is
        /// selected. That is existing behaviour, not a defect: the selector
        /// starts with no track chosen and says so.
        /// </summary>
        private void RefreshTabCaptions()
        {
            SubtitleTab.Text = Caption(_subtitleTitle, SubtitleListView.SelectedIndex);
            SecondSubtitleTab.Text = Caption(_secondSubtitleTitle, SecondSubListView.SelectedIndex);
            VideoTab.Text = Caption(_videoTitle, VideoListView.SelectedIndex);
            AudioTab.Text = Caption(_audioTitle, AudioListView.SelectedIndex);

            // The item's automation name is derived from Text, which already
            // carries the localized caption, so it is never anonymous. Set it
            // explicitly as well: the name then cannot drift from the visible
            // label, and a screen reader announces the same words the tab shows.
            AutomationProperties.SetName(SubtitleTab, SubtitleTab.Text);
            AutomationProperties.SetName(SecondSubtitleTab, SecondSubtitleTab.Text);
            AutomationProperties.SetName(VideoTab, VideoTab.Text);
            AutomationProperties.SetName(AudioTab, AudioTab.Text);
        }

        private static string Caption(string title, int index)
        {
            return $"{title} {index}";
        }

        /// <summary>
        /// A SelectorBar has no content host, so the four lists are stacked in
        /// one Grid cell and this is the only thing that swaps them. It shows
        /// exactly the list belonging to the tab strip's selection and hides
        /// the other three; an unselected bar (SelectedItem null) leaves the
        /// subtitle list up, which is what the initial IsSelected="True" plus
        /// the XAML Visibility defaults already do.
        /// </summary>
        private void TrackSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            var selected = sender.SelectedItem;
            SubtitleListView.Visibility = selected == SubtitleTab ? Visibility.Visible : Visibility.Collapsed;
            SecondSubListView.Visibility = selected == SecondSubtitleTab ? Visibility.Visible : Visibility.Collapsed;
            VideoListView.Visibility = selected == VideoTab ? Visibility.Visible : Visibility.Collapsed;
            AudioListView.Visibility = selected == AudioTab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SubtitleListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SubtitleTab.Text = Caption(_subtitleTitle, SubtitleListView.SelectedIndex);
            AutomationProperties.SetName(SubtitleTab, SubtitleTab.Text);
        }

        private void SecondSubListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SecondSubtitleTab.Text = Caption(_secondSubtitleTitle, SecondSubListView.SelectedIndex);
            AutomationProperties.SetName(SecondSubtitleTab, SecondSubtitleTab.Text);
        }

        private void VideoListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoTab.Text = Caption(_videoTitle, VideoListView.SelectedIndex);
            AutomationProperties.SetName(VideoTab, VideoTab.Text);
        }

        private void AudioListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AudioTab.Text = Caption(_audioTitle, AudioListView.SelectedIndex);
            AutomationProperties.SetName(AudioTab, AudioTab.Text);
        }

        public void LoadVideoTracks(IReadOnlyList<MpvTrack> tracks)
        {
            VideoItems.Clear();
            var selectedIndex = -1;
            if (tracks?.Count > 0)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.Selected)
                    {
                        selectedIndex = i;
                    }

                    VideoItems.Add(new TrackItem(track.Index, $"{track.Index} {track.Title} {track.Codec}"));
                }
            }

            VideoListView.SelectedIndex = selectedIndex;
        }

        public void LoadSubtitleTracks(IReadOnlyList<MpvTrack> tracks, string offLabel)
        {
            SubtitleItems.Clear();
            SubtitleItems.Add(new TrackItem(-1, offLabel));
            var selectedIndex = 0;
            if (tracks?.Count > 0)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.Selected)
                    {
                        selectedIndex = i + 1;
                    }

                    SubtitleItems.Add(new TrackItem(track.Index, $"{track.Index} {track.Title} {track.Lang}"));
                }
            }

            SubtitleListView.SelectedIndex = selectedIndex;
        }

        public void LoadAudioTracks(IReadOnlyList<MpvTrack> tracks)
        {
            AudioItems.Clear();
            var selectedIndex = -1;
            if (tracks?.Count > 0)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.Selected)
                    {
                        selectedIndex = i;
                    }

                    AudioItems.Add(new TrackItem(track.Index, $"{track.Index} {track.Title} {track.Codec}"));
                }
            }

            AudioListView.SelectedIndex = selectedIndex;
        }

        public void LoadSecondSubtitleTracks(IReadOnlyList<MpvTrack> tracks, int currentId, string offLabel)
        {
            SecondSubItems.Clear();
            SecondSubItems.Add(new TrackItem(-1, offLabel));

            var selectedIndex = 0;
            if (tracks?.Count > 0)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.Index == currentId)
                    {
                        selectedIndex = i + 1;
                    }

                    SecondSubItems.Add(new TrackItem(track.Index, $"{track.Index} {track.Title} {track.Lang}"));
                }
            }

            SecondSubListView.SelectedIndex = selectedIndex;
        }

        public void SetSecondSubVisibility(bool visible)
        {
            if (visible)
            {
                VisualStateManager.GoToState(this, "NormalState", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "SecondSubState", false);
            }
        }

        private void VideoListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem item)
            {
                VideoTrackSelected?.Invoke(this, item.Index);
            }
        }

        private void SubtitleListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem item)
            {
                SubtitleTrackSelected?.Invoke(this, item.Index);
            }
        }

        private void AudioListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem item)
            {
                AudioTrackSelected?.Invoke(this, item.Index);
            }
        }

        private void SecondSubListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem item)
            {
                SecondSubTrackSelected?.Invoke(this, item.Index);
            }
        }
    }
}
