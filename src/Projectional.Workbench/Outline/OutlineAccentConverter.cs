using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>Turns a row's concept key into the colour its badge is drawn in.</summary>
/// <remarks>
/// A converter rather than a brush on the row, for the reason the explorer's converters exist: the row is a value a
/// test builds without a rendering platform, and which colour a concept gets is a drawing decision.
/// </remarks>
internal sealed class OutlineAccentConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        OutlineIcons.BrushOf(value as string);

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A row's colour is not something the view decides.");
}
