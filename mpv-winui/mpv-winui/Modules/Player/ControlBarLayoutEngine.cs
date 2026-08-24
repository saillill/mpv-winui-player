using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Pure parsing/partitioning rules behind the control-bar layout editor.
    /// Kept free of UI state so the ordering grammar ("id:zone" zones,
    /// comma-separated custom orders, hidden-icon lists) stays unit-testable;
    /// PlayerControl supplies the settings strings and the live buttons.
    /// </summary>
    internal static class ControlBarLayoutEngine
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

        private static readonly string[] MovableIds =
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

        /// <summary>
        /// Reorders a command bar's canvas buttons to the custom order. Only
        /// ids present in this partition are affected; the rest keep their
        /// default relative order. "volume" maps to two controls (mute + slider).
        /// </summary>
        public static ICommandBarElement[] ReorderMovable((string Id, ICommandBarElement Element)[] defaults, IReadOnlyList<string> custom)
        {
            var result = new List<ICommandBarElement>(defaults.Length);
            var remaining = new HashSet<string>(defaults.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var id in custom)
            {
                if (!remaining.Remove(id))
                {
                    continue;
                }
                foreach (var (did, el) in defaults)
                {
                    if (string.Equals(did, id, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(el);
                    }
                }
            }
            // ids not mentioned in the custom order keep their default position.
            foreach (var (id, el) in defaults)
            {
                if (remaining.Contains(id))
                {
                    result.Add(el);
                }
            }
            return result.ToArray();
        }
    }
}
