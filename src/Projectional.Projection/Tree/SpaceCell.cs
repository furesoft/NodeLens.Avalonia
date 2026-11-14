namespace Projectional.Projection;

/// <summary>
/// The blank between two cells that the notation asked for.
/// </summary>
/// <remarks>
/// It exists as a cell rather than as padding on its neighbour for two reasons: it keeps the tree a
/// faithful picture of the notation, and it gives hit testing somewhere to land between two cells,
/// which is what makes clicking the gap put the caret where the user aimed. It is one column wide;
/// wider gaps are several spaces.
/// </remarks>
public sealed class SpaceCell : EditorCell
{
    public SpaceCell(CellAddress address)
        : base(address)
    {
    }

    /// <summary>How many columns the cell occupies, which is the one thing layout needs to know.</summary>
    public const int Width = 1;

    internal override string Describe() => "space";
}
