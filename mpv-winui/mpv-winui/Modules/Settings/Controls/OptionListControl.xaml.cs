using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionListControl : UserControl
{
    public OptionListControl()
    {
        InitializeComponent();
    }

    public bool ShowHeaders
    {
        get => (bool)GetValue(ShowHeadersProperty);
        set => SetValue(ShowHeadersProperty, value);
    }

    public static readonly DependencyProperty ShowHeadersProperty = DependencyProperty.Register(
        nameof(ShowHeaders),
        typeof(bool),
        typeof(OptionListControl),
        new PropertyMetadata(true, (d, e) =>
        {
            if (d is OptionListControl self)
            {
                self.ApplyItemsSource();
            }
        }));

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            return;
        }

        if (args.Item is not Option option)
        {
            return;
        }

        if (args.ItemContainer.ContentTemplateRoot is OptionControlBase control)
        {
            control.Setting = option;
        }
        args.Handled = true;
    }

    public List<Option> OptionList
    {
        get => (List<Option>)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
            nameof(OptionList),
            typeof(List<Option>),
            typeof(OptionListControl),
            new PropertyMetadata((List<Option>)[], OnOptionListChanged)
            );

    private static void OnOptionListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OptionListControl self)
        {
            self.ApplyItemsSource();
        }
    }

    private void ApplyItemsSource()
    {
        if (OptionList is { Count: > 0 } options && (ShowHeaders || options.Any(o => !string.IsNullOrEmpty(o.Section))))
        {
            var groups = new List<OptionGroup>();
            foreach (var option in options)
            {
                var key = ShowHeaders ? option.Category : option.Section ?? string.Empty;
                var group = groups.FirstOrDefault(g => g.Key == key);
                if (group is null)
                {
                    group = new OptionGroup { Key = key };
                    groups.Add(group);
                }
                group.Add(option);
            }

            var viewSource = new CollectionViewSource { IsSourceGrouped = true, Source = groups };
            OptionListView.ItemsSource = viewSource.View;
        }
        else
        {
            OptionListView.ItemsSource = OptionList;
        }
    }
}
