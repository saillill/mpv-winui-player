using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Video page of the quick-control panel: four narrow horizontal slider
/// cards (label | slider | value | reset) followed by the filter/zoom rows.
/// </summary>
public sealed partial class QuickControlPanel
{
    private void BuildPanelVideo(StackPanel root)
    {
        var lang = AppContext.AppLang;

        root.Children.Add(PanelOptionCard(PanelSliderRow(lang.PanelBrightness, "brightness", -100, 100, 1)));
        root.Children.Add(PanelOptionCard(PanelSliderRow(lang.PanelContrast, "contrast", -100, 100, 1)));
        root.Children.Add(PanelOptionCard(PanelSliderRow(lang.PanelSaturation, "saturation", -100, 100, 1)));
        root.Children.Add(PanelOptionCard(PanelSliderRow(lang.PanelHue, "hue", -100, 100, 1)));

        var sharp = PanelToggleButton(lang.PanelSharpen, "\uF47D",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[unsharp=5:5:1.0]" : ""));
        var blur = PanelToggleButton(lang.PanelBlur, "\uF8FB",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[gblur=sigma=1.0]" : ""));
        var post = PanelToggleButton(lang.PanelPost, "\uF489",
            on => MediaPlayer?.Command("set", "deband", on ? "yes" : "no"));
        var deinterlace = PanelToggleButton(lang.SettingsDeinterlace, "\uF2BE",
            on => MediaPlayer?.Command("set", "deinterlace", on ? "yes" : "no"));

        // Centered inside the stretched last card so the taller button area
        // fills the remaining 400px panel height instead of leaving a gap.
        var effects = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
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
