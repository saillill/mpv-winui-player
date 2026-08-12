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
    }
}
