using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionStringListControl : OptionControlBase
{
    private bool _loading;
    private List<OptionChoice> _choices = [];

    public OptionStringListControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is not null)
        {
            LabelText.Text = newValue.Label;
            UpdateDescription(DescriptionText);
            InputBox.IsEnabled = newValue.IsEnabled;

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

                InputBox.ItemsSource = _choices;
                if (newValue.Getter is Func<object?> func && func() is string current)
                {
                    InputBox.Text = DisplayText(current);
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
        InputBox.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_loading || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var text = sender.Text?.Trim() ?? string.Empty;
        sender.ItemsSource = _choices
            .Where(c => c.Label.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || c.Value.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is OptionChoice choice)
        {
            _loading = true;
            try
            {
                sender.Text = choice.Label;
            }
            finally
            {
                _loading = false;
            }
            Commit(choice.Value);
        }
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is OptionChoice choice)
        {
            Commit(choice.Value);
        }
        else
        {
            Commit(sender.Text?.Trim() ?? string.Empty);
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        Commit(((AutoSuggestBox)sender).Text?.Trim() ?? string.Empty);
    }

    private string DisplayText(string value)
    {
        var choice = _choices.FirstOrDefault(c => string.Equals(c.Value, value, StringComparison.Ordinal))
                  ?? _choices.FirstOrDefault(c => string.Equals(c.Label, value, StringComparison.OrdinalIgnoreCase));
        return choice?.Label ?? value;
    }

    private void Commit(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var choice = _choices.FirstOrDefault(c => string.Equals(c.Label, raw, StringComparison.OrdinalIgnoreCase))
                  ?? _choices.FirstOrDefault(c => string.Equals(c.Value, raw, StringComparison.OrdinalIgnoreCase));
        Setting?.Setter?.Invoke(choice?.Value ?? raw);
    }
}
