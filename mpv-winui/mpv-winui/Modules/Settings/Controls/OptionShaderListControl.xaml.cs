using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.FileSystem;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Ordered GLSL shader list editor: each row can be enabled/disabled, reordered
/// or removed; the serialized value uses ';' separators with a '!' prefix for
/// disabled entries (matching <c>MpvSettings</c>).
/// </summary>
public sealed partial class OptionShaderListControl : OptionControlBase
{
    private readonly ObservableCollection<ShaderEntry> _entries = [];
    private bool _loading;

    public OptionShaderListControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        LabelText.Text = newValue?.Label ?? string.Empty;
        UpdateDescription(DescriptionText);
        AddButton.Content = mpv_winui.AppContext.AppLang.Add;

        _loading = true;
        try
        {
            _entries.Clear();
            var raw = (newValue?.Getter?.Invoke() as string) ?? string.Empty;
            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith('!'))
                {
                    if (part.Length > 1)
                    {
                        _entries.Add(new ShaderEntry(part[1..], false));
                    }
                }
                else
                {
                    _entries.Add(new ShaderEntry(part, true));
                }
            }
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        var enabled = Setting?.IsEnabled ?? true;
        ShaderItems.IsEnabled = enabled;
        AddButton.IsEnabled = enabled;
    }

    private void Commit()
    {
        if (_loading || Setting is null)
        {
            return;
        }

        var value = string.Join(';', _entries.Select(e => (e.Enabled ? string.Empty : "!") + e.Path));
        Setting.Setter?.Invoke(value);
        Setting.NotifyChanged();
    }

    private void Entry_Changed(object sender, RoutedEventArgs e) => Commit();

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShaderEntry entry })
        {
            return;
        }

        var index = _entries.IndexOf(entry);
        if (index > 0)
        {
            _entries.Move(index, index - 1);
            Commit();
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShaderEntry entry })
        {
            return;
        }

        var index = _entries.IndexOf(entry);
        if (index >= 0 && index < _entries.Count - 1)
        {
            _entries.Move(index, index + 1);
            Commit();
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ShaderEntry entry })
        {
            _entries.Remove(entry);
            Commit();
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = await FilePickerHelper.PickSingleFileAsync(picker =>
            {
                picker.FileTypeFilter.Add(".glsl");
                picker.FileTypeFilter.Add(".vs");
                picker.FileTypeFilter.Add(".fs");
            });
            if (file is null)
            {
                return;
            }

            _entries.Add(new ShaderEntry(file.Path, true));
            Commit();
        }
        catch (Exception ex)
        {
            mpv_winui.AppContext.AppLogger.Error(ex, "shader picker failed");
        }
    }
}
