using mpv_winui.Infrastructure.Common.Media;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Infrastructure.Motion;
using mpv_winui.Domains.Playlist;
using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

// Part of MpvPlayerPage. The partial keyword means the compiler sees one class;
// this file exists so a reader looking for one responsibility opens one file
// instead of scrolling through three thousand lines.
namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// the music library: its three groupings, the album page, and the tags behind both.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// The music library's three presentations.
        ///
        /// Songs is the flat list; Albums and Artists group the same files by the
        /// one tag each of them surfaces. The grouping uses the same per-file
        /// metadata the rest of the library reads, so there is no second index to
        /// keep in step - and a file with no tag lands in a single "unknown" group
        /// rather than being dropped, which is what the reference players do.
        /// </summary>
        private void MusicTabSongs_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Songs);

        private void MusicTabAlbums_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Albums);

        private void MusicTabArtists_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Artists);

        private MusicGrouping _musicGrouping = MusicGrouping.Songs;

        private void SetMusicGrouping(MusicGrouping grouping)
        {
            if (_musicGrouping == grouping)
            {
                return;
            }

            _musicGrouping = grouping;

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }

        private void AlbumBack_Click(object sender, RoutedEventArgs e) => CloseAlbum();

        /// <summary>The album's tracks, in table order, replacing the queue.</summary>
        private void AlbumPlayAll_Click(object sender, RoutedEventArgs e) => PlayAlbum(shuffle: false);

        /// <summary>
        /// The album's tracks in a shuffled order.
        ///
        /// Shuffled here rather than by mpv's shuffle property: this is a one-shot
        /// request to play this album in another order, and leaving mpv shuffling
        /// afterwards would silently change what "next" means for the rest of the
        /// session.
        /// </summary>
        private void AlbumShuffle_Click(object sender, RoutedEventArgs e) => PlayAlbum(shuffle: true);

        private void PlayAlbum(bool shuffle)
        {
            var paths = _albumTracks
                .Where(t => !t.IsMissing)
                .Select(t => t.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            if (shuffle)
            {
                // Fisher-Yates with the shared Random: a single pass over the list
                // rather than OrderBy(random), which is both slower and biased.
                for (var i = paths.Count - 1; i > 0; i--)
                {
                    var j = Random.Shared.Next(i + 1);
                    (paths[i], paths[j]) = (paths[j], paths[i]);
                }
            }

            PlayFiles(paths, OpenMode.Replace).FireAndForget(OnException);
        }

        /// <summary>
        /// "Add to": appends the album to whatever is already queued.
        /// </summary>
        private void AlbumAddTo_Click(object sender, RoutedEventArgs e)
        {
            var paths = _albumTracks
                .Where(t => !t.IsMissing)
                .Select(t => t.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            PlayFiles(paths, OpenMode.Append).FireAndForget(OnException);
        }

        /// <summary>Clicking a row in the album table plays it.</summary>
        private void AlbumTrackList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LibraryEntry entry)
            {
                PlayLibraryEntry(entry);
            }
        }

        /// <summary>The album currently shown, or null when the page is closed.</summary>
        private string? _openAlbum;

        /// <summary>
        /// The entries of the open album, in playing order.
        ///
        /// Kept as a list rather than re-derived on every action: "play all" and
        /// "add to" must act on exactly what the table shows.
        /// </summary>
        private List<LibraryEntry> _albumTracks = [];

        /// <summary>
        /// Shows one album: its cover, its identity and its tracks.
        ///
        /// Called from a card click while the Albums tab is up. The tracks are the
        /// library's own entries filtered by the same album key the grid grouped
        /// on, so the table cannot disagree with the cards behind it.
        /// </summary>
        private void OpenAlbum(string album)
        {
            _openAlbum = album;

            _albumTracks = _librarySection == LibrarySectionMusic
                ? _libraryAll
                    .Where(e => MediaKinds.IsAudio(e.Path))
                    .Where(e => string.Equals(
                        string.IsNullOrWhiteSpace(e.Album) ? AppContext.AppLang.LibraryUnknownAlbum : e.Album,
                        album,
                        StringComparison.CurrentCultureIgnoreCase))
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                : [];

            ApplyAlbumChrome();

            LibraryHost.Visibility = Visibility.Collapsed;

            AlbumHost.Visibility = Visibility.Visible;

            MotionBehaviors.FadeIn(AlbumHost);
        }

        /// <summary>Closes the album page and puts the library back.</summary>
        private void CloseAlbum()
        {
            if (_openAlbum is null)
            {
                return;
            }

            HideSurfacesOverLibrary();

            LibraryHost.Visibility = Visibility.Visible;

            // The grid may have been reloaded while the album was open (a folder
            // re-bind, a rescan); re-applying is cheaper than tracking that.
            ApplyLibraryFilter();
        }

        /// <summary>
        /// Fills the album page from <see cref="_openAlbum"/> and
        /// <see cref="_albumTracks"/>.
        ///
        /// All strings, the artwork and the labels come from here rather than
        /// from markup, because the page is one surface reused for every album.
        /// </summary>
        private async void ApplyAlbumChrome()
        {
            var lang = AppContext.AppLang;

            AlbumBackText.Text = lang.LibraryAlbumBack;

            AlbumColumnTitle.Text = lang.LibraryColumnTitle;
            AlbumColumnArtist.Text = lang.LibraryColumnArtist;
            AlbumColumnGenre.Text = lang.LibraryColumnGenre;
            AlbumColumnLength.Text = lang.LibraryColumnLength;

            AlbumPlayAllText.Text = lang.LibraryPlayAll;
            AlbumShuffleText.Text = lang.LibraryShufflePlay;
            AlbumAddToText.Text = lang.LibraryAlbumAddTo;

            AlbumTitle.Text = _openAlbum ?? string.Empty;

            // The artist line is the album's own artist, which every track of an
            // album shares; the first track that has one speaks for the album.
            var artist = _albumTracks
                .Select(e => e.Artist)
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? string.Empty;

            AlbumArtist.Text = artist;

            AlbumSummary.Text = string.Format(
                lang.LibraryAlbumSummary,
                _albumTracks.Count,
                FormatTotalLength(_albumTracks));

            AlbumTrackList.ItemsSource = _albumTracks;

            var empty = _albumTracks.Count == 0;

            AlbumEmptyText.Text = lang.LibraryAlbumEmpty;

            AlbumEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            AlbumTrackList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            AlbumPlayAllButton.IsEnabled = !empty;
            AlbumShuffleButton.IsEnabled = !empty;
            AlbumAddToButton.IsEnabled = !empty;

            await ApplyAlbumArtworkAsync();
        }

        /// <summary>
        /// Total running time of the open album, or "--:--" when no track reports
        /// one.
        ///
        /// Not printed as 0:00 for an album whose lengths the index skipped: that
        /// reads as an empty album rather than an unknown one.
        /// </summary>
        private static string FormatTotalLength(IReadOnlyList<LibraryEntry> tracks)
        {
            var known = tracks.Where(t => t.Duration is not null).ToList();

            if (known.Count == 0)
            {
                return "--:--";
            }

            var total = TimeSpan.FromTicks(known.Sum(t => t.Duration!.Value.Ticks));

            return total.TotalHours >= 1
                ? $"{(int)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}"
                : $"{total.Minutes}:{total.Seconds:00}";
        }

        /// <summary>
        /// The album's cover.
        ///
        /// From the first track's embedded picture, through the same native getter
        /// the music page uses - the player is told which path to read, so this
        /// does not depend on the album's own file being the one playing.
        /// </summary>
        private async Task ApplyAlbumArtworkAsync()
        {
            AlbumArtwork.Visibility = Visibility.Collapsed;
            AlbumArtworkFallbackIcon.Visibility = Visibility.Visible;

            if (_albumTracks.Count == 0 || _mediaPlayer is null)
            {
                return;
            }

            try
            {
                // Try each track until one carries art: an album ripped as one
                // file per track usually tags only the first, and a compilation
                // may tag only some.
                foreach (var track in _albumTracks)
                {
                    if (!_mediaPlayer.TryGetCoverArtForPath(track.Path, out var stream) || stream is null)
                    {
                        continue;
                    }

                    var bitmap = new BitmapImage();

                    await bitmap.SetSourceAsync(stream);

                    AlbumArtwork.Source = bitmap;

                    AlbumArtwork.Visibility = Visibility.Visible;

                    AlbumArtworkFallbackIcon.Visibility = Visibility.Collapsed;

                    return;
                }
            }
            catch (Exception ex)
            {
                // Same rule as the music page: no art is a normal state.
                _logger.Warn(ex, "album: cover art unavailable");
            }
        }

        /// <summary>
        /// Builds the music library's "Albums" and "Artists" tabs: one header and
        /// one card grid per tag, in the same container the video library's
        /// folder view uses.
        ///
        /// A file with no tag is not dropped: it lands in one group labelled
        /// "unknown" (localised), which is what the reference players do and what
        /// keeps the card count honest. The group order is alphabetical by tag.
        /// </summary>
        private void BuildMusicTagGroups(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            // The album card, not the video one: square cover, album then artist. Both
            // grouped music tabs render albums, and the video card's 16:9 crop loses the
            // sleeve while its single caption loses the artist.
            var cardTemplate = (DataTemplate)Resources["AlbumCardTemplate"];
            var headerTemplate = (DataTemplate)Resources["VideoFolderHeaderTemplate"];

            var byAlbum = _musicGrouping == MusicGrouping.Albums;

            // "Unknown" sorts last rather than first: it is the fallback bucket,
            // not the most important album in the library.
            var unknownLabel = byAlbum
                ? AppContext.AppLang.LibraryUnknownAlbum
                : AppContext.AppLang.LibraryUnknownArtist;

            // Grouped by tag, then filed under an index letter. The letter is the
            // PRIMARY sort key: without it a pinyin-ordered library lists 周杰伦 under
            // Z after 王菲 under W - each heading correct, the sequence wrong.
            var groups = entries
                .GroupBy(entry =>
                {
                    var key = byAlbum ? entry.Album : entry.Artist;

                    return string.IsNullOrWhiteSpace(key) ? "\uFFFF" : key;
                }, StringComparer.CurrentCultureIgnoreCase)
                .Select(group =>
                {
                    var name = group.Key == "\uFFFF" ? unknownLabel : group.Key;

                    return new VideoFolderGroup(
                        name,
                        group.ToList(),
                        Path: string.Empty,
                        Letter: LibraryIndexLetter.For(name, ch => PinyinInitial.For(ch.ToString())));
                })
                // Index order first, then the culture's own order inside a letter - so
                // the rail can jump to a letter and the entries there still read the
                // way the user expects.
                .OrderBy(group => Array.IndexOf(LibraryIndexLetter.All, group.Letter))
                .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // Letter -> the presenter hosting that group's header, filled as the groups
            // are built so the rail knows where to jump instead of walking the visual
            // tree and guessing which presenter is which.
            _groupHeaders.Clear();

            foreach (var group in groups)
            {
                var header = new ContentPresenter
                {
                    Content = group,
                    ContentTemplate = headerTemplate,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                VideoFolderGroups.Children.Add(header);

                if (!string.IsNullOrEmpty(group.Letter))
                {
                    // First group wins: several albums can share a letter and the rail
                    // should land on the first of them.
                    _groupHeaders.TryAdd(group.Letter, header);
                }

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = group.Items,
                };
                repeater.Layout = AlbumCardLayout();

                repeater.ElementPrepared += LibraryGrid_ElementPrepared;
                VideoFolderGroups.Children.Add(repeater);
            }

            BuildLibraryIndexRail(groups);
        }

        /// <summary>
        /// Fills in album / artist / length for the music entries, in one batch.
        ///
        /// The folder scan cannot carry these: it enumerates paths and never opens
        /// a file, because doing so for a thousand-track folder takes minutes.
        /// Reading them afterwards keeps the grid on screen while the tags arrive,
        /// and only the music library pays for it.
        /// </summary>
        private async Task LoadMusicTagsAsync(int generation)
        {
            var audio = _libraryAll.Where(e => !e.IsVideo && MediaKinds.IsAudio(e.Path)).ToList();

            if (audio.Count == 0)
            {
                return;
            }

            var paths = audio.Select(e => e.Path).ToList();

            try
            {
                var tags = await LibraryTagsReader.ReadAsync(paths);

                if (generation != _libraryGeneration)
                {
                    return;
                }

                for (var i = 0; i < _libraryAll.Count; i++)
                {
                    var entry = _libraryAll[i];

                    if (!tags.TryGetValue(entry.Path, out var found))
                    {
                        continue;
                    }

                    var replaced = LibraryEntry.FromFileWithTags(
                        entry.Path, entry.Title, entry.Time, found);

                    if (replaced is null)
                    {
                        continue;
                    }

                    // The tag read builds a NEW entry, and the thumbnail is not a
                    // tag - so it has to be carried across or the card loses its
                    // cover. This is the "cover shows once, then goes grey" bug:
                    // the thumbnails load into the instances this loop replaces.
                    //
                    // The instance being replaced is the authority: it already holds
                    // whatever LoadThumbnailsAsync decoded for this path.
                    if (entry.Thumbnail is { } existing)
                    {
                        replaced.SetThumbnail(existing);
                    }

                    // The tick is state on the ENTRY, so a rebuild that replaces the
                    // entry would silently untick a card the user had just selected.
                    // Outside the thumbnail check on purpose: selection must survive
                    // whether or not a cover has decoded yet, and the thumbnail source
                    // and the entry being replaced are different objects.
                    replaced.SetSelected(entry.IsSelected);

                    replaced.SetSelectionMode(_selectionMode);

                    _libraryAll[i] = replaced;
                }

                // The grid holds the same objects the list did, so it has to be
                // handed the new instances rather than refreshed in place.
                if (_librarySection == LibrarySectionMusic && _libraryLoaded)
                {
                    ApplyLibraryFilter();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "library: music tags unavailable");
            }
        }

        /// <summary>
        /// Groups video entries by the directory each was played from, ordered
        /// by folder name. Two directories that share a leaf name (two
        /// "Downloads" folders) stay separate groups: the key is the full path,
        /// only the label is the leaf.
        /// </summary>
        /// <summary>
        /// The card grid metrics, in one place.
        ///
        /// The flat grid's numbers live in MpvPlayerPage.xaml; these two repeaters are
        /// built in code and used to carry their own copy. When the card became 16:9
        /// the markup moved and these did not, which would have laid landscape cards
        /// out in square cells in both grouped views.
        ///
        /// 253 x 200 is the markup's pair: 253 wide through 16:9 is 142, plus the two
        /// caption lines. Change both together - the markup comment says so too.
        /// </summary>
        /// <summary>
        /// The album grid: squarer, narrower tiles than the video grid.
        ///
        /// MinItemWidth 170 is the reference's own cover size (255 px at DPI 1.5), and
        /// MinItemHeight leaves room for a square cover plus the album and artist lines.
        ///
        /// Its own method rather than a parameter on CardLayout(): the two widths mean
        /// different things - a 16:9 card is 253 wide BECAUSE it is 142 tall, while a
        /// square tile is 170 for its own sake - and a shared method taking two magic
        /// numbers would hide which one belongs to which kind of card.
        /// </summary>
        private static UniformGridLayout AlbumCardLayout() => new()
        {
            MinItemWidth = 170,
            MinItemHeight = 246,
            MinColumnSpacing = 12,
            MinRowSpacing = 20,
        };
    }
}
