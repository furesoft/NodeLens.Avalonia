using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>Turns a folder row's name into the glyph that says what the folder holds.</summary>
/// <remarks>
/// A converter rather than nine flags on the row: which icon an aspect gets is a drawing decision, and putting it in
/// the row would make a value type depend on a rendering platform for something no test can assert anyway.
/// </remarks>
internal sealed class FolderIconConverter : IValueConverter
{
    /// <summary>The instance the template binds through.</summary>
    internal static FolderIconConverter Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>A name with no glyph answers <see langword="null"/>, which draws nothing rather than the wrong thing.</remarks>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string folder ? FolderIcons.Of(folder) : null;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A folder's icon is not something the view decides.");
}
