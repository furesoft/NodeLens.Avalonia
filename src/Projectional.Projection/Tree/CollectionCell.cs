namespace Projectional.Projection;

/// <summary>
/// A child slot that holds many children, shown as its items with separators between them.
/// </summary>
/// <remarks>
/// <para>
/// The collection owns the interleaving, so nothing above it has to know that separators exist. That
/// matters for layout: separators are ordinary cells that take columns, and a renderer that had to
/// insert them itself would duplicate the document-order walk that every other part of the editor
/// relies on.
/// </para>
/// <para>
/// <see cref="Items"/> and <see cref="Separators"/> are kept next to the interleaved
/// <see cref="StructureCell.Content"/> because the two questions are different: navigation wants the
/// items, layout wants everything in document order. A separator is never reused between gaps, since
/// a cell belongs to exactly one place in the tree.
/// </para>
/// <para>
/// The blank of a gap is built here rather than by the builder, because only the collection knows how
/// the separator and the spacing compose. <see cref="Separators"/> therefore holds what the notation
/// declared and not the blank after it: it is empty when the notation declares no separator, and then
/// the spacing alone separates the items.
/// </para>
/// </remarks>
public sealed class CollectionCell : StructureCell
{
    public CollectionCell(
        CellAddress address,
        string slot,
        string emptyText,
        IReadOnlyList<EditorCell> items,
        IReadOnlyList<EditorCell> separators,
        int spacing = 1)
        : base(address, Interleave(address, slot, items, separators, spacing))
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(separators);
        ArgumentNullException.ThrowIfNull(emptyText);
        ArgumentOutOfRangeException.ThrowIfNegative(spacing);

        Slot = slot;
        EmptyText = emptyText;
        Items = items;
        Separators = separators;
        Spacing = spacing;
    }

    public string Slot { get; }

    /// <summary>What to show when the slot holds nothing.</summary>
    public string EmptyText { get; }

    /// <summary>The projected children, without the separators between them.</summary>
    public IReadOnlyList<EditorCell> Items { get; }

    /// <summary>
    /// The declared separators, one between each neighbouring pair of <see cref="Items"/>, or empty when
    /// the notation declares none and the spacing alone separates them.
    /// </summary>
    public IReadOnlyList<EditorCell> Separators { get; }

    /// <summary>
    /// How many spaces follow a separator, or stand between two items when the notation declares none.
    /// </summary>
    /// <remarks>
    /// The blank comes after the separator and not on both sides of it, because whether a token wants a
    /// space in front of it is a property of the token: a comma and a semicolon do not, a word like
    /// <c>and</c> does. An author who wants one writes it into the separator notation, where it is
    /// visible, instead of asking every collection that uses the token to pad it.
    /// </remarks>
    public int Spacing { get; }

    /// <summary>Whether the slot holds nothing and is showing <see cref="EmptyText"/> instead.</summary>
    public bool IsEmpty => Items.Count == 0;

    internal override string Describe() => $"collection {Slot}";

    private static EditorCell[] Interleave(
        CellAddress address,
        string slot,
        IReadOnlyList<EditorCell> items,
        IReadOnlyList<EditorCell> separators,
        int spacing)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(separators);

        var gaps = Math.Max(0, items.Count - 1);

        // A collection either separates every gap or none of them: a separator on some gaps only
        // would be a second notation, not a list with a hole in it.
        if (separators.Count != 0 && separators.Count != gaps)
        {
            throw new ProjectionException(
                $"Collection cell for slot '{slot}' holds {items.Count} item(s) and needs either {gaps} separator(s) or none, but got {separators.Count}.");
        }

        if (items.Count == 0)
        {
            return [];
        }

        var hasSeparator = separators.Count > 0;
        var perGap = spacing + (hasSeparator ? 1 : 0);
        var cells = new EditorCell[items.Count + (gaps * perGap)];
        var next = 0;

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                if (hasSeparator)
                {
                    cells[next++] = separators[i - 1];
                }

                for (var space = 0; space < spacing; space++)
                {
                    cells[next++] = new SpaceCell(address);
                }
            }

            cells[next++] = items[i];
        }

        return cells;
    }
}
