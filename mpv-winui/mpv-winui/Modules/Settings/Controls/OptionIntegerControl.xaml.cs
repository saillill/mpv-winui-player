using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionIntegerControl : OptionControlBase
{
    private bool _loading;

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

            _loading = true;
            try
            {
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
            finally
            {
                _loading = false;
            }
        }
    }

    public override (bool IsValid, string? ErrorMessage) Validate()
    {
        var val = NumberBox.Value;
        if (Setting is not null)
        {
            if (Setting.Min.HasValue && val < Setting.Min.Value)
            {
                return (false, $"Minimum value is {Setting.Min.Value}");
            }

            if (Setting.Max.HasValue && val > Setting.Max.Value)
            {
                return (false, $"Maximum value is {Setting.Max.Value}");
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

    private void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading)
        {
            TryCommit();
        }
    }

    private void OnLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TryCommit();
    }
}
