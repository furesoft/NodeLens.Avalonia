namespace Projectional.Projection;

/// <summary>
/// The blank between two cells that the notation asked for.
/// </summary>
/// <remarks>
/// It exists as a cell rather than as padding on its neighbour for two reasons: it keeps the tree a
/// faithful picture of the notation, and it gives hit testing somewhere to land between two cells,
/// which is what makes clicking the gap put the caret where the user aimed. Its text is one space, so
/// a wider gap is several spaces rather than a wider cell.
/// </remarks>
public sealed class SpaceCell : TextCell
{
    public SpaceCell(CellAddress address)
        : base(address, " ")
    {
    }

    internal override string Describe() => "space";
}
