using System;
using Microsoft.UI.Xaml.Data;

namespace mpv_winui.Infrastructure.Common.View;

/// <summary>
/// Dims an index letter that has nothing under it.
///
/// The rail shows every letter; the ones with no content are disabled by a binding, and
/// this only makes that visible. A full-weight letter that does nothing when clicked
/// reads as broken rather than as empty, which is the state the reference avoids by
/// greying them.
///
/// A named type rather than a nested class because the DataTemplate that uses it lives
/// in XAML, and markup can only name a type it can resolve.
/// </summary>
public sealed partial class IndexLetterOpacityConverter : IValueConverter
{
    /// <summary>Opacity for a letter with no content.</summary>
    public double Dim { get; set; } = 0.35;

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 1.0 : Dim;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
