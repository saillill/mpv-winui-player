using Microsoft.UI.Xaml;
using System;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Fork-only helpers on <see cref="MpvPlayerPage"/> whose defining partials
    /// were replaced by the upstream versions during the merge.
    ///
    /// Everything here is expressed against the merged (upstream) structure, so
    /// these are additions rather than restorations of the old files. Grouped in
    /// one file so it is easy to review and later fold back into the owner
    /// partials.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// Re-applies localized text to this page's own chrome. The control bar
        /// and menu bar have their own re-localization entry points that the
        /// caller invokes separately.
        /// </summary>
        private void ApplyLocalizedStrings()
        {
            var lang = AppContext.AppLang;
            if (lang is null)
            {
                return;
            }

            if (PlayerControl is not null)
            {
                PlayerControl.ApplyLocalizedStrings();
            }
        }
    }
}
