using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using mpv_winui.Modules.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringControl : OptionControlBase
{
    private bool _loading;
    private bool _pathMode;
    private bool _capturing;
    private static OptionStringControl? _activeCapture;
    private readonly HashSet<string> _pendingModifiers = new(StringComparer.OrdinalIgnoreCase);
    private HIMC _imeContext;

    public OptionStringControl()
    {
        InitializeComponent();
        Tapped += OnRowTapped;
        Loaded += (_, _) => AttachRootHandlers();
        Unloaded += (_, _) =>
        {
            DetachRootHandlers();
            if (_capturing)
            {
                StopKeyCapture();
            }
        };
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
            InputColumn.Width = new GridLength(260);
            InputBox.IsEnabled = newValue.IsEnabled;
            InputBox.IsReadOnly = newValue.ReadOnly;
            BrowseButton.IsEnabled = newValue.IsEnabled;
            OpenButton.IsEnabled = newValue.IsEnabled;
            ResetButton.IsEnabled = newValue.IsEnabled;
            if (newValue.KeyCaptureEditable)
            {
                InputBox.Visibility = Visibility.Collapsed;
                DisplayText.Visibility = Visibility.Visible;
                DisplayText.MaxWidth = 260;
                DisplayText.IsTextSelectionEnabled = false;
                DisplayText.Text = newValue.Getter is Func<object?> keyFunc && keyFunc() is string keyValue
                    ? keyValue
                    : string.Empty;
            }
            else
            {
                InputBox.Visibility = Visibility.Visible;
            }

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
            return (false, mpv_winui.AppContext.AppLang.ValidationValueNotEmpty);
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
        if (Setting?.KeyCaptureEditable == true)
        {
            StartKeyCapture();
        }
        else
        {
            InputBox.Visibility = Visibility.Visible;
            DisplayText.Visibility = Visibility.Collapsed;
            InputBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnRowTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (Setting?.KeyCaptureEditable == true
            && !(e.OriginalSource is DependencyObject source && IsDescendantOf(source, ResetButton)))
        {
            StartKeyCapture();
        }
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

    private void OnResetBindingClick(object sender, RoutedEventArgs e)
    {
        if (Setting is { } option)
        {
            option.KeyCaptureReset?.Invoke(option);
            if (option.Getter is Func<object?> func && func() is string value)
            {
                DisplayText.Text = value;
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
        _pendingModifiers.Clear();
        DisplayText.Text = mpv_winui.AppContext.AppLang.KeyCapturePlaceholder;
        DisableIme(true);
    }

    private void StopKeyCapture()
    {
        _capturing = false;
        if (ReferenceEquals(_activeCapture, this))
        {
            _activeCapture = null;
        }
        DisplayText.Text = Setting?.Getter is Func<object?> func && func() is string value
            ? value
            : string.Empty;
        DisableIme(false);
    }

    /// <summary>
    /// Temporarily detaches the IME context while capturing, so keyboard
    /// input such as "j"/"k" is reported as a plain key and never converted
    /// into Chinese/Japanese text. The previous context is restored on stop.
    /// </summary>
    private void DisableIme(bool disable)
    {
        try
        {
            var windowId = mpv_winui.Modules.Settings.SettingsWindow.Instance?.AppWindow.Id
                ?? mpv_winui.App.Window!.AppWindow.Id;
            var hwnd = new HWND(Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId));
            if (hwnd == HWND.Null)
            {
                return;
            }

            if (disable)
            {
                _imeContext = PInvoke.ImmGetContext(hwnd);
                PInvoke.ImmAssociateContext(hwnd, HIMC.Null);
            }
            else if (_imeContext != HIMC.Null)
            {
                PInvoke.ImmAssociateContext(hwnd, _imeContext);
                _imeContext = HIMC.Null;
            }
        }
        catch
        {
            // IME toggling is best-effort; capture still works without it.
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

        var modifier = ModifierName(e.Key);
        if (modifier is not null)
        {
            if (!_pendingModifiers.Add(modifier))
            {
                _pendingModifiers.Remove(modifier);
            }
            DisplayText.Text = BuildCombo(_pendingModifiers, null);
            return;
        }

        var newKey = FormatKey(e.Key);
        if (newKey is null)
        {
            return;
        }
        ApplyCapturedKey(BuildCombo(_pendingModifiers, newKey));
    }

    private void OnCapturedPointer(object sender, PointerRoutedEventArgs e)
    {
        if (!_capturing || Setting?.KeyCaptureEditable != true)
        {
            return;
        }

        // The click that started the capture must not be captured itself.
        if (e.OriginalSource is DependencyObject source
            && (IsDescendantOf(source, InputBox) || IsDescendantOf(source, DisplayText)))
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
            DisplayText.Text = newKey;
            option.KeyCaptureReplaced?.Invoke(option, newKey);
        }
    }

    private static string? ModifierName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => "Ctrl",
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => "Shift",
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => "Alt",
            VirtualKey.LeftWindows or VirtualKey.RightWindows => "Meta",
            _ => null,
        };
    }

    private static string BuildCombo(IEnumerable<string> modifiers, string? key)
    {
        var parts = new List<string>();
        foreach (var modifier in modifiers)
        {
            if (!parts.Contains(modifier))
            {
                parts.Add(modifier);
            }
        }
        if (key is not null)
        {
            parts.Add(key);
        }
        return string.Join("+", parts).ToLowerInvariant();
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
            // mpv key names: main keyboard digits are the bare digit, and the
            // keypad uses the KP prefix (see mpv --input-keylist). VirtualKey
            // ToString() would produce the invalid NUMBER*/NUMBERPAD* names.
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            VirtualKey.NumberPad0 => "KP0",
            VirtualKey.NumberPad1 => "KP1",
            VirtualKey.NumberPad2 => "KP2",
            VirtualKey.NumberPad3 => "KP3",
            VirtualKey.NumberPad4 => "KP4",
            VirtualKey.NumberPad5 => "KP5",
            VirtualKey.NumberPad6 => "KP6",
            VirtualKey.NumberPad7 => "KP7",
            VirtualKey.NumberPad8 => "KP8",
            VirtualKey.NumberPad9 => "KP9",
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
