using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// String grammar of the control-bar layout settings: layout-style
    /// normalization, "id:zone" zone overrides, custom orders and hidden-icon
    /// lists. Deliberately free of any UI types so the exact source shipped
    /// with the app can be compiled into unit tests via a linked file
    /// (see mpv-winrt-test/ControlBarLayoutGrammarTests.cs).
    /// </summary>
    internal static class ControlBarLayoutGrammar
    {
        /// <summary>"modernx"/"center"/"right" collapse into "modernx"; anything else is "classic".</summary>
        public static string Normalize(string? value)
        {
            return value switch
            {
                "modernx" or "center" or "right" => "modernx",
                _ => "classic",
            };
        }

        internal static readonly string[] MovableIds =
            ["volume", "tracks", "random", "panel", "aspect", "fullwindow", "fullscreen", "pip"];

        /// <summary>Parses persisted per-id zone overrides ("id:0,id:2"); zone 1 is the fixed transport.</summary>
        public static Dictionary<string, int> ParseZones(string? settingRaw)
        {
            var zones = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in (settingRaw ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon >= token.Length - 1)
                {
                    continue;
                }
                if (int.TryParse(token[(colon + 1)..], out var zone) && (zone == 0 || zone == 2))
                {
                    zones[token[..colon]] = zone;
                }
            }
            return zones;
        }

        /// <summary>
        /// Parses a custom order into the allowed canvas ids. 原版 and 居中 keep
        /// separate stored orders so editing one style never reorders the other.
        /// </summary>
        public static List<string> ParseCustomOrder(string? orderRaw)
        {
            return (orderRaw ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => MovableIds.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>Parses the user-hidden icon id list (',' or ';' separated).</summary>
        public static HashSet<string> ParseHiddenIcons(string? hiddenRaw)
        {
            return new HashSet<string>(
                hiddenRaw?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
