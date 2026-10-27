using System;
using System.IO;
using System.Linq;

namespace mpv_winui.Lexicon.Language
{
    /// <summary>
    /// Language-file concerns: enumerating available languages and resolving
    /// / loading the AppLang strings for a language code. The switch flow
    /// (persisting the choice, pushing user-data/mpvw/language, raising
    /// LanguageChanged) stays in AppContext, which owns those hooks.
    /// </summary>
    public static class LanguageManager
    {
        /// <summary>Used whenever a requested code is missing or unsafe.</summary>
        public const string DefaultLanguage = "en-US";

        /// <summary>User-overridable language files (win over the bundled ones).</summary>
        public static string UserLanguageDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui", "languages");

        /// <summary>Enumerates app Languages/*.json as available languages; falls back to a built-in list when the directory is missing.</summary>
        public static string[] GetAvailableLanguages()
        {
            var dir = Path.Combine(System.AppContext.BaseDirectory, "Languages");
            if (Directory.Exists(dir))
            {
                var names = Directory.GetFiles(dir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(x => LanguageCode.IsSafe(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (names.Length > 0)
                {
                    return names!;
                }
            }

            return [DefaultLanguage, "zh-CN"];
        }

        /// <summary>
        /// The language code to actually use for <paramref name="lang"/>: the code
        /// itself when it is a plain language tag, otherwise the default.
        ///
        /// <para>Callers that persist the choice or push it into an mpv command
        /// should go through this, so the sanitised value is what gets stored and
        /// sent rather than only what gets opened.</para>
        /// </summary>
        public static string Normalize(string? lang) =>
            LanguageCode.IsSafe(lang) ? lang! : DefaultLanguage;

        /// <summary>
        /// Resolves the JSON file for a language code: user dir first, then the
        /// bundled Languages\ dir. Returns the first existing path, or the user
        /// path when neither exists (the loader then reports the missing file).
        ///
        /// <para>A code that is not a plain language tag is replaced with
        /// <see cref="DefaultLanguage"/> before it reaches the path, so a stored
        /// value can never point the load at a file outside the languages
        /// directory.</para>
        /// </summary>
        public static string ResolveFilePath(string lang)
        {
            var code = Normalize(lang);

            var candidates = new[]
            {
                Path.Combine(UserLanguageDirectory, code + ".json"),
                Path.Combine(System.AppContext.BaseDirectory, "Languages", code + ".json"),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return candidates[0];
        }

        /// <summary>Loads the language strings for <paramref name="code"/> into <paramref name="lang"/>.</summary>
        public static void Load(AppLang lang, string code)
        {
            var path = ResolveFilePath(code);

            // The file is read and parsed exactly once and shared by both
            // tables: the properties, and the customize-mode captions that
            // AppLang resolves by name. Those captions have no settable
            // property, because AppLang reads its own properties by reflection
            // and a JSON entry named like one would overwrite the property
            // rather than accompany it.
            var root = LanguageFile.ReadRoot(path);

            lang.ApplyJson(root);
            CustomizeStrings.Load(root);
        }
    }
}
