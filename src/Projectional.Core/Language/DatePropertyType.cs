using System.Globalization;

namespace Projectional.Core;

internal sealed class DatePropertyType : PropertyType
{
    /// <summary>
    /// The only accepted spelling. One pattern means a date cannot be re-spelled by a hand
    /// edit, which is what keeps a save after a load byte-identical.
    /// </summary>
    public const string Pattern = "yyyy-MM-dd";

    public DatePropertyType()
        : base("date")
    {
    }

    public override bool TryParse(string? text, out object? value)
    {
        if (DateOnly.TryParseExact(text, Pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    public override string Format(object value) => value is DateOnly date
        ? date.ToString(Pattern, CultureInfo.InvariantCulture)
        : throw new ArgumentException($"Expected a DateOnly but got '{value}'.", nameof(value));
}
