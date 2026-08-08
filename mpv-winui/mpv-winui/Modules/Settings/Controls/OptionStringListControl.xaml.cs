using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringListControl : OptionControlBase
{
    private bool _loading;

    public OptionStringListControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateHelpButton(HelpButton);

            _loading = true;
            Combo.Items.Clear();
            try
            {
                if (newValue.Choices is { Count: > 0 })
                {
                    foreach (var choice in newValue.Choices)
                    {
                        Combo.Items.Add(new ComboBoxItem { Content = choice.Label, Tag = choice.Value });
                    }
                }
                else if (newValue.Options is not null)
                {
                    foreach (var opt in newValue.Options)
                    {
                        Combo.Items.Add(new ComboBoxItem { Content = opt, Tag = opt });
                    }
                }

                if (newValue.Getter is Func<object?> func && func() is string current)
                {
                    for (var i = 0; i < Combo.Items.Count; i++)
                    {
                        if (Combo.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), current, StringComparison.Ordinal))
                        {
                            Combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _loading = false;
            }
        }
    }

    public override (bool IsValid, string? ErrorMessage) Validate() => (true, null);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && Setting?.Setter is not null && Combo.SelectedItem is ComboBoxItem { Tag: string val })
        {
            Setting?.Setter?.Invoke(val);
        }
    }
}
