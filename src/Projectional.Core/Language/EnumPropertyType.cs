namespace Projectional.Core;

internal sealed class EnumPropertyType : PropertyType
{
    private readonly HashSet<string> _symbols;

    public EnumPropertyType(params string[] symbols)
        : base(NameFor(symbols))
    {
        _symbols = new HashSet<string>(symbols, StringComparer.Ordinal);
    }

    public override bool TryParse(string? text, out object? value)
    {
        if (text is not null && _symbols.Contains(text))
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    public override string Format(object value) =>
        value is string text && _symbols.Contains(text)
            ? text
            : throw new ArgumentException($"'{value}' is not a symbol of {Name}.", nameof(value));

    /// <summary>
    /// Validates the symbols and renders the name in declaration order, so that two enumerations
    /// are equal exactly when they offer the same symbols in the same order.
    /// </summary>
    private static string NameFor(string[] symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        if (symbols.Length == 0)
        {
            throw new ArgumentException("An enumeration needs at least one symbol.", nameof(symbols));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("An enumeration symbol cannot be empty.", nameof(symbols));
            }

            if (!seen.Add(symbol))
            {
                throw new ArgumentException($"'{symbol}' is declared as an enumeration symbol twice.", nameof(symbols));
            }
        }

        return $"enum({string.Join(", ", symbols)})";
    }
}
