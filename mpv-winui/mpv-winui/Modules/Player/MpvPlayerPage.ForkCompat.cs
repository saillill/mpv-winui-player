using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using mpv_winrt;

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
        // --------------------------------------------------------- localization

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

        // -------------------------------------------------------------- menu bar

        /// <summary>
        /// Rebuilds the custom menu bar entries. The merged code base loads the
        /// definitions through <c>SetupCustomMenuBarItems</c>; this is the
        /// "rebuild after a definition or language change" entry point that the
        /// language-changed handler and MainWindow call.
        /// </summary>
        public void RebuildMenuBar()
        {
            if (MainMenuBar is null)
            {
                return;
            }

            // Drop the previously injected custom items (everything between the
            // built-in head and the trailing item) before re-adding them.
            while (MainMenuBar.Items.Count > 3)
            {
                MainMenuBar.Items.RemoveAt(MainMenuBar.Items.Count - 2);
            }

            SetupCustomMenuBarItems();
        }
    }
}
