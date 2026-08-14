using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        // Every option renders through the same common templates (the
        // OptionTemplateSelector below); no tier filtering.
        var items = new List<object>(OptionList.Count + 8);
        foreach (var option in OptionList)
        {
            if (!option.IsVisible)
            {
                continue;
            }

            if (option.ShowSectionHeader)
            {
                items.Add(new SectionHeaderItem { Caption = option.Section ?? string.Empty });
            }

            items.Add(option);
        }

        OptionListView.ItemsSource = items;
    }

    /// <summary>Rebuilds the list (e.g. after an option becomes visible/hidden).</summary>
    public void Refresh()
    {
        var offset = GetScrollOffset();
        ApplyItemsSource();
        if (offset > 0)
        {
            DispatcherQueue.TryEnqueue(() => SetScrollOffset(offset));
        }
    }

    /// <summary>Returns the current vertical offset of the options list.</summary>
    public double GetScrollOffset()
    {
        return FindScrollViewer(OptionListView)?.VerticalOffset ?? 0;
    }

    /// <summary>Restores the vertical offset after the list is rebuilt.</summary>
    public void SetScrollOffset(double offset)
    {
        var viewer = FindScrollViewer(OptionListView);
        if (viewer is not null && offset > 0)
        {
            viewer.ChangeView(null, offset, null, disableAnimation: true);
        }
    }

    /// <summary>Scrolls the option with the given key into view (settings search).</summary>
    public void ScrollToOption(string key)
    {
        foreach (var option in OptionList)
        {
            if (option.Key == key)
            {
                OptionListView.ScrollIntoView(option);
                return;
            }
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
            if (FindScrollViewer(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
