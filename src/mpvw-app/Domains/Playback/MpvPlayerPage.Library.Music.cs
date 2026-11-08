using mpv_winui.Infrastructure.Common.Media;
using mpv_winui.Domains.Playback.Cards;
using mpv_winui.Domains.Playback.Controls;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        /// The genre dropdown's caption, its accessible name, and its items.
        ///
        /// <para>The items are rebuilt rather than declared because the vocabulary comes
        /// from the tracks that happen to be loaded: a genre that no file uses cannot be
        /// offered, and caching the list would keep offering one after its last file was
        /// removed.</para>
        /// </summary>
        private void ApplyGenreStrings()
        {
            var lang = AppContext.AppLang;

            AutomationProperties.SetName(MusicGenreButton, lang.LibraryColumnGenre);

            ToolTipService.SetToolTip(MusicGenreButton, lang.LibraryColumnGenre);

            MusicGenreButton.Content = $"{lang.LibraryColumnGenre}: {CurrentGenreLabel()}";

            if (MusicGenreButton.Flyout is not MenuFlyout menu)
            {
                return;
            }

            menu.Items.Clear();

            menu.Items.Add(GenreItem(lang.LibraryGenreAll, "all", _musicGenre.Kind == MusicGenreKind.All));

            foreach (var name in _musicGenreVocabulary)
            {
                var chosen = _musicGenre.Kind == MusicGenreKind.Named
                    && string.Equals(_musicGenre.Name, name, StringComparison.OrdinalIgnoreCase);

                menu.Items.Add(GenreItem(name, "genre:" + name, chosen));
            }

            // Unknown LAST: it is the fallback bucket, not the most important genre,
            // and a list that opens on "unknown" reads as if it were a real tag.
            menu.Items.Add(GenreItem(lang.LibraryUnknownGenre, "unknown", _musicGenre.Kind == MusicGenreKind.Unknown));
        }

        /// <summary>One ticked-able entry in the genre menu.</summary>
        private RadioMenuFlyoutItem GenreItem(string text, string tag, bool chosen)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = text,
                Tag = tag,
                GroupName = "MusicGenre",
                IsChecked = chosen,
            };

            item.Click += MusicGenre_Click;

            return item;
        }

        /// <summary>What the dropdown shows for the current choice.</summary>
        private string CurrentGenreLabel() => _musicGenre.Kind switch
        {
            MusicGenreKind.All => AppContext.AppLang.LibraryGenreAll,
            MusicGenreKind.Unknown => AppContext.AppLang.LibraryUnknownGenre,
            _ => _musicGenre.Name,
        };

        /// <summary>
        /// Rebuilds the genre menu each time it opens.
        ///
        /// <para>The vocabulary is filled by the library load, which finishes long after
        /// this page is constructed. Building the menu once at startup left it holding
        /// only the two fixed entries for the whole session - the genres arrived in the
        /// field and never reached the flyout. Rebuilding on open is also what makes a
        /// language change and a re-scan show up without a second code path.</para>
        /// </summary>
        private void MusicGenre_Opening(object sender, object e) => ApplyGenreStrings();

        private void MusicGenre_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem { Tag: string tag })
            {
                return;
            }

            _musicGenre = tag switch
            {
                "all" => MusicGenreSelection.All,
                "unknown" => MusicGenreSelection.Unknown,
                _ when tag.StartsWith("genre:", StringComparison.Ordinal)
                    => MusicGenreSelection.Named(tag["genre:".Length..]),
                _ => MusicGenreSelection.All,
            };

            ApplyGenreStrings();

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }
        private void MusicTabSongs_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Songs);

        private void MusicTabAlbums_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Albums);

        private void MusicTabArtists_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Artists);

        private MusicGrouping _musicGrouping = MusicGrouping.Songs;

        /// <summary>
        /// The genre the dropdown is filtering to.
        ///
        /// <para>Session-only, and deliberately not persisted: a genre picked here is a
        /// look at the library right now, not a preference, and restoring it on the next
        /// launch would leave the user staring at a filtered library with no memory of
        /// having filtered it.</para>
        /// </summary>
        private MusicGenreSelection _musicGenre = MusicGenreSelection.All;

        /// <summary>
        /// Every genre name the loaded tracks use, sorted, for the dropdown.
        ///
        /// <para>Filled from the library BEFORE the genre filter is applied (spec 8.3.11).
        /// Taken after that filter, the entry the user just chose would vanish from the
        /// menu on the next reload: the button would still name it while the menu had
        /// nothing to tick.</para>
        /// </summary>
        private IReadOnlyList<string> _musicGenreVocabulary = [];

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
            // Normalised HERE, at the single entry point, rather than at each caller.
            // A whitespace-only tag and an absent one are the same artist as far as the
            // grouping is concerned - both land in the unknown bucket - so storing the
            // raw value would make the page open EMPTY: its own comparison normalises,
            // and "" would not match "   ". Normalising once also keeps CloseAlbum's
            // "did we come from an artist" test honest, since that test is a null check.
            _openArtist = string.IsNullOrWhiteSpace(artist) ? string.Empty : artist;

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
        /// Grows or shrinks one heading's text under the pointer.
        ///
        /// <para>A RenderTransform rather than a font-size change: a font change re-measures
        /// the line and nudges every card below it, so the list would twitch as the pointer
        /// moved down it. A scale transform moves nothing but the pixels of the text, and
        /// its centre is the text's own centre, which is what makes it read as the label
        /// growing rather than sliding.</para>
        /// </summary>
        private static void ScaleHeader(TextBlock label, float factor)
        {
            label.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);

            if (label.RenderTransform is not ScaleTransform scale)
            {
                scale = new ScaleTransform();

                label.RenderTransform = scale;
            }

            scale.ScaleX = factor;

            scale.ScaleY = factor;
        }

        /// <summary>How many letters fit across the sheet before it wraps.</summary>
        private const int PickerColumns = 8;

        /// <summary>
        /// Opens the heading picker over the content area.
        ///
        /// <para><b>A sheet, not a flyout.</b> The picker used to be a flyout anchored to the
        /// heading that was tapped - sized to its content, hanging off one row, and the reader
        /// had to find it. It now covers the rectangle the cards occupy, centred in it, so
        /// opening it reads as the content becoming the index.</para>
        ///
        /// <para><b>The letters are built here, into a Grid.</b> An ItemsRepeater with
        /// UniformGridLayout cannot lay out under an infinite height constraint, and centring
        /// the repeater asks for exactly that - it collapsed and took the whole content area
        /// with it, twice, before it was traced. Fifty-three buttons need no virtualisation,
        /// and a Grid filled by hand has no constraint to disagree with.</para>
        /// </summary>
        private void GroupHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {

            FillGroupPicker();

            // Sized BEFORE it is shown, and that order is the whole of the fix for the
            // twitch.
            //
            // Visibility used to come first, so the sheet was laid out and painted once at
            // its natural scale - full size - and only then did the first timer tick drop it
            // to 0.55 and start growing. One frame at 100% followed by a jump to 55% is the
            // 抽搐: it looked like the sheet flinched before expanding.
            ResetSheetTransform(0.55f, 0f);

            LibraryGroupPickerHost.Visibility = Visibility.Visible;

            PlaySheetAnimation(from: 0.55f, to: 1f, fromOpacity: 0f, toOpacity: 1f);
        }

        /// <summary>
        /// Puts the sheet at a given scale and opacity without animating.
        ///
        /// <para>Called before the sheet is made visible, so the very first frame it paints is
        /// already the small one. Setting these properties on an element that is still
        /// Collapsed is free - nothing is measured or painted until it is shown.</para>
        /// </summary>
        private void ResetSheetTransform(float scaleTo, float opacityTo)
        {
            var sheet = LibraryGroupPickerSheet;

            var scale = sheet.RenderTransform as ScaleTransform ?? new ScaleTransform();

            sheet.RenderTransform = scale;

            scale.ScaleX = scaleTo;

            scale.ScaleY = scaleTo;

            sheet.Opacity = opacityTo;
        }

        /// <summary>
        /// The expansion: the sheet grows out of the middle while the list behind it softens.
        ///
        /// <para><b>Driven frame by frame, not by an animation.</b> Three earlier attempts all
        /// took the process down: a compositor StartAnimation on the scroll port's visual
        /// (0xc000027b on the XAML side), and a code-built Storyboard whose target could not be
        /// resolved through the XAML namescope (0x80070578 from combase). A DispatcherQueueTimer
        /// asks the UI thread for a tick, writes two plain properties, and returns - nothing in
        /// it can fail on a namescope, a compositor, or an element's visual lifetime.</para>
        ///
        /// <para><b>Eased, not linear.</b> The 0-1 progress is cubed out of the way by
        /// <see cref="Ease"/>: an expanding surface that moves at constant speed reads as
        /// mechanical, and the whole point of the motion is that the letters arrive.</para>
        /// </summary>
        private void PlaySheetAnimation(float from, float to, float fromOpacity, float toOpacity)
        {
            _sheetAnimation?.Stop();

            var sheet = LibraryGroupPickerSheet;

            var scale = sheet.RenderTransform as ScaleTransform ?? new ScaleTransform();

            sheet.RenderTransform = scale;

            var steps = 16;

            var step = 0;

            var timer = DispatcherQueue.CreateTimer();

            timer.Interval = TimeSpan.FromMilliseconds(12);

            timer.IsRepeating = true;

            timer.Tick += (_, _) =>
            {
                step++;

                var progress = Ease((float)step / steps);

                scale.ScaleX = from + ((to - from) * progress);

                scale.ScaleY = scale.ScaleX;

                sheet.Opacity = fromOpacity + ((toOpacity - fromOpacity) * progress);

                // The list softens over the same span, so the two movements are one gesture
                // rather than a fade and a grow that happen to overlap.
                LibraryScroll.Opacity = 1.0 - progress;

                if (step < steps)
                {
                    return;
                }

                timer.Stop();

                sheet.Opacity = toOpacity;

                scale.ScaleX = to;

                scale.ScaleY = to;

                LibraryScroll.Opacity = 1.0 - toOpacity;
            };

            _sheetAnimation = timer;

            timer.Start();
        }

        /// <summary>Fast at the start, settled at the end: cubic ease-out.</summary>
        private static float Ease(float t)
        {
            var inverse = 1f - t;

            return 1f - (inverse * inverse * inverse);
        }

        /// <summary>
        /// Builds the letter buttons into <see cref="LibraryGroupPickerLetters"/>.
        ///
        /// <para>Rebuilt on every open rather than cached: which buckets have content is a
        /// function of the current list, and a cached grid would keep pointing at headings
        /// that no longer exist.</para>
        /// </summary>
        private void FillGroupPicker()
        {
            LibraryGroupPickerLetters.Children.Clear();

            LibraryGroupPickerLetters.ColumnDefinitions.Clear();

            LibraryGroupPickerLetters.RowDefinitions.Clear();

            var entries = BuildGroupPickerEntries();

            var columns = System.Math.Min(PickerColumns, System.Math.Max(1, entries.Count));

            for (var column = 0; column < columns; column++)
            {
                LibraryGroupPickerLetters.ColumnDefinitions.Add(new ColumnDefinition());
            }

            var rows = (entries.Count + columns - 1) / columns;

            for (var row = 0; row < rows; row++)
            {
                LibraryGroupPickerLetters.RowDefinitions.Add(new RowDefinition());
            }

            var current = CurrentGroupKey();

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];

                var letter = new Button
                {
                    Content = entry.Letter,
                    Tag = entry.Letter,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),

                    // 1, not 0: the hover outline is the same border, just coloured. Zero
                    // thickness would reserve no room and the cell would shift 1px on hover.
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Style = (Style)Resources["MpvLetterButtonStyle"],

                    // An empty bucket cannot be clicked. A grey letter that still accepted a
                    // press would scroll nowhere, which reads as a broken button.
                    IsEnabled = entry.HasContent,
                };

                letter.Click += GroupPickerItem_Click;

                // Hover rendering. The style's template is deliberately bare - one
                // ContentPresenter and no visual states - so there is nothing for a
                // pointer to light up. A faint accent wash does it: the glyph already
                // carries the meaning, and tinting the ink would fight the accent that
                // marks where the reader already is.
                // The hover pair: a darkened fill and a hairline outline.
                //
                // Same shape as the card hover frame - radius 8, 1px stroke - so a letter
                // and a card answer the pointer the same way. The fill DARKENS rather than
                // tinting with the accent: in a dense grid of glyphs an accent wash reads as
                // "this one is selected", which is what the full-strength accent is already
                // saying about the letter the reader is standing on.
                var hoverFill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));

                var hoverStroke = new SolidColorBrush(Windows.UI.Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));

                var clearFill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

                var clearStroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

                letter.PointerEntered += (s, _) =>
                {
                    // A dimmed bucket cannot be clicked, so it must not light up either: a
                    // hover wash on a dead letter promises a jump that will not happen.
                    if (((Button)s).IsEnabled)
                    {
                        ((Button)s).Background = hoverFill;

                        ((Button)s).BorderBrush = hoverStroke;
                    }
                };

                letter.PointerExited += (s, _) =>
                {
                    ((Button)s).Background = clearFill;

                    ((Button)s).BorderBrush = clearStroke;
                };

                if (!entry.HasContent)
                {
                    letter.Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
                }
                else if (entry.Letter == current)
                {
                    // Where the reader already is, in the accent: a plain dark weight was
                    // indistinguishable from every other letter that has content.
                    letter.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                }

                Grid.SetRow(letter, index / columns);

                Grid.SetColumn(letter, index % columns);

                LibraryGroupPickerLetters.Children.Add(letter);
            }
        }

        /// <summary>
        /// What the sheet offers: every bucket under a letter sort, only the real headings
        /// under any other.
        ///
        /// <para>A letter sort is a closed set - "is there anything under Q" needs Q to be
        /// visible and dimmed when the answer is no. Years, artists and months have no such
        /// set: padding those would invent headings the list does not have.</para>
        /// </summary>
        private List<LibraryIndexBucket> BuildGroupPickerEntries()
        {
            var keyType = GroupKeyFor(EffectiveMusicSort, _musicGrouping == MusicGrouping.Albums);

            if (keyType == LibraryGroupKey.Letter)
            {
                return LibraryIndexLetter.BuildRail(_groupHeaders.Keys);
            }

            return _groupHeaders.Keys
                .Select(key => new LibraryIndexBucket(LabelForPicker(key), true))
                .ToList();
        }

        /// <summary>
        /// What one button prints: a letter prints its bucket, anything else its own heading
        /// label, because a year or an artist name is what the reader is looking for.
        /// </summary>
        private string LabelForPicker(string key)
        {
            if (_groupHeaders.TryGetValue(key, out var header)
                && header is ContentPresenter { Content: VideoFolderGroup group })
            {
                return group.Name;
            }

            return key;
        }

        /// <summary>
        /// Closes the sheet when the pointer is pressed on the empty area around it.
        ///
        /// <para><b>Press, not tap, and that is on purpose.</b> A tap on empty space used to
        /// close the sheet, which meant a mis-aimed click while reaching for a letter threw
        /// the reader back to the list. The request was 「不是随便点到某个地方就可以退出去，
        /// 必须要点到某个字母、某个按钮才会退出去」: a pressed click outside is a deliberate
        /// 'not this' and still closes, but a stray tap does nothing.</para>
        /// </summary>
        private void LibraryGroupPickerBackdrop_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            CloseGroupPicker();
        }

        /// <summary>
        /// Takes the sheet down with no motion.
        ///
        /// <para>For the case where the list underneath is being replaced rather than the
        /// reader closing the sheet: switching library section rebuilds every heading, so
        /// there is nothing for a shrink to land on. Animating here would play a close over
        /// content that has already changed.</para>
        /// </summary>
        private void HideGroupPicker()
        {
            _sheetAnimation?.Stop();

            LibraryGroupPickerHost.Visibility = Visibility.Collapsed;

            LibraryScroll.Opacity = 1.0;

            ResetSheetTransform(1f, 1f);
        }

        /// <summary>Hides the sheet, if it is up.</summary>
        private void CloseGroupPicker()
        {
            // Shrinks back the way it came, then hides. Hiding first would make the sheet
            // vanish rather than close, and the list would snap to full weight under nothing.
            _sheetAnimation?.Stop();

            var sheet = LibraryGroupPickerSheet;

            var scale = sheet.RenderTransform as ScaleTransform ?? new ScaleTransform();

            sheet.RenderTransform = scale;

            var steps = 10;

            var step = 0;

            var timer = DispatcherQueue.CreateTimer();

            timer.Interval = TimeSpan.FromMilliseconds(12);

            timer.IsRepeating = true;

            timer.Tick += (_, _) =>
            {
                step++;

                var progress = Ease((float)step / steps);

                var shrink = 1f - (0.45f * progress);

                scale.ScaleX = shrink;

                scale.ScaleY = shrink;

                sheet.Opacity = 1f - progress;

                LibraryScroll.Opacity = progress;

                if (step < steps)
                {
                    return;
                }

                timer.Stop();

                LibraryGroupPickerHost.Visibility = Visibility.Collapsed;

                sheet.Opacity = 1f;

                scale.ScaleX = 1f;

                scale.ScaleY = 1f;

                LibraryScroll.Opacity = 1.0;
            };

            _sheetAnimation = timer;

            timer.Start();
        }

        /// <summary>Jumps to the heading that was picked, and closes the sheet.</summary>
        private void GroupPickerItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string key })
            {
                return;
            }

            if (_groupHeaders.TryGetValue(key, out var header))
            {
                header.StartBringIntoView(new BringIntoViewOptions
                {
                    // A little air above the heading, so the row it labels is not pinned to the
                    // very top edge of the scroll port.
                    VerticalAlignmentRatio = 0.1,
                });
            }

            CloseGroupPicker();
        }

        /// <summary>Which heading is nearest the top of the port, as a plain key.</summary>
        private string CurrentGroupKey()
        {
            foreach (var (key, header) in _groupHeaders)
            {
                if (header.TransformToVisual(LibraryScroll).TransformPoint(new Windows.Foundation.Point(0, 0)).Y >= 0)
                {
                    return key;
                }
            }

            return _groupHeaders.Count > 0 ? _groupHeaders.Keys.First() : string.Empty;
        }


        /// <summary>
        /// Fills a grouped music tab: songs under headings, or albums as cards.
        ///
        /// <para><b>What groups by what.</b> The sort decides the headings. A-Z and Z-A
        /// file under index letters; the year sort files under years; the artist sort
        /// files under artist names. That is why the grouping lives in
        /// <see cref="LibraryGrouper"/> rather than here: the three tabs and four key
        /// types would otherwise be twelve chances to disagree about where an untagged
        /// file goes.</para>
        ///
        /// <para><b>Albums become cards, not headings.</b> The Albums tab used to put the
        /// album name in a heading and hang its tracks beneath as track cards. It now puts
        /// one ALBUM card in the grid, captioned with the album and its artist - the same
        /// shape as the artist page's album grid. A card needs an entry to render, so each
        /// album contributes its first track as a carrier and the caption is overwritten;
        /// the album's real track list comes from <see cref="OpenAlbum"/>, which reads the
        /// whole library rather than this carrier.</para>
        /// </summary>
        private void BuildMusicTagGroups(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            var albumMode = _musicGrouping == MusicGrouping.Albums;

            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];

            // The ONE-LINE header, the same one the artists tab uses. The music header
            // carried a 56px sleeve, the album name on its own row, the path under it and
            // a track count at the trailing edge - which was right when the heading WAS the
            // album and the cards under it were its tracks. Now the heading is a letter or a
            // year and the cards are albums or songs, so a sleeve for it is a picture of
            // nothing and the count read 0 on every row.

            var key = GroupKeyFor(EffectiveMusicSort, albumMode);

            // One item per heading for the Albums tab: the album's carrier. Songs tab
            // keeps one item per track, because a song card stands for a song.
            var sources = albumMode ? AlbumCarriers(entries) : entries;

            var groups = LibraryGrouper.Group(
                Enumerable.Range(0, sources.Count).ToList(),
                key,
                i => albumMode ? sources[i].Album : sources[i].Title,
                i => albumMode ? sources[i].Year : sources[i].Year,
                i => sources[i].Artist,
                i => sources[i].AddedAt,
                ch => PinyinInitial.For(ch.ToString()),
                descending: EffectiveMusicSort == VideoSortMode.NameDesc);

            _groupHeaders.Clear();

            _groupItems.Clear();

            foreach (var group in groups)
            {
                // The heading: a TextBlock that is the hit target, and nothing else.
                //
                // LEFT, not Stretch. The Border used to fill the row, so the whole width of
                // the grid was clickable and the pointer grew the letter from wherever it
                // happened to be. Only the words are the target now - "应该是只在文字上交互
                // 而不是一整行都是按钮".
                //
                // No Box: a Border with no background and no border paints nothing, which is
                // the point. What it has is a Tapped handler and a Tag, so the words are
                // clickable without a control template putting a rectangle around them.
                var headerLabel = new TextBlock
                {
                    Text = LabelForGroup(group),
                    FontSize = 22,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["MpvAccentHeadingBrush"],
                    TextTrimming = TextTrimming.CharacterEllipsis,

                    // The growth happens about the text's own centre, and the transform has
                    // to be allowed to draw OUTSIDE the layout slot or the glyphs are clipped
                    // the moment they grow - which read as the heading vanishing on hover.
                    RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5),
                };

                var header = new Border
                {
                    Child = headerLabel,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Padding = new Thickness(0),

                    // The frame gutter, the same 6 the pictures carry. Without it the heading
                    // starts at the cell's outer edge and overhangs the thumbnails to the left.
                    Margin = new Thickness(CardMetrics.HoverFrameInset, 16, 0, 8),
                };

                header.Tag = group.SortKey;

                header.Tapped += GroupHeader_Tapped;

                // The only pointer feedback: the text grows from its left edge. A growth from
                // the left keeps the heading's left edge exactly where the thumbnails' is, so
                // hovering never makes the column look like it moved.
                header.PointerEntered += (_, _) => ScaleHeader(headerLabel, 1.08f);

                header.PointerExited += (_, _) => ScaleHeader(headerLabel, 1f);


                VideoFolderGroups.Children.Add(header);

                _groupHeaders.TryAdd(group.SortKey, header);

                var items = group.Items.Select(i => sources[i]).ToList();

                _groupItems[group.SortKey] = items;

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = items,
                };

                repeater.Layout = AlbumCardLayout();

                repeater.ElementPrepared += LibraryGrid_ElementPrepared;

                VideoFolderGroups.Children.Add(repeater);
            }
        }

        /// <summary>Which heading a sort produces.</summary>
        private static LibraryGroupKey GroupKeyFor(VideoSortMode sort, bool albumMode) => sort switch
        {
            VideoSortMode.Year => LibraryGroupKey.Year,
            VideoSortMode.Artist => LibraryGroupKey.Artist,
            VideoSortMode.Added => LibraryGroupKey.AddedMonth,
            _ => LibraryGroupKey.Letter,
        };

        /// <summary>
        /// One track per album, chosen stably, for the Albums tab's cards.
        ///
        /// <para>Ordered by title so the carrier does not follow scan order: the same
        /// album must pick the same track on every rebuild, or its artwork would change
        /// when the user switched tabs.</para>
        /// </summary>
        private static List<LibraryEntry> AlbumCarriers(List<LibraryEntry> entries)
        {
            return entries
                .GroupBy(
                    entry => string.IsNullOrWhiteSpace(entry.Album) ? string.Empty : entry.Album,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(album => album
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .First())
                .ToList();
        }

        /// <summary>
        /// What a heading prints.
        ///
        /// <para>The grouper returns an empty label for its unknown bucket because it
        /// cannot reach the language table; the page names it here.</para>
        /// </summary>
        private string LabelForGroup(LibraryGrouping group)
        {
            if (group.Label.Length > 0)
            {
                return group.Label;
            }

            return _musicGrouping == MusicGrouping.Albums
                ? AppContext.AppLang.LibraryUnknownAlbum
                : AppContext.AppLang.LibraryUnknownArtist;
        }

        /// <summary>
        /// Fills the artists tab: one circular tile per artist, filed under letters.
        ///
        /// <para><b>Grouped by LETTER, not by artist.</b> A repeater draws its items
        /// across the row, so a one-item repeater is one tile on a line of its own: the
        /// tab became a single column of circles, each under a heading repeating the name
        /// printed on the tile below it.</para>
        ///
        /// <para><b>The tile is a carrier, not a track.</b> Each artist contributes
        /// exactly one entry - their first track by title - so the grid holds people
        /// rather than songs. The artist's real track list is re-derived by
        /// <see cref="OpenArtist"/>.</para>
        /// </summary>
        private void BuildArtistTiles(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];


            var carriers = entries
                .GroupBy(
                    entry => string.IsNullOrWhiteSpace(entry.Artist) ? string.Empty : entry.Artist,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(artist => artist
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .First())
                .ToList();

            // Artists are never sorted - the tab has no sort menu - so the key is always
            // the letter. Expressed through the same grouper so a Chinese artist lands in
            // 拼音X here exactly as it does on the other two tabs.
            var groups = LibraryGrouper.Group(
                Enumerable.Range(0, carriers.Count).ToList(),
                LibraryGroupKey.Letter,
                i => carriers[i].Artist,
                i => carriers[i].Year,
                i => carriers[i].Artist,
                i => carriers[i].AddedAt,
                ch => PinyinInitial.For(ch.ToString()));

            _groupHeaders.Clear();

            _groupItems.Clear();

            foreach (var group in groups)
            {
                // A plain TextBlock in a hit-testing Border, not a Button.
                //
                // A heading is a LABEL, and a Button brings its own box: a Rest fill, a
                // hover fill, a pressed fill and a focus ring, all of which draw a
                // rectangle around a single letter. What the heading should do is grow a
                // little under the pointer, which is a property of the text, not of a
                // control template.
                //
                // The Button was added in an earlier pass to give this a click pattern and
                // an automation name. Both are kept: a Border with a Tapped handler still
                // hit-tests, and AutomationProperties still names it.
                var headerLabel = new TextBlock
                {
                    Text = LabelForGroup(group),
                    FontSize = 22,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["MpvAccentHeadingBrush"],
                    TextTrimming = TextTrimming.CharacterEllipsis,

                    // Grows from its left edge so the column never looks like it moved, and
                    // may draw outside its slot or the glyphs are clipped as they grow.
                    RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5),
                };

                var header = new Border
                {
                    Child = headerLabel,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Padding = new Thickness(0),

                    // The frame gutter, the same 6 the pictures carry. Without it the heading
                    // starts at the cell's outer edge and overhangs the thumbnails to the left.
                    Margin = new Thickness(CardMetrics.HoverFrameInset, 16, 0, 8),
                };

                header.Tag = group.SortKey;

                header.Tapped += GroupHeader_Tapped;

                // The only pointer feedback: the text grows from its left edge. No fill, no
                // ring, no box - the heading is a label that happens to be clickable.
                header.PointerEntered += (_, _) => ScaleHeader(headerLabel, 1.08f);

                header.PointerExited += (_, _) => ScaleHeader(headerLabel, 1f);

            AutomationProperties.SetAutomationId(header, "LibraryGroupHeader");

                VideoFolderGroups.Children.Add(header);

                _groupHeaders.TryAdd(group.SortKey, header);

                var items = group.Items.Select(i => carriers[i]).ToList();

                _groupItems[group.SortKey] = items;

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = items,
                };

                repeater.Layout = AlbumCardLayout();

                repeater.ElementPrepared += LibraryGrid_ElementPrepared;

                VideoFolderGroups.Children.Add(repeater);
            }
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