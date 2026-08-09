using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using mpv_winui.Modules.FileSystem;
using System;
using System.IO;
using Windows.System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringControl : OptionControlBase
{
    private bool _loading;
    private bool _pathMode;
    private bool _capturing;
    private static OptionStringControl? _activeCapture;

    public OptionStringControl()
    {
        InitializeComponent();
        InputBox.GotFocus += OnInputGotFocus;
        InputBox.LostFocus += OnInputLostFocus;
        Loaded += (_, _) => AttachRootHandlers();
        Unloaded += (_, _) => DetachRootHandlers();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateDescription(DescriptionText);
            InputBox.PlaceholderText = newValue.Placeholder ?? string.Empty;
            _pathMode = newValue.PickFolder || newValue.PickFile || newValue.OpenFolder;

            BrowseButton.Content = mpv_winui.AppContext.AppLang.Browse;
            BrowseButton.Visibility = newValue.PickFolder || newValue.PickFile ? Visibility.Visible : Visibility.Collapsed;
            OpenButton.Content = mpv_winui.AppContext.AppLang.Open;
            OpenButton.Visibility = newValue.OpenFolder ? Visibility.Visible : Visibility.Collapsed;
            ResetButton.Content = mpv_winui.AppContext.AppLang.Reset;
            ResetButton.Visibility = newValue.KeyCaptureEditable ? Visibility.Visible : Visibility.Collapsed;
            InputColumn.Width = newValue.PickFolder || newValue.PickFile || newValue.OpenFolder
                ? new GridLength(2, GridUnitType.Star)
                : new GridLength(newValue.ReadOnly ? 220 : 340);
            InputBox.IsEnabled = newValue.IsEnabled;
            InputBox.IsReadOnly = newValue.ReadOnly;
            BrowseButton.IsEnabled = newValue.IsEnabled;
            OpenButton.IsEnabled = newValue.IsEnabled;
            ResetButton.IsEnabled = newValue.IsEnabled;

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
                UpdatePathDisplay();
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
        Setting?.NotifyChanged();
        UpdatePathDisplay();
        return true;
    }

    private void UpdatePathDisplay()
    {
        if (!_pathMode)
        {
            return;
        }

        var text = InputBox.Text;
        DisplayText.Text = string.IsNullOrEmpty(text) ? InputBox.PlaceholderText : text;
        DisplayText.Visibility = Visibility.Visible;
        InputBox.Visibility = Visibility.Collapsed;
    }

    private void OnDisplayTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        InputBox.Visibility = Visibility.Visible;
        DisplayText.Visibility = Visibility.Collapsed;
        InputBox.Focus(FocusState.Programmatic);
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

    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        if (Setting?.KeyCaptureEditable == true)
        {
            StartKeyCapture();
        }
    }

    private void OnInputLostFocus(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopKeyCapture();
        }
    }

    private void OnResetBindingClick(object sender, RoutedEventArgs e)
    {
        if (Setting is { } option)
        {
            option.KeyCaptureReset?.Invoke(option);
            if (option.Getter is Func<object?> func && func() is string value)
            {
                InputBox.Text = value;
            }
        }
    }

    private void StartKeyCapture()
    {
        if (_capturing)
        {
            return;
        }

        _capturing = true;
        _activeCapture?.StopKeyCapture();
        _activeCapture = this;
        InputBox.PlaceholderText = mpv_winui.AppContext.AppLang.KeyCapturePlaceholder;
    }

    private void StopKeyCapture()
    {
        _capturing = false;
        if (ReferenceEquals(_activeCapture, this))
        {
            _activeCapture = null;
        }
        if (Setting?.Placeholder is { } placeholder)
        {
            InputBox.PlaceholderText = placeholder;
        }
        else
        {
            InputBox.PlaceholderText = string.Empty;
        }
    }

    private void AttachRootHandlers()
    {
        if (XamlRoot?.Content is UIElement root)
        {
            root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnCapturedKey), true);
            root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnCapturedPointer), true);
        }
    }

    private void DetachRootHandlers()
    {
        if (XamlRoot?.Content is UIElement root)
        {
            root.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnCapturedKey));
            root.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnCapturedPointer));
        }
    }

    private void OnCapturedKey(object sender, KeyRoutedEventArgs e)
    {
        if (!_capturing || Setting?.KeyCaptureEditable != true)
        {
            return;
        }

        var newKey = FormatKey(e.Key);
        if (newKey is null)
        {
            return;
        }
        ApplyCapturedKey(newKey);
    }

    private void OnCapturedPointer(object sender, PointerRoutedEventArgs e)
    {
        if (!_capturing || Setting?.KeyCaptureEditable != true)
        {
            return;
        }

        // The click that gave the input box focus must not be captured itself.
        if (e.OriginalSource is DependencyObject source && IsDescendantOf(source, InputBox))
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        string? newKey = null;
        if (properties.IsLeftButtonPressed)
        {
            newKey = "MBTN_LEFT";
        }
        else if (properties.IsRightButtonPressed)
        {
            newKey = "MBTN_RIGHT";
        }
        else if (properties.IsMiddleButtonPressed)
        {
            newKey = "MBTN_MID";
        }
        else if (properties.IsXButton1Pressed)
        {
            newKey = "MBTN_BACK";
        }
        else if (properties.IsXButton2Pressed)
        {
            newKey = "MBTN_FORWARD";
        }

        if (newKey is not null)
        {
            ApplyCapturedKey(newKey);
        }
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void ApplyCapturedKey(string newKey)
    {
        StopKeyCapture();
        if (Setting is { } option && string.IsNullOrEmpty(newKey) == false)
        {
            InputBox.Text = newKey;
            option.KeyCaptureReplaced?.Invoke(option, newKey);
        }
    }

    private static string? FormatKey(VirtualKey key)
    {
        if (key is VirtualKey.Shift
            or VirtualKey.Control
            or VirtualKey.Menu
            or VirtualKey.LeftWindows
            or VirtualKey.RightWindows
            or VirtualKey.CapitalLock
            or VirtualKey.NumberKeyLock
            or VirtualKey.Scroll
            or VirtualKey.LeftShift
            or VirtualKey.RightShift
            or VirtualKey.LeftControl
            or VirtualKey.RightControl
            or VirtualKey.LeftMenu
            or VirtualKey.RightMenu)
        {
            return null;
        }

        return key switch
        {
            VirtualKey.Space => "SPACE",
            VirtualKey.Left => "LEFT",
            VirtualKey.Right => "RIGHT",
            VirtualKey.Up => "UP",
            VirtualKey.Down => "DOWN",
            _ => key.ToString().ToUpperInvariant(),
        };
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var owner = mpv_winui.Modules.Settings.SettingsWindow.Instance?.AppWindow.Id
                        ?? mpv_winui.App.Window!.AppWindow.Id;
            if (Setting?.PickFile == true)
            {
                var filePicker = new FileOpenPicker(owner);
                filePicker.FileTypeFilter.Add(".ttf");
                filePicker.FileTypeFilter.Add(".otf");
                filePicker.FileTypeFilter.Add(".ttc");
                var file = await filePicker.PickSingleFileAsync();
                if (file?.Path is string filePath && !string.IsNullOrEmpty(filePath))
                {
                    InputBox.Text = filePath;
                    TryCommit();
                }
                return;
            }

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
            if (Setting?.PickFile == true)
            {
                path = AppData.Current.ResolveLocalData(Path.Combine("mpv", "fonts"));
            }
            else
            {
                return;
            }
        }
        else if (Setting?.PickFile == true && File.Exists(path))
        {
            path = Path.GetDirectoryName(path) ?? path;
        }

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
