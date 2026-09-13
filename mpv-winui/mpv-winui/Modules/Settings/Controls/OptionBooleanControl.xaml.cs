using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionBooleanControl : OptionControlBase
{
    private bool _loading;

    public OptionBooleanControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateDescription(DescriptionText);
            // Bare Windows-settings switch: the control carries no on/off
            // captions at all — the track alone, right-aligned, does the job.
            ToggleSwitch.IsEnabled = newValue.IsEnabled;

            _loading = true;
            try
            {
                if (newValue.Getter is Func<object?> func)
                {
                    ToggleSwitch.IsOn = func() is bool value && value;
                }
            }
            finally
            {
                _loading = false;
            }
        }
    }
    public override (bool IsValid, string? ErrorMessage) Validate() => (true, null);

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        ToggleSwitch.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loading && Setting?.Setter is not null)
        {
            Setting?.Setter?.Invoke(ToggleSwitch.IsOn);
            Setting?.NotifyChanged();
        }
    }
}
