using System.Globalization;

namespace Projectional.Language;

/// <summary>A whole number.</summary>
/// <param name="Content">The number itself.</param>
public sealed record IntValue(int Content) : Value
{
    /// <inheritdoc />
    public override string Display => Content.ToString(CultureInfo.InvariantCulture);
}
