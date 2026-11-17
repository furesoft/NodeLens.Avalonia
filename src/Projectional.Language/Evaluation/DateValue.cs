using System.Globalization;

namespace Projectional.Language;

/// <summary>A calendar date, without a time of day.</summary>
/// <param name="Content">The date.</param>
public sealed record DateValue(DateOnly Content) : Value
{
    /// <inheritdoc />
    /// <remarks>ISO spelling, because a trace that changed with the machine's culture would not be comparable.</remarks>
    public override string Display => Content.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
