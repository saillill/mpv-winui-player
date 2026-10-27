using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace mpv_winui.Infrastructure.Common.View;

/// <summary>
/// Turns an element's measured width into the height of a box with a fixed aspect.
///
/// Exists so a library card's thumbnail can be 16:9 without the markup knowing how
/// wide the card will be. The width belongs to the grid: UniformGridLayout derives the
/// column count from MinItemWidth, so the same card is 253 DIP on a maximised window
/// and much narrower on a small one. A fixed Height would be right at exactly one of
/// those and would either crop the picture or leave a gap at every other.
///
/// One-way and self-reading: it is bound to the element's own ActualWidth, so the
/// height follows a relayout without any code behind it.
/// </summary>
public sealed partial class AspectHeightConverter : IValueConverter
{
    /// <summary>
    /// Width divided by this gives the height. 16:9 is 1.777..., portrait posters
    /// would use a value below 1.
    /// </summary>
    public double Aspect { get; set; } = 16.0 / 9.0;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var width = value switch
        {
            double d => d,
            float f => f,
            _ => 0.0,
        };

        if (width <= 0 || Aspect <= 0)
        {
            // Not measured yet. NaN lets the Border keep whatever the layout gives
            // it for this frame instead of collapsing to zero and flashing.
            return double.NaN;
        }

        return width / Aspect;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
