using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Projectional.Core;

/// <summary>
/// The type of a <see cref="PropertySlot"/>.
/// </summary>
/// <remarks>
/// The set is deliberately closed: <c>string</c>, <c>int</c>, <c>bool</c>, <c>date</c> and
/// enumerations of symbols. Anything richer — numbers with units, expressions, structured
/// values — is modelled as a child node instead, so that it can carry its own slots and be
/// validated and projected like any other part of the model.
/// </remarks>
public abstract class PropertyType : IEquatable<PropertyType>
{
    private protected PropertyType(string name) => Name = name;

    /// <summary>Canonical name, including the symbols for an enumeration.</summary>
    public string Name { get; }

    /// <summary>A text property.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "These are the type names a DSL author writes in a concept declaration, so mirroring them is clearer than inventing different ones.")]
    public static PropertyType String { get; } = new StringPropertyType();

    /// <summary>A whole-number property.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "These are the type names a DSL author writes in a concept declaration, so mirroring them is clearer than inventing different ones.")]
    public static PropertyType Integer { get; } = new IntegerPropertyType();

    public static PropertyType Boolean { get; } = new BooleanPropertyType();

    public static PropertyType Date { get; } = new DatePropertyType();

    /// <summary>Creates an enumeration over <paramref name="symbols"/>.</summary>
    public static PropertyType Enum(params string[] symbols) => new EnumPropertyType(symbols);

    /// <summary>
    /// Parses a stored property value. This is what makes invariant 6 — every stored value
    /// parses against its declared type — checkable on every write.
    /// </summary>
    public abstract bool TryParse(string? text, out object? value);

    /// <summary>Formats a value in the canonical spelling used for storage and comparison.</summary>
    public abstract string Format(object value);

    /// <summary>Validates the canonical text form, which is what a model file contains.</summary>
    public bool IsValid(string? text) => TryParse(text, out _);

    public bool Equals(PropertyType? other) =>
        other is not null && other.GetType() == GetType() && other.Name == Name;

    public override bool Equals(object? obj) => Equals(obj as PropertyType);

    public override int GetHashCode() => HashCode.Combine(GetType(), Name);

    public override string ToString() => Name;
}

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

    public override string Format(object value) => value as string
        ?? throw new ArgumentException($"A value of type 'string' is required, got '{value.GetType()}'.", nameof(value));
}

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

    public override string Format(object value) => value is int parsed
        ? parsed.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentException($"A value of type 'int' is required, got '{value.GetType()}'.", nameof(value));
}

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

    public override string Format(object value) => value is bool parsed
        ? parsed ? "true" : "false"
        : throw new ArgumentException($"A value of type 'bool' is required, got '{value.GetType()}'.", nameof(value));
}

internal sealed class DatePropertyType : PropertyType
{
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

    public override string Format(object value) => value is DateOnly parsed
        ? parsed.ToString(Pattern, CultureInfo.InvariantCulture)
        : throw new ArgumentException($"A value of type 'date' is required, got '{value.GetType()}'.", nameof(value));
}

internal sealed class EnumPropertyType : PropertyType
{
    private readonly HashSet<string> _symbols;

    public EnumPropertyType(params string[] symbols)
        : base(BuildName(symbols))
    {
        _symbols = new HashSet<string>(symbols, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> Symbols => _symbols;

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

    public override string Format(object value) => value is string symbol && _symbols.Contains(symbol)
        ? symbol
        : throw new ArgumentException($"'{value}' is not a symbol of {Name}.", nameof(value));

    private static string BuildName(string[] symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        if (symbols.Length == 0)
        {
            throw new ArgumentException("An enumeration type needs at least one symbol.", nameof(symbols));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentException("An enumeration symbol must not be empty.", nameof(symbols));
            }

            if (!seen.Add(symbol))
            {
                throw new ArgumentException($"Duplicate enumeration symbol '{symbol}'.", nameof(symbols));
            }
        }

        return $"enum({string.Join(", ", symbols)})";
    }
}
