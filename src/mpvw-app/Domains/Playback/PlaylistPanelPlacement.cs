namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// Where the playlist panel's top edge goes, given the anchor it hangs from.
    ///
    /// This is pure arithmetic with no WinUI types on purpose: the panel is a
    /// Popup with ShouldConstrainToRootBounds="False", so this code is the only
    /// thing that keeps it on the display, and the one thing worth asserting
    /// about it is arithmetic. Linking it into tests/conf is what lets that
    /// assertion exist; a model of the formula is the whole test.
    ///
    /// The defect it was extracted from: x was clamped to the work area and y
    /// was not. The panel is 640 DIPs tall and its anchor is the playlist key
    /// on the tool row, so on a 1366x768 display its bottom edge landed 112
    /// DIPs below the work area and the lower half of the list could not be
    /// reached by any means.
    /// </summary>
    internal static class PlaylistPanelPlacement
    {
        /// <summary>
        /// The panel's top edge: the anchor, pulled up far enough that the
        /// panel's bottom stays inside [top, bottom].
        ///
        /// When the panel is taller than that span there is no placement that
        /// fits, and the top edge wins: it carries the header and the close
        /// button, so dropping it off the top would leave a panel that cannot
        /// be dismissed with the pointer. A panel whose bottom is cut off is
        /// scrollable; one whose close button is off screen is not.
        /// </summary>
        /// <param name="anchorY">Where the panel would go if nothing constrained it.</param>
        /// <param name="height">The panel's height, capped by its MaxHeight.</param>
        /// <param name="top">The work area's top, in the same coordinate space.</param>
        /// <param name="bottom">The work area's bottom, in the same coordinate space.</param>
        internal static double ClampY(double anchorY, double height, double top, double bottom)
        {
            var lowest = bottom - height;

            // A span shorter than the panel: pin the top edge rather than the
            // bottom, so the header and its close button stay on screen.
            if (lowest < top)
            {
                return top;
            }

            if (anchorY > lowest)
            {
                return lowest;
            }

            if (anchorY < top)
            {
                return top;
            }

            return anchorY;
        }
    }
}
