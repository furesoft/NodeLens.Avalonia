namespace Projectional.Language;

/// <summary>
/// Inline cells drawn only while a child slot of the node holds at least one child.
/// </summary>
/// <remarks>
/// <para>
/// A notation may have something to say only while there is something to say it about: <c>concept Rule extends
/// Condition</c> reads as the concept it describes, while a header that carried an empty list everywhere would
/// say the same thing about every concept in a language. This is the cell that makes the difference expressible -
/// the word and the list exist in the notation always and in the tree only while the slot holds a child.
/// </para>
/// <para>
/// It is the sibling of <see cref="ConditionalNotationCell"/> and the difference between them is where the value
/// comes from: a conditional hides a word that is <em>derived</em> from a property the user changes elsewhere,
/// while this cell hides a slot the user has to fill. Hiding an empty slot therefore removes the place the first
/// child is written into, which a language using this cell owes a second way in for - an
/// <c>Intention</c> that creates the first child, the same answer <c>abstract</c> has because its word has no cell
/// either. A language that adds no such entry makes the slot unreachable, so that pair is a decision rather than
/// a detail.
/// </para>
/// <para>
/// A cell that does not hold produces <em>nothing</em>: not an empty cell, not a cell with no text, because a
/// zero-width cell is a caret stop on an address that draws no text. Inline only, like the property conditional:
/// a block form arrives when a notation needs to hide a whole line.
/// </para>
/// </remarks>
public sealed class WhenFilledNotationCell : InlineNotationCell
{
    /// <summary>Draws <paramref name="content"/> only while <paramref name="slot"/> holds a child.</summary>
    /// <param name="slot">The child slot the condition reads, which the drawn concept has to declare.</param>
    /// <param name="content">The inline cells to draw while the slot is filled.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">An element of <paramref name="content"/> is <see langword="null"/>.</exception>
    public WhenFilledNotationCell(string slot, params ReadOnlySpan<InlineNotationCell> content)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
        Content = [.. content];

        foreach (var cell in Content)
        {
            ArgumentNullException.ThrowIfNull(cell);
        }
    }

    /// <summary>The child slot the condition reads.</summary>
    public string Slot { get; }

    /// <summary>The inline cells drawn while the slot is filled.</summary>
    public IReadOnlyList<InlineNotationCell> Content { get; }

    public override string ToString() => $"when {Slot} is filled ({Content.Count})";
}
