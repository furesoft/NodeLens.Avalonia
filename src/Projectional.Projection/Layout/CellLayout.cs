using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Where every cell of one editor tree ended up, and how big the tree became.
/// </summary>
/// <remarks>
/// <para>
/// A layout belongs to one tree and one measure, so it is valid exactly as long as that tree is. It is
/// the shared input of drawing, hit testing and anything that needs to know how much space a model
/// takes, which is why it is a value the caller can pass around instead of a service it has to ask.
/// </para>
/// <para>
/// Placements are kept in document order, parents before children. That single order is what makes
/// the two lookups work without extra structures: taking the first match for an address gives the
/// outermost cell with that address, and taking the last cell that contains a point gives the
/// innermost one under it.
/// </para>
/// </remarks>
public sealed class CellLayout
{
    private readonly Dictionary<EditorCell, LayoutRect> _bounds;
    private readonly Dictionary<EditorCell, LaidOutText> _text;

    internal CellLayout(
        EditorTree tree,
        LayoutSize size,
        IReadOnlyList<PlacedCell> cells,
        IReadOnlyDictionary<EditorCell, LaidOutText> text)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(text);

        Tree = tree;
        Size = size;
        Cells = cells;
        _text = new Dictionary<EditorCell, LaidOutText>(text);

        _bounds = new Dictionary<EditorCell, LayoutRect>(cells.Count);

        foreach (var placed in cells)
        {
            _bounds[placed.Cell] = placed.Bounds;
        }
    }

    /// <summary>The tree that was laid out.</summary>
    public EditorTree Tree { get; }

    /// <summary>How much room the whole tree takes, including the parts outside any viewport.</summary>
    public LayoutSize Size { get; }

    /// <summary>Every cell with its bounds, in document order.</summary>
    public IReadOnlyList<PlacedCell> Cells { get; }

    /// <summary>Where a specific cell was placed, or <see langword="null"/> if it is not in this layout.</summary>
    public LayoutRect? BoundsOf(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return _bounds.TryGetValue(cell, out var bounds) ? bounds : null;
    }

    /// <summary>
    /// Where the first cell showing <paramref name="address"/> was placed, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Containers carry the address of the node they arrange, so the first match is the outermost one -
    /// the whole node, not one of the containers nested inside it. Callers that want a specific cell
    /// already have the cell.
    /// </remarks>
    public LayoutRect? BoundsOf(CellAddress address)
    {
        foreach (var placed in Cells)
        {
            if (placed.Cell.Address == address)
            {
                return placed.Bounds;
            }
        }

        return null;
    }

    /// <summary>
    /// How the text of <paramref name="cell"/> was broken into lines, or <see langword="null"/> when this
    /// layout drew no text for it.
    /// </summary>
    /// <remarks>
    /// Only the cells that draw a text of their own are in here: a cell that is empty on purpose - a slot
    /// waiting for a node - draws its marker on a single line without measuring it as text.
    /// </remarks>
    public LaidOutText? LinesOf(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return _text.TryGetValue(cell, out var lines) ? lines : null;
    }

    /// <summary>How the text of <paramref name="cell"/> is broken into lines, measured if need be.</summary>
    public LaidOutText TextOf(EditorCell cell, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(measure);

        return LinesOf(cell)
            ?? LaidOutText.OneLine(CellText.Of(cell), (cell as TextCell)?.Emphasis ?? TextEmphasis.None, measure);
    }

    /// <summary>
    /// The innermost cell covering the point, or <see langword="null"/> when the point is outside the
    /// laid-out tree.
    /// </summary>
    public PlacedCell? Find(double x, double y)
    {
        for (var i = Cells.Count - 1; i >= 0; i--)
        {
            if (Cells[i].Bounds.Contains(x, y))
            {
                return Cells[i];
            }
        }

        return null;
    }

    public override string ToString() => $"{Tree} laid out at {Size}";
}
