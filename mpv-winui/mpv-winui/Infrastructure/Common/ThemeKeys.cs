namespace mpv_winui.Infrastructure.Common;

/// <summary>
/// The stored string values for the theme and backdrop settings.
///
/// These four names used to live on <c>AppSettings</c>, which put them behind the
/// settings module: <c>WindowStyleManager</c> — infrastructure — imported the whole
/// settings namespace to compare against four string constants. That is the one
/// direction the domain layering forbids, and the constants were the only reason.
///
/// They are a storage vocabulary, not settings state: the settings model, the
/// settings UI and the window chrome all have to agree on the exact strings, so
/// they belong below all three.
/// </summary>
public static class ThemeKeys
{
    public const string ThemeType_Auto = "Auto";
    public const string ThemeType_Light = "Light";
    public const string ThemeType_Dark = "Dark";

    public const string BackdropType_Acrylic = "Acrylic";
    public const string BackdropType_Mica = "Mica";
    public const string BackdropType_None = "None";
}