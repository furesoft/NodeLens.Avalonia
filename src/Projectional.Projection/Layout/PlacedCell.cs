namespace Projectional.Projection;

/// <summary>
/// One cell together with where layout put it.
/// </summary>
/// <remarks>
/// A cell does not know its own bounds: the tree is a structure and the layout is a projection of it,
/// so the same tree can be laid out at different widths, or thrown away and rebuilt, without any cell
/// having to invalidate anything. Keeping the two apart is also what lets a placement list be
/// immutable and shared by rendering, hit testing and the caret.
/// </remarks>
public readonly record struct PlacedCell(EditorCell Cell, LayoutRect Bounds)
{
    /// <summary>The place in the model this cell shows, which is what the caret stores.</summary>
    public CellAddress Address => Cell.Address;

    public override string ToString() => $"{Cell} at {Bounds}";
}
