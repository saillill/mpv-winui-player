using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace mpv_winui.Modules.Common.View;

/// <summary>
/// Resolves theme resources for elements whose effective theme differs from
/// the application theme. Application.Current.Resources always returns the
/// brush of App.RequestedTheme, which is wrong for windows forced to Dark.
/// </summary>
public static class ThemeResource
{
    public static Brush Brush(FrameworkElement element, string key)
    {
        var theme = element.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(theme, out var dictionary)
            && dictionary is ResourceDictionary themeDictionary
            && themeDictionary.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        return (Brush)Application.Current.Resources[key];
    }
}
