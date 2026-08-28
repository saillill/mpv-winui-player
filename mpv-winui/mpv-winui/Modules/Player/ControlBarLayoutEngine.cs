using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// UI-side half of the control-bar layout rules: reorders live command-bar
    /// elements. Pure string parsing lives in
    /// <see cref="ControlBarLayoutGrammar"/> so it can be unit-tested without
    /// WinUI.
    /// </summary>
    internal static class ControlBarLayoutEngine
    {
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
