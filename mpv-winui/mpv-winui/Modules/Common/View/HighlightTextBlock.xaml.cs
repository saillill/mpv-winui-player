using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;

namespace mpv_winui.Modules.Common.View;

/// <summary>
/// Text block that emphasizes case-insensitive occurrences of <see cref="Query"/>
/// inside <see cref="Text"/> (used by the playlist search filter).
/// </summary>
public sealed partial class HighlightTextBlock : UserControl
{
    public HighlightTextBlock()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(HighlightTextBlock),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public string Query
    {
        get => (string)GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(
        nameof(Query),
        typeof(string),
        typeof(HighlightTextBlock),
        new PropertyMetadata(string.Empty, OnContentChanged));

    // Deliberately shadows Control.FontSize: WinUI has no OverrideMetadata,
    // so re-registering the DP is the only way to hook size changes into
    // RebuildInlines for the inner TextBlock.
    public new double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize),
        typeof(double),
        typeof(HighlightTextBlock),
        new PropertyMetadata(12d, OnContentChanged));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HighlightTextBlock self)
        {
            self.RebuildInlines();
        }
    }

    private void RebuildInlines()
    {
        RootTextBlock.FontSize = FontSize;
        RootTextBlock.Inlines.Clear();

        var text = Text ?? string.Empty;
        var query = (Query ?? string.Empty).Trim();
        if (query.Length == 0 || text.Length == 0)
        {
            RootTextBlock.Inlines.Add(new Run { Text = text });
            return;
        }

        var accent = Application.Current.Resources.TryGetValue(
            "AccentTextFillColorPrimaryBrush",
            out var resource) ? resource as Brush : null;
        var matchWeight = new Windows.UI.Text.FontWeight(600);

        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }
            if (index > start)
            {
                RootTextBlock.Inlines.Add(new Run { Text = text[start..index] });
            }
            RootTextBlock.Inlines.Add(new Run
            {
                Text = text.Substring(index, query.Length),
                Foreground = accent,
                FontWeight = matchWeight,
            });
            start = index + query.Length;
        }

        if (start < text.Length)
        {
            RootTextBlock.Inlines.Add(new Run { Text = text[start..] });
        }
    }
}
