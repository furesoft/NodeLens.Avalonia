using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>Turns a row's icon key into the glyph it is drawn with.</summary>
/// <remarks>
/// A converter rather than a geometry on the row: which mark an aspect gets is a drawing decision, and putting it in
/// the row would make a value type depend on a rendering platform for something no test could assert anyway. The key
/// itself is a plain string, so a test can say what a row will be drawn as without one.
/// </remarks>
internal sealed class ProjectIconConverter : IValueConverter
{
    /// <inheritdoc/>
    /// <remarks>A key with no glyph answers <see langword="null"/>, which draws nothing rather than the wrong thing.</remarks>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? ProjectIcons.GlyphOf(key) : null;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A row's icon is not something the view decides.");
}
