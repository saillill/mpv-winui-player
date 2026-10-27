using System;

namespace mpv_winui.Lexicon.Language
{
    /// <summary>
    /// Turns a language code into something safe to build a file name from.
    ///
    /// <para><b>Why this exists.</b> The code comes from the persisted
    /// <c>CurrentLanguage</c> setting and used to be concatenated straight into
    /// <c>Path.Combine(userDir, code + ".json")</c>. A value like
    /// <c>..\..\some</c> then resolved outside the languages directory, so
    /// whatever <c>Load</c> read was chosen by the settings file rather than by
    /// the app. The read is only a JSON parse, but the path the app opens should
    /// not be steerable from a stored string at all.</para>
    ///
    /// <para>It lives on its own, with no WinUI or <c>AppContext</c> dependency,
    /// so <c>mpv-conf-test</c> can link it and assert the rule directly — the
    /// same reason <c>MpvProtocolUri</c> and <c>MpvConfValueCodec</c> are
    /// separate.</para>
    /// </summary>
    internal static class LanguageCode
    {
        /// <summary>
        /// A code is accepted only if it looks like a language tag: ASCII letters
        /// and digits in <c>xx</c> or <c>xx-YY</c> shape, optionally with further
        /// subtags (<c>zh-Hans-CN</c>). Anything else — separators, dots,
        /// colons, spaces, drive letters — returns false.
        /// </summary>
        public static bool IsSafe(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            // A leading or trailing separator would still let "en/" and "/en"
            // through a naive split, so check the shape of every segment.
            if (code.Length > 64)
            {
                return false;
            }

            var segments = code.Split('-');
            foreach (var segment in segments)
            {
                if (segment.Length is 0 or > 8)
                {
                    return false;
                }

                foreach (var c in segment)
                {
                    if (!IsAsciiLetterOrDigit(c))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
