using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
        /// The three text lines, from the file's own tags.
        ///
        /// The tags win: mpv's media-title is "Artist - Title" at best, while the
        /// file carries album and album artist as well - and those are the two
        /// lines the page exists to show. The split of media-title survives as the
        /// fallback for a file MediaInfo cannot read, because a plain "Artist -
        /// Title" is still better than a filename in both slots.
        /// </summary>
        private void ApplyMusicMetadata()
        {
            var tags = ReadMediaTags();

            var title = TagOr(tags, "title", string.Empty);
            var artist = TagOr(tags, "artist", string.Empty);
            var album = TagOr(tags, "album", string.Empty);

            if (string.IsNullOrWhiteSpace(title))
            {
                // No tag at all: fall back to mpv's own naming.
                var mediaTitle = _mediaTitle;

                var separator = mediaTitle.LastIndexOf(" - ", StringComparison.Ordinal);

                if (separator > 0)
                {
                    artist = artist.Length > 0 ? artist : mediaTitle[..separator].Trim();

                    title = mediaTitle[(separator + 3)..].Trim();
                }
                else
                {
                    title = mediaTitle;
                }
            }

            MusicTitle.Text = title;

            MusicArtist.Text = artist;

            // Empty rather than the folder name: "track01.flac" is not an album,
            // and the line collapses when there is nothing to say.
            MusicAlbum.Text = album;

            UpdateArtwork();
        }

        /// <summary>
        /// The current file's tags, or an empty map.
        ///
        /// The native getter is answered by MediaInfo, which is already deployed
        /// beside the exe; an absent DLL, a file with no tags and "nothing is
        /// playing" all come back as an empty map rather than an exception, which
        /// is why this has no failure branch of its own.
        /// </summary>
        private IReadOnlyDictionary<string, string> ReadMediaTags()
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var native = _mediaPlayer?.GetMediaTags();

                if (native is null)
                {
                    return tags;
                }

                foreach (var pair in native)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Value))
                    {
                        tags[pair.Key] = pair.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "music: reading media tags failed");
            }

            return tags;
        }

        private static string TagOr(IReadOnlyDictionary<string, string> tags, string key, string fallback) =>
            tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        /// <summary>
        /// The embedded cover, decoded from the file's own bytes.
        ///
        /// Not from the swap chain: audio-display=embedded-first does make mpv
        /// decode the picture, but that frame lives in the swap chain and this
        /// build's screenshot-raw refuses to export it (MPV_ERROR_INVALID_PARAMETER,
        /// verified with both vo=libmpv and vo=null). MediaInfo parses the tag
        /// itself and hands back the original JPEG/PNG, which is the same picture
        /// without depending on the render path.
        ///
        /// Three states, and the glow tracks the artwork: a real cover, a
        /// coverless track (placeholder glyph, flat page), and a failure to read
        /// (same as coverless, because the user cannot act on the difference).
        /// </summary>
        private async void UpdateArtwork()
        {
            MusicArtwork.Visibility = Visibility.Collapsed;

            MusicArtworkGlow.Visibility = Visibility.Collapsed;

            MusicArtworkFallbackIcon.Visibility = Visibility.Visible;

            try
            {
                if (_mediaPlayer is null)
                {
                    return;
                }

                if (!_mediaPlayer.TryGetCoverArt(out var stream) || stream is null)
                {
                    return;
                }

                var bitmap = new BitmapImage();

                await bitmap.SetSourceAsync(stream);

                // Both copies, because the glow is the same picture scaled up
                // behind the slot; one BitmapImage can back two Images.
                MusicArtwork.Source = bitmap;

                MusicArtworkGlow.Source = bitmap;

                MusicArtwork.Visibility = Visibility.Visible;

                MusicArtworkGlow.Visibility = Visibility.Visible;

                MusicArtworkFallbackIcon.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // A corrupt tag, an unsupported image format, a stream that went
                // away: all of them mean "show the placeholder".
                _logger.Warn(ex, "music: cover art unavailable");
            }
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
