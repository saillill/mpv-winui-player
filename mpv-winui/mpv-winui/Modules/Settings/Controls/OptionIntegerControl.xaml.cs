using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionIntegerControl : OptionControlBase
{
    public OptionIntegerControl()
    {
        InitializeComponent();

        NumberBox.SmallChange = 1;
        NumberBox.LargeChange = 10;
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateDescription(DescriptionText);
            NumberBox.IsEnabled = newValue.IsEnabled;

            if (newValue.Min.HasValue)
            {
                NumberBox.Minimum = newValue.Min.Value;
            }

            if (newValue.Max.HasValue)
            {
                NumberBox.Maximum = newValue.Max.Value;
            }

            if (newValue.Step.HasValue)
            {
                NumberBox.SmallChange = newValue.Step.Value;
            }

            if (newValue.Getter is Func<object?> func && func() is double value)
            {
                NumberBox.Value = value;
            }
            else
            {
                NumberBox.Value = 0;
            }
        }
    }

    public override (bool IsValid, string? ErrorMessage) Validate()
    {
        var val = NumberBox.Value;
        if (double.IsNaN(val))
        {
            return (false, mpv_winui.AppContext.AppLang.ValidationValueNotEmpty);
        }

        if (Setting is not null)
        {
            if (Setting.Min.HasValue && val < Setting.Min.Value)
            {
                return (false, string.Format(mpv_winui.AppContext.AppLang.ValidationMinValue, Setting.Min.Value));
            }

            if (Setting.Max.HasValue && val > Setting.Max.Value)
            {
                return (false, string.Format(mpv_winui.AppContext.AppLang.ValidationMaxValue, Setting.Max.Value));
            }
        }
        return (true, null);
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        NumberBox.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private bool TryCommit()
    {
        var (valid, error) = Validate();
        if (!valid)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return false;
        }
        ErrorText.Visibility = Visibility.Collapsed;
        Setting?.Setter?.Invoke((int)NumberBox.Value);
        Setting?.NotifyChanged();
        return true;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            TryCommit();
        }
    }

    private void OnLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TryCommit();
    }
}
