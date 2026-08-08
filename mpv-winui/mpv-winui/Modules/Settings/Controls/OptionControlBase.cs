using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace mpv_winui.Modules.Settings.Controls;

public abstract class OptionControlBase : UserControl
{
    /// <summary>Localized badge text for the option source (MPV / plugin / app / hybrid).</summary>
    protected void UpdateSourceBadge(TextBlock? badgeText)
    {
        if (badgeText is null)
        {
            return;
        }

        var text = Setting?.Source switch
        {
            OptionSource.Plugin => mpv_winui.AppContext.AppLang.OptionSourcePlugin,
            OptionSource.App => mpv_winui.AppContext.AppLang.OptionSourceApp,
            OptionSource.Hybrid => mpv_winui.AppContext.AppLang.OptionSourceHybrid,
            _ => mpv_winui.AppContext.AppLang.OptionSourceMpv,
        };
        badgeText.Text = text;
    }

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

    /// <summary>Shows the yellow "may be ineffective" warning below the description.</summary>
    protected void UpdateWarning(TextBlock? block)
    {
        if (block is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Setting?.Warning))
        {
            block.Text = string.Empty;
            block.Visibility = Visibility.Collapsed;
            return;
        }

        block.Text = Setting.Warning;
        block.Visibility = Visibility.Visible;
    }

    /// <summary>Called when the option's Warning or IsEnabled changes; derived controls refresh their state.</summary>
    protected virtual void OnOptionStateChanged()
    {
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
            control.DetachOption((Option?)e.OldValue);
            control.AttachOption((Option?)e.NewValue);
            control.OnSettingChanged((Option?)e.OldValue, (Option?)e.NewValue);
            control.OnOptionStateChanged();
        }
    }

    protected virtual void OnSettingChanged(Option? oldValue, Option? newValue)
    {
    }

    private void AttachOption(Option? option)
    {
        if (option is not null)
        {
            option.PropertyChanged += OnOptionPropertyChanged;
        }
    }

    private void DetachOption(Option? option)
    {
        if (option is not null)
        {
            option.PropertyChanged -= OnOptionPropertyChanged;
        }
    }

    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Option.Warning) || e.PropertyName == nameof(Option.IsEnabled))
        {
            OnOptionStateChanged();
        }
    }

    public virtual (bool IsValid, string? ErrorMessage) Validate() => (true, null);
}
