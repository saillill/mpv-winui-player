using Microsoft.UI.Xaml;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionActionControl : OptionControlBase
{
    public OptionActionControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is null)
        {
            return;
        }

        LabelText.Text = newValue.Label;
        UpdateDescription(DescriptionText);
        ActionButton.Content = newValue.ActionLabel ?? newValue.Label;
        ActionButton.Visibility = newValue.ActionKind == OptionActionKind.None
            ? Visibility.Collapsed
            : Visibility.Visible;
        StatusText.Text = newValue.ActionStatus?.Invoke() ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        ActionButton.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        Setting?.ActionHandler?.Invoke(Setting);
        StatusText.Text = Setting?.ActionStatus?.Invoke() ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
