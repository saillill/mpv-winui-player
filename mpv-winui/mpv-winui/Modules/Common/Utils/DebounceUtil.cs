using System;
using Windows.System.Threading;

namespace mpv_winui.Modules.Common.Utils
{
    public static class DebounceUtil
    {
        public static Action Debounce(Action action, TimeSpan delay)
        {
            ThreadPoolTimer? timer = null;
            return () =>
            {
                timer?.Cancel();
                timer = ThreadPoolTimer.CreateTimer((sender) =>
                {
                    if (object.ReferenceEquals(sender, timer))
                    {
                        action();
                    }
                }, delay);
            };
        }

        public static Action<T> Debounce<T>(Action<T> action, TimeSpan delay)
        {
            ThreadPoolTimer? timer = null;
            return (arg) =>
            {
                timer?.Cancel();
                timer = ThreadPoolTimer.CreateTimer((sender) =>
                {
                    if (object.ReferenceEquals(sender, timer))
                    {
                        action(arg);
                    }
                }, delay);
            };
        }

        /// <summary>
        /// Leading + trailing throttle: the first call runs immediately, calls
        /// inside the interval are coalesced into one trailing run with the
        /// latest argument. Use this where the value must track its source in
        /// near-real-time (e.g. the video swapchain while the window is being
        /// drag-resized) — a plain Debounce postpones the update until the
        /// source goes quiet, which reads as "the video lags behind the
        /// window".
        /// </summary>
        public static Action<T> Throttle<T>(Action<T> action, TimeSpan interval)
        {
            var gate = new object();
            ThreadPoolTimer? trailing = null;
            bool hasPending = false;
            T pending = default!;
            DateTime lastRun = DateTime.MinValue;

            return arg =>
            {
                bool runNow = false;
                lock (gate)
                {
                    var now = DateTime.UtcNow;
                    if (now - lastRun >= interval)
                    {
                        lastRun = now;
                        runNow = true;
                    }
                    else
                    {
                        pending = arg;
                        hasPending = true;
                        if (trailing is null)
                        {
                            var due = interval - (now - lastRun);
                            var timer = ThreadPoolTimer.CreateTimer(_ =>
                            {
                                T value = default!;
                                lock (gate)
                                {
                                    if (!hasPending)
                                    {
                                        trailing = null;
                                        return;
                                    }
                                    value = pending;
                                    hasPending = false;
                                    trailing = null;
                                    lastRun = DateTime.UtcNow;
                                }
                                action(value);
                            }, due > TimeSpan.Zero ? due : TimeSpan.Zero);
                            trailing = timer;
                        }
                    }
                }
                if (runNow)
                {
                    action(arg);
                }
            };
        }
    }
}
