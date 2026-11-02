using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace mpv_winui.Domains.Playlist
{
    /// <summary>
    /// The named-playlist file: the lines of an <c>.m3u</c>, and the rules a
    /// playlist's name has to satisfy before it can become one.
    ///
    /// <para><b>Why this is separate.</b> The app already writes this format and
    /// reads it. <c>MpvPlayerPage_Playlist.ExportPlaylistAsync</c> builds it with
    /// a StringBuilder behind a file picker, and <c>ImportPlaylistAsync</c> hands
    /// the file to mpv's <c>loadlist</c>. Named playlists turn that one-off export
    /// into one file per list under the app's data root, so the format has to be
    /// reachable without a picker and without a window.</para>
    ///
    /// <para><b>Deliberately pure BCL.</b> No WinUI, no <c>Windows.Storage</c>, no
    /// UI thread, no file access: parsing happens on a string and formatting
    /// produces one. That is what lets <c>mpv-conf-test</c> - a plain net10.0
    /// project that cannot reference Microsoft.UI.Xaml - link this exact source
    /// and assert it, the same trade <c>MpvConfValueCodec</c> and
    /// <c>LanguageCode</c> make.</para>
    ///
    /// <para><b>The format.</b> <c>#EXTM3U</c>, then for each entry an
    /// <c>#EXTINF:-1,title</c> line and the path on a line of its own. This is
    /// what the exporter writes; the two must not disagree, because a file the app
    /// wrote has to load back as the playlist it was.</para>
    /// </summary>
    internal static class NamedPlaylistFile
    {
        /// <summary>The first line of every file this app writes.</summary>
        private const string Header = "#EXTM3U";

        /// <summary>
        /// An <c>#EXTINF</c> record up to its title.
        ///
        /// The duration field is -1, "unknown", which is the truth here: the app
        /// knows a title and a path for an entry that has not been opened, and
        /// invents neither. mpv reads the part after the comma as the title; the
        /// duration only matters to a player that wants to show a total before
        /// loading anything.
        /// </summary>
        private const string ExtInfPrefix = "#EXTINF:-1,";

        /// <summary>
        /// The line separator, spelled out rather than left to
        /// <see cref="Environment.NewLine"/>.
        ///
        /// They are the same two bytes on the only platform this app runs on, but
        /// a literal means the tests assert the bytes that will actually be
        /// written instead of whatever separator the test host happens to report.
        /// </summary>
        private const string NewLine = "\r\n";

        /// <summary>
        /// The characters a file name may not contain on this platform.
        ///
        /// Asked of the BCL once rather than restated: the set is what decides
        /// whether saving a playlist would fail, so it is the only list that
        /// cannot drift from reality.
        /// </summary>
        private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();

        /// <summary>
        /// The characters no path on this platform can contain, asked of the same
        /// BCL that knows the answer.
        ///
        /// Used to tell a path from a line that only looks like one: a line
        /// holding a NUL or any other control character is not a path, however
        /// non-empty it is, and adding it to the playlist would put an entry in
        /// the list that mpv cannot open.
        /// </summary>
        private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();

        /// <summary>
        /// A byte-order mark, which some editors leave at the front of a file and
        /// which <see cref="string.Trim()"/> does not consider whitespace.
        /// </summary>
        private const char ByteOrderMark = '\uFEFF';

        /// <summary>
        /// The paths an m3u's text holds, in file order.
        ///
        /// <para>A line carries a path unless it is blank, begins with <c>#</c>,
        /// or contains something no path can contain. The first rule keeps the
        /// <c>#EXTM3U</c> header and the <c>#EXTINF</c> record for each entry out
        /// of the result; the third keeps a corrupt or mis-decoded file from
        /// turning into entries that cannot be opened. What is left is the path
        /// itself, trimmed of the whitespace a hand-edited file collects.</para>
        /// </summary>
        /// <remarks>
        /// Garbage reads as an empty playlist rather than throwing. A file that
        /// something else wrote, or that was truncated, must not be able to take
        /// the window down on the way to being displayed; mpv's own loader is
        /// equally forgiving, so "no entries" is also what playing it would do.
        /// </remarks>
        public static IReadOnlyList<string> ParsePaths(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<string>();
            }

            // One split for every line-ending convention: LF leaves a trailing CR
            // for Trim to remove, and a bare CR splits too. RemoveEmptyEntries
            // just avoids handing the loop the empty half of a CRLF pair.
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var paths = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                // The BOM is trimmed separately because it survives a decode that
                // did not ask for it - a caller reading bytes as UTF-8 itself gets
                // one in front of "#EXTM3U", which would otherwise read as a path.
                var text = line.Trim().TrimStart(ByteOrderMark).Trim();

                if (text.Length == 0 ||
                    text[0] == '#' ||
                    text.IndexOfAny(InvalidPathCharacters) >= 0)
                {
                    continue;
                }

                paths.Add(text);
            }

            return paths;
        }

        /// <summary>
        /// The text of an m3u holding these items, with no trailing newline.
        ///
        /// <para>Every line break inside a title becomes one space, so a title can
        /// never end its own <c>#EXTINF</c> record early and leave the rest of
        /// itself to be read back as a path - a playlist with an entry nobody
        /// asked for. A CRLF pair becomes a single space, not two: it is one break
        /// to a reader, and the exporter's two <c>Replace</c> calls collapse it to
        /// two, which is the one respect in which this version differs from
        /// it.</para>
        ///
        /// <para>The last path is not terminated by a newline. A writer that saves
        /// this to disk can add one; a joined string cannot carry one without
        /// claiming a third line that does not exist.</para>
        /// </summary>
        public static string Format(IReadOnlyList<(string Title, string Path)> items)
        {
            var builder = new StringBuilder(Header);

            foreach (var item in items)
            {
                var title = item.Title.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

                builder.Append(NewLine)
                    .Append(ExtInfPrefix)
                    .Append(title)
                    .Append(NewLine)
                    .Append(item.Path);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Whether a playlist name can be used as a file name, and why not.
        ///
        /// <para>Three rules, each of which a list name has to survive to become a
        /// file: it must not be empty; it must not begin or end with whitespace,
        /// which Windows silently strips from a file name and which would make the
        /// name the user typed differ from the name on disk; and it must not
        /// contain any character <see cref="Path.GetInvalidFileNameChars"/>
        /// refuses, or saving would throw at the moment the user asked for it.</para>
        ///
        /// <para><paramref name="reason"/> is a short English phrase for a log
        /// line or a diagnostics message - the UI's own wording comes from the
        /// lexicon, not from here.</para>
        /// </summary>
        public static bool IsValidName(string name, out string? reason)
        {
            // Covers null as well as "", so a missing name is refused rather than
            // thrown on.
            if (string.IsNullOrWhiteSpace(name))
            {
                reason = "the name is empty";
                return false;
            }

            // Ordinal: this is about the exact characters that would go into the
            // file name, not about a culture's idea of equivalently-spaced text.
            if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                reason = "the name cannot begin or end with whitespace";
                return false;
            }

            var bad = name.IndexOfAny(InvalidFileNameCharacters);
            if (bad >= 0)
            {
                reason = $"the name cannot contain '{Describe(name[bad])}'";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// A character as it can be shown in a message. Control characters are
        /// named by code point: printed raw, they would rewrite the line of the
        /// log they land in.
        /// </summary>
        private static string Describe(char c) =>
            char.IsControl(c) ? $"U+{(int)c:X4}" : c.ToString();
    }
}
