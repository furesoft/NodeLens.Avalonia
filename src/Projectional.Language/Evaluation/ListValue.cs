namespace Projectional.Language;

/// <summary>Several values in an order, as one answer.</summary>
/// <remarks>
/// A list is what a rule answers with when the answer is every entry of a slot rather than a summary of them: which places a
/// route stops at, in the order it stops there. Equality is by content <em>and by order</em>, unlike a record - a list that
/// names the same values in another order is another list, because the order is what the answer means. The rule lives in
/// <see cref="ValueSequence"/> for the reason <see cref="RecordFields"/> holds the record's.
/// </remarks>
public sealed record ListValue : Value
{
    private readonly ValueSequence _items;

    /// <summary>A list of <paramref name="items"/>, in that order.</summary>
    /// <param name="items">The values, in the order the rule produced them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public ListValue(IEnumerable<Value> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items = new ValueSequence(items);
    }

    /// <summary>The values, in order.</summary>
    public IReadOnlyList<Value> Items => _items.Items;

    /// <inheritdoc />
    public override string Display => "[" + string.Join(", ", _items.Items.Select(item => item.Display)) + "]";
}
