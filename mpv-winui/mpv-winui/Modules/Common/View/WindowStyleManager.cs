using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings;
using System;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT;

namespace mpv_winui.Modules.Common.View;

public sealed partial class WindowStyleManager : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _contentRoot;
    private readonly UISettings _uiSettings = new();
    private SystemBackdropConfiguration? _configurationSource;
    private DesktopAcrylicController? _acrylicController;
    private MicaController? _micaController;
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

        _window.DispatcherQueue.EnsureSystemDispatcherQueue();
        _configurationSource = new SystemBackdropConfiguration
        {
            IsInputActive = true
        };

        ApplyBackdrop();

        UpdateBackdropTheme(_theme);

        _uiSettings.ColorValuesChanged += OnColorValuesChanged;
    }

    /// <summary>Re-applies the backdrop material from the current setting (live toggle support).</summary>
    public void UpdateBackdrop()
    {
        ApplyBackdrop();
        UpdateBackdropTheme(_theme);
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
        _acrylicController?.Dispose();
        _acrylicController = null;
        _micaController?.Dispose();
        _micaController = null;

        switch (AppContext.AppSetting.BackdropType)
        {
            case AppSettings.BackdropType_Mica:
            {
                if (MicaController.IsSupported())
                {
                    _micaController = new MicaController();
                    _micaController?.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
                    _micaController?.SetSystemBackdropConfiguration(_configurationSource);
                    ApplyMicaTint();
                }
                break;
            }
            default:
            {
                if (DesktopAcrylicController.IsSupported())
                {
                    _acrylicController = new DesktopAcrylicController();
                    _acrylicController?.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
                    _acrylicController?.SetSystemBackdropConfiguration(_configurationSource);
                }
                break;
            }
        }
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
        UpdateBackdropTheme(theme);
        UpdateTitleBarColors(theme);
    }

    public void Cleanup()
    {
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
        _acrylicController?.Dispose();
        _acrylicController = null;
        _micaController?.Dispose();
        _micaController = null;
        _configurationSource = null;
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
            else
            {
                UpdateBackdropTheme(_theme);
            }
        });
    }

    private void UpdateContentTheme(ElementTheme theme)
    {
        _contentRoot.RequestedTheme = theme;
    }

    private void UpdateBackdropTheme(ElementTheme theme)
    {
        _configurationSource?.Theme = theme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            _ => SystemBackdropTheme.Light
        };

        _acrylicController?.Kind = DesktopAcrylicKind.Thin;
        _acrylicController?.TintOpacity = GetBackdropTintOpacity();
        _acrylicController?.TintColor = GetBackdropTintColor(theme);
        _acrylicController?.LuminosityOpacity = GetBackdropLuminosityOpacity();

        ApplyMicaTint();
    }

    private void ApplyMicaTint()
    {
        if (_micaController is null)
        {
            return;
        }

        _micaController.TintColor = GetBackdropTintColor(_theme);
        _micaController.TintOpacity = GetBackdropTintOpacity();
        _micaController.LuminosityOpacity = GetBackdropLuminosityOpacity();
    }

    private Color GetBackdropTintColor(ElementTheme theme)
    {
        if (AppContext.AppSetting.ThemeType == AppSettings.ThemeType_Custom)
        {
            if (TryParseColor(AppContext.AppSetting.ThemeAccentColor) is { } custom)
            {
                return custom;
            }

            return theme == ElementTheme.Dark
                ? Color.FromArgb(255, 0x2C, 0x2C, 0x2C)
                : Colors.White;
        }

        // Follow-system / Light / Dark sync the Windows accent color; the
        // UISettings.ColorValuesChanged handler re-runs this when it changes.
        return _uiSettings.GetColorValue(UIColorType.Accent);
    }

    private float GetBackdropTintOpacity()
    {
        var opacity = Math.Clamp(AppContext.AppSetting.ThemeOpacity, 0, 100);
        return 1f - opacity / 100f;
    }

    private float GetBackdropLuminosityOpacity()
    {
        return Math.Clamp(AppContext.AppSetting.ThemeLuminosity, 0, 100) / 100f;
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.Trim().TrimStart('#');
        if (value.Length == 6
            && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        if (value.Length == 8
            && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        }

        return null;
    }

    private void UpdateTitleBarColors(ElementTheme theme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _window.AppWindow.TitleBar;
        //titleBar.ForegroundColor = Colors.White;
        //titleBar.BackgroundColor = Colors.Green;
        titleBar.ButtonForegroundColor = theme == ElementTheme.Dark ? Colors.White : Colors.Black;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        //titleBar.ButtonHoverForegroundColor = Colors.Gainsboro;
        //titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        //titleBar.ButtonPressedForegroundColor = Colors.Gray;
        //titleBar.ButtonPressedBackgroundColor = Colors.LightGreen;

        //titleBar.InactiveForegroundColor = Colors.Gainsboro;
        //titleBar.InactiveBackgroundColor = Colors.SeaGreen;
        //titleBar.ButtonInactiveForegroundColor = Colors.Gainsboro;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
