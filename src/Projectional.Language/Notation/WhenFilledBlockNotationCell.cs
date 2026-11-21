namespace Projectional.Language;

/// <summary>
/// Block cells drawn only while a child slot of the node holds at least one child.
/// </summary>
/// <remarks>
/// <para>
/// The block sibling of <see cref="WhenFilledNotationCell"/>, and the difference is the row. A line that draws
/// nothing still leaves the row it would have drawn, because layout gives every vertical cell at least one line -
/// which is exactly what makes an empty <see cref="LineNotationCell"/> a blank line. A block that should leave
/// <em>no</em> row therefore has to be absent from the tree rather than empty in it, and the projection skips it.
/// </para>
/// <para>
/// It exists for the notation that puts one group of lines under a condition: a concept shows its
/// <c>implements</c> list and its alignment, or shows nothing at all where the list would be. Hiding the slot the
/// first child is written into costs the same second way in an inline <see cref="WhenFilledNotationCell"/> does -
/// <c>StructureIntentions.ImplementInterface</c> creates the first entry.
/// </para>
/// </remarks>
public sealed class WhenFilledBlockNotationCell : BlockNotationCell
{
    /// <summary>Draws <paramref name="content"/> only while <paramref name="slot"/> holds a child.</summary>
    /// <param name="slot">The child slot the condition reads, which the drawn concept has to declare.</param>
    /// <param name="content">The blocks to draw while the slot is filled.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">An element of <paramref name="content"/> is <see langword="null"/>.</exception>
    public WhenFilledBlockNotationCell(string slot, params ReadOnlySpan<BlockNotationCell> content)
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

    /// <summary>The blocks drawn while the slot is filled.</summary>
    public IReadOnlyList<BlockNotationCell> Content { get; }

    public override string ToString() => $"when {Slot} is filled ({Content.Count} blocks)";
}
