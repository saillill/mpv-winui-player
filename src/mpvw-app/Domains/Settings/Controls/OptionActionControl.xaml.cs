using Microsoft.UI.Xaml;

namespace mpv_winui.Domains.Settings.Controls;

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

        // The second button is opt-in per row, and the container is recycled, so
        // it is set unconditionally -- including back to Collapsed -- or a row
        // that has one would leave it showing on the next row that reuses this
        // control.
        SecondaryActionButton.Content = newValue.SecondaryActionLabel ?? string.Empty;
        SecondaryActionButton.Visibility = string.IsNullOrEmpty(newValue.SecondaryActionLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Clearing before re-reading is not belt-and-braces, it is the fix for
        // a real leak: these controls are created by a DataTemplate and WinUI
        // recycles the container when the items source is replaced, so the same
        // OptionActionControl instance renders several different rows over one
        // session. Whatever the previous row's result said stayed on screen
        // until the new row's status was read, and because the read happens on
        // a property-changed callback that only fires when Setting is assigned
        // a *different* object, a recycled container whose new Setting compared
        // equal (or whose callback had not run yet) kept the neighbour's line.
        // That is the shape of "clicking 关联媒体文件 puts 文件关联已创建。 under
        // 导出设置 / 导入设置 / 编辑 mpv.conf": a status belonging to one row
        // painted on three others.
        StatusText.Text = string.Empty;
        StatusText.Visibility = Visibility.Collapsed;

        UpdateStatus();
    }

    /// <summary>
    /// Reads this row's own result and shows or hides the line.
    ///
    /// Called from the change path and again after the click, because a
    /// fire-and-forget handler returns before its work finishes: the line has
    /// to be re-read once the action actually completed.
    /// </summary>
    private void UpdateStatus()
    {
        StatusText.Text = Setting?.ActionStatus?.Invoke() ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        var enabled = Setting?.IsEnabled ?? true;
        ActionButton.IsEnabled = enabled;
        SecondaryActionButton.IsEnabled = enabled;
    }

    protected override void OnOptionActionStatusChanged() => UpdateStatus();

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        Setting?.ActionHandler?.Invoke(Setting);
        UpdateStatus();
    }

    private void OnSecondaryActionClick(object sender, RoutedEventArgs e)
    {
        Setting?.SecondaryActionHandler?.Invoke(Setting);
        UpdateStatus();
    }
}
