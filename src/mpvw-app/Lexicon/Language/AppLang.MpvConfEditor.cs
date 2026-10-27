using System;

namespace mpv_winui.Lexicon.Language
{
    /// <summary>
    /// Strings for the in-app mpv.conf editor and the two buttons that reach it.
    ///
    /// <para>A partial rather than an addition to AppLang.cs: the loader fills
    /// every public property on the merged type by name, so a property declared
    /// here is populated from the language files exactly like the ones in the
    /// main file, without editing a 1500-line class to add seven lines.</para>
    /// </summary>
    public partial class AppLang
    {
        /// <summary>First button on the mpv.conf row: open the in-app editor.</summary>
        public string SettingsEditMpvConfDirect { get; set; } = "Edit directly";

        /// <summary>Second button on the mpv.conf row: reveal the file in Explorer.</summary>
        public string SettingsRevealMpvConf { get; set; } = "Show in folder";

        /// <summary>Colour legend chips in the editor's status bar.</summary>
        public string MpvConfLegendComment { get; set; } = "Comment";

        public string MpvConfLegendKey { get; set; } = "Option";

        public string MpvConfLegendValue { get; set; } = "Value";

        public string MpvConfLegendSection { get; set; } = "Section";
    }
}
