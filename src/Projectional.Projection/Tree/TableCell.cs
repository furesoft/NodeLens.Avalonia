namespace Projectional.Projection;

/// <summary>
/// A child slot drawn as a table: a header row and one row per child, every row as wide as the
/// header.
/// </summary>
/// <remarks>
/// <para>
/// The grid is fixed at construction. A cell holds exactly one value, so the builder emits one cell
/// per column even when the model has nothing there - an unset property is an empty text and an
/// absent child is an empty slot - which is what lets layout compute a column width once instead of
/// per row.
/// </para>
/// <para>
/// <see cref="StructureCell.Content"/> is the flattened grid in document (row-major) order, so every
/// walk over the tree already sees the table's cells in the order they are drawn. A table with no rows
/// keeps no content at all and shows <see cref="EmptyText"/> instead, exactly like
/// <see cref="CollectionCell"/>, because there is no header worth showing above an empty list.
/// </para>
/// </remarks>
public sealed class TableCell : StructureCell
{
    public TableCell(
        CellAddress address,
        string slot,
        string emptyText,
        IReadOnlyList<EditorCell> headers,
        IReadOnlyList<IReadOnlyList<EditorCell>> rows)
        : base(address, Flatten(slot, headers, rows))
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(emptyText);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        Slot = slot;
        EmptyText = emptyText;
        Headers = headers;
        Rows = rows;
    }

    public string Slot { get; }

    /// <summary>What to show when the slot holds no rows.</summary>
    public string EmptyText { get; }

    /// <summary>The header cells, left to right.</summary>
    public IReadOnlyList<EditorCell> Headers { get; }

    /// <summary>The row cells, each holding one cell per column.</summary>
    public IReadOnlyList<IReadOnlyList<EditorCell>> Rows { get; }

    /// <summary>How many columns the grid has, which is also how many cells every row holds.</summary>
    public int ColumnCount => Headers.Count;

    /// <summary>Whether the slot holds nothing and is showing <see cref="EmptyText"/> instead.</summary>
    public bool IsEmpty => Rows.Count == 0;

    internal override string Describe() => $"table {Slot}";

    private static EditorCell[] Flatten(
        string slot,
        IReadOnlyList<EditorCell> headers,
        IReadOnlyList<IReadOnlyList<EditorCell>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            if (headers.Count > 0)
            {
                throw new ProjectionException(
                    $"The table cell for slot '{slot}' has {headers.Count} header(s) but no rows; an empty table shows its placeholder instead.");
            }

            return [];
        }

        var content = new EditorCell[headers.Count * (rows.Count + 1)];
        var next = 0;

        foreach (var header in headers)
        {
            content[next++] = header;
        }

        foreach (var row in rows)
        {
            if (row.Count != headers.Count)
            {
                throw new ProjectionException(
                    $"A row of the table cell for slot '{slot}' holds {row.Count} cell(s) where the table has {headers.Count} column(s).");
            }

            foreach (var cell in row)
            {
                content[next++] = cell;
            }
        }

        return content;
    }
}
