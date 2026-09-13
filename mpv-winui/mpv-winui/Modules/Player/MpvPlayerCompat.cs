using Microsoft.UI.Xaml.Controls;
using mpv_winrt;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Bridges the fork's wrapper-era API surface onto the upstream raw native
    /// <see cref="MpvPlayer"/>.
    ///
    /// The upstream refactor removed the <c>MpvMediaPlayer</c> wrapper class and made the
    /// WinUI layer talk to the native WinRT object directly. The fork's own partial files
    /// (PlayerControl.*, MpvPlayerPage_*, PiPWindow) still call the shape the wrapper used
    /// to expose: <c>player.Native.X</c> for events, and simple properties such as
    /// <c>Volume</c> / <c>Position</c> / <c>Playing</c> instead of the native
    /// <c>Volume()</c> / <c>Position()</c> / <c>IsPaused()</c> methods.
    ///
    /// Rather than rewriting every call site, this file re-expresses those members in terms
    /// of the native API. It is additive only - nothing in the merged upstream code is
    /// changed - so it can be deleted wholesale once the fork partials are themselves
    /// migrated onto the native shape.
    /// </summary>
    public static class MpvPlayerCompat
    {
        // ---------------------------------------------------------------- misc

        /// <summary>Fire-and-forget command enqueue (wrapper-era name).</summary>
        public static Task EnqueueCommand(this MpvPlayer player, string command)
        {
            return player.RunCommandAsync(command).AsTask();
        }

        /// <summary>Fire-and-forget batch command enqueue (wrapper-era name).</summary>
        public static Task EnqueueCommands(this MpvPlayer player, IList<string> commands)
        {
            return player.RunCommandAsync(commands).AsTask();
        }

        /// <summary>
        /// Wrapper-era "flush pending commands". The upstream native player executes
        /// commands synchronously inside <c>Command</c>, so there is nothing to drain.
        /// </summary>
        public static Task DrainCommandsAsync(this MpvPlayer player)
        {
            return Task.CompletedTask;
        }

        // ------------------------------------------------------------ playback

        /// <summary>Inverse of the native <c>IsPaused()</c>.</summary>
        public static bool Playing(this MpvPlayer player)
        {
            return !player.IsPaused();
        }



        /// <summary>Wrapper-era playback-rate name for the native playback speed.</summary>
        public static double PlaybackRate(this MpvPlayer player)
        {
            return player.PlaybackSpeed();
        }

        /// <summary>Wrapper-era playback-rate name for the native playback speed.</summary>
        public static void PlaybackRate(this MpvPlayer player, double value)
        {
            player.PlaybackSpeed(value);
        }

        // -------------------------------------------------------------- tracks

        /// <summary>Wrapper-era track-list name for the native <c>GetVideoTracks()</c>.</summary>
        public static IReadOnlyList<MpvTrack> VideoTracks(this MpvPlayer player)
        {
            return player.GetVideoTracks();
        }

        /// <summary>Wrapper-era track-list name for the native <c>GetAudioTracks()</c>.</summary>
        public static IReadOnlyList<MpvTrack> AudioTracks(this MpvPlayer player)
        {
            return player.GetAudioTracks();
        }

        /// <summary>Wrapper-era track-list name for the native <c>GetSubtitleTracks()</c>.</summary>
        public static IReadOnlyList<MpvTrack> SubtitleTracks(this MpvPlayer player)
        {
            return player.GetSubtitleTracks();
        }

        /// <summary>Wrapper-era track-list name for the native <c>GetChapters()</c>.</summary>
        public static IReadOnlyList<MpvChapter> Chapters(this MpvPlayer player)
        {
            return player.GetChapters();
        }

        /// <summary>Wrapper-era device-list name for the native <c>GetAudioDevices()</c>.</summary>
        public static IReadOnlyList<MpvAudioDevice> AudioDevices(this MpvPlayer player)
        {
            return player.GetAudioDevices();
        }

        /// <summary>Wrapper-era adapter-list name for the native <c>GetGpuAdapters()</c>.</summary>
        public static IReadOnlyList<MpvGpuAdapter> GpuAdapters(this MpvPlayer player)
        {
            return player.GetGpuAdapters();
        }

        /// <summary>Wrapper-era secondary-subtitle alias for <c>GetSubtitleTracks()</c>.</summary>
        public static IReadOnlyList<MpvTrack> SecondSubtitleTracks(this MpvPlayer player)
        {
            return player.GetSubtitleTracks();
        }

        // -------------------------------------------------------------- shuffle

        /// <summary>Wrapper-era property name for the native <c>Shuffle()</c> getter.</summary>
        public static bool ShuffleEnabled(this MpvPlayer player)
        {
            return player.Shuffle();
        }

        /// <summary>Wrapper-era property name for the native <c>SetShuffle()</c> setter.</summary>
        public static void ShuffleEnabled(this MpvPlayer player, bool enabled)
        {
            player.SetShuffle(enabled);
        }

        // ------------------------------------------------------------- repeat

        /// <summary>
        /// Wrapper-era single <c>RepeatState</c> view over the native player's two
        /// independent loop flags (<c>LoopFile</c> / <c>LoopPlaylist</c>).
        ///
        /// <c>All</c> = loop the playlist, <c>One</c> = loop the current file,
        /// <c>None</c> = neither.
        /// </summary>
        public static RepeatState GetRepeatMode(this MpvPlayer player)
        {
            if (player.LoopFile())
            {
                return RepeatState.One;
            }
            return player.LoopPlaylist() ? RepeatState.All : RepeatState.None;
        }

        /// <summary>See <see cref="GetRepeatMode"/>.</summary>
        public static void SetRepeatMode(this MpvPlayer player, RepeatState state)
        {
            switch (state)
            {
                case RepeatState.One:
                    player.SetLoopPlaylist(false);
                    player.LoopFile(true);
                    break;
                case RepeatState.All:
                    player.LoopFile(false);
                    player.SetLoopPlaylist(true);
                    break;
                default:
                    player.LoopFile(false);
                    player.SetLoopPlaylist(false);
                    break;
            }
        }

        // ------------------------------------------------------------ A-B loop

        /// <summary>
        /// Wrapper-era "toggle A-B loop" helper. The native player exposes the two
        /// loop points as separate getters/setters; the fork's button cycles them
        /// (set A, set B, then clear both).
        /// </summary>
        public static void ToggleAbLoop(this MpvPlayer player)
        {
            var a = player.AbLoopA();
            var b = player.AbLoopB();
            var position = player.Position();

            // Neither point set yet -> mark A at the current position.
            if (a < 0 && b < 0)
            {
                player.Command("ab-loop-a", position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // A set but B not -> close the loop.
            if (a >= 0 && b < 0)
            {
                player.Command("ab-loop-b", position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // Both set -> clear the loop.
            player.Command("ab-loop-a", "no");
            player.Command("ab-loop-b", "no");
        }
    }
}
