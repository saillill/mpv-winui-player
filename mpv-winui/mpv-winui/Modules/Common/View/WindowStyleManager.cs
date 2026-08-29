using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings;
using System;
using Windows.UI.ViewManagement;

namespace mpv_winui.Modules.Common.View;

/// <summary>
/// Applies the Windows-conventional personalization model: the app chooses a
/// theme mode (follow system / light / dark) and a backdrop material
/// (Mica / Desktop Acrylic / none) via Window.SystemBackdrop. Everything the
/// OS owns stays with the OS — the material tint comes from the wallpaper
/// (Mica), the system "Transparency effects" toggle makes the material fall
/// back to a solid color on its own, and the accent color is the system one.
/// The framework keeps the backdrop theme in sync with the window's
/// RequestedTheme, so no manual SystemBackdropConfiguration is needed.
/// </summary>
public sealed class WindowStyleManager : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _contentRoot;
    private readonly UISettings _uiSettings = new();
    private ElementTheme _theme;

    public WindowStyleManager(Window window)
    {
        _window = window;
        _contentRoot = (FrameworkElement)window.Content;
    }

    public void Setup()
    {
        _theme = GetThemeType();
        UpdateTitleBarColors(_theme);
        UpdateContentTheme(_theme);
        UpdateUiFont();

        ApplyBackdrop();
        _uiSettings.ColorValuesChanged += OnColorValuesChanged;
    }

    /// <summary>Re-applies the backdrop material from the current setting (live toggle support).</summary>
    public void UpdateBackdrop()
    {
        ApplyBackdrop();
    }

    /// <summary>Applies the user-selected UI font to the window content.</summary>
    public void UpdateUiFont()
    {
        var font = AppContext.AppSetting.UiFont;
        if (string.IsNullOrWhiteSpace(font))
        {
            Application.Current.Resources.Remove("ContentControlThemeFontFamily");
        }
        else
        {
            Application.Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(font);
        }
    }

    private void ApplyBackdrop()
    {
        // XAML SystemBackdrop classes handle theme tracking, focus state and
        // fallback (no hardware / transparency off / battery saver / high
        // contrast) themselves — exactly the Windows Settings behavior.
        _window.SystemBackdrop = AppContext.AppSetting.BackdropType switch
        {
            AppSettings.BackdropType_Mica => MicaController.IsSupported()
                ? new MicaBackdrop()
                : null,
            AppSettings.BackdropType_Acrylic => DesktopAcrylicController.IsSupported()
                ? new DesktopAcrylicBackdrop()
                : null,
            _ => null,
        };
    }

    public ElementTheme GetThemeType()
    {
        return ResolveTheme();
    }

    /// <summary>Resolves the effective element theme from the current setting.</summary>
    public static ElementTheme ResolveTheme()
    {
        return AppContext.AppSetting.ThemeType switch
        {
            AppSettings.ThemeType_Dark => ElementTheme.Dark,
            AppSettings.ThemeType_Light => ElementTheme.Light,
            _ => App.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark,
        };
    }

    public void UpdateTheme(ElementTheme theme)
    {
        if (_theme == theme)
        {
            return;
        }

        _theme = theme;
        UpdateContentTheme(theme);
        UpdateTitleBarColors(theme);
    }

    public void Cleanup()
    {
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        _window.DispatcherQueue.RunAsync(() =>
        {
            var theme = GetThemeType();
            if (_theme != theme)
            {
                UpdateTheme(theme);
            }
        });
    }

    private void UpdateContentTheme(ElementTheme theme)
    {
        _contentRoot.RequestedTheme = theme;
    }

    private void UpdateTitleBarColors(ElementTheme theme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _window.AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = theme == ElementTheme.Dark ? Colors.White : Colors.Black;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
