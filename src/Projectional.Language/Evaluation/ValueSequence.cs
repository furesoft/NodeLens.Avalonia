namespace Projectional.Language;

/// <summary>The values of a <see cref="ListValue"/>, compared by content and by order.</summary>
/// <remarks>
/// The same reason <see cref="RecordFields"/> exists: a record's generated equality compares its fields, and an array or a
/// list compares by identity. The order is part of the content here, so two lists are equal only when they agree entry by
/// entry.
/// </remarks>
internal sealed class ValueSequence : IEquatable<ValueSequence>
{
    private readonly Value[] _items;

    internal ValueSequence(IEnumerable<Value> items) => _items = [.. items];

    /// <summary>The values, in order.</summary>
    internal IReadOnlyList<Value> Items => _items;

    /// <summary>Equal when both hold the same values in the same order.</summary>
    public bool Equals(ValueSequence? other) => other is not null && _items.SequenceEqual(other._items);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ValueSequence);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);

        foreach (var item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
