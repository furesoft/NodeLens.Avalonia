using System.Diagnostics.CodeAnalysis;

namespace Projectional.Core;

/// <summary>
/// The type of a <see cref="PropertySlot"/>.
/// </summary>
/// <remarks>
/// The set is deliberately closed: <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c>, <c>date</c> and
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

    /// <summary>A truth value.</summary>
    public static PropertyType Boolean { get; } = new BooleanPropertyType();

    /// <summary>A number with a fractional part, held as a <see cref="double"/>.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "These are the type names a DSL author writes in a concept declaration, so mirroring them is clearer than inventing different ones.")]
    public static PropertyType Float { get; } = new FloatPropertyType();

    /// <summary>A calendar date without time or zone.</summary>
    public static PropertyType Date { get; } = new DatePropertyType();

    /// <summary>Creates an enumeration over <paramref name="symbols"/>.</summary>
    public static PropertyType Enum(params ReadOnlySpan<string> symbols) => new EnumPropertyType(symbols);

    /// <summary>
    /// The values this type spells out, in declaration order - empty for a type whose values are not a
    /// closed set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An enumeration is the one property type whose legal values are known without parsing anything, which
    /// is exactly what completion needs: the editor can offer them instead of asking the user to remember
    /// the spelling. A string or a number answers with nothing, so a caller reads "no symbols" rather than
    /// special-casing the type.
    /// </para>
    /// <para>
    /// Declaration order, not sorted and not a set: it is the language author's ranking, and it is what a
    /// list of proposals shows before the user has typed anything.
    /// </para>
    /// </remarks>
    public virtual IReadOnlyList<string> Symbols => [];

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
