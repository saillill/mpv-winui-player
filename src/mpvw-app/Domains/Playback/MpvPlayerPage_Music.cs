using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
using mpv_winui.Infrastructure.Motion;
using mpv_winui.Domains.Playback.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;

namespace mpv_winui.Domains.Playback
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

        /// <summary>
        /// The current cover's colours, or <c>None</c> when it has no usable hue.
        ///
        /// Held so the album page and a re-entered music page can tint from the
        /// same answer without decoding the cover again.
        /// </summary>
        private AlbumColorExtractor.Palette _palette = AlbumColorExtractor.None;

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
        /// Paints the page with the cover's colour.
        ///
        /// Two surfaces, two opacities: the wash is the darkened hue over the whole
        /// page, and the glow is the saturated hue behind the cover, so the artwork
        /// sits in light that came from itself.
        ///
        /// With no palette both go back to the theme's own page brush rather than
        /// to a hardcoded grey - a coverless track should look like the rest of the
        /// app, not like a special case.
        /// </summary>
        private void ApplyPalette()
        {
            if (_palette.Surface == 0)
            {
                MusicPageWash.Fill = (Brush)Application.Current.Resources["MpvMusicPageBrush"];

                MusicArtworkGlow.Fill = (Brush)Application.Current.Resources["MpvMusicArtworkFallbackBrush"];

                MusicArtworkGlow.Opacity = 0.0;

                return;
            }

            // The theme's own colours, then the cover's tint on top. Reading the base from
            // resources is what makes the wash follow Light/Dark; using the extracted colour
            // directly is why it used to ignore the theme completely.
            MusicPageWash.Fill = ArtworkPalette.Tinted(
                _palette.Surface,
                ThemeColor("MpvMusicPageBrush"),
                WashTintWeight);

            MusicArtworkGlow.Fill = ArtworkPalette.Tinted(
                _palette.Accent,
                ThemeColor("MpvMusicArtworkFallbackBrush"),
                GlowTintWeight);

            // Faint: it is light coming off the cover, not a second picture.
            MusicArtworkGlow.Opacity = 0.22;
        }

        /// <summary>
        /// How far the page wash moves from the theme colour toward the cover's colour.
        ///
        /// Small on purpose. The wash sits behind body text, so it has to keep the theme's
        /// contrast; 0.30 is enough for two albums to look different and not enough to escape the
        /// theme, which is what "以官方主题画刷为基底进行偏移" asks for.
        /// </summary>
        private const double WashTintWeight = 0.30;

        /// <summary>
        /// How far the artwork glow moves toward the cover's colour.
        ///
        /// Higher than the wash because the glow IS the cover's light - it carries no text and sits
        /// at 0.22 opacity behind the artwork, so the cover's hue is the subject rather than a tint
        /// on a background.
        /// </summary>
        private const double GlowTintWeight = 0.55;

        /// <summary>
        /// The colour of a theme brush by key.
        ///
        /// Falls back to transparent when the key is missing rather than throwing: a resource rename
        /// should make the wash neutral, not take the music page down.
        /// </summary>
        private static Windows.UI.Color ThemeColor(string key)
        {
            if (Application.Current.Resources.TryGetValue(key, out var value)
                && value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return Windows.UI.Color.FromArgb(0, 0, 0, 0);
        }

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

            MusicArtworkFallbackIcon.Visibility = Visibility.Visible;

            // Cleared here, not only on success: a track without a cover must not
            // keep the previous track's colour on the page.
            _palette = AlbumColorExtractor.None;

            ApplyPalette();

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

                // The palette is read from the same stream, before BitmapImage
                // takes it: decoding twice would be a second full-file read for a
                // colour, and the thumbnail decode here is 48x48.
                _palette = await ArtworkPalette.FromStreamAsync(stream);

                ApplyPalette();

                var bitmap = new BitmapImage();

                await bitmap.SetSourceAsync(stream);

                MusicArtwork.Source = bitmap;

                MusicArtwork.Visibility = Visibility.Visible;

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
                _lyrics[_currentLyricIndex].Scale = 1.0;
            }

            _currentLyricIndex = index;

            if (index >= 0)
            {
                _lyrics[index].Opacity = 1.0;
                _lyrics[index].Scale = 1.18;

                // The line arriving, not the line leaving: popping the outgoing one
                // would draw the eye backwards.
                if (MusicLyricsView.ContainerFromIndex(index) is ListViewItem current)
                {
                    MotionBehaviors.SpringPop(current, peak: 1.02f);
                }

                ScrollLyricsTo(index);
            }
        }

        /// <summary>
        /// Glides the active line to the reading position.
        ///
        /// Not <c>ListView.ScrollIntoView</c>: that jumps, and it parks the line at
        /// whichever edge it had to scroll towards, so the words you are reading
        /// move out from under your eye on every line. This centres the line, which
        /// is what a fixed reading position means.
        ///
        /// The target is the row's top within the content, minus half the viewport,
        /// plus half the row - the arithmetic that puts the middle of the line on
        /// the middle of the frame.
        /// </summary>
        private void ScrollLyricsTo(int index)
        {
            if (index < 0 || index >= _lyrics.Count)
            {
                return;
            }

            if (MusicLyricsView.ContainerFromIndex(index) is not ListViewItem container)
            {
                // Not realised yet: the list virtualises, so a line off screen has
                // no container to measure. ScrollIntoView is the only option before
                // one exists, and it is correct here - the list is far enough away
                // that there is nothing for the eye to track anyway.
                MusicLyricsView.ScrollIntoView(_lyrics[index]);

                return;
            }

            var scroller = FindScrollViewer(MusicLyricsView);

            if (scroller?.Content is not UIElement content)
            {
                return;
            }

            var rowTop = container
                .TransformToVisual(content)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;

            var target = rowTop
                - (scroller.ViewportHeight / 2.0)
                + (container.ActualHeight / 2.0);

            var maxOffset = Math.Max(0, scroller.ScrollableHeight);

            target = Math.Clamp(target, 0, maxOffset);

            var distance = Math.Abs(target - scroller.VerticalOffset);

            // A small move is not worth animating; the eye cannot see 10 px of
            // easing and the animation would still cost a frame.
            if (distance < 12)
            {
                scroller.ChangeView(null, target, null, disableAnimation: true);

                return;
            }

            // disableAnimation: false is what makes it a glide. The duration is the
            // ScrollViewer's own, which is tuned against the platform's motion -
            // overriding it per call is how a UI ends up with five different
            // scroll speeds.
            scroller.ChangeView(null, target, null, disableAnimation: false);
        }

        /// <summary>
        /// The ScrollViewer inside a ListView.
        ///
        /// Walked rather than cached: the template is applied lazily, so a field
        /// captured at load time would be null for the first track.
        /// </summary>
        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is ScrollViewer scroller)
                {
                    return scroller;
                }

                if (FindScrollViewer(child) is { } found)
                {
                    return found;
                }
            }

            return null;
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

        private AudioCapture? _audioCapture;

        private readonly List<Border> _rhythmBars = [];

        private bool _rhythmRendering;

        /// <summary>
        /// Starts the real spectrum.
        ///
        /// The bars are real: WASAPI loopback captures what the machine is playing,
        /// <see cref="SpectrumAnalyzer"/> turns each block into sixteen
        /// logarithmically spaced band levels, and this draws them.
        ///
        /// It used to be sixteen Storyboards with hand-picked periods and phases -
        /// a synthetic pulse. That is replaced rather than kept as a fallback
        /// because a fallback here is a second behaviour, and the fake one is
        /// indistinguishable from the real one to anyone who has not measured it.
        ///
        /// If capture cannot start (no render device, no loopback) the bars stay
        /// flat and the page is otherwise unaffected: a visualiser that cannot
        /// hear is a still image, not an error.
        /// </summary>
        private void StartRhythm()
        {
            if (_audioCapture is not null)
            {
                return;
            }

            MusicRhythmStrip.Children.Clear();

            _rhythmBars.Clear();

            var columns = MusicRhythmStrip.ColumnDefinitions.Count;

            var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

            for (var i = 0; i < columns; i++)
            {
                var bar = new Border
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CornerRadius = new CornerRadius(2),
                    Background = accent,

                    // Resting height: a floor of 2 rather than 0 so a silent band is
                    // a line on the baseline instead of nothing at all, which would
                    // make the strip vanish between tracks.
                    Height = 2,
                };

                Grid.SetColumn(bar, i);

                MusicRhythmStrip.Children.Add(bar);

                _rhythmBars.Add(bar);
            }

            MusicRhythmStrip.Visibility = Visibility.Visible;

            _audioCapture = new AudioCapture();

            if (!_audioCapture.Start(columns))
            {
                // Capture is unavailable; leave the bars at rest rather than
                // pretending. Disposing stops it from being started twice.
                _audioCapture.Dispose();
                _audioCapture = null;
                return;
            }

            if (!_rhythmRendering)
            {
                _rhythmRendering = true;

                CompositionTarget.Rendering += OnRhythmRendering;
            }
        }

        private void StopRhythm()
        {
            if (_rhythmRendering)
            {
                _rhythmRendering = false;

                CompositionTarget.Rendering -= OnRhythmRendering;
            }

            _audioCapture?.Dispose();

            _audioCapture = null;

            _rhythmBars.Clear();

            MusicRhythmStrip.Children.Clear();
        }

        /// <summary>
        /// One frame of the strip.
        ///
        /// The levels arrive already smoothed (the analyser's decay), so this only
        /// maps dB onto pixels. Running it from Rendering rather than a timer keeps
        /// it in step with the compositor and stops it when the window is hidden.
        /// </summary>
        private void OnRhythmRendering(object? sender, object e)
        {
            var levels = _audioCapture?.LatestLevels();

            if (levels is null || levels.Length != _rhythmBars.Count)
            {
                return;
            }

            // The strip's own height is the scale: a bar at 0 dB fills it, one at
            // -60 dB sits on the baseline. Measured from the Grid rather than
            // hardcoded so a different row height cannot clip the peaks.
            var available = MusicRhythmStrip.ActualHeight;

            if (available <= 4)
            {
                return;
            }

            const double floorDb = -60.0;

            for (var i = 0; i < levels.Length; i++)
            {
                // 0 dB is full height; -60 dB is the 2px floor. Clamped because a
                // clipped sample can exceed 0.
                var normalised = Math.Clamp((levels[i] - floorDb) / -floorDb, 0.0, 1.0);

                // Squared so the quiet half of the range is visible: dB is already
                // logarithmic, and a straight map spends most of the height in the
                // top 10 dB.
                var height = 2.0 + (normalised * normalised * (available - 2.0));

                _rhythmBars[i].Height = height;
            }
        }
    }
}
