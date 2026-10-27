using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.View;
using mpv_winui.Infrastructure.Motion;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionCheckListControl : OptionControlBase
{
    private bool _expanded;

    public OptionCheckListControl()
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
        ApplyButton.Content = newValue.CheckApplyLabel ?? mpv_winui.AppContext.AppLang.Apply;
        ApplyButton.Visibility = newValue.CheckApplyHandler is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateStatus();
        BuildCheckList();
        ApplyEnabledState();
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        ApplyEnabledState();
    }

    /// <summary>
    /// Reflects the row's enabled state on the parts the user can interact with.
    ///
    /// <para>This control was the only option row that never applied
    /// <c>IsEnabled</c> to anything: every other control disables its own input,
    /// so a disabled row of theirs looks disabled. This card stayed fully
    /// painted while <see cref="OnCardTapped"/> silently returned on the same
    /// flag, which is the worst of both -- a card that swallows every click and
    /// never says why. The guard and the appearance now come from one
    /// place.</para>
    /// </summary>
    private void ApplyEnabledState()
    {
        var enabled = Setting?.IsEnabled ?? true;

        // The boxes rather than their containers: CheckItemsControl is a
        // StackPanel, and a Panel has no IsEnabled for children to inherit, so
        // disabling it would have silently done nothing.
        foreach (var box in _boxes)
        {
            box.IsEnabled = enabled;
        }

        ApplyButton.IsEnabled = enabled;
        // The chevron is the card's only affordance, so dimming it is what says
        // "this does not open" before the click is wasted.
        ExpandIcon.Opacity = enabled ? 1.0 : 0.4;
    }

    /// <summary>The boxes currently on screen, so their enabled state can be set
    /// as a group without walking the visual tree (the ItemsControls hold their
    /// items as plain objects until they are realized).</summary>
    private readonly List<CheckBox> _boxes = [];

    protected override void OnOptionActionStatusChanged() => UpdateStatus();

    /// <summary>
    /// Pulls the apply result from the option and shows or hides the line.
    ///
    /// <para>Called both on change and after Apply, because the handler runs on
    /// the apply click and the result only exists once it has returned.</para>
    /// </summary>
    private void UpdateStatus()
    {
        StatusText.Text = Setting?.ActionStatus?.Invoke() ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>The whole card toggles the panel, matching the layout cards.</summary>
    private void OnCardTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (Setting?.IsEnabled == false)
        {
            return;
        }

        // Clicks inside the expanded panel (checkboxes, Apply) must not
        // collapse the card again.
        if (e.OriginalSource is Microsoft.UI.Xaml.DependencyObject source
            && IsDescendantOf(source, ExpandedPanel))
        {
            return;
        }

        _expanded = !_expanded;
        ExpandIcon.Glyph = _expanded ? "\uE70E" : "\uE70D";

        if (_expanded)
        {
            ExpandedPanel.Visibility = Visibility.Visible;
            MotionBehaviors.RowReveal(ExpandedPanel);
        }
        else
        {
            // Collapse only once the exit has played, so the panel does not
            // disappear from under a running animation.
            MotionBehaviors.RowCollapse(ExpandedPanel, completed: () =>
            {
                if (!_expanded)
                {
                    ExpandedPanel.Visibility = Visibility.Collapsed;
                    MotionBehaviors.Reset(ExpandedPanel);
                }
            });
        }
    }

    private static bool IsDescendantOf(Microsoft.UI.Xaml.DependencyObject? node, Microsoft.UI.Xaml.DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void BuildCheckList()
    {
        CheckItemsControl.Children.Clear();
        _boxes.Clear();
        var items = Setting?.CheckItemsProvider?.Invoke()
            ?? Setting?.CheckItems
            ?? [];
        if (items.Count == 0)
        {
            return;
        }

        // Rows with a secondary description need full width; plain short
        // checkboxes keep the compact wrap layout used by file associations.
        var panelTemplate = items.Any(i => !string.IsNullOrEmpty(i.Description))
            ? TryGetResource<ItemsPanelTemplate>("CheckListPanel", Resources)
            : TryGetResource<ItemsPanelTemplate>("CheckWrapPanel", Resources);

        string? lastGroup = null;
        ItemsControl? groupWrap = null;
        foreach (var item in items)
        {
            if (groupWrap is null || !string.Equals(item.Group, lastGroup, StringComparison.Ordinal))
            {
                lastGroup = item.Group;
                if (!string.IsNullOrEmpty(item.Group))
                {
                    CheckItemsControl.Children.Add(new TextBlock
                    {
                        Text = item.Group,
                        Style = TryGetResource<Style>("CaptionTextBlockStyle", Application.Current.Resources),
                        Foreground = ThemeResource.Brush(this, "TextFillColorSecondaryBrush"),
                        Margin = new Thickness(0, 6, 0, 2),
                    });
                }
                groupWrap = new ItemsControl
                {
                    ItemsPanel = panelTemplate ?? TryGetResource<ItemsPanelTemplate>("CheckWrapPanel", Resources),
                };
                CheckItemsControl.Children.Add(groupWrap);
            }

            var box = new CheckBox
            {
                IsChecked = item.IsChecked,
                Tag = item,
                MinWidth = 132,
            };
            box.Content = BuildCheckContent(item);
            box.Checked += OnItemChecked;
            box.Unchecked += OnItemChecked;
            groupWrap.Items.Add(box);
            _boxes.Add(box);
        }
    }

    private static object BuildCheckContent(OptionCheckItem item)
    {
        if (!string.IsNullOrEmpty(item.Description))
        {
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock
            {
                Text = item.Label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.Description,
                Style = TryGetResource<Style>("CaptionTextBlockStyle", Application.Current.Resources),
                Foreground = ThemeResource.Brush(panel, "TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
            return panel;
        }

        if (string.IsNullOrEmpty(item.Glyph))
        {
            return item.Label;
        }

        var glyphPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        glyphPanel.Children.Add(item.Glyph.StartsWith("F1 ", StringComparison.Ordinal)
            ? new Viewbox
            {
                Width = 14,
                Height = 14,
                // A fresh geometry per row: one instance can belong to a
                // single PathIcon only (see FluentIcons for the lifetime
                // rule), so neither a stored geometry nor a second
                // ConvertValue-produced one is safe to hand out here.
                Child = new PathIcon
                {
                    Data = FluentIcons.Geometry(item.Glyph),
                },
            }
            : new FontIcon { Glyph = item.Glyph, FontSize = 14 });
        glyphPanel.Children.Add(new TextBlock { Text = item.Label, VerticalAlignment = VerticalAlignment.Center });
        return glyphPanel;
    }

    /// <summary>
    /// Safely resolves a resource used by the checklist. Under Release/AOT the
    /// framework resource lookup can surface an unexpected type; a missing or
    /// mistyped resource must not crash the settings page (fall back to a
    /// neutral value and log).
    /// </summary>
    private static T? TryGetResource<T>(string key, ResourceDictionary source) where T : class
    {
        try
        {
            return source[key] as T;
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "option checklist resource missing, key={}", key);
            return null;
        }
    }

    private void OnItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && Setting is { } option && box.Tag is OptionCheckItem item)
        {
            item.IsChecked = box.IsChecked == true;
            option.CheckChanged?.Invoke(option, item.Value, item.IsChecked, item.Target);
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Setting?.CheckApplyHandler?.Invoke(Setting);
        UpdateStatus();
    }
}
