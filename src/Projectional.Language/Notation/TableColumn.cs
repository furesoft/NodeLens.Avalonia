namespace Projectional.Language;

/// <summary>
/// One column of a table notation: a slot of the row concept, drawn under a header.
/// </summary>
/// <remarks>
/// <para>
/// A column names a slot rather than carrying a cell, because the cell that shows a row's value is
/// built from the row itself: a property column becomes a property cell, a child column becomes a
/// child cell, a reference column becomes a reference cell. That is what lets the same table draw rows
/// of different concepts as long as they declare the slot.
/// </para>
/// <para>
/// The header, by contrast, is a cell of the table's own notation, so it may only be made of
/// constants and spaces. A header that named a slot would have to be answered by whichever row
/// happened to be first, which is exactly the kind of hidden dependency a projection must not have.
/// </para>
/// </remarks>
public sealed class TableColumn
{
    public TableColumn(string slot, InlineNotationCell header)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(header);

        if (header is not (ConstantNotationCell or SpaceNotationCell))
        {
            throw new ArgumentException(
                $"The header of column '{slot}' is a '{header.GetType().Name}', but a header may only be made of constants and spaces.",
                nameof(header));
        }

        Slot = slot;
        Header = header;
    }

    /// <summary>The property, single child or reference of the row concept this column shows.</summary>
    public string Slot { get; }

    /// <summary>What is drawn above the column.</summary>
    public InlineNotationCell Header { get; }

    /// <summary>
    /// What a column shows when the row has nothing there: no child, no property value, or no reference target.
    /// </summary>
    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"{Slot} as {Header}";
}
