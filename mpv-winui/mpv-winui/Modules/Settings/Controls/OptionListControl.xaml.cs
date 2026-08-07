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
            if (e.NewValue is List<Option> options && options.Count > 0)
            {
                var groups = new List<OptionGroup>();
                foreach (var option in options)
                {
                    var group = groups.FirstOrDefault(g => g.Key == option.Category);
                    if (group is null)
                    {
                        group = new OptionGroup { Key = option.Category };
                        groups.Add(group);
                    }
                    group.Add(option);
                }

                var viewSource = new CollectionViewSource { IsSourceGrouped = true, Source = groups };
                self.OptionListView.ItemsSource = viewSource.View;
            }
            else
            {
                self.OptionListView.ItemsSource = null;
            }
        }
    }
}
