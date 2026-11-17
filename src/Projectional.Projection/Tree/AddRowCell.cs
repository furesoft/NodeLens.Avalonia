namespace Projectional.Projection;

/// <summary>
/// The row a table shows when its slot holds nothing: the place a press or Enter adds the first row.
/// </summary>
/// <remarks>
/// <para>
/// It is a cell of its own rather than a <see cref="ChildCell"/> or a <see cref="CollectionCell"/>,
/// because the slot it stands for holds many children and is drawn as a grid. A cell that said
/// "single child" would be a lie about the model, and one that said "collection" would claim a
/// separator the table does not have.
/// </para>
/// <para>
/// The text is the table's <see cref="TableCell.EmptyText"/>, so a language that already says what an
/// empty slot looks like says it here too - and a table with no rows is the one place where that
/// placeholder is drawn inside a grid rather than instead of one.
/// </para>
/// </remarks>
public sealed class AddRowCell : TextCell
{
    public AddRowCell(CellAddress address, string slot, string text)
        : base(address, text)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    /// <summary>The child slot the first row would go into.</summary>
    public string Slot { get; }

    internal override string Describe() => $"add row {Slot}";
}
