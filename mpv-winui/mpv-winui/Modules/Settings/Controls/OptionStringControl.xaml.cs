using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.IO;
using Windows.System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringControl : OptionControlBase
{
    private bool _loading;

    public OptionStringControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateDescription(DescriptionText);

            BrowseButton.Content = mpv_winui.AppContext.AppLang.Browse;
            BrowseButton.Visibility = newValue.PickFolder ? Visibility.Visible : Visibility.Collapsed;
            OpenButton.Content = mpv_winui.AppContext.AppLang.Open;
            OpenButton.Visibility = newValue.OpenFolder ? Visibility.Visible : Visibility.Collapsed;
            InputColumn.Width = newValue.PickFolder || newValue.OpenFolder
                ? new GridLength(2, GridUnitType.Star)
                : new GridLength(260);
            InputBox.IsEnabled = newValue.IsEnabled;
            BrowseButton.IsEnabled = newValue.IsEnabled;
            OpenButton.IsEnabled = newValue.IsEnabled;

            _loading = true;
            try
            {
                if (newValue.Getter is Func<object?> func && func() is string value)
                {
                    InputBox.Text = value;
                }
                else
                {
                    InputBox.Text = string.Empty;
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
        if (!(Setting?.AllowEmpty ?? false) && string.IsNullOrWhiteSpace(InputBox.Text))
        {
            return (false, "Value cannot be empty");
        }

        return (true, null);
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        var enabled = Setting?.IsEnabled ?? true;
        InputBox.IsEnabled = enabled;
        BrowseButton.IsEnabled = enabled;
        OpenButton.IsEnabled = enabled;
    }

    private bool TryCommit()
    {
        var (valid, error) = Validate();
        if (!valid)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return false;
        }
        ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        Setting?.Setter?.Invoke(InputBox.Text);
        return true;
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TryCommit();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            TryCommit();
        }
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var owner = mpv_winui.Modules.Settings.SettingsWindow.Instance?.AppWindow.Id
                        ?? mpv_winui.App.Window!.AppWindow.Id;
            var picker = new FolderPicker(owner);
            var folder = await picker.PickSingleFolderAsync();
            if (folder?.Path is string path && !string.IsNullOrEmpty(path))
            {
                InputBox.Text = path;
                TryCommit();
            }
        }
        catch (Exception ex)
        {
            mpv_winui.AppContext.AppLogger.Error(ex, "Failed to pick folder");
        }
    }

    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var path = InputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            await Launcher.LaunchFolderPathAsync(path);
        }
        catch (Exception ex)
        {
            mpv_winui.AppContext.AppLogger.Error(ex, "Failed to open folder");
        }
    }
}
