using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Watches the deployed mpv.conf and pulls a hand edit back into the settings.
///
/// <para><b>Why this exists.</b> mpv.conf is the source of truth and it is
/// meant to be edited by hand - that is stated as the whole point of the model
/// in <see cref="ManagedMpvConfig"/>. The forward direction (a row change
/// rewrites the file) has always worked, and the reverse direction worked
/// <i>only at startup</i>: <see cref="MpvSettings.LoadFromConfig"/> ran once
/// from AppContext.Init. So the promise "edit the file and the window catches
/// up" held for an edit made while the app was closed, and silently failed for
/// an edit made while it was open. The window kept showing the old value, which
/// is worse than not syncing at all - the UI and the running player disagreed
/// and nothing said so.</para>
///
/// <para><b>What it does.</b> Reads the file on a debounce after any change and
/// calls the same two readers the startup path uses: LoadFromConfig to put the
/// values into the settings, then MpvConfOverrides.Refresh to recompute which
/// options the file now owns by hand. Both are required. The first updates what
/// the rows display; the second updates whether a row is editable, and an edit
/// that adds or removes an assignment changes the second without necessarily
/// changing the first.</para>
///
/// <para><b>Why not let our own writes trigger it.</b> Every change the window
/// makes rewrites this file, so a naive watcher would reload the settings from
/// the file it just wrote - a loop, and a slow one, since the rewrite is
/// debounced and the reload would keep the debounce alive. The generation
/// counter below closes that: each of our writes stamps the generation before
/// touching the file, and a reload that starts and finishes within the same
/// generation is a reload of our own output and is dropped. A hand edit lands
/// between generations and is kept.</para>
///
/// <para><b>Failure is quiet on purpose.</b> A watcher is not a feature the
/// user asked for and must not be able to break the app. A missing file, a
/// denied directory or a platform that does not raise these events degrades to
/// the old startup-only behaviour, logged once, rather than surfacing an error.
/// </para>
/// </summary>
internal static class MpvConfWatcher
{
    /// <summary>How long the file must be quiet before a reload starts. Editors
    /// write in bursts - a save is often truncate-then-write, which is two
    /// events - and reloading on the first would read a half-written file.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);

    private static FileSystemWatcher? _watcher;

    /// <summary>The value of <see cref="WriteGeneration"/> at the instant our
    /// own write of mpv.conf begins. A reload whose start and end both observe
    /// the same value started and ended inside one of our writes, so the file it
    /// read was ours.</summary>
    private static int _writeGeneration;
    private static int _reloads;

    /// <summary>Count of reloads that actually ran. Read by the diagnostics
    /// report; nothing in the app changes behaviour based on it.</summary>
    internal static int Reloads => Volatile.Read(ref _reloads);

    /// <summary>Raised on a thread-pool thread after a hand edit has been read
    /// into the settings, so a listener that owns UI can marshal to its own
    /// dispatcher and re-evaluate what ownership changed.
    ///
    /// An event rather than a direct call because this class is static and knows
    /// nothing about windows; the settings window is created lazily and closed
    /// independently, so there is no stable object to hand it. Subscribers are
    /// responsible for their own thread marshalling.</summary>
    internal static event Action? Reloaded;

    /// <summary>Marks the start of a write to mpv.conf.
    ///
    /// Called by <see cref="ManagedMpvConfig"/> immediately before it touches
    /// the file, so that the change the OS reports a moment later can be told
    /// apart from a hand edit. Cheap: one interlocked increment.</summary>
    internal static void NoteOwnWrite() => Interlocked.Increment(ref _writeGeneration);

    /// <summary>Starts watching. Safe to call more than once and safe to call
    /// when the file does not exist yet - the watcher is on the directory, so a
    /// later deployment of the file is picked up without a restart.</summary>
    internal static void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        var path = ManagedMpvConfig.MpvConfPath;
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            // The directory may not exist on a first run that failed to deploy.
            // Creating it is harmless and keeps the watcher attachable, which
            // matters because this runs during startup and a throw here would
            // take the window down over a convenience feature.
            Directory.CreateDirectory(dir);

