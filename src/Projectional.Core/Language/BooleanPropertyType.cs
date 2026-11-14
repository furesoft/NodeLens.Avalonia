namespace Projectional.Core;

internal sealed class BooleanPropertyType : PropertyType
{
    public BooleanPropertyType()
        : base("bool")
    {
    }

    public override bool TryParse(string? text, out object? value)
    {
        if (bool.TryParse(text, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    public override string Format(object value) => value is bool flag
        ? flag ? "true" : "false"
        : throw new ArgumentException($"Expected a bool but got '{value}'.", nameof(value));
}
