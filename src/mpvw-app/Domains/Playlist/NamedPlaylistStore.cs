using NLog;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Platform.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mpv_winui.Domains.Playlist
{
    /// <summary>
    /// The named playlists: one <c>.m3u</c> per list, under the app's own local
    /// data root.
    ///
    /// <para><b>Why a file per list rather than one manifest.</b> The app already
    /// writes and reads this format (<see cref="NamedPlaylistFile"/>), mpv loads a
    /// list with a single <c>loadlist</c>, and a person can open one in an editor
    /// or hand it to someone else. A manifest would be easier to rename in place
    /// and would lose every list at once the first time one write went wrong;
    /// here a damaged file costs one list.</para>
    ///
    /// <para><b>The folder is derived, never built.</b>
    /// <see cref="AppData.ResolveLocalData"/> is the single place the local data
    /// root is resolved: the unpackaged root is
    /// <c>%LOCALAPPDATA%\mpv-winui\mpv-winui</c> - two levels, not one - and the
    /// packaged app uses a different tree entirely. <c>AppData</c>'s own comments
    /// record six call sites that built this path by hand one directory short, so
    /// every value a setting persisted became invisible to the player. This class
    /// is not the seventh: <see cref="ResolveDirectory"/> asks <c>AppData</c>, and
    /// nothing here concatenates a root.</para>
    ///
    /// <para><b>Writes are atomic.</b> Every file goes through
    /// <see cref="AtomicFile"/>, the writer <c>mpv.conf</c> uses, for the same
    /// reason: a reader - this store, mpv, or a person watching the folder - sees
    /// either the whole old list or the whole new one, never a truncated half of
    /// either.</para>
    ///
    /// <para><b>Nothing here throws.</b> A playlist operation can fail for reasons
    /// the user neither caused nor can fix: a file open in another program, a
    /// folder that went read-only, a name a sync client holds a handle on. The UI
    /// that calls this has no way to recover from an exception, and losing a list
    /// is not worth taking the window down. Each failure is logged and becomes a
    /// no-op or an absent entry.</para>
    ///
    /// <para><b>Testable without a window.</b> <see cref="NamedPlaylistStore(string)"/>
    /// takes the folder, so <c>tests/conf</c> can drive this against a temporary
    /// directory. That is deliberate: the properties worth asserting here are
    /// properties of what lands on disk, and the defect this class exists to
    /// prevent - a data path one directory short - is invisible to anything that
    /// does not write a file.</para>
    /// </summary>
    internal sealed class NamedPlaylistStore
    {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(NamedPlaylistStore));

        /// <summary>The folder inside the local data root. A name, not a path.</summary>
        private const string FolderName = "playlists";

        /// <summary>The extension that makes a file a list.</summary>
        private const string Extension = ".m3u";

        /// <summary>
        /// The folder the lists live in, or <see cref="string.Empty"/> when it
        /// could not be resolved at all.
        ///
        /// Empty rather than <c>null</c> so every method has one cheap test instead
        /// of a null check it could forget, and because a store that can do
        /// nothing is the right degradation for a window that still has to open.
        /// </summary>
        private readonly string _directory;

        /// <summary>
        /// The store over the app's own playlists folder - the constructor the app
        /// uses.
        ///
        /// <para>Creates the folder when it is missing. <c>List()</c> must not be
        /// the first thing to discover there is nothing there: on a fresh install
        /// every section asks what it holds.</para>
        /// </summary>
        public NamedPlaylistStore()
            : this(ResolveDirectory())
        {
        }

        /// <summary>
        /// The store over a given folder.
        ///
        /// <para><b>Internal, and a test seam.</b> <c>tests/conf</c> compiles this
        /// file's source directly, so it can reach an internal constructor without
        /// the app's own surface growing a parameter it would only ever pass one
        /// value to. The folder names a real directory: the behaviour below is
        /// about real files, and the only way to assert it is to let it happen
        /// somewhere disposable.</para>
        /// </summary>
        internal NamedPlaylistStore(string directory)
        {
            _directory = directory ?? string.Empty;
            EnsureDirectory();
        }

        /// <summary>
        /// The default folder, asked of the one derivation that exists.
        ///
        /// <para>Guarded not because resolving is expected to fail, but because a
        /// store is constructed while a page loads: an unwritable
        /// <c>%LOCALAPPDATA%</c> should cost the playlists section, not the
        /// window.</para>
        /// </summary>
        private static string ResolveDirectory()
        {
            try
            {
                return AppData.Current.ResolveLocalData(FolderName);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: the local data root could not be resolved, the store is inert");

                return string.Empty;
            }
        }

        /// <summary>
        /// Every list in the folder, by name.
        ///
        /// <para><b>One unreadable file costs one list.</b> A folder holding nine
        /// good playlists and one that is locked, truncated or undecodable shows
        /// nine: the unreadable one is logged and skipped, because a list is a
        /// convenience and the other nine are not expendable to protect it.</para>
        ///
        /// <para>Sorted by name with the culture's comparison, ignoring case - the
        /// rule the library's own folders already use - so that "autumn" and
        /// "Autumn" sort beside each other and a Chinese name sorts where a reader
        /// of Chinese expects it, rather than by code point.</para>
        /// </summary>
        public IReadOnlyList<NamedPlaylist> List()
        {
            var lists = new List<NamedPlaylist>();

            if (_directory.Length == 0)
            {
                return lists;
            }

            string[] files;
            try
            {
                // Asked every time rather than cached: the folder can go away
                // between two calls (a cleanup, a failed sync), and a missing
                // folder is an empty section, not a reason to throw.
                if (!Directory.Exists(_directory))
                {
                    return lists;
                }

                // Filtered here rather than by passing "*.m3u" to GetFiles. That
                // pattern is a Win32 one, and Win32's extension matching can also
                // match a longer extension through an 8.3 short name - a file
                // called "x.m3u8" is the shape of the mistake, and it would appear
                // as a list with a name that has no file.
                files = Directory.GetFiles(_directory)
                    .Where(file => string.Equals(Path.GetExtension(file), Extension, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: could not list the folder {}", _directory);

                return lists;
            }

            foreach (var file in files)
            {
                if (!TryReadEntries(file, out var paths))
                {
                    continue;
                }

                lists.Add(new NamedPlaylist(Path.GetFileNameWithoutExtension(file), paths));
            }

            return lists
                .OrderBy(list => list.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Starts a list, or adds to one already carrying that name.
        ///
        /// <para><b>A repeated name merges rather than replaces.</b> The naming
        /// dialog treats an existing name as "these files go into that list", so
        /// replacing here would make the dialog a lie: it would report the list as
        /// extended while the folder held only the files just selected. See
        /// <see cref="Append"/>, which is the same operation asked for from the
        /// other menu item.</para>
        ///
        /// <para>An empty sequence still creates the file, header and all: "new
        /// playlist" with nothing ticked is a real thing to ask for, and a list
        /// with no entries is one mpv's <c>loadlist</c> accepts.</para>
        /// </summary>
        public void Create(string name, IEnumerable<string> paths)
        {
            Merge(name, paths, "created");
        }

        /// <summary>
        /// Adds entries to a list, skipping any it already holds.
        ///
        /// <para>Deduplicated because "add to" is pressed repeatedly in use: the
        /// same file added twice is not two entries to play, and a list that grows
        /// a duplicate every time the button is pressed is the failure a person
        /// notices immediately. Compared ignoring case, because two spellings of
        /// one Windows path are one file.</para>
        ///
        /// <para>A name with no list yet is created: the caller clicked an item
        /// that led here, and dropping the request would be a silent no-op.</para>
        /// </summary>
        public void Append(string name, IEnumerable<string> paths)
        {
            Merge(name, paths, "appended to");
        }

        /// <summary>
        /// Renames a list, refusing to replace one that already exists.
        ///
        /// <para>Two files cannot both keep one name, so a rename onto an occupied
        /// name has to destroy something - and which of the two the user meant to
        /// keep is recorded nowhere. Refused and logged instead, so the UI can
        /// offer a different name.</para>
        ///
        /// <para>A rename that only changes case is allowed. On this file system
        /// the target "exists" either way, but it is the same file, and
        /// "autumn" to "Autumn" is the correction a person types most often by
        /// accident and then cannot make if it is refused.</para>
        /// </summary>
        public void Rename(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                return;
            }

            var source = PathOf(from, out var sourceReason);
            if (source is null)
            {
                _logger.Warn("playlists: cannot rename from {}, {}", Describe(from), sourceReason);

                return;
            }

            var target = PathOf(to, out var targetReason);
            if (target is null)
            {
                _logger.Warn(
                    "playlists: cannot rename {} to {}, {}", Describe(from), Describe(to), targetReason);

                return;
            }

            try
            {
                if (!File.Exists(source))
                {
                    _logger.Info("playlists: nothing to rename, {} does not exist", Describe(from));

                    return;
                }

                // Ordinal-ignore-case because the question is which file the path
                // denotes, not which characters were typed.
                var sameFile = string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
                if (!sameFile && File.Exists(target))
                {
                    _logger.Warn(
                        "playlists: refused to rename {} to {}, a list of that name exists",
                        Describe(from), Describe(to));

                    return;
                }

                File.Move(source, target);

                _logger.Info("playlists: renamed {} to {}", Describe(from), Describe(to));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: could not rename {} to {}", Describe(from), Describe(to));
            }
        }

        /// <summary>Deletes a list. A name with no file behind it is not an error.</summary>
        public void Delete(string name)
        {
            var file = PathOf(name, out var reason);
            if (file is null)
            {
                _logger.Warn("playlists: cannot delete {}, {}", Describe(name), reason);

                return;
            }

            try
            {
                if (!File.Exists(file))
                {
                    return;
                }

                File.Delete(file);

                _logger.Info("playlists: deleted {}", Describe(name));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: could not delete {}", Describe(name));
            }
        }

        /// <summary>
        /// The entries one list holds; empty when it has none or cannot be read.
        ///
        /// <para>Internal for the tests and for a caller that wants one list's
        /// contents without enumerating the folder.</para>
        /// </summary>
        internal IReadOnlyList<string> Read(string name)
        {
            var file = FilePath(name);

            if (file is null || !TryReadEntries(file, out var paths))
            {
                return Array.Empty<string>();
            }

            return paths;
        }

        /// <summary>
        /// The file a list of that name occupies, or <c>null</c> when the name
        /// cannot be a file name.
        ///
        /// <para>Internal, and the only way anything else in the app should learn a
        /// list's file: playing a list hands this path to mpv's <c>loadlist</c>, and
        /// a caller that derived the same path a second way is how the playlists
        /// folder and the folder everything else uses come apart.</para>
        /// </summary>
        internal string? FilePath(string name)
        {
            return PathOf(name, out _);
        }

        /// <summary>
        /// Reads a list and merges entries into it, then writes the result whole.
        ///
        /// <para><b>A list that cannot be read is never written.</b> Merging into a
        /// file that failed to load would start from nothing, and the write would
        /// replace a list that still exists with only the new entries - the one
        /// failure in this class that loses data instead of skipping it. The write
        /// is abandoned instead.</para>
        /// </summary>
        private void Merge(string name, IEnumerable<string> paths, string verb)
        {
            var file = PathOf(name, out var reason);
            if (file is null)
            {
                _logger.Warn("playlists: cannot write {}, {}", Describe(name), reason);

                return;
            }

            if (paths is null)
            {
                _logger.Warn("playlists: cannot write {}, no entries were given", Describe(name));

                return;
            }

            if (!TryReadEntries(file, out var existing))
            {
                return;
            }

            var merged = new List<string>(existing);
            var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in paths)
            {
                // Asked of the format rather than restated here: a blank entry, a
                // path holding a newline, or one with a character no Windows path
                // can contain would be written and then dropped or split on the
                // way back in, so Count would depend on whether the file had been
                // read again since. The one rule that decides is ParsePaths's own.
                if (!SurvivesARoundTrip(entry) || !seen.Add(entry))
                {
                    continue;
                }

                merged.Add(entry);
            }

            // Nothing new and the file is already there: leave it alone. Rewriting
            // would bump its timestamp and show a sync client a change that did
            // not happen.
            if (merged.Count == existing.Count && File.Exists(file))
            {
                return;
            }

            Write(file, name, merged, verb);
        }

        /// <summary>
        /// Whether an entry written to the file would come back as the same single
        /// entry.
        ///
        /// <para>Asked of <see cref="NamedPlaylistFile.ParsePaths"/> itself, by
        /// formatting one entry and parsing it, rather than by restating its rules
        /// here. A path holding a line break would become two entries; one holding
        /// a character no path can contain would vanish; either way the list's
        /// count would differ between the write and the next read, and the two
        /// rules could only drift.</para>
        ///
        /// <para>Cheap: one entry, once, per call - and only for entries that were
        /// not already in the list.</para>
        /// </summary>
        private static bool SurvivesARoundTrip(string? entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return false;
            }

            try
            {
                var roundTripped = NamedPlaylistFile.ParsePaths(
                    NamedPlaylistFile.Format([(string.Empty, entry)]));

                return roundTripped.Count == 1 &&
                       string.Equals(roundTripped[0], entry, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: an entry could not be checked, dropping it");

                return false;
            }
        }

        /// <summary>
        /// Writes one list whole, through the atomic writer.
        ///
        /// <para>The <c>#EXTINF</c> title is the entry file's own name, which is
        /// what every player shows for an m3u and what <c>ExportPlaylistAsync</c>
        /// writes. A list whose entries were all titled "entry" would be a list
        /// nobody could read outside this app.</para>
        ///
        /// <para><b>No encoding argument on purpose.</b> <see cref="AtomicFile"/>'s
        /// default is UTF-8 without a preamble, which is what the exporter writes.
        /// Passing <c>Encoding.UTF8</c> - the BCL name for "UTF-8 with a BOM" -
        /// would put a byte-order mark in front of <c>#EXTM3U</c> in a file other
        /// players also open. <c>ParsePaths</c> survives a BOM, which is exactly
        /// why nothing here would notice the mistake.</para>
        /// </summary>
        private void Write(string file, string name, IReadOnlyList<string> paths, string verb)
        {
            try
            {
                var items = new List<(string Title, string Path)>(paths.Count);
                foreach (var entry in paths)
                {
                    items.Add((Path.GetFileNameWithoutExtension(entry), entry));
                }

                AtomicFile.WriteAllText(file, NamedPlaylistFile.Format(items));

                _logger.Info("playlists: {} {} ({} entries)", verb, Describe(name), items.Count);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: could not write {}", Describe(name));
            }
        }

        /// <summary>
        /// The file's entries, or <c>false</c> when the file could not be read.
        ///
        /// A missing file is success with no entries. "Not there yet" and "there
        /// and unreadable" have to be told apart, because only the second one means
        /// a following write would destroy something.
        /// </summary>
        private bool TryReadEntries(string file, out IReadOnlyList<string> paths)
        {
            paths = Array.Empty<string>();

            try
            {
                if (!File.Exists(file))
                {
                    return true;
                }

                paths = NamedPlaylistFile.ParsePaths(File.ReadAllText(file));

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "playlists: could not read {}", file);

                return false;
            }
        }

        /// <summary>
        /// The path a name denotes, or <c>null</c> with a reason when it cannot
        /// denote one.
        ///
        /// <para>Validation comes before the combination rather than after: a name
        /// holding a slash, a colon or a NUL would otherwise make
        /// <see cref="Path.Combine(string, string)"/> throw, and this class does
        /// not throw.</para>
        /// </summary>
        private string? PathOf(string? name, out string? reason)
        {
            if (_directory.Length == 0)
            {
                reason = "the playlists folder could not be resolved";

                return null;
            }

            // IsValidName refuses null as well as "", so a caller that passed one
            // gets a logged no-op rather than an exception from Path.Combine.
            if (!NamedPlaylistFile.IsValidName(name!, out reason))
            {
                return null;
            }

            return Path.Combine(_directory, name + Extension);
        }

        /// <summary>Creates the folder, logging rather than throwing when it cannot.</summary>
        private void EnsureDirectory()
        {
            if (_directory.Length == 0)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_directory);
            }
            catch (Exception ex)
            {
                // Not fatal yet: every write creates it again and every read
                // reports nothing. Warned because this is then the reason a list
                // does not appear, and it should already be in the log when the
                // user asks.
                _logger.Warn(ex, "playlists: could not create the folder {}", _directory);
            }
        }

        /// <summary>
        /// A name as a log line can show it: <c>null</c> spelled out, because an
        /// interpolated null reads as an empty field and hides the bug.
        /// </summary>
        private static string Describe(string? name) => name is null ? "(null)" : $"'{name}'";
    }
}
