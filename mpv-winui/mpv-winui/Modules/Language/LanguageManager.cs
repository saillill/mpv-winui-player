using System;
using System.IO;
using System.Linq;

namespace mpv_winui.Modules.Language
{
    /// <summary>
    /// Language-file concerns: enumerating available languages and resolving
    /// / loading the AppLang strings for a language code. The switch flow
    /// (persisting the choice, pushing user-data/mpvw/language, raising
    /// LanguageChanged) stays in AppContext, which owns those hooks.
    /// </summary>
    public static class LanguageManager
    {
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
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (names.Length > 0)
                {
                    return names!;
                }
            }

            return ["en-US", "zh-CN"];
        }

        /// <summary>
        /// Resolves the JSON file for a language code: user dir first, then the
        /// bundled Languages\ dir. Returns the first existing path, or the user
        /// path when neither exists (the loader then reports the missing file).
        /// </summary>
        public static string ResolveFilePath(string lang)
        {
            var candidates = new[]
            {
                Path.Combine(UserLanguageDirectory, lang + ".json"),
                Path.Combine(System.AppContext.BaseDirectory, "Languages", lang + ".json"),
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
            lang.LoadFromJson(ResolveFilePath(code));
        }
    }
}
