using Microsoft.UI.Xaml;
using mpv_winrt;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
using mpv_winui.Infrastructure.Motion;
using System;
using System.Globalization;

// Part of MpvPlayerPage. See the class comment on the main file.
namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// The transient message capsule: this app's replacement for mpv's OSD text.
    ///
    /// **Why it exists.** mpv draws its own on-screen text into the video frame:
    /// "Volume: 74%", the track-switch banner, the seek bar, and metadata_osd's
    /// title card. That text is rendered by mpv, at mpv's font size, in mpv's
    /// corner, in a box whose contrast depends on whatever happens to be behind
    /// it, and none of it follows this app's type or colour. Turning it off and
    /// drawing nothing would be worse: several of those messages are the only
    /// feedback a keyboard-driven action has.
    ///
    /// **What is shown.** Only things a person just did, and only where nothing
    /// else on screen already says so:
    ///
    /// <list type="bullet">
    /// <item>volume - the slider is not always on screen</item>
    /// <item>speed - the button shows a number, but only once you find it</item>
    /// <item>pause/play - a paused frame and a still frame look identical</item>
    /// <item>repeat and shuffle - the glyph changes, at 16px, in the corner</item>
    /// <item>a track switch - the sound changes and nothing visible does</item>
    /// </list>
    ///
    /// **Not passive state.** Nothing here fires on a timer, on a buffering
    /// transition or on a position tick. mpv's OSD reports all of those, and they
    /// are the ones reported as "a box keeps appearing over the picture".
    ///
    /// **One message at a time.** A second message replaces the first rather than
    /// stacking. The dismissal timer restarts on each call, so an event that
    /// repeats ends 2.4 s after the LAST one rather than after the first.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// How long a message stays up after the last one arrived.
        ///
        /// 2.4 s. mpv's own default is 1 s, which is too short to read a track
        /// title; the reference players sit between 2 and 3.
        /// </summary>
        private static readonly TimeSpan OsdDwell = TimeSpan.FromMilliseconds(2400);

        private DispatcherTimer? _osdTimer;

        /// <summary>
        /// True while a message is fading out. A call arriving during the fade has
        /// to cancel it, or the fade's completion callback collapses the capsule
        /// that was just revealed again - the same defect the card overlay had.
        /// </summary>
        private bool _osdHiding;

        /// <summary>True once the first event has arrived, so the start-up burst is skipped.</summary>
        private bool _osdSawFirstVolume;

        /// <summary>
        /// Shows one message. <paramref name="glyph"/> is a FluentIcons constant or
        /// null for text only.
        ///
        /// Every caller is an event handler that already runs on the UI thread -
        /// the native events are marshalled through DispatcherQueue - so this does
        /// not marshal again.
        /// </summary>
        private void ShowOsd(string text, string? glyph = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _osdHiding = false;

            if (_osdTimer is null)
            {
                _osdTimer = new DispatcherTimer { Interval = OsdDwell };
                _osdTimer.Tick += (_, _) =>
                {
                    _osdTimer.Stop();
                    HideOsd();
                };
            }

            _osdTimer.Stop();

            OsdCapsuleText.Text = text;

            // Assigned, never toggled: a message with no icon must clear the one
            // the previous message left, or the volume glyph ends up in front of
            // "Repeat off".
            if (string.IsNullOrEmpty(glyph))
            {
                OsdCapsuleIcon.Visibility = Visibility.Collapsed;
                OsdCapsuleIcon.Data = null;
            }
            else
            {
                OsdCapsuleIcon.Data = FluentIcons.Geometry(glyph);
                OsdCapsuleIcon.Visibility = Visibility.Visible;
            }

            if (OsdCapsule.Visibility != Visibility.Visible)
            {
                OsdCapsule.Visibility = Visibility.Visible;
                MotionBehaviors.FadeIn(OsdCapsule);
            }

            _osdTimer.Start();
        }

        /// <summary>
        /// Fades the capsule out and collapses it once the fade has run.
        ///
        /// Nothing here is ever clickable, so a fading capsule cannot swallow a
        /// click meant for the picture underneath.
        /// </summary>
        private void HideOsd()
        {
            if (OsdCapsule.Visibility != Visibility.Visible)
            {
                return;
            }

            _osdHiding = true;

            MotionBehaviors.FadeOut(OsdCapsule, () =>
            {
                // Re-checked inside the callback: a message can arrive within the
                // 83 ms fade, and collapsing then would hide the capsule that
                // ShowOsd has just revealed.
                if (!_osdHiding)
                {
                    return;
                }

                _osdHiding = false;
                OsdCapsule.Visibility = Visibility.Collapsed;
                MotionBehaviors.Reset(OsdCapsule);
            });
        }

        /// <summary>
        /// Whether the capsule may draw right now.
        ///
        /// Only while the PICTURE is the surface. Over the library it would float
        /// on the browse grid, which is neither where mpv drew its own OSD nor
        /// where any of these messages belongs - the user is looking at
        /// thumbnails, not at a frame. A track advancing behind the library is
        /// exactly the case this app must stay quiet about.
        /// </summary>
        private bool OsdAllowed => _playbackActive && !_libraryVisible;

        // ---------------------------------------------------------------- wiring

        /// <summary>
        /// Subscribes the capsule's handlers.
        ///
        /// Called from the same place as the page's other per-load handlers, so
        /// the capsule cannot end up listening to a player the page has torn down.
        /// </summary>
        private void AttachOsdEvents()
        {
            if (_mediaPlayer is null)
            {
                return;
            }

            _mediaPlayer.VolumeChanged += Osd_VolumeChanged;
            _mediaPlayer.SpeedChanged += Osd_SpeedChanged;
            _mediaPlayer.PlaybackStateChanged += Osd_PlaybackStateChanged;
            _mediaPlayer.LoopFileChanged += Osd_RepeatChanged;
            _mediaPlayer.LoopPlaylistChanged += Osd_RepeatChanged;
            _mediaPlayer.ShuffleChanged += Osd_ShuffleChanged;
        }

        private void DetachOsdEvents()
        {
            if (_mediaPlayer is null)
            {
                return;
            }

            _mediaPlayer.VolumeChanged -= Osd_VolumeChanged;
            _mediaPlayer.SpeedChanged -= Osd_SpeedChanged;
            _mediaPlayer.PlaybackStateChanged -= Osd_PlaybackStateChanged;
            _mediaPlayer.LoopFileChanged -= Osd_RepeatChanged;
            _mediaPlayer.LoopPlaylistChanged -= Osd_RepeatChanged;
            _mediaPlayer.ShuffleChanged -= Osd_ShuffleChanged;

            _osdTimer?.Stop();
        }

        // ---------------------------------------------------------------- events

        private void Osd_VolumeChanged(VolumeChangedEventArgs args) =>
            DispatcherQueue.RunAsync(() =>
            {
                // The player reports its volume once while initialising, before
                // the first file exists. That is not something the user did, and a
                // capsule on an empty window reads as a glitch.
                if (!_osdSawFirstVolume)
                {
                    _osdSawFirstVolume = true;

                    return;
                }

                if (!OsdAllowed)
                {
                    return;
                }

                // IsMuted is the player's own answer, and the only correct one at
                // volume 0: a slider dragged to zero and a mute are different
                // states that both report 0.0, and only one of them says "Muted".
                if (args.IsMuted || args.Volume <= 0.5)
                {
                    ShowOsd(AppContext.AppLang.OsdMuted, FluentIcons.Music);

                    return;
                }

                ShowOsd(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        AppContext.AppLang.OsdVolumeFormat,
                        ((int)Math.Round(args.Volume)).ToString(CultureInfo.CurrentCulture) + "%"),
                    FluentIcons.Music);
            });

        private void Osd_SpeedChanged(SpeedChangedEventArgs args) =>
            DispatcherQueue.RunAsync(() =>
            {
                // 1.0 is the default and fires on every load; announcing "Speed 1x"
                // each time would be noise rather than feedback.
                if (!OsdAllowed || Math.Abs(args.Speed - 1.0) < 0.01)
                {
                    return;
                }

                ShowOsd(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        AppContext.AppLang.OsdSpeedFormat,
                        args.Speed.ToString("0.##", CultureInfo.CurrentCulture)));
            });

        /// <summary>
        /// Pause and play only.
        ///
        /// This event also fires on idle, which is a passive state - the file ran
        /// out, not something the user did. Announcing it would put a capsule on
        /// screen every time a video ends.
        /// </summary>
        private void Osd_PlaybackStateChanged(PlaybackStateChangedEventArgs args) =>
            DispatcherQueue.RunAsync(() =>
            {
                if (!OsdAllowed || args.IsIdle)
                {
                    return;
                }

                var lang = AppContext.AppLang;

                ShowOsd(
                    args.IsPaused ? lang.OsdPaused : lang.OsdPlaying,
                    args.IsPaused ? FluentIcons.Pause : FluentIcons.Play);
            });

        /// <summary>
        /// Repeat mode. The event carries no argument, so the current state is
        /// read back off the player - one property read, and the only way to know
        /// which of the two loop flags is now set.
        ///
        /// File repeat wins the message when both are on: "repeat one" is the
        /// narrower, more surprising of the two, and it is the one that changes
        /// what a track ending does.
        /// </summary>
        private void Osd_RepeatChanged() =>
            DispatcherQueue.RunAsync(() =>
            {
                if (!OsdAllowed || _mediaPlayer is null)
                {
                    return;
                }

                var lang = AppContext.AppLang;

                if (_mediaPlayer.LoopFile())
                {
                    ShowOsd(lang.OsdLoopFile, FluentIcons.ArrowRepeatAll);

                    return;
                }

                ShowOsd(
                    _mediaPlayer.LoopPlaylist() ? lang.OsdLoopPlaylist : lang.OsdLoopOff,
                    FluentIcons.ArrowRepeatAll);
            });

        private void Osd_ShuffleChanged() =>
            DispatcherQueue.RunAsync(() =>
            {
                if (!OsdAllowed || _mediaPlayer is null)
                {
                    return;
                }

                var lang = AppContext.AppLang;

                ShowOsd(
                    _mediaPlayer.Shuffle() ? lang.OsdShuffleOn : lang.OsdShuffleOff,
                    FluentIcons.ArrowShuffle);
            });

        // ------------------------------------------------- no track announcement
        //
        // A track switch is deliberately NOT reported by this capsule, and the
        // reason is recorded here rather than left to be rediscovered.
        //
        // The natural source is MpvPlayer.TrackChanged, and it cannot be used:
        // the IDL declares it, the projection compiles, and the native component
        // only ever ADDS and REMOVES handlers - `m_trackChangedEvent` is never
        // invoked. A subscription therefore builds, runs, and silently never
        // fires. That is exactly the c49b461 failure mode this repository has a
        // checker for, and tools/check-winrt-events.py caught it immediately.
        //
        // The alternatives are worse than the gap. TrackListChanged fires when a
        // file loads (a full new list), not when the selected track changes, so
        // using it would announce the wrong thing at the wrong time. Subscribing
        // to mpv's own `aid`/`sid` properties needs a raise added to the native
        // component - a C++ change with its own build and its own risk, for one
        // message. When someone does wire that raise, this is where the handler
        // goes; until then the app says nothing rather than saying something
        // untrue.
    }
}
