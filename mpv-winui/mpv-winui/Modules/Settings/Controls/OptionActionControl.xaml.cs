using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionActionControl : OptionControlBase
{
    private bool _listening;

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
        CaptureDisplay.Visibility = newValue.ActionKind == OptionActionKind.KeyCapture
            ? Visibility.Visible
            : Visibility.Collapsed;
        CaptureText.Text = mpv_winui.AppContext.AppLang.KeyCapturePlaceholder;
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
        if (Setting?.ActionKind == OptionActionKind.KeyCapture)
        {
            ToggleListening();
            return;
        }

        Setting?.ActionHandler?.Invoke(Setting);
        StatusText.Text = Setting?.ActionStatus?.Invoke() ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ToggleListening()
    {
        if (_listening)
        {
            StopListening();
        }
        else
        {
            StartListening();
        }
    }

    private void StartListening()
    {
        if (XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        _listening = true;
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnCapturedKey), true);
        root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnCapturedPointer), true);
        ActionButton.Content = mpv_winui.AppContext.AppLang.KeyCaptureStop;
        CaptureText.Text = mpv_winui.AppContext.AppLang.KeyCapturePlaceholder;
    }

    private void StopListening()
    {
        if (XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        root.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnCapturedKey));
        root.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnCapturedPointer));
        _listening = false;
        ActionButton.Content = mpv_winui.AppContext.AppLang.KeyCaptureStart;
    }

    private void OnCapturedKey(object sender, KeyRoutedEventArgs e)
    {
        if (!_listening)
        {
            return;
        }

        var key = FormatKey(e.Key);
        if (key is null)
        {
            return;
        }
        CaptureText.Text = key;
        StopListening();
    }

    private void OnCapturedPointer(object sender, PointerRoutedEventArgs e)
    {
        if (!_listening)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            CaptureText.Text = "鼠标左键";
        }
        else if (properties.IsRightButtonPressed)
        {
            CaptureText.Text = "鼠标右键";
        }
        else if (properties.IsMiddleButtonPressed)
        {
            CaptureText.Text = "鼠标中键";
        }
        else if (properties.IsXButton1Pressed)
        {
            CaptureText.Text = "鼠标侧键1";
        }
        else if (properties.IsXButton2Pressed)
        {
            CaptureText.Text = "鼠标侧键2";
        }
        else
        {
            return;
        }

        StopListening();
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
            VirtualKey.Space => "Space",
            VirtualKey.Left => "←",
            VirtualKey.Right => "→",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            _ => key.ToString(),
        };
    }
}
