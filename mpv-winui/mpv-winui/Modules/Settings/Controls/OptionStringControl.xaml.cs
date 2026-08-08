using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;

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
            UpdateHelpButton(HelpButton);

            BrowseButton.Content = mpv_winui.AppContext.AppLang.Browse;
            BrowseButton.Visibility = newValue.PickFolder ? Visibility.Visible : Visibility.Collapsed;

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
            var picker = new FolderPicker(mpv_winui.App.Window!.AppWindow.Id);
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
}
