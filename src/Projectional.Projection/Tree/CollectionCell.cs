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
/// </remarks>
public sealed class CollectionCell : StructureCell
{
    public CollectionCell(
        CellAddress address,
        string slot,
        string emptyText,
        IReadOnlyList<EditorCell> items,
        IReadOnlyList<EditorCell> separators)
        : base(address, Interleave(slot, items, separators))
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(separators);
        ArgumentNullException.ThrowIfNull(emptyText);

        Slot = slot;
        EmptyText = emptyText;
        Items = items;
        Separators = separators;
    }

    public string Slot { get; }

    /// <summary>What to show when the slot holds nothing.</summary>
    public string EmptyText { get; }

    /// <summary>The projected children, without the separators between them.</summary>
    public IReadOnlyList<EditorCell> Items { get; }

    /// <summary>The separators, one between each neighbouring pair of <see cref="Items"/>.</summary>
    public IReadOnlyList<EditorCell> Separators { get; }

    /// <summary>Whether the slot holds nothing and is showing <see cref="EmptyText"/> instead.</summary>
    public bool IsEmpty => Items.Count == 0;

    internal override string Describe() => $"collection {Slot}";

    private static EditorCell[] Interleave(
        string slot,
        IReadOnlyList<EditorCell> items,
        IReadOnlyList<EditorCell> separators)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(separators);

        var expected = Math.Max(0, items.Count - 1);

        if (separators.Count != expected)
        {
            throw new ProjectionException(
                $"Collection cell for slot '{slot}' holds {items.Count} item(s) and needs {expected} separator(s), but got {separators.Count}.");
        }

        if (items.Count == 0)
        {
            return [];
        }

        var cells = new EditorCell[(items.Count * 2) - 1];
        var next = 0;

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                cells[next++] = separators[i - 1];
            }

            cells[next++] = items[i];
        }

        return cells;
    }
}
