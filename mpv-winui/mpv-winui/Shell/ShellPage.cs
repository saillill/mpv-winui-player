using System;

namespace mpv_winui;

/// <summary>
/// Which of the playing page's states is up.
///
/// One host page, two states: the video panel cannot leave the visual tree
/// (libmpv binds it once), so "which page is showing" is a state rather
/// than a navigation. The states differ in what the surrounding chrome
/// does, which is what this names:
///
///   * <see cref="Library"/> - the library is up, nothing is playing.
///   * <see cref="Playing"/> - the playing page: video up, library down.
///     The navigation pane absorbs its clicks and the title bar's back key
///     is disabled, because there is nowhere to go back to; the page's own
///     withdraw button is the single way out.
///   * <see cref="LibraryWhilePlaying"/> - withdrawn to the library with
///     the video still running; the title bar's key returns to the video.
/// </summary>
/// <remarks>
/// This lived in <c>Modules/Player/MpvPlayerPage_Library.cs</c>, next to the
/// library the state switches to. But every consumer is in the shell -- the
/// title bar reads it to decide what "back" means, and <c>MainWindow</c>
/// subscribes to the change event -- so the player module owned the vocabulary
/// the shell was written in. It sits in the shell namespace now, and the player
/// imports it like any other consumer.
/// </remarks>
internal enum ShellPage
{
    Library,
    Playing,
    LibraryWhilePlaying,
}
