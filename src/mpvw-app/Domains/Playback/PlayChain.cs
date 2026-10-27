using NLog;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// The five links between "a file was chosen" and "a picture is on screen",
    /// each logged at Info so a user report can be traced without debug logging.
    ///
    /// <para>**Why this exists.** The chain has five links: the open request, the
    /// command reaching mpv, mpv reporting the file loaded, the page switching to
    /// the video, and the picture getting a surface with a real size. None of them
    /// logged anything that reached the default log file, so "the video does not
    /// play" could not be traced past "something happened". The failure that
    /// started this is exactly that shape: mpv loaded the file
    /// (`media info changed: video=960x540`), reported `0x0` a frame later, and the
    /// UI stayed on the library.</para>
    ///
    /// <para>**Why a separate logger name.** These lines must survive the release
    /// default, while mpv's forwarded chatter stays at Warn so playback does not
    /// grow the file. One logger name is the smallest thing that lets a reader
    /// isolate the chain: `grep "play-chain:"`.</para>
    ///
    /// <para>**Why every call is Info and none is Debug.** The point is that a user
    /// who reports the bug does not need to have had debug logging on.
    /// `tools/check-play-chain-log.py` fails if a `play-chain:` line drops below
    /// Info, because that regression is silent: the line simply stops
    /// appearing.</para>
    ///
    /// <para>**Thread-safety.** Called from mpv's native event thread as well as the
    /// UI thread. NLog's logger is thread-safe and the file target opens per write,
    /// so no dispatch is needed - and adding one would queue these lines behind the
    /// UI thread, which is the opposite of useful when the UI is what is broken.</para>
    /// </summary>
    public static class PlayChain
    {
        private static readonly Logger _logger = LogManager.GetLogger("PlayChain");

        /// <summary>
        /// Link 1. A file is about to be handed to mpv. Logged before the command is
        /// built, so its absence means the request never left the app.
        /// </summary>
        public static void Open(int count, string firstPath, object type, object mode) =>
            _logger.Info("play-chain: open requested count={0} mode={1} type={2} first={3}",
                count, mode, type, firstPath);

        /// <summary>
        /// Link 2. The loadfile command has been handed to mpv.
        ///
        /// Named "sent", not "accepted": <c>mpv_command</c> returns a status the
        /// native layer discards, so from here the app genuinely does not know
        /// whether mpv took the command. Claiming acceptance would be exactly the
        /// kind of unsupported claim this class exists to prevent.
        /// </summary>
        public static void Sent(string path) =>
            _logger.Info("play-chain: loadfile sent {0}", path);

        /// <summary>
        /// Link 3. mpv raised FILE_LOADED. Fires once per file.
        /// </summary>
        public static void Loaded(string path) =>
            _logger.Info("play-chain: file-loaded {0}", path);

        /// <summary>
        /// Link 4. The page chose which surface to show, and says why. The reason
        /// matters as much as the choice: "library" is correct when the user
        /// withdrew to browse and wrong when they just opened a file.
        /// </summary>
        public static void Page(string surface, string reason) =>
            _logger.Info("play-chain: page -> {0} ({1})", surface, reason);

        /// <summary>
        /// Link 5a. mpv reports a real picture size. The `file` argument is not
        /// decoration: the first version of this line did not carry it, and the log
        /// then showed a 960x540 surface with no way to tell whether it belonged to
        /// the file the user asked for.
        /// </summary>
        public static void SurfaceSized(int width, int height, string file) =>
            _logger.Info("play-chain: surface sized {0}x{1} file={2}", width, height, file);

        /// <summary>
        /// Link 5b. The surface has collapsed to nothing: mpv reports a video size
        /// of 0x0 after having reported a real one, or the panel measured as 1x1.
        ///
        /// <para>Both mean the same thing to a user - a loaded file and no picture -
        /// and both are otherwise invisible, because every other status property
        /// (`idle-active`, `filename`, `playlist-count`) still reads as if playback
        /// were fine.</para>
        /// </summary>
        public static void SurfaceLost(string what) =>
            _logger.Info("play-chain: surface lost ({0})", what);
    }
}
