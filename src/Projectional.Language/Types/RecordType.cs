using System.Globalization;

namespace Projectional.Language;

/// <summary>
/// A record: a fixed set of named fields, each with its own type.
/// </summary>
/// <remarks>
/// <para>
/// Structural, not nominal: two records are the same type when they have the same field names and each
/// field's type accepts the other's. That is what lets a language describe a shape without declaring a
/// named type for it, which is the whole reason a DSL wants records at all.
/// </para>
/// <para>
/// Field order is kept for display and is not part of the comparison - a record is a set of names, and
/// two records that differ only in the order they were written are the same shape.
/// </para>
/// </remarks>
public sealed class RecordType : IType
{
    private readonly Dictionary<string, IType> _fields;

    /// <summary>Creates a record of <paramref name="fields"/>.</summary>
    /// <param name="fields">The field names and their types, in the order they should be shown.</param>
    /// <exception cref="ArgumentException">A field name is empty or appears twice.</exception>
    public RecordType(IEnumerable<(string Name, IType Type)> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        List<(string Name, IType Type)> ordered = [];
        _fields = new Dictionary<string, IType>(StringComparer.Ordinal);

        foreach (var (name, type) in fields)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(type);

            if (!_fields.TryAdd(name, type))
            {
                throw new ArgumentException($"The record declares the field '{name}' twice.", nameof(fields));
            }

            ordered.Add((name, type));
        }

        Fields = ordered;
    }

    /// <summary>The fields, in the order they were declared.</summary>
    public IReadOnlyList<(string Name, IType Type)> Fields { get; }

    /// <inheritdoc />
    public string Name =>
        $"({string.Join(", ", Fields.Select(entry => $"{entry.Name}: {entry.Type.Name}"))})";

    /// <inheritdoc />
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted, and so is a record with an unknown field: refusing either
    /// would turn one mistake into a finding on every expression that reads the record.
    /// </remarks>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        var unwrapped = NamedType.Unwrap(actual);

        if (unwrapped is UnknownType)
        {
            return true;
        }

        if (unwrapped is not RecordType record || record._fields.Count != _fields.Count)
        {
            return false;
        }

        foreach (var (name, type) in _fields)
        {
            if (!record._fields.TryGetValue(name, out var other) || !type.Accepts(other))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A record is comparable with another record when both hold the same field names and each field's type is
    /// comparable, so its comparability is as structural as its acceptance. Field order is not part of the
    /// comparison, for the reason it is not part of acceptance: a record is a set of names.
    /// </remarks>
    public bool IsComparableTo(IType other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var unwrapped = NamedType.Unwrap(other);

        if (unwrapped is UnknownType)
        {
            return true;
        }

        if (unwrapped is not RecordType record || record._fields.Count != _fields.Count)
        {
            return false;
        }

        foreach (var (name, type) in _fields)
        {
            if (!record._fields.TryGetValue(name, out var otherType) || !type.IsComparableTo(otherType))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() => Name;
}
