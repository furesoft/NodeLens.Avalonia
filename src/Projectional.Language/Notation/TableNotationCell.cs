namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a table: one row per child, one cell per column.
/// </summary>
/// <remarks>
/// <para>
/// A table is a block cell, not a grid of lines, because the columns must line up across rows and a
/// line cannot know what the lines above it did. Layout therefore computes the column widths from the
/// whole table, and the notation only says what the columns are.
/// </para>
/// <para>
/// The variant name is not part of this cell. Whether a concept is shown as a table is a
/// <see cref="Notation.Variant"/> on the notation, so the same concept may keep a text form and a
/// table form and the reader picks one.
/// </para>
/// <para>
/// Every shown slot keeps its natural address: a cell under <c>rules</c> is still addressed as the
/// element of <c>rules</c> it shows, which is why the caret, undo and hit testing need no knowledge of
/// tables at all.
/// </para>
/// </remarks>
public sealed class TableNotationCell : BlockNotationCell
{
    public TableNotationCell(string slot, params ReadOnlySpan<TableColumn> columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        var declared = columns.ToArray();

        if (declared.Length == 0)
        {
            throw new ArgumentException($"The table over slot '{slot}' has no columns.", nameof(columns));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in declared)
        {
            ArgumentNullException.ThrowIfNull(column, nameof(columns));

            if (!seen.Add(column.Slot))
            {
                throw new ArgumentException(
                    $"The table over slot '{slot}' declares column '{column.Slot}' twice.",
                    nameof(columns));
            }
        }

        Slot = slot;
        Columns = declared;
    }

    /// <summary>The child slot that holds the rows.</summary>
    public string Slot { get; }

    /// <summary>The columns, left to right, which is also the order they are drawn and addressed in.</summary>
    public IReadOnlyList<TableColumn> Columns { get; }

    /// <summary>What to show when the slot holds no rows.</summary>
    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"table {Slot} ({Columns.Count} column(s))";
}
