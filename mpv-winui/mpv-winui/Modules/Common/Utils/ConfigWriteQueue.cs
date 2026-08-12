using NLog;
using System;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Common.Utils
{
    /// <summary>
    /// Serializes config-file writes (mpv.conf managed block, script-opts
    /// files) so concurrent setting changes cannot interleave mid-write.
    /// Writes run one at a time on the thread pool and never throw out of
    /// the queue (errors are logged), so callers can fire-and-forget.
    /// </summary>
    public static class ConfigWriteQueue
    {
        private static readonly Logger Logger = LogManager.GetLogger("App");

        private static readonly object Gate = new();
        private static Task _tail = Task.CompletedTask;
        private static bool _coalescingWritePending;
        private static int _coalescingVersion;

        /// <summary>Chains a config write onto the shared serialized queue.</summary>
        public static Task Enqueue(Func<Task> write)
        {
            lock (Gate)
            {
                _tail = _tail.ContinueWith(
                    async _ =>
                    {
                        try
                        {
                            await write();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "config write failed");
                        }
                    },
                    TaskScheduler.Default).Unwrap();
                return _tail;
            }
        }

        /// <summary>
        /// Chains a write that only needs to run once for the latest state:
        /// if a coalescing write is already queued (but not yet started), new
        /// enqueues collapse into it. The write callback must read the latest
        /// state when it runs (PluginConfigWriter does exactly that).
        /// </summary>
        public static Task EnqueueCoalescing(Func<Task> write)
        {
            lock (Gate)
            {
                _coalescingVersion++;
                if (_coalescingWritePending)
                {
                    // The already-queued write will observe the latest settings
                    // when it executes, so nothing more needs to be chained.
                    return _tail;
                }

                _coalescingWritePending = true;
                _tail = _tail.ContinueWith(
                    async _ =>
                    {
                        try
                        {
                            // Write until quiescent: an edit that lands while a
                            // write is in flight bumps the version and forces
                            // one more write, so the last state is never lost.
                            while (true)
                            {
                                int start;
                                lock (Gate)
                                {
                                    start = _coalescingVersion;
                                }
                                await write();
                                lock (Gate)
                                {
                                    if (_coalescingVersion == start)
                                    {
                                        _coalescingWritePending = false;
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "config write failed");
                        }
                        finally
                        {
                            lock (Gate)
                            {
                                _coalescingWritePending = false;
                            }
                        }
                    },
                    TaskScheduler.Default).Unwrap();
                return _tail;
            }
        }
    }
}
