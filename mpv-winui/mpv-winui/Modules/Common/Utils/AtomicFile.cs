using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Common.Utils
{
    /// <summary>
    /// Writes a file so that an interruption leaves either the previous contents
    /// or the new ones, and never a truncated half of either.
    ///
    /// <para><b>Why this exists.</b> The settings layer treated its own files as
    /// authoritative - mpv.conf is documented as the single source of truth -
    /// and wrote them with <see cref="File.WriteAllText(string, string)"/>, which
    /// truncates the destination and then fills it. A crash, a forced shutdown,
    /// or a full disk in that window left a partial file, and the next launch
    /// read the authored configuration out of something that no longer
    /// parses.</para>
    ///
    /// <para><b>How.</b> Write to a sibling temporary file, flush it to disk, and
    /// rename it over the destination. The rename is the only step that changes
    /// what a reader can see, and a same-directory rename is atomic on NTFS. The
    /// temporary is deliberately a sibling rather than a file in %TEMP%: a
    /// cross-volume rename degrades to copy-then-delete and stops being atomic,
    /// which is the whole point.</para>
    ///
    /// <para><see cref="File.Replace(string, string, string)"/> is used when the
    /// destination exists because it also carries over the destination's
    /// attributes; a plain move is used when there is nothing to replace.</para>
    ///
    /// <para><b>Not a durability guarantee.</b> This protects against a torn
    /// file, not against data loss from a failure that happens after the flush
    /// returns. It also does not attempt the multi-file transaction that
    /// mpv.conf plus its state file would ideally be; the state file is written
    /// second and only ever describes what the config file already says.</para>
    /// </summary>
    internal static class AtomicFile
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false);

        /// <summary>Replaces <paramref name="path"/> with <paramref name="contents"/> atomically.</summary>
        public static void WriteAllText(string path, string contents, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            encoding ??= DefaultEncoding;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Hidden rather than merely temporary: a reader that globs the config
            // directory must not pick up a half-written file that looks like one
            // of ours.
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            SetHidden(temporary);

            try
            {
                using (var stream = new FileStream(
                           temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
                    {
                        writer.Write(contents);
                        writer.Flush();
                    }

                    // Force the bytes out before the rename makes them visible. A
                    // rename that lands before its data would leave the new file
                    // present but empty after a power loss.
                    stream.Flush(flushToDisk: true);
                }

                Promote(temporary, path);
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }
        }

        /// <summary>Asynchronous counterpart of <see cref="WriteAllText"/>.</summary>
        public static async Task WriteAllTextAsync(string path, string contents, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            encoding ??= DefaultEncoding;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            SetHidden(temporary);

            try
            {
                using (var stream = new FileStream(
                           temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           bufferSize: 4096, useAsync: true))
                {
                    using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
                    {
                        await writer.WriteAsync(contents).ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);
                    }

                    stream.Flush(flushToDisk: true);
                }

                Promote(temporary, path);
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="File.WriteAllLinesAsync(string, IEnumerable{string}, System.Threading.CancellationToken)"/>:
        /// one line per entry, <see cref="Environment.NewLine"/> separated, with a
        /// trailing separator, and nothing at all for an empty sequence.
        /// </summary>
        public static Task WriteAllLinesAsync(string path, IEnumerable<string> lines, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(lines);

            return WriteAllTextAsync(path, JoinLines(lines), encoding);
        }

        /// <summary>
        /// Synchronous counterpart of <see cref="WriteAllLinesAsync"/>. Same
        /// joining rule, so callers that already hold their lines on the calling
        /// thread (the input.conf rewrite does) do not need to become async just
        /// to get atomicity.
        /// </summary>
        public static void WriteAllLines(string path, IEnumerable<string> lines, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(lines);

            WriteAllText(path, JoinLines(lines), encoding);
        }

        /// <summary>
        /// One line per entry, <see cref="Environment.NewLine"/> separated, with a
        /// trailing separator, and nothing at all for an empty sequence. Shared by
        /// the synchronous and asynchronous writers so they cannot drift.
        /// </summary>
        private static string JoinLines(IEnumerable<string> lines)
        {
            var joined = string.Join(Environment.NewLine, lines);
            if (joined.Length > 0)
            {
                joined += Environment.NewLine;
            }

            return joined;
        }

        /// <summary>
        /// Makes the new contents visible under the real name. Kept separate so
        /// the two public entry points cannot drift in how they do it.
        /// </summary>
        private static void Promote(string temporary, string path)
        {
            if (File.Exists(path))
            {
                File.Replace(temporary, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static void SetHidden(string path)
        {
            try
            {
                var info = new FileInfo(path);
                info.Attributes = FileAttributes.Hidden;
            }
            catch (IOException)
            {
                // Best effort only: hiding is cosmetic, and a file system that
                // refuses it must not fail the write.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
