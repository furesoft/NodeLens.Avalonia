using System.Globalization;

namespace Projectional.Core;

internal sealed class IntegerPropertyType : PropertyType
{
    public IntegerPropertyType()
        : base("int")
    {
    }

    public override bool TryParse(string? text, out object? value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    public override string Format(object value) => value is int number
        ? number.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentException($"Expected an int but got '{value}'.", nameof(value));
}
