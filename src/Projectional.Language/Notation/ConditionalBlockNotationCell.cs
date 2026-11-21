namespace Projectional.Language;

/// <summary>
/// Block cells drawn only while a property of the node holds a value.
/// </summary>
/// <remarks>
/// <para>
/// The block sibling of <see cref="ConditionalNotationCell"/>, and it exists for the same reason
/// <see cref="WhenFilledBlockNotationCell"/> does: an empty line still takes the row layout reserves for it, so a
/// group of lines that should be absent has to leave the tree rather than draw nothing. A notation that hides a
/// property's word can be inline; one that hides the *shape* the property chooses cannot.
/// </para>
/// <para>
/// The case it was added for is C# 10's file-scoped namespace: `namespace A;` and `namespace A { ... }` are one
/// concept with one boolean, and the braces, the indent and the closing line exist in only one of the two. Two
/// concepts would have made switching forms a re-creation of the node, and a variant is a reader's preference rather
/// than something the model decides ([`INotationChoice`]).
/// </para>
/// <para>
/// It is a decision, not a detail, that this cell has no inline counterpart to match: the inline conditional already
/// answers "hide these cells while the property says so", so the pair is complete. A language using this cell for a
/// property the user can change owes nothing extra - the value is written through an ordinary property cell or an
/// intention, both of which exist.
/// </para>
/// </remarks>
public sealed class ConditionalBlockNotationCell : BlockNotationCell
{
    /// <summary>Draws <paramref name="content"/> only while <paramref name="slot"/> holds <paramref name="value"/>.</summary>
    /// <param name="slot">The property the condition reads, which the drawn concept has to declare.</param>
    /// <param name="value">The value that property has to hold for the blocks to be drawn.</param>
    /// <param name="content">The blocks to draw while the property holds it.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> or <paramref name="value"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">An element of <paramref name="content"/> is <see langword="null"/>.</exception>
    public ConditionalBlockNotationCell(string slot, string value, params ReadOnlySpan<BlockNotationCell> content)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(value);

        Slot = slot;
        Value = value;
        Content = [.. content];

        foreach (var cell in Content)
        {
            ArgumentNullException.ThrowIfNull(cell);
        }
    }

    /// <summary>The property the condition reads.</summary>
    public string Slot { get; }

    /// <summary>The value that property has to hold for the blocks to be drawn.</summary>
    public string Value { get; }

    /// <summary>The blocks drawn while the property holds <see cref="Value"/>.</summary>
    public IReadOnlyList<BlockNotationCell> Content { get; }

    public override string ToString() => $"blocks while {Slot} = \"{Value}\" ({Content.Count})";
}
