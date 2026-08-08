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

        if (args.Item is SectionHeaderItem)
        {
            args.ItemContainer.Style = (Style)Resources["SectionHeaderItemStyle"];
            args.Handled = true;
            return;
        }

        if (args.Item is not Option option)
        {
            return;
        }

        args.ItemContainer.Style = (Style)Resources["OptionListViewItemStyle"];
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
        // Sections are rendered as separate list rows (SectionHeaderItem),
        // so the caption keeps its own typography and is not merged into a card.
        var items = new List<object>(OptionList.Count + 8);
        foreach (var option in OptionList)
        {
            if (option.ShowSectionHeader)
            {
                items.Add(new SectionHeaderItem { Caption = option.Section ?? string.Empty });
            }
            items.Add(option);
        }
        OptionListView.ItemsSource = items;
    }
}
