using mpv_winui.Infrastructure.Common.Media;
using mpv_winui.Domains.Playback.Cards;
using mpv_winui.Domains.Playback.Controls;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
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

            // The grouping is part of the toolbar row's predicate - the artists tab
            // keeps no row - and ApplySectionHeader is where that predicate is
            // assigned, so switching tabs has to go through it. Before the load
            // guard on purpose: the three tabs are clickable before the first list
            // arrives, and a row that waits for the load would stay wrong until the
            // next section switch.
            ApplySectionHeader();

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

        /// <summary>
        /// The album currently shown, and the artist it was opened from.
        ///
        /// <para><b>Why the artist is part of the key.</b> The album page used to be
        /// identified by the album's name alone, and it filtered the library by that
        /// name alone. Two artists each having a "Greatest Hits" is ordinary, and both
        /// of their tracks landed on one page. From the artist page the album is a
        /// pair, so the pair is what gets remembered.</para>
        ///
        /// <para><b>Null means "opened from the Albums tab".</b> That tab groups by
        /// album name, so it has no artist to narrow by and must keep showing the
        /// whole album - the same page it showed before this existed.</para>
        ///
        /// <para>A tuple rather than a nullable record: it is a value type, so it can
        /// never be null, which is why <see cref="_albumOpened"/> carries the "is the
        /// page up at all" answer that the old <c>string?</c> used to carry.</para>
        /// </summary>
        private (string Album, string? Artist) _openAlbum;

        /// <summary>
        /// Whether the album page is up. The tuple cannot answer that by itself.
        /// </summary>
        private bool _albumOpened;

        /// <summary>
        /// The artist whose page is open, or null when the host shows an album.
        ///
        /// The artist page and the album page share one host (they are one surface
        /// with two states), so this is the state that says which one is showing and
        /// therefore where the back button goes.
        /// </summary>
        private string? _openArtist;

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
        /// Called from a card click while the Albums tab is up, and from a card click
        /// on the artist page. The tracks are the library's own entries filtered by the
        /// same album key the grid grouped on, so the table cannot disagree with the
        /// cards behind it.
        /// </summary>
        private void OpenAlbum(string album) => OpenAlbum(album, artist: null);

        /// <summary>
        /// Shows one album, optionally narrowed to one artist.
        ///
        /// <paramref name="artist"/> null is the Albums tab: group by name, show the
        /// whole album. Non-null is the artist page, where the album was grouped inside
        /// that artist and a same-named album by someone else must not join it.
        ///
        /// An empty string is a REAL artist here - the "unknown artist" bucket - so the
        /// test is <c>is null</c>, never <c>IsNullOrWhiteSpace</c>.
        /// </summary>
        private void OpenAlbum(string album, string? artist)
        {
            _openAlbum = (album, artist);

            _albumOpened = true;

            _albumTracks = _librarySection == LibrarySectionMusic
                ? _libraryAll
                    .Where(e => MediaKinds.IsAudio(e.Path))
                    .Where(e => string.Equals(
                        string.IsNullOrWhiteSpace(e.Album) ? AppContext.AppLang.LibraryUnknownAlbum : e.Album,
                        album,
                        StringComparison.CurrentCultureIgnoreCase))
                    .Where(e => artist is null
                        || string.Equals(
                            string.IsNullOrWhiteSpace(e.Artist) ? string.Empty : e.Artist,
                            artist,
                            StringComparison.CurrentCultureIgnoreCase))
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                : [];

            ApplyAlbumChrome();

            // The artist page and the album page are two states of one host: going into
            // an album from the artist page leaves the artist page hidden, and the back
            // button's word follows from _openArtist still being set.
            ArtistHost.Visibility = Visibility.Collapsed;

            LibraryHost.Visibility = Visibility.Collapsed;

            AlbumHost.Visibility = Visibility.Visible;

            MotionBehaviors.FadeIn(AlbumHost);
        }

        /// <summary>Closes the album page and puts the library back.</summary>
        private void CloseAlbum()
        {
            if (!_albumOpened)
            {
                return;
            }

            // From the artist page, "back" is one step, not all the way out: the album
            // was reached from that artist's albums, and dropping the user on the tile
            // wall would lose the place they were reading.
            if (_openArtist is not null)
            {
                _albumOpened = false;

                _albumTracks = [];

                ApplyArtistChrome();

                // The host is put back HERE, not left to OpenAlbum having collapsed it:
                // OpenAlbum did collapse it on the way in, so a branch that only rebuilds
                // the artist's contents leaves an empty host on screen. Visibility is
                // restored before the fade so the page animates as one surface rather
                // than popping.
                ArtistHost.Visibility = Visibility.Visible;

                MotionBehaviors.FadeIn(AlbumHost);

                return;
            }

            HideSurfacesOverLibrary();

            LibraryHost.Visibility = Visibility.Visible;

            // The grid may have been reloaded while the album was open (a folder
            // re-bind, a rescan); re-applying is cheaper than tracking that.
            ApplyLibraryFilter();
        }

        /// <summary>The artist page's back command: one level out, to the tile wall.</summary>
        private void ArtistBack_Click(object sender, RoutedEventArgs e) => CloseArtist();

        /// <summary>
        /// Shows one artist: their portrait, their name, and their albums.
        ///
        /// <para><b>The albums are grouped here, not carried in.</b> The wall's tiles
        /// hold one carrier entry each (a tile is a person, not a track), so the
        /// artist's real tracks are re-derived from the library. Grouping by album key
        /// gives one card per album, and the carrier for each card is that album's first
        /// track - the same trick the tiles use, for the same reason: a card needs an
        /// entry to render, and an album has none of its own.</para>
        ///
        /// <para><b>An empty artist is a real artist.</b> It is the "unknown artist"
        /// bucket, which is one group on the wall like any other, so it opens like any
        /// other.</para>
        /// </summary>
        private void OpenArtist(string artist)
        {
            _openArtist = artist;

            _albumOpened = false;

            _albumTracks = [];

            ApplyArtistChrome();

            LibraryHost.Visibility = Visibility.Collapsed;

            AlbumHost.Visibility = Visibility.Visible;

            ArtistHost.Visibility = Visibility.Visible;

            MotionBehaviors.FadeIn(AlbumHost);
        }

        /// <summary>
        /// Closes the artist page and puts the library back.
        ///
        /// Goes through <see cref="HideSurfacesOverLibrary"/> rather than collapsing
        /// the host by hand: that method is the one place that knows every surface
        /// standing above the library, and it also drops the artist state.
        /// </summary>
        private void CloseArtist()
        {
            if (_openArtist is null)
            {
                return;
            }

            HideSurfacesOverLibrary();

            LibraryHost.Visibility = Visibility.Visible;

            ApplyLibraryFilter();
        }

        /// <summary>
        /// Fills the artist page from <see cref="_openArtist"/>: its words, its albums,
        /// and where the back button goes.
        ///
        /// Its own method because two callers rebuild this page - opening an artist, and
        /// returning to it from one of their albums - and both must produce the same
        /// surface.
        /// </summary>
        private void ApplyArtistChrome()
        {
            var lang = AppContext.AppLang;

            var named = string.IsNullOrWhiteSpace(_openArtist)
                ? lang.LibraryUnknownArtist
                : _openArtist;

            ArtistDetailName.Text = named;

            // The back button says where it goes. From an album opened FROM this artist
            // the album page draws its own back button, and that one has to say
            // "back to artist" rather than "back to music" - see ApplyAlbumChrome.
            ArtistBackText.Text = lang.LibraryAlbumBack;

            AutomationProperties.SetName(ArtistBackButton, lang.LibraryAlbumBack);
            ToolTipService.SetToolTip(ArtistBackButton, lang.LibraryAlbumBack);

            var tracks = _libraryAll
                .Where(e => MediaKinds.IsAudio(e.Path))
                .Where(e => string.Equals(
                    string.IsNullOrWhiteSpace(e.Artist) ? string.Empty : e.Artist,
                    _openArtist ?? string.Empty,
                    StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            // One card per album, in album order. The carrier is the album's first
            // track by title, so the grid is stable across rebuilds rather than
            // reshuffling with the library's own ordering.
            var albums = tracks
                .GroupBy(
                    e => string.IsNullOrWhiteSpace(e.Album) ? "\uFFFF" : e.Album,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(group => group
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .First())
                .OrderBy(
                    e => string.IsNullOrWhiteSpace(e.Album) ? "\uFFFF" : e.Album,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            ArtistAlbumGrid.Layout = AlbumCardLayout();

            // The template, without which an ItemsRepeater draws nothing at all and
            // ElementPrepared never fires - the grid was declared bare in markup and
            // this is the only place that fills it. Same resource the other three
            // repeaters use, so an album card here is the album card everywhere else.
            ArtistAlbumGrid.ItemTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];

            ArtistAlbumGrid.ItemsSource = albums;

            ArtistAlbumGrid.ElementPrepared -= ArtistAlbumGrid_ElementPrepared;
            ArtistAlbumGrid.ElementPrepared += ArtistAlbumGrid_ElementPrepared;
        }

        /// <summary>
        /// Prepares one album card on the artist page.
        ///
        /// <para><b>Two things differ from the same card on the Albums tab.</b> The
        /// caption is the album and its artist rather than the file name - this grid
        /// lists albums, and "01 - Track.flac" says nothing about which album a card
        /// stands for. And the click opens the album narrowed to this artist, because
        /// the album was grouped inside them.</para>
        /// </summary>
        private void ArtistAlbumGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is not LibraryCard card
                || sender.ItemsSourceView is not { } view
                || args.Index < 0 || args.Index >= view.Count
                || view.GetAt(args.Index) is not LibraryEntry album)
            {
                return;
            }

            card.IsArtist = false;

            card.IsAlbum = true;

            card.SetEntry(album, ResolveCover(album));

            // The caption, over the top of what SetEntry just wrote. Done here rather
            // than by a card mode because it is one grid's rule, not a property of the
            // card: the Songs and Albums tabs show the file name and must keep doing so.
            //
            // Reassigned on every prepare, so a recycled element cannot arrive wearing
            // the previous album's name.
            card.SetAlbumCaption(album.AlbumTitle, album.AlbumArtistText);

            card.CardTapped -= ArtistAlbumCard_Tapped;
            card.CardTapped += ArtistAlbumCard_Tapped;

            // The two discs and the tick box are live on this card, so they have to mean
            // something here. All three get the same handlers the rest of the library
            // uses, which is what keeps an artist-page card from clicking into silence:
            // the disc plays the album's carrier track, "more" opens the shared card menu,
            // and the tick behaves as it does everywhere else - album cards are tickable
            // (「专辑卡也能勾选，和点击不冲突」), which is a different rule from the artist
            // TILES, whose lack of a tick box is spec 8.1.4 and a fact about the person,
            // not about this card.
            card.PlayRequested -= Card_PlayRequested;
            card.PlayRequested += Card_PlayRequested;

            card.MoreRequested -= Card_MoreRequested;
            card.MoreRequested += Card_MoreRequested;

            card.SelectionRequested -= Card_SelectionRequested;
            card.SelectionRequested += Card_SelectionRequested;
        }

        /// <summary>Opens the album a card on the artist page stands for.</summary>
        private void ArtistAlbumCard_Tapped(object? sender, EventArgs e)
        {
            if (_openArtist is null || sender is not LibraryCard { Entry: { } album })
            {
                return;
            }

            OpenAlbum(
                string.IsNullOrWhiteSpace(album.Album)
                    ? AppContext.AppLang.LibraryUnknownAlbum
                    : album.Album,
                _openArtist);
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

            // The back button says where it actually goes. Opened from an artist, one
            // step out is that artist's albums, not the whole library - and a button
            // labelled "back to music" that lands on one artist is a wrong statement
            // rather than a shorthand.
            var backToArtist = _openAlbum.Artist is not null;

            AlbumBackText.Text = backToArtist ? lang.LibraryBackToArtist : lang.LibraryAlbumBack;

            AutomationProperties.SetName(
                AlbumBackButton, backToArtist ? lang.LibraryBackToArtist : lang.LibraryAlbumBack);

            ToolTipService.SetToolTip(
                AlbumBackButton, backToArtist ? lang.LibraryBackToArtist : lang.LibraryAlbumBack);

            AlbumColumnTitle.Text = lang.LibraryColumnTitle;
            AlbumColumnArtist.Text = lang.LibraryColumnArtist;
            AlbumColumnGenre.Text = lang.LibraryColumnGenre;
            AlbumColumnLength.Text = lang.LibraryColumnLength;

            AlbumPlayAllText.Text = lang.LibraryPlayAll;
            AlbumShuffleText.Text = lang.LibraryShufflePlay;
            AlbumAddToText.Text = lang.LibraryAlbumAddTo;

            // Same reason as the selection bar: an icon beside a TextBlock
            // leaves the button itself unnamed to UIA, so the four album
            // actions carry their accessible name and tooltip from here.
            //
            // AlbumBackButton is NOT named here: its word depends on where the page
            // was opened from, and that is decided above. Naming it twice would let
            // the second write undo the first - measured in this file before.
            AutomationProperties.SetName(AlbumPlayAllButton, lang.LibraryPlayAll);
            AutomationProperties.SetName(AlbumShuffleButton, lang.LibraryShufflePlay);
            AutomationProperties.SetName(AlbumAddToButton, lang.LibraryAlbumAddTo);
            ToolTipService.SetToolTip(AlbumPlayAllButton, lang.LibraryPlayAll);
            ToolTipService.SetToolTip(AlbumShuffleButton, lang.LibraryShufflePlay);
            ToolTipService.SetToolTip(AlbumAddToButton, lang.LibraryAlbumAddTo);

            AlbumDetailTitle.Text = _openAlbum.Album;

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
            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];
            // The music group header, not the folder one: see the note on
            // MusicGroupHeaderTemplate. The folder header reserved a line for a path
            // that a tag group does not have, so every album name sat above an empty
            // band.
            var headerTemplate = (DataTemplate)Resources["MusicGroupHeaderTemplate"];

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

            // Every header was just built, and by now the thumbnails may already
            // be decoded - the loader starts after the grid is up, and this method
            // can run later than that when a tab switch rebuilds the groups. Those
            // decodes happened before these headers existed, so no notification
            // carried them here: the header read the thumbnail at construction and
            // got null. Telling them once, now, is the difference between a sleeve
            // and a glyph.
        }

        /// <summary>
        /// Fills the artists tab with one circular tile per artist.
        ///
        /// <para><b>Why not <see cref="BuildMusicTagGroups"/>.</b> That method groups
        /// the tracks of one tag and hands the whole group to a repeater, so the
        /// Artists tab used to be a wall of TRACK cards under an artist heading - the
        /// heading was the artist, the cards underneath were songs. This method gives
        /// each artist exactly one card instead: the tile stands for the person, and
        /// their songs arrive one level down.</para>
        ///
        /// <para><b>One carrier entry per tile.</b> A card needs an entry to render, and
        /// an artist has none of their own, so the tile carries the group's first track.
        /// <see cref="VideoFolderGroup.Items"/> must therefore hold exactly that one
        /// entry - the repeater draws one card per item, so handing it the whole group
        /// rebuilds the wall this replaces. The artist's real track list is re-derived
        /// by <see cref="OpenArtist"/> from the library.</para>
        /// </summary>
        private void BuildArtistTiles(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];

            // Its own header shape: the album header carries a 56px sleeve and a track
            // count, and a person has neither. One letter is all this wall needs.
            var headerTemplate = (DataTemplate)Resources["ArtistGroupHeaderTemplate"];

            // "Unknown" sorts last, like the two tag groupings: it is the fallback
            // bucket, not the most important artist in the library.
            var unknownLabel = AppContext.AppLang.LibraryUnknownArtist;

            var groups = entries
                .GroupBy(
                    entry => string.IsNullOrWhiteSpace(entry.Artist) ? "\uFFFF" : entry.Artist,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(group =>
                {
                    var name = group.Key == "\uFFFF" ? unknownLabel : group.Key;

                    // Exactly one: see the note above. Ordered by title first so the
                    // carrier is stable across rebuilds rather than following whatever
                    // order the scan produced.
                    var carrier = group
                        .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                        .First();

                    return new VideoFolderGroup(
                        name,
                        new List<LibraryEntry> { carrier },
                        Path: string.Empty,
                        Letter: LibraryIndexLetter.For(name, ch => PinyinInitial.For(ch.ToString())));
                })
                .OrderBy(group => Array.IndexOf(LibraryIndexLetter.All, group.Letter))
                .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

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
                    // First group wins, as in the tag groupings: several artists can
                    // share a letter and the rail lands on the first of them.
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

                // Album sizes, counted ONCE from the entries that are about to be
                // rebuilt. Doing it inside the loop would make it O(n^2) over a
                // library of a thousand tracks.
                //
                // Keyed by PATH, not by album name. The entries here have not been
                // through the tag pass yet - their Album is empty, because a history
                // entry never opens the file - so counting by album name groups every
                // track into one bucket in the first pass while the second pass looks
                // up real tag names and finds nothing. Measured: the count stayed
                // null for a one-track album until this changed.

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

                    // Same argument for the playing marker, and the symptom is the
                    // same shape: the tags finish, the tree is rebuilt, and the
                    // stripe vanishes from under the user's eye. Carrying it here is
                    // what makes the marker survive a rebuild nobody asked for.
                    replaced.SetNowPlaying(entry.IsNowPlaying);

                    replaced.SetSelectionMode(_selectionMode);

                    _libraryAll[i] = replaced;
                }

                // Second pass: the count is per album, the entries are per file, and
                // the instances have just been replaced. Writing it here keeps
                // FromFileWithTags ignorant of a fact it cannot know from one file.
                //
                // The sizes are recounted HERE, from the entries as they now are.
                // The first count ran before the tag pass, when every Album was still
                // empty - so it grouped the whole library into one bucket. Counting
                // again after the pass is the fix, and it costs one pass over a list
                // that is already in memory.
                var albumSizes = _libraryAll
                    .Where(e => !e.IsVideo && MediaKinds.IsAudio(e.Path))
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Album) ? "\uFFFF" : e.Album,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(),
                        StringComparer.CurrentCultureIgnoreCase);

                for (var i = 0; i < _libraryAll.Count; i++)
                {
                    var entry = _libraryAll[i];

                    if (entry.IsVideo || !MediaKinds.IsAudio(entry.Path))
                    {
                        continue;
                    }

                    var key = string.IsNullOrWhiteSpace(entry.Album) ? "\uFFFF" : entry.Album;

                    if (albumSizes.TryGetValue(key, out var size) && entry.TrackCount is null)
                    {
                        _libraryAll[i] = entry.WithTrackCount(AlbumTrackCount.For(size));
                    }
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
        /// Fills in video lengths, so their cards can show one.
        ///
        /// <para><b>Why a pass of its own.</b> <see cref="LoadMusicTagsAsync"/> reads
        /// <c>MusicProperties</c>, which has no handler for a video container - it
        /// throws, the reader swallows it into <c>MusicTags.Empty</c>, and a video
        /// entry never gained a <c>Duration</c>. So the card's second line never
        /// appeared, which is what 「视频没有显示秒数」 was reporting: not a formatting
        /// bug, a missing read. Folding this into the music pass would not work
        /// either, because that one rebuilds entries through
        /// <c>FromFileWithTags</c> and would write empty music tags over the video
        /// entry's fields.</para>
        ///
        /// <para>Same shape as the music pass: cards first, numbers second, and the
        /// mutable state carried across so a cover that just decoded and a tick the
        /// user just made both survive the rebuild.</para>
        /// </summary>
        private async Task LoadVideoDurationsAsync(int generation)
        {
            var videos = _libraryAll.Where(e => e.IsVideo && e.Duration is null).ToList();

            if (videos.Count == 0)
            {
                return;
            }

            var paths = videos.Select(e => e.Path).ToList();

            try
            {
                var lengths = await LibraryTagsReader.ReadDurationsAsync(paths);

                if (generation != _libraryGeneration)
                {
                    return;
                }

                for (var i = 0; i < _libraryAll.Count; i++)
                {
                    var entry = _libraryAll[i];

                    if (!entry.IsVideo || entry.Duration is not null)
                    {
                        continue;
                    }

                    if (!lengths.TryGetValue(entry.Path, out var length))
                    {
                        // No length from the index: leave the entry alone so the card
                        // keeps its one caption line. Writing a zero instead would
                        // print 0:00, which is a lie about the file.
                        continue;
                    }

                    _libraryAll[i] = entry.WithDuration(length);
                }

                // Rebuilt so the new second line actually appears: the entry carries
                // the length, but the card that drew it was built before the value
                // existed. Safe here because the caller has awaited the thumbnail
                // decodes - see FinishVideoDurationsAsync - so ResolveCover finds the
                // covers in _libraryThumbnails instead of racing them.
                if (_libraryLoaded)
                {
                    ApplyLibraryFilter();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "library: video lengths unavailable");
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
        /// 265 x 216 is the markup's pair: 253 of picture through 16:9 is 142, plus
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
        /// <summary>
        /// The home section: two bands, watched and listened, each with its own grid.
        ///
        /// The home grid used to show every entry in one flow, and the two kinds of
        /// card are different shapes - a 16:9 frame and a square sleeve. Dropping a
        /// square card into a landscape cell is what made a music card read as "a
        /// strange rectangle": it was a square tile stretched into a 16:9 slot, with
        /// the same MinItemHeight as its neighbours.
        ///
        /// Two bands with two layouts fixes both the mixing and the shape, and it is
        /// why the home section reuses the group container: that container already
        /// holds a stack of header + repeater pairs, which is exactly two bands.
        /// </summary>
        private void BuildHomeSections(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            _groupHeaders.Clear();

            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];

            var listened = entries.Where(e => MediaKinds.IsAudio(e.Path)).ToList();
            var watched = entries.Where(e => !MediaKinds.IsAudio(e.Path)).ToList();

            AddHomeBand("最近观看", watched, isAlbum: false);
            AddHomeBand("最近收听", listened, isAlbum: true);

            void AddHomeBand(string caption, List<LibraryEntry> band, bool isAlbum)
            {
                if (band.Count == 0)
                {
                    return;
                }

                // A plain heading rather than the group header: these two bands are
                // not groups of a tag, they are the two halves of the home page, and
                // the count belongs nowhere in particular.
                //
                // The LEFT margin is the frame's gutter, and it is the whole of
                // 「视频音频卡片和最近观看这些主页文本的对齐应该是同内侧缩略图对齐，不是外部
                // 遮罩框」. Every card's cell begins with CardMetrics.HoverFrameInset of
                // empty gutter before its picture does, so a heading at margin 0 lined
                // up with the hover frame's outer edge - an invisible box - while the
                // thumbnails began 6 DIP further in. Indenting by the same 6 puts the
                // heading on the pictures' left edge.
                var heading = new TextBlock
                {
                    Text = caption,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Margin = new Thickness(CardMetrics.HoverFrameInset, 12, 0, 8),
                };

                VideoFolderGroups.Children.Add(heading);

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    Layout = isAlbum ? AlbumCardLayout() : CardLayout(),
                };

                repeater.ElementPrepared += LibraryGrid_ElementPrepared;

                // ItemsSource is what ElementPrepared reads the entry back out of, so
                // it has to be set before the repeater is measured.
                repeater.ItemsSource = band;

                VideoFolderGroups.Children.Add(repeater);
            }
        }

        /// <summary>
        /// Points the flat grid's single ItemsRepeater at the cell shape its
        /// section needs.
        ///
        /// An ItemsRepeater has ONE Layout, and every section shares this
        /// repeater, so the shape has to be switched rather than declared. The
        /// markup carries the landscape one because that is what Home, Videos and
        /// Search use; only the music library is square.
        ///
        /// Assigning a new Layout is a full re-flow of the repeater, which is why
        /// this is called from ApplyLibraryFilter (once per filter change) and not
        /// from a size-changed handler.
        /// </summary>
        private void ApplyLibraryGridLayout(bool square)
        {
            if (square)
            {
                LibraryGrid.Layout = AlbumCardLayout();

                return;
            }

            // Rebuilt rather than restored from a field: UniformGridLayout is not
            // shared between two repeaters in this page, so a fresh instance cannot
            // pick up a stale MinItemHeight from whichever section ran last.
            LibraryGrid.Layout = CardLayout();
        }

        private static UniformGridLayout AlbumCardLayout()
        {
            // 136 is 0.8 x the 170 this used to be, scaled with the landscape grid's
            // 253 -> 202 so the two keep their ratio. The cell height is derived: the
            // square tile plus the caption band.
            //
            // As in CardLayout, this is the CONTENT's width: CellWidthFor adds the
            // frame's gutter, so the cell is 148 and the sleeve stays 136. Both
            // spacings come from CardMetrics so the two grids cannot drift apart.
            const double width = 150;

            return new UniformGridLayout
            {
                MinItemWidth = CardMetrics.CellWidthFor(width),
                MinItemHeight = CardMetrics.CellHeightFor(width, isAlbum: true),
                MinColumnSpacing = CardMetrics.GridColumnSpacing,
                MinRowSpacing = CardMetrics.GridRowSpacing,
            };
        }
    }
}
