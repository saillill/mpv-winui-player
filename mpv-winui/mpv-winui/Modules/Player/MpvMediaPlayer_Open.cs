using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Player
{
    public enum FileType
    {
        File,
        Folder,
        Url,
    }

    public record FileItem(string Path, FileType Type = FileType.File);

    public enum OpenMode
    {
        Replace,
        Append,
        InsertNext,
    }

    public partial class MpvMediaPlayer
    {
        public bool IsSubtitleFile(string path)
        {
            var ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) && _subtitleExtensions.Value.Contains(ext);
        }

        public void Open(FileItem file, OpenMode action = OpenMode.Replace)
        {
            Open((FileItem[])[file], action);
        }

        public void Open(IReadOnlyList<FileItem> files, OpenMode action = OpenMode.Replace)
        {
            if (files?.Count > 0)
            {
                bool allSub = true;
                foreach (var file in files)
                {
                    if (file.Type == FileType.Folder || !IsSubtitleFile(file.Path))
                    {
                        allSub = false;
                        break;
                    }
                }

                if (allSub)
                {
                    foreach (var file in files)
                    {
                        _ = _commandQueue.EnqueueVector(["osd-auto", "sub-add", file.Path]);
                    }
                    return;
                }

                switch (action)
                {
                    case OpenMode.Replace:
                    {
                        for (int i = 0; i < files.Count; i++)
                        {
                            _ = _commandQueue.EnqueueVector(["osd-auto", "loadfile", files[i].Path, i == 0 ? "replace+play" : "append"]);
                        }

                        // Single local file replaces: fill the playlist with
                        // same-directory siblings in the background. The
                        // patched native autocreate-playlist did this scan
                        // synchronously inside loadfile, stalling playback
                        // start for seconds-to-minutes on large directories.
                        if (files.Count == 1 && files[0].Type == FileType.File)
                        {
                            StartAutoPlaylistScan(files[0].Path);
                        }
                        break;
                    }

                    case OpenMode.Append:
                    {
                        for (int i = 0; i < files.Count; i++)
                        {
                            _ = _commandQueue.EnqueueVector(["osd-auto", "loadfile", files[i].Path, "append"]);
                        }
                        break;
                    }

                    case OpenMode.InsertNext:
                    {
                        for (int i = files.Count - 1; i >= 0; i--)
                        {
                            _ = _commandQueue.EnqueueVector(["osd-auto", "loadfile", files[i].Path, i > 0 ? "insert-next" : "insert-next-play"]);
                        }
                        break;
                    }
                }
            }
        }

        public Task OpenAsync(FileItem file, OpenMode action = OpenMode.Replace)
        {
            return Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Open(file, action);
                await _commandQueue.DrainAsync();
                AppContext.AppLogger.Debug($"OpenAsync drained in {sw.ElapsedMilliseconds}ms ({action})");
            });
        }

        public Task OpenAsync(IReadOnlyList<FileItem> files, OpenMode action = OpenMode.Replace)
        {
            return Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Open(files, action);
                await _commandQueue.DrainAsync();
                AppContext.AppLogger.Debug($"OpenAsync drained in {sw.ElapsedMilliseconds}ms ({files.Count} item(s), {action})");
            });
        }

        // App-level replacement for the native autocreate-playlist scan.
        // "same" appends only files sharing the opened file's extension;
        // "filter" uses the enabled video/audio/image extension lists; "no"
        // never scans. Enumeration is name-based (no file probing), so it
        // completes in milliseconds even on huge directories, and every
        // append checks the generation so a newer Replace cancels the stale
        // scan instead of interleaving old siblings into the new playlist.
        private int _autoPlaylistGeneration;
        private volatile bool _autoPlaylistStopped;

        private void StartAutoPlaylistScan(string path)
        {
            var mode = mpv_winui.AppContext.AppSetting.AutoCreatePlaylist;
            if (mode is not ("same" or "filter"))
            {
                return;
            }

            int generation = ++_autoPlaylistGeneration;
            Task.Run(() =>
            {
                try
                {
                    var directory = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    {
                        return;
                    }

                    IEnumerable<string> siblings;
                    if (mode == "same")
                    {
                        var ext = Path.GetExtension(path);
                        if (ext.Length == 0)
                        {
                            return;
                        }
                        siblings = Directory.EnumerateFiles(directory, "*" + ext, SearchOption.TopDirectoryOnly);
                    }
                    else
                    {
                        var allowed = BuildAutoPlaylistExtensions();
                        siblings = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                            .Where(f => allowed.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
                    }

                    foreach (var sibling in NaturalOrderBy(siblings, StringComparer.OrdinalIgnoreCase))
                    {
                        if (generation != _autoPlaylistGeneration || _autoPlaylistStopped)
                        {
                            return;
                        }
                        if (string.Equals(sibling, path, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        _ = _commandQueue.EnqueueVector(["osd-auto", "loadfile", sibling, "append"]);
                    }
                }
                catch (Exception ex)
                {
                    mpv_winui.AppContext.AppLogger.Error(ex, "auto playlist scan failed");
                }
            });
        }

        private string[] BuildAutoPlaylistExtensions()
        {
            string Parse(string? raw) => (raw ?? string.Empty).Replace(';', ',');
            return new[] { Parse(mpv_winui.AppContext.AppSetting.VideoExts),
                           Parse(mpv_winui.AppContext.AppSetting.AudioExts),
                           Parse(mpv_winui.AppContext.AppSetting.ImageExts) }
                .SelectMany(blob => blob.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(e => e.StartsWith('.') ? e : "." + e)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IEnumerable<string> NaturalOrderBy(IEnumerable<string> source, StringComparer comparer)
        {
            var list = source.ToList();
            list.Sort((a, b) =>
            {
                int ia = 0, ib = 0;
                while (ia < a.Length && ib < b.Length)
                {
                    char ca = a[ia], cb = b[ib];
                    if (char.IsDigit(ca) && char.IsDigit(cb))
                    {
                        long na = 0, nb = 0;
                        int sa = ia, sb = ib;
                        while (ia < a.Length && char.IsDigit(a[ia])) na = na * 10 + (a[ia++] - '0');
                        while (ib < b.Length && char.IsDigit(b[ib])) nb = nb * 10 + (b[ib++] - '0');
                        int cmp = na.CompareTo(nb);
                        if (cmp != 0) return cmp;
                    }
                    else
                    {
                        int cmp = comparer.Compare(a[ia].ToString(), b[ib].ToString());
                        if (cmp != 0) return cmp;
                        ia++; ib++;
                    }
                }
                return (a.Length - ia).CompareTo(b.Length - ib);
            });
            return list;
        }
    }
}
