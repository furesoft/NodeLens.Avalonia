using Projectional.Language;

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
/// <paramref name="slotAddress"/> is the address the affordance row carries, and it is the slot's
/// rather than the table's on purpose: a table that is the root of its node's notation carries the
/// node's address, which its headers carry too, so an affordance addressed that way would be
/// indistinguishable from the first header. It defaults to the table's own address for a table that
/// is nested, where the two are the same.
/// </para>
/// <para>
/// <see cref="StructureCell.Content"/> is the flattened grid in document (row-major) order, so every
/// walk over the tree already sees the table's cells in the order they are drawn. A table with no rows
/// still draws its header, followed by one <see cref="AddRowCell"/> row: the header is what tells the
/// reader what the columns are, and the affordance row is what the caret can stand in to add the first
/// one. <see cref="Rows"/> stays empty in that case - the affordance is not a row of the model.
/// </para>
/// </remarks>
public sealed class TableCell : StructureCell
{
    public TableCell(
        CellAddress address,
        string slot,
        string emptyText,
        IReadOnlyList<EditorCell> headers,
        IReadOnlyList<IReadOnlyList<EditorCell>> rows,
        CellAddress? slotAddress = null,
        PaintRole? headerFill = null)
        : base(address, Flatten(slotAddress ?? address, slot, emptyText, headers, rows))
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(emptyText);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        Slot = slot;
        EmptyText = emptyText;
        Headers = headers;
        Rows = rows;
        HeaderFill = headerFill;
    }

    public string Slot { get; }

    /// <summary>What to show when the slot holds no rows, drawn in the first column of the affordance row.</summary>
    public string EmptyText { get; }

    /// <summary>The header cells, left to right.</summary>
    public IReadOnlyList<EditorCell> Headers { get; }

    /// <summary>The row cells, each holding one cell per column.</summary>
    public IReadOnlyList<IReadOnlyList<EditorCell>> Rows { get; }

    /// <summary>How many columns the grid has, which is also how many cells every row holds.</summary>
    public int ColumnCount => Headers.Count;

    /// <summary>Whether the slot holds nothing and is showing the affordance row instead.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>
    /// The role the header row is filled with, or <see langword="null"/> for a header that is only text.
    /// </summary>
    /// <remarks>
    /// Layout reports where the band goes rather than drawing it, and a painter turns the role into a colour, so a
    /// table says "this is my header" and nothing in the projection layer knows what a brush is.
    /// </remarks>
    public PaintRole? HeaderFill { get; }

    internal override string Describe() => $"table {Slot}";

    private static EditorCell[] Flatten(
        CellAddress affordanceAddress,
        string slot,
        string emptyText,
        IReadOnlyList<EditorCell> headers,
        IReadOnlyList<IReadOnlyList<EditorCell>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (headers.Count == 0)
        {
            throw new ProjectionException(
                $"The table cell for slot '{slot}' has no columns; a table always draws its header, even when the slot is empty.");
        }

        var drawn = rows.Count == 0 ? [AffordanceRow(affordanceAddress, slot, emptyText, headers.Count)] : rows;
        var content = new EditorCell[headers.Count * (drawn.Count + 1)];
        var next = 0;

        foreach (var header in headers)
        {
            content[next++] = header;
        }

        foreach (var row in drawn)
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

    /// <summary>
    /// The one row an empty table draws: the placeholder in the first column, blanks in the rest, so
    /// that the grid stays rectangular and every column keeps a width.
    /// </summary>
    private static EditorCell[] AffordanceRow(CellAddress address, string slot, string emptyText, int columnCount)
    {
        var row = new EditorCell[columnCount];
        row[0] = new AddRowCell(address, slot, emptyText);

        for (var i = 1; i < columnCount; i++)
        {
            row[i] = new SpaceCell(address);
        }

        return row;
    }
}
