using System.Globalization;

namespace Projectional.Language;

/// <summary>A number with a fractional part.</summary>
/// <param name="Content">The number itself.</param>
public sealed record FloatValue(double Content) : Value
{
    /// <inheritdoc />
    public override string Display => Content.ToString(CultureInfo.InvariantCulture);
}
