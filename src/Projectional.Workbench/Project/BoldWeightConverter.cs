using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>Draws a row that stands for a whole tree - a heading or a project - in semibold, and every other row plainly.</summary>
/// <remarks>
/// A weight is a drawing decision, so the row says <c>IsBold</c> and the converter says what that means: the same
/// split the other converters in this folder follow.
/// </remarks>
internal sealed class BoldWeightConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.SemiBold : FontWeight.Normal;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A row's weight is not something the view decides.");
}
