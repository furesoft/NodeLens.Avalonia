using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Projectional.Workbench;

/// <summary>
/// Whether a list has nothing in it, so a panel can say what to do instead of showing a blank rectangle.
/// </summary>
/// <remarks>
/// Bound to a control's item count rather than to a property the shell sets: the count is the same fact the rows are,
/// and a second flag for it could disagree with the list it describes.
/// </remarks>
internal sealed class IsEmptyConverter : IValueConverter
{
    public static IsEmptyConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not int count || count == 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
