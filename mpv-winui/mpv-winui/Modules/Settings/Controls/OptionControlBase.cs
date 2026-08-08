using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace mpv_winui.Modules.Settings.Controls;

public abstract class OptionControlBase : UserControl
{
    /// <summary>Shows or updates the info (i) button used to explain uncommon options.</summary>
    protected void UpdateHelpButton(Button? button)
    {
        if (button is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Setting?.Description))
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        button.Visibility = Visibility.Visible;
        var tip = new TextBlock
        {
            Text = Setting.Description,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380
        };
        ToolTipService.SetToolTip(button, tip);
    }

    public Option? Setting
    {
        get => (Option?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty SettingProperty =
        DependencyProperty.Register(nameof(Setting), typeof(Option),
            typeof(OptionControlBase), new PropertyMetadata(null, OnSettingChanged));

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OptionControlBase control)
        {
            control.OnSettingChanged((Option?)e.OldValue, (Option?)e.NewValue);
        }
    }

    protected virtual void OnSettingChanged(Option? oldValue, Option? newValue)
    {
    }

    public virtual (bool IsValid, string? ErrorMessage) Validate() => (true, null);
}
