using mpv_winrt;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Serializes mpv command execution onto a single background worker so a
    /// batched startup option pass and later interactive commands cannot race
    /// each other. FIFO order is preserved; input forwarding (keydown/keyup)
    /// and property setters intentionally bypass this queue to stay low
    /// latency, matching the audit plan.
    /// </summary>
    public sealed class MpvCommandQueue : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(MpvCommandQueue));

        private readonly MpvPlayer _mpvPlayer;
        private readonly Channel<QueueItem> _channel;
        private readonly Task _worker;
        private bool _stopped;

        public MpvCommandQueue(MpvPlayer mpvPlayer)
        {
            _mpvPlayer = mpvPlayer;
            _channel = Channel.CreateUnbounded<QueueItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            _worker = Task.Run(RunAsync);
        }

        private sealed class QueueItem
        {
            public required Action<MpvPlayer> Execute
            {
                get;
                init;
            }
            public TaskCompletionSource? Completion
            {
                get;
                init;
            }
        }

        public Task EnqueueCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return Task.CompletedTask;
            }
            return EnqueueCore(player => player.CommandString(command));
        }

        public Task EnqueueCommands(IEnumerable<string> commands)
        {
            var list = new List<string>();
            foreach (var command in commands)
            {
                if (!string.IsNullOrEmpty(command))
                {
                    list.Add(command);
                }
            }
            if (list.Count == 0)
            {
                return Task.CompletedTask;
            }
            return EnqueueCore(player => player.ApplyCommandStrings(list));
        }

        public Task EnqueueVector(IList<string> args)
        {
            if (args is null || args.Count == 0)
            {
                return Task.CompletedTask;
            }
            return EnqueueCore(player => player.Command(args));
        }

        public Task DrainAsync()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_stopped)
            {
                tcs.TrySetResult();
                return tcs.Task;
            }
            var item = new QueueItem
            {
                Execute = _ => { },
                Completion = tcs,
            };
            if (!_channel.Writer.TryWrite(item))
            {
                tcs.TrySetResult();
            }
            return tcs.Task;
        }

        private Task EnqueueCore(Action<MpvPlayer> execute)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_stopped)
            {
                tcs.TrySetCanceled();
                return tcs.Task;
            }
            var item = new QueueItem
            {
                Execute = execute,
                Completion = tcs,
            };
            if (!_channel.Writer.TryWrite(item))
            {
                tcs.TrySetCanceled();
            }
            return tcs.Task;
        }

        private async Task RunAsync()
        {
            var reader = _channel.Reader;
            try
            {
                while (await reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            item.Execute(_mpvPlayer);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "mpv command failed");
                        }
                        finally
                        {
                            item.Completion?.TrySetResult();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "command queue worker stopped unexpectedly");
            }
        }

        /// <summary>Drains remaining queued commands, then stops the worker.</summary>
        public void Stop()
        {
            if (_stopped)
            {
                return;
            }
            _stopped = true;
            _channel.Writer.TryComplete();
            try
            {
                _worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Exit must never be blocked by a hung worker.
            }
        }

        public void Dispose() => Stop();
    }
}
