namespace Projectional.Language;

/// <summary>The named values of a <see cref="RecordValue"/>, compared by name and by content.</summary>
/// <remarks>
/// A type of its own rather than a list field, because a record's <em>generated</em> equality compares its fields with
/// <see cref="EqualityComparer{T}.Default"/>, and a list of pairs compares by identity - which would make two records that
/// name the same values two different values. This class carries the content rule instead, and the record inherits it by
/// holding one.
/// </remarks>
internal sealed class RecordFields : IEquatable<RecordFields>
{
    private readonly KeyValuePair<string, Value>[] _fields;

    internal RecordFields(IEnumerable<KeyValuePair<string, Value>> fields) => _fields = [.. fields];

    /// <summary>The fields, in the order the rule wrote them.</summary>
    internal IReadOnlyList<KeyValuePair<string, Value>> Items => _fields;

    /// <summary>The value under <paramref name="name"/>, or <see langword="null"/> while there is no such field.</summary>
    internal Value? Value(string name)
    {
        foreach (var field in _fields)
        {
            if (string.Equals(field.Key, name, StringComparison.Ordinal))
            {
                return field.Value;
            }
        }

        return null;
    }

    /// <summary>Equal when both hold the same names with the same values, whatever order they are in.</summary>
    public bool Equals(RecordFields? other) =>
        other is not null
        && _fields.Length == other._fields.Length
        && _fields.All(field => other.Value(field.Key) is { } value && value.Equals(field.Value));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as RecordFields);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);

        foreach (var field in _fields.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            hash.Add(field.Key, StringComparer.Ordinal);
            hash.Add(field.Value);
        }

        return hash.ToHashCode();
    }
}