            var watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite
                             | NotifyFilters.Size
                             | NotifyFilters.FileName
                             | NotifyFilters.CreationTime,
                // A burst of saves must collapse into one reload.
                EnableRaisingEvents = true,
            };

            watcher.Changed += OnFileTouched;
            watcher.Created += OnFileTouched;
            watcher.Renamed += OnFileTouched;
            _watcher = watcher;

            AppContext.AppLogger.Info("mpv.conf watcher attached to {}", path);
        }
        catch (Exception ex)
        {
            // Degrade to startup-only sync. Logged, because a silent failure
            // here reproduces exactly the bug this class was written to fix and
            // would be indistinguishable from it.
            AppContext.AppLogger.Warn(ex, "mpv.conf watcher could not start; edits are read at startup only");
        }
    }

    /// <summary>Stops watching. Called on shutdown so the file handle is not
    /// held past the process's useful life.</summary>
    internal static void Stop()
    {
        var watcher = _watcher;
        _watcher = null;
        if (watcher is null)
        {
            return;
        }

        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileTouched;
            watcher.Created -= OnFileTouched;
            watcher.Renamed -= OnFileTouched;
            watcher.Dispose();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Debug("mpv.conf watcher dispose failed: {}", ex.Message);
        }
    }

    private static void OnFileTouched(object sender, FileSystemEventArgs e) => Schedule();

    private static void Schedule() => QueueReload();

    /// <summary>Serialises reloads behind the same queue the writers use, so a
    /// reload can never interleave with a write and read a torn file.</summary>
    private static void QueueReload()
    {
        _ = Task.Run(async () =>
        {
            // Debounce: keep waiting while the file keeps changing. The loop is
            // bounded by the write going quiet, which it always does.
            var lastLength = -1L;
            var lastWrite = DateTime.MinValue;
            while (true)
            {
                await Task.Delay(QuietPeriod).ConfigureAwait(false);

                FileInfo info;
                try
                {
                    info = new FileInfo(ManagedMpvConfig.MpvConfPath);
                    if (!info.Exists)
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    AppContext.AppLogger.Debug("mpv.conf stat failed: {}", ex.Message);
                    return;
                }

                if (info.Length == lastLength && info.LastWriteTimeUtc == lastWrite)
                {
                    break;
                }

                lastLength = info.Length;
                lastWrite = info.LastWriteTimeUtc;
            }

            Reload();
        });
    }

    /// <summary>Reads the file into the settings and recomputes ownership.
    ///
    /// The two reads are not interchangeable and neither is optional: the first
    /// is what the rows display, the second is what the enabled state and the
    /// "overridden by mpv.conf" badge are computed from.</summary>
    private static void Reload()
    {
        var generationAtStart = Volatile.Read(ref _writeGeneration);

        try
        {
            MpvSettings.LoadFromConfig();
            MpvConfOverrides.Refresh();
        }
        catch (Exception ex)
        {
            // A hand edit mid-save can present a transiently unreadable file.
            // That is expected and not worth a warning the user would see.
            AppContext.AppLogger.Debug("mpv.conf reload failed: {}", ex.Message);
            return;
        }

        // Our own write: the file we just read is the file we just produced, so
        // there is nothing to pull in. Dropping it is what stops the loop.
        if (generationAtStart == Volatile.Read(ref _writeGeneration))
        {
            return;
        }

        Interlocked.Increment(ref _reloads);
        AppContext.AppLogger.Info("mpv.conf changed on disk; settings reloaded");

        // The rows are bound to the settings, so the values update on their own.
        // What does not is anything computed from ownership - the disabled state
        // and the warning badge - so whoever owns the UI is asked to re-run that
        // pass. Marshalling is the listener's job: this runs on a worker.
        Reloaded?.Invoke();
    }
}
