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
        // Sections are rendered as in-control captions (Option.ShowSectionHeader),
        // which is more reliable than ListView grouping for mixed empty/named groups.
        OptionListView.ItemsSource = OptionList;
    }
}
