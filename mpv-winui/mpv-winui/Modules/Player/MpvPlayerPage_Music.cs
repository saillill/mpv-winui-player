using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// The music page: artwork, title, artist, lyrics and the rhythm strip.
    ///
    /// A separate surface from the video page, not the video page with a poster:
    /// audio has no frame, so every video affordance - the swap chain letterbox,
    /// zoom, aspect, the fullscreen overlay bar - is meaningless there. What audio
    /// does have is what this page shows, and what the reference players lead with.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>True while the music page is the visible surface.</summary>
        private bool _musicPageVisible;

        /// <summary>
        /// The last media-title mpv reported. Cached because there is no getter on
        /// the native player - the title arrives on the media-info event, and the
        /// music page is shown at a moment when that event has already fired.
        /// </summary>
        private string _mediaTitle = string.Empty;

        private IReadOnlyList<LyricLine> _lyrics = [];

        private int _currentLyricIndex = -1;

        /// <summary>
        /// Decides which page the current track belongs on and shows it.
        ///
        /// Called from the same place the video/library switch happens, so the
        /// three surfaces cannot disagree about what is up.
        /// </summary>
        private async void ShowMusicPageFor(string? path)
        {
            var isAudio = MediaKinds.IsAudio(path);

            if (!isAudio)
            {
                MusicHost.Visibility = Visibility.Collapsed;

                _musicPageVisible = false;

                PlayerControl.IsAudioOnly = false;

                StopRhythm();

                return;
            }

            MusicHost.Visibility = Visibility.Visible;

            _musicPageVisible = true;

            // The bar drops zoom / aspect / PiP / fullwindow while this is up: none
            // of them means anything without a frame.
            PlayerControl.IsAudioOnly = true;

            LibraryHost.Visibility = Visibility.Collapsed;

            ApplyMusicMetadata();

            StartRhythm();

            _lyrics = await LyricsLoader.LoadAsync(path, MusicTitle.Text);

            ApplyLyrics(_lyrics);
        }

        /// <summary>
        /// Splits mpv's media-title into the page's two lines.
        ///
        /// mpv reports "Artist - Title" when the file carries both, and just the
        /// file name when it carries neither, so the separator is the only signal
        /// available without a full tag reader. When there is no separator the
        /// title takes the whole string and the artist stays empty rather than
        /// showing a filename in both slots.
        /// </summary>
        private void ApplyMusicMetadata()
        {
            var title = _mediaTitle;

            var separator = title.LastIndexOf(" - ", StringComparison.Ordinal);

            if (separator > 0)
            {
                MusicArtist.Text = title[..separator].Trim();

                MusicTitle.Text = title[(separator + 3)..].Trim();
            }
            else
            {
                MusicArtist.Text = string.Empty;

                MusicTitle.Text = title;
            }

            // The album line has no source yet: mpv's media-title carries artist
            // and title only. Left empty rather than filled with the file name,
            // which would read as an album called "track01.flac".
            MusicAlbum.Text = string.Empty;

            UpdateArtwork();
        }

        /// <summary>
        /// Puts whatever cover mpv extracted into the artwork slot.
        ///
        /// audio-display=embedded-first makes mpv decode the embedded picture and
        /// report it as a video output frame, so a still image is on the swap
        /// chain while audio plays. That frame is not reachable as a XAML image -
        /// it is in the swap chain - so this slot shows the page's own fallback
        /// glyph until a cover source exists. The glow behind it follows the same
        /// rule, which is why both start hidden.
        /// </summary>
        private void UpdateArtwork()
        {
            MusicArtwork.Visibility = Visibility.Collapsed;

            MusicArtworkGlow.Visibility = Visibility.Collapsed;

            MusicArtworkFallbackIcon.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Renders the parsed lyrics, or says why there are none.
        ///
        /// Line rows rather than one text block: the current line has to be
        /// highlightable and scrollable to, and that needs one element per line.
        /// </summary>
        private void ApplyLyrics(IReadOnlyList<LyricLine> lines)
        {
            _currentLyricIndex = -1;

            MusicLyricsView.ItemsSource = null;

            if (lines.Count == 0)
            {
                MusicLyricsView.Visibility = Visibility.Collapsed;

                MusicLyricsEmpty.Text = AppContext.AppLang.MusicNoLyrics;

                MusicLyricsEmpty.Visibility = Visibility.Visible;

                return;
            }

            MusicLyricsEmpty.Visibility = Visibility.Collapsed;

            MusicLyricsView.Visibility = Visibility.Visible;

            MusicLyricsView.ItemsSource = lines;
        }

        /// <summary>
        /// Moves the highlight to the line whose time has passed.
        ///
        /// Called from the position update, not on a timer of its own: the page
        /// already receives coalesced position events, and a second clock would
        /// drift against them.
        /// </summary>
        private void UpdateLyricsHighlight(double positionSeconds)
        {
            if (!_musicPageVisible || _lyrics.Count == 0)
            {
                return;
            }

            var index = -1;

            for (var i = 0; i < _lyrics.Count; i++)
            {
                if (_lyrics[i].Time.TotalSeconds <= positionSeconds)
                {
                    index = i;
                }
                else
                {
                    break;
                }
            }

            if (index == _currentLyricIndex)
            {
                return;
            }

            if (_currentLyricIndex >= 0 && _currentLyricIndex < _lyrics.Count)
            {
                _lyrics[_currentLyricIndex].Opacity = 0.55;
            }

            _currentLyricIndex = index;

            if (index >= 0)
            {
                _lyrics[index].Opacity = 1.0;

                MusicLyricsView.ScrollIntoView(_lyrics[index]);
            }
        }

        /// <summary>Clicking a lyric line seeks to it: the reference players do, and it is free.</summary>
        private void MusicLyricsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LyricLine line && line.Time > TimeSpan.Zero)
            {
                _mediaPlayer?.Position(line.Time.TotalSeconds);
            }
        }

        // ------------------------------------------------------- rhythm strip

        private Storyboard? _rhythmStoryboard;

        /// <summary>
        /// The bars between the page and the transport bar.
        ///
        /// Built in code rather than as sixteen Borders in markup: the bar count is
        /// a layout decision (one per column of the strip), and the animation is
        /// per-bar, so sixteen near-identical element + storyboard pairs are
        /// clearer generated than written out.
        ///
        /// Deterministic pseudo-random rather than a real analyser: mpv does not
        /// expose per-band levels to the host, and a synthetic pulse driven by the
        /// bar's own columns still reads as "the music is playing". Deliberately
        /// not driven by volume - a quiet passage would then look stopped.
        /// </summary>
        private void StartRhythm()
        {
            if (_rhythmStoryboard is not null)
            {
                return;
            }

            MusicRhythmStrip.Children.Clear();

            var board = new Storyboard();

            var columns = MusicRhythmStrip.ColumnDefinitions.Count;

            for (var i = 0; i < columns; i++)
            {
                var bar = new Border
                {
                    Width = double.NaN,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CornerRadius = new CornerRadius(2),
                    Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                    Height = 6,
                };

                Grid.SetColumn(bar, i);

                MusicRhythmStrip.Children.Add(bar);

                // Each bar gets its own period and phase so the row reads as a
                // wave rather than as one block pulsing.
                var phase = i * 0.37;

                var period = 0.55 + (i % 5) * 0.11;

                var peak = 18 + (i % 7) * 3;

                var grow = new DoubleAnimation
                {
                    From = 6,
                    To = peak,
                    Duration = new Duration(TimeSpan.FromSeconds(period)),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(phase),
                    EnableDependentAnimation = true,
                };

                Storyboard.SetTarget(grow, bar);

                Storyboard.SetTargetProperty(grow, "Height");

                board.Children.Add(grow);
            }

            _rhythmStoryboard = board;

            board.Begin();

            MusicRhythmStrip.Visibility = Visibility.Visible;
        }

        private void StopRhythm()
        {
            _rhythmStoryboard?.Stop();

            _rhythmStoryboard = null;

            MusicRhythmStrip.Children.Clear();
        }
    }
}
