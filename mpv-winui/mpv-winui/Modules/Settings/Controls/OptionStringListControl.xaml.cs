using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringListControl : OptionControlBase
{
    private const string CustomKey = "__custom__";

    private bool _loading;
    private List<OptionChoice> _choices = [];
    private string _lastCustom = string.Empty;

    public OptionStringListControl()
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
        Combo.IsEnabled = newValue.IsEnabled;

        _loading = true;
        try
        {
            _choices = newValue.ChoicesProvider?.Invoke()?.ToList()
                       ?? newValue.Choices?.ToList()
                       ?? [];
            if (_choices.Count == 0 && newValue.Options is not null)
            {
                _choices = newValue.Options.Select(o => new OptionChoice(o, o)).ToList();
            }
            if (newValue.AllowCustom)
            {
                _choices.Add(new OptionChoice(CustomKey, mpv_winui.AppContext.AppLang.OptionValueCustom));
            }

            Combo.Items.Clear();
            foreach (var choice in _choices)
            {
                Combo.Items.Add(new ComboBoxItem { Content = choice.Label, Tag = choice.Value });
            }

            if (newValue.Getter is Func<object?> func && func() is string current)
            {
                var index = FindPresetIndex(current);
                if (index >= 0)
                {
                    Combo.SelectedIndex = index;
                    CustomInput.Visibility = Visibility.Collapsed;
                    InputColumn.Width = new GridLength(260);
                }
                else
                {
                    if (!newValue.AllowCustom)
                    {
                        Combo.SelectedIndex = _choices.Count > 0 ? 0 : -1;
                        CustomInput.Visibility = Visibility.Collapsed;
                        InputColumn.Width = new GridLength(260);
                        return;
                    }
                    _lastCustom = current;
                    SelectCustom(showAndFocus: false);
                    CustomInput.Text = current;
                }
            }
        }
        finally
        {
            _loading = false;
        }
    }

    public override (bool IsValid, string? ErrorMessage) Validate() => (true, null);

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        var enabled = Setting?.IsEnabled ?? true;
        Combo.IsEnabled = enabled;
        CustomInput.IsEnabled = enabled;
    }

    private int FindPresetIndex(string value)
    {
        for (var i = 0; i < _choices.Count; i++)
        {
            if (_choices[i].Value == CustomKey)
            {
                continue;
            }
            if (string.Equals(_choices[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private void SelectCustom(bool showAndFocus)
    {
        var hasCustom = false;
        for (var i = 0; i < _choices.Count; i++)
        {
            if (_choices[i].Value == CustomKey)
            {
                hasCustom = true;
                Combo.SelectedIndex = i;
                break;
            }
        }
        if (!hasCustom)
        {
            return;
        }
        CustomInput.Visibility = Visibility.Visible;
        InputColumn.Width = new GridLength(260);
        if (showAndFocus)
        {
            CustomInput.Focus(FocusState.Programmatic);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Combo.SelectedItem is not ComboBoxItem { Tag: string val })
        {
            return;
        }

        if (val == CustomKey)
        {
            CustomInput.Visibility = Visibility.Visible;
            InputColumn.Width = new GridLength(260);
            CustomInput.Text = _lastCustom;
            CustomInput.Focus(FocusState.Programmatic);
        }
        else
        {
            CustomInput.Visibility = Visibility.Collapsed;
            InputColumn.Width = new GridLength(260);
            Setting?.Setter?.Invoke(val);
            Setting?.NotifyChanged();
        }
    }

    private void OnCustomKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitCustom();
        }
    }

    private void OnCustomLostFocus(object sender, RoutedEventArgs e)
    {
        CommitCustom();
    }

    private void CommitCustom()
    {
        var text = CustomInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _lastCustom = text;
        Setting?.Setter?.Invoke(text);
        Setting?.NotifyChanged();
    }
}
