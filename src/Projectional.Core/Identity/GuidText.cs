using System.Globalization;

namespace Projectional.Core;

/// <summary>
/// The one textual form identity values are written in, shared by every id type so that a
/// hand-edited file cannot introduce a second spelling of the same value.
/// </summary>
internal static class GuidText
{
    private const string Layout = "D";

    public static bool TryParse(string? text, out Guid value) =>
        Guid.TryParseExact(text, Layout, out value) && value != Guid.Empty;

    public static string Format(Guid value) => value.ToString(Layout, CultureInfo.InvariantCulture);
}
