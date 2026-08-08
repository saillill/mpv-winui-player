using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Settings.Controls;

public abstract class OptionControlBase : UserControl
{
    /// <summary>Shows the option description below the title (Windows Settings style).</summary>
    protected void UpdateDescription(TextBlock? block)
    {
        if (block is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Setting?.Description))
        {
            block.Text = string.Empty;
            block.Visibility = Visibility.Collapsed;
            return;
        }

        block.Text = Setting.Description;
        block.Visibility = Visibility.Visible;
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
