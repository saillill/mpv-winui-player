using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Player;

/// <summary>Video page of the quick-control panel (picture sliders and filters).</summary>
public sealed partial class QuickControlPanel
{
    private Slider? _panelBrightnessSlider;
    private Slider? _panelContrastSlider;
    private Slider? _panelSaturationSlider;
    private Slider? _panelHueSlider;

    private void BuildPanelVideo(StackPanel root)
    {
        var lang = AppContext.AppLang;
        _panelBrightnessSlider = PanelPropertySlider("brightness", -100, 100, 1, lang.PanelBrightness);
        _panelContrastSlider = PanelPropertySlider("contrast", -100, 100, 1, lang.PanelContrast);
        _panelSaturationSlider = PanelPropertySlider("saturation", -100, 100, 1, lang.PanelSaturation);
        _panelHueSlider = PanelPropertySlider("hue", -100, 100, 1, lang.PanelHue);

        root.Children.Add(PanelOptionCard(PanelSection(
            lang.PanelBrightness,
            PanelSliderWithReset(_panelBrightnessSlider, PanelResetButton("brightness", _panelBrightnessSlider)))));
        root.Children.Add(PanelOptionCard(PanelSection(
            lang.PanelContrast,
            PanelSliderWithReset(_panelContrastSlider, PanelResetButton("contrast", _panelContrastSlider)))));
        root.Children.Add(PanelOptionCard(PanelSection(
            lang.PanelSaturation,
            PanelSliderWithReset(_panelSaturationSlider, PanelResetButton("saturation", _panelSaturationSlider)))));
        root.Children.Add(PanelOptionCard(PanelSection(
            lang.PanelHue,
            PanelSliderWithReset(_panelHueSlider, PanelResetButton("hue", _panelHueSlider)))));

        var sharp = PanelToggleButton(lang.PanelSharpen, "\uF47D",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[unsharp=5:5:1.0]" : ""));
        var blur = PanelToggleButton(lang.PanelBlur, "\uF8FB",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[gblur=sigma=1.0]" : ""));
        var post = PanelToggleButton(lang.PanelPost, "\uF489",
            on => MediaPlayer?.Command("set", "deband", on ? "yes" : "no"));
        var deinterlace = PanelToggleButton(lang.SettingsDeinterlace, "\uF2BE",
            on => MediaPlayer?.Command("set", "deinterlace", on ? "yes" : "no"));

        var effects = new StackPanel { Spacing = 8 };
        effects.Children.Add(PanelButtonRow(sharp, blur, post, deinterlace));
        effects.Children.Add(PanelButtonRow(
            PanelIconButton(lang.PanelRotate, "\uF13E",
                () => MediaPlayer?.Command(["cycle-values", "video-rotate", "90", "180", "270", "0"])),
            PanelIconButton(lang.PanelZoomIn, "\uF8C5",
                () => MediaPlayer?.Command("add", "video-zoom", "0.1")),
            PanelIconButton(lang.PanelZoomOut, "\uF8C7",
                () => MediaPlayer?.Command("add", "video-zoom", "-0.1")),
            PanelIconButton(lang.PanelZoomReset, "\uEE8D",
                () => MediaPlayer?.Command("set", "video-zoom", "0"))));
        root.Children.Add(PanelOptionCard(effects));
    }
}
