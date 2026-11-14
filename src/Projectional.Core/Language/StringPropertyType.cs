namespace Projectional.Core;

internal sealed class StringPropertyType : PropertyType
{
    public StringPropertyType()
        : base("string")
    {
    }

    public override bool TryParse(string? text, out object? value)
    {
        value = text ?? string.Empty;
        return true;
    }

    public override string Format(object value) => value is string text
        ? text
        : throw new ArgumentException($"Expected a string but got '{value}'.", nameof(value));
}
