using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>Turns a row's icon key into the colour its glyph or badge is drawn in.</summary>
/// <remarks>
/// The colour is the aspect, not the row kind, so a folder and the files inside it share it: that is what makes a
/// green block of the tree mean "these are the concepts" at a glance.
/// </remarks>
internal sealed class ProjectIconBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ProjectIcons.BrushOf(value as string);

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A row's colour is not something the view decides.");
}
