using System;

namespace mpv_winui.Platform.Activation
{
    /// <summary>
    /// The <c>mpvw://</c> protocol contract in one place: the scheme, the query
    /// shape, and how an argument or an activation URI becomes a file path.
    ///
    /// <para><b>Why this is separate from <see cref="ActivationService"/>.</b>
    /// It is deliberately pure BCL, with no WinUI or AppLifecycle dependency, so
    /// the exact source the app ships can be linked into <c>mpv-conf-test</c> and
    /// asserted directly. The contract needs that: the registered scheme and the
    /// parser had drifted apart once already - one registration site wrote
    /// <c>mpvw</c>, another wrote <c>mpv-winui</c>, and the packaged activation
    /// path compared against a third literal - and nothing in the build could see
    /// any of it, because none of those values was reachable from a test.</para>
    ///
    /// <para><b>Why the scheme is <c>mpvw</c> while the product is
    /// <c>mpv-winui</c>.</b> The scheme matches the executable
    /// (<c>mpvw.exe</c>, from <c>AssemblyName</c>) and the protocol already
    /// declared in <c>Package.appxmanifest</c>. The product name, the data root
    /// (<c>%LOCALAPPDATA%\mpv-winui</c>) and the ARP key
    /// (<c>HKCU\Software\mpv-winui-player</c>) keep <c>mpv-winui</c> and must not
    /// be renamed to match this.</para>
    /// </summary>
    internal static class MpvProtocolUri
    {
        /// <summary>The registered URI scheme.</summary>
        public const string Scheme = "mpvw";

        private const string SchemePrefix = Scheme + ":";

        private const string FileQueryPrefix = "?file=";

        /// <summary>
        /// Reads the file path out of a raw command-line argument, as the
        /// unpackaged activation path receives it.
        ///
        /// <para>Unpackaged, a custom protocol launched through the shell arrives
        /// as a <c>Launch</c> activation with the URI sitting in the arguments
        /// rather than as a <c>Protocol</c> activation, so this is not a fallback
        /// but the normal path there.</para>
        /// </summary>
        /// <returns>True when the argument is this app's URI and carries a file.</returns>
        public static bool TryParseArgument(string? arg, out string? path)
        {
            path = null;

            if (string.IsNullOrWhiteSpace(arg))
            {
                return false;
            }

            if (!arg.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!Uri.TryCreate(arg, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return TryParseQuery(uri.Query, out path);
        }

        /// <summary>
        /// Reads the file path out of an already-split activation URI, as the
        /// packaged <c>Protocol</c> activation path receives it.
        ///
        /// <para>Kept beside <see cref="TryParseArgument"/> and sharing its query
        /// decoding, because the two entry points are handed the same URI and must
        /// not disagree about it. The packaged path previously compared the scheme
        /// against a literal that matched neither the manifest nor the unpackaged
        /// parser, so it rejected every activation without logging anything.</para>
        /// </summary>
        /// <returns>True when the scheme is this app's and the query carries a file.</returns>
        public static bool TryParseActivation(string? scheme, string? query, out string? path)
        {
            path = null;

            if (!string.Equals(scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParseQuery(query, out path);
        }

        /// <summary>
        /// Decodes the <c>?file=&lt;percent-encoded path&gt;</c> query. Shared by
        /// both entry points above.
        /// </summary>
        public static bool TryParseQuery(string? query, out string? path)
        {
            path = null;

            if (query is null || !query.StartsWith(FileQueryPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var value = query.Substring(FileQueryPrefix.Length);
            if (value.Length == 0)
            {
                return false;
            }

            path = Uri.UnescapeDataString(value);
            return path.Length > 0;
        }
    }
}
