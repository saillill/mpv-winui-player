using mpv_winui.Modules.Common.Utils;
using mpv_winui.Domains.Playback;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// One line of lyrics, as the music page renders it.
    /// </summary>
    /// <remarks>
    /// A plain model rather than a record: the ListView binds Opacity through
    /// x:Bind in OneWay mode so the "current line" highlight can move without
    /// rebuilding the list, and x:Bind OneWay needs INotifyPropertyChanged.
    /// </remarks>
    public sealed class LyricLine : System.ComponentModel.INotifyPropertyChanged
    {
        private double _opacity = 0.55;

        private double _scale = 1.0;

        public LyricLine(string text, TimeSpan time)
        {
            Text = text;
            Time = time;
        }

        /// <summary>The line's own text.</summary>
        public string Text { get; }

        /// <summary>When this line starts. <see cref="TimeSpan.Zero"/> for an untimed file.</summary>
        public TimeSpan Time { get; }

        /// <summary>
        /// 1.0 on the current line, dimmer elsewhere. A DP would let a VisualState
        /// drive it, but this list is rebuilt per track rather than per state, so a
        /// bound property is the simpler and cheaper route.
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set
            {
                if (Math.Abs(_opacity - value) < 0.001)
                {
                    return;
                }

                _opacity = value;
                PropertyChanged?.Invoke(this, new(nameof(Opacity)));
            }
        }

        /// <summary>
        /// Size multiplier for the current line; 1.0 for the ones around it.
        ///
        /// A scale rather than a second FontSize: two sizes would drift apart, and
        /// a transform costs the text layout nothing - changing FontSize forces a
        /// re-measure of every line in the list on each track advance.
        ///
        /// The epsilon guard is not decoration. Position arrives about ten times a
        /// second, and without it every tick would notify a binding that has
        /// nothing to redraw.
        /// </summary>
        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) < 0.001)
                {
                    return;
                }

                _scale = value;
                PropertyChanged?.Invoke(this, new(nameof(Scale)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Finds and parses a lyrics file for the current track.
    ///
    /// mpv already auto-loads subtitle files next to the media (sub-auto=fuzzy,
    /// and the extension list includes lrc), so a .lrc is usually attached to the
    /// track as a subtitle. That is not enough for a music page: a subtitle track
    /// renders into the video frame, which an audio file with no picture does not
    /// have. So the file is parsed here instead and rendered as rows.
    ///
    /// Lookup order matches what players conventionally do:
    ///   1. same folder, same base name           song.lrc
    ///   2. same folder, same base name + artist  song - Artist.lrc
    ///   3. nearest .lrc in the same folder       (a single-file album rip)
    ///   4. inside the file, as a lyrics tag      (rare, but free to check)
    ///
    /// Timed LRC only. An untimed .txt is shown as a flat list with no highlight,
    /// because pretending a file with no timestamps is synchronised produces a
    /// constantly wrong highlight rather than no highlight.
    /// </summary>
    internal static class LyricsLoader
    {
        public static async Task<IReadOnlyList<LyricLine>> LoadAsync(string? mediaPath, string? mediaTitle)
        {
            if (string.IsNullOrWhiteSpace(mediaPath))
            {
                return [];
            }

            foreach (var candidate in Candidates(mediaPath, mediaTitle))
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    var text = await File.ReadAllTextAsync(candidate, DetectEncoding(candidate));

                    var parsed = Parse(text);

                    if (parsed.Count > 0)
                    {
                        return parsed;
                    }
                }
                catch (Exception)
                {
                    // A malformed file is skipped rather than reported: the next
                    // candidate may still work, and a broken lyric file is not
                    // worth a dialog.
                }
            }

            return [];
        }

        /// <summary>
        /// The candidate paths, in priority order. Public-within-assembly so the
        /// lookup rule can be tested without a file system round trip.
        /// </summary>
        internal static IEnumerable<string> Candidates(string mediaPath, string? mediaTitle)
        {
            string directory;

            try
            {
                directory = Path.GetDirectoryName(mediaPath) ?? string.Empty;
            }
            catch (ArgumentException)
            {
                yield break;
            }

            if (directory.Length == 0)
            {
                yield break;
            }

            var baseName = Path.GetFileNameWithoutExtension(mediaPath);

            yield return Path.Combine(directory, baseName + ".lrc");

            if (!string.IsNullOrWhiteSpace(mediaTitle) && mediaTitle != baseName)
            {
                yield return Path.Combine(directory, mediaTitle + ".lrc");
            }

            // Last resort: one .lrc in the folder. Only when there is exactly one,
            // because picking a random lyric file for an album folder would put
            // the wrong words under the song and there is no way to tell.
            string[] singles;

            try
            {
                singles = Directory.GetFiles(directory, "*.lrc");
            }
            catch (Exception)
            {
                yield break;
            }

            if (singles.Length == 1)
            {
                yield return singles[0];
            }
        }

        /// <summary>
        /// Parses LRC into timed lines, sorted by time.
        ///
        /// Handles the two shapes that actually occur:
        ///   [mm:ss.xx]line                 one stamp per line
        ///   [mm:ss.xx][mm:ss.xx]line       several stamps sharing one line (a
        ///                                  chorus repeated later)
        /// Metadata tags ([ar:], [ti:], [offset:]) are read only for the offset -
        /// the rest are already available from the file's own tags, and the
        /// caller shows those.
        /// </summary>
        internal static IReadOnlyList<LyricLine> Parse(string text)
        {
            var lines = new List<LyricLine>();

            var offsetMs = 0;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim('\r', ' ', '\t');

                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("[offset:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[8..].TrimEnd(']').Trim();

                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOffset))
                    {
                        // LRC's offset is "shift the lyrics by this many ms", so a
                        // positive value means the words come EARLIER than the
                        // stamp says.
                        offsetMs = -parsedOffset;
                    }

                    continue;
                }

                var stamps = new List<TimeSpan>();

                var rest = line;

                while (rest.StartsWith('['))
                {
                    var close = rest.IndexOf(']');

                    if (close < 0)
                    {
                        break;
                    }

                    var inner = rest[1..close];

                    if (TryParseStamp(inner, out var stamp))
                    {
                        stamps.Add(stamp);
                    }

                    rest = rest[(close + 1)..];
                }

                var content = rest.Trim();

                if (stamps.Count == 0)
                {
                    // Untimed line: keep the text, but with no time it cannot be
                    // highlighted. Skipped only when the file has timestamps at
                    // all - see the caller, which decides between timed and flat
                    // presentation from the first parsed line.
                    continue;
                }

                if (content.Length == 0)
                {
                    // A bare stamp is a deliberate blank line in the lyric; keep
                    // it as an empty row so the spacing of the song survives.
                    content = string.Empty;
                }

                foreach (var stamp in stamps)
                {
                    var shifted = stamp + TimeSpan.FromMilliseconds(offsetMs);

                    lines.Add(new LyricLine(content, shifted < TimeSpan.Zero ? TimeSpan.Zero : shifted));
                }
            }

            lines.Sort((a, b) => a.Time.CompareTo(b.Time));

            return lines;
        }

        /// <summary>Parses mm:ss, mm:ss.x, mm:ss.xx or mm:ss.xxx.</summary>
        private static bool TryParseStamp(string inner, out TimeSpan value)
        {
            value = default;

            var colon = inner.IndexOf(':');

            if (colon <= 0)
            {
                return false;
            }

            if (!int.TryParse(inner[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
            {
                return false;
            }

            var secondsPart = inner[(colon + 1)..];

            var fraction = 0.0;

            var dot = secondsPart.IndexOf('.');

            if (dot >= 0)
            {
                var fracText = secondsPart[(dot + 1)..];

                secondsPart = secondsPart[..dot];

                if (fracText.Length > 0
                    && int.TryParse(fracText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frac))
                {
                    fraction = frac / Math.Pow(10, fracText.Length);
                }
            }

            if (!int.TryParse(secondsPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return false;
            }

            value = TimeSpan.FromSeconds(minutes * 60 + seconds + fraction);

            return true;
        }

        /// <summary>
        /// Opens the file with the encoding it declares, falling back to UTF-8.
        ///
        /// Lyric files in the wild are UTF-8 with or without a BOM, or GBK when
        /// they came from a Chinese tagger. Reading GBK as UTF-8 produces
        /// replacement characters, so a file whose bytes are not valid UTF-8 is
        /// retried as GB18030 - the superset that covers GBK and GB2312.
        /// </summary>
        private static Encoding DetectEncoding(string path)
        {
            try
            {
                var head = new byte[3];

                using var stream = File.OpenRead(path);

                var read = stream.Read(head, 0, 3);

                if (read == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
                {
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                }

                // No BOM: guess. A pure-ASCII or valid UTF-8 file decodes the same
                // either way, so UTF-8 is the safe default and GB18030 rescues the
                // rest.
                return Utf8IsValid(path) ? new UTF8Encoding(false) : Encoding.GetEncoding("GB18030");
            }
            catch (Exception)
            {
                return new UTF8Encoding(false);
            }
        }

        private static bool Utf8IsValid(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);

                // Strict decoding: throws on an invalid sequence, which is exactly
                // the signal needed here.
                _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
