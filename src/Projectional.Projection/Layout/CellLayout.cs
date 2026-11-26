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
    private readonly Dictionary<EditorCell, LaidOutText> _text;
    private readonly ITextMeasure _measure;

    // Built on the first question that needs it and never invalidated. Painting does not need it - a painter
    // walks Cells and has each cell's bounds in hand - but the caret, hit test and markers do, and building it
    // eagerly cost one insertion per cell on every layout.
    private Dictionary<EditorCell, LayoutRect>? _bounds;

    internal CellLayout(
        EditorTree tree,
        LayoutSize size,
        IReadOnlyList<PlacedCell> cells,
        Dictionary<EditorCell, LaidOutText> text,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);

        Tree = tree;
        Size = size;
        Cells = cells;
        _measure = measure;

        // Kept, not copied: the layouter builds it for this layout and then forgets it, and a copy of a
        // per-cell map is one full pass over the document for nothing.
        _text = text;
    }

    /// <summary>The tree that was laid out.</summary>
    public EditorTree Tree { get; }

    /// <summary>How much room the whole tree takes, including the parts outside any viewport.</summary>
    public LayoutSize Size { get; }

    /// <summary>Every cell with its bounds, in document order.</summary>
    public IReadOnlyList<PlacedCell> Cells { get; }

    /// <summary>Where a specific cell was placed, or <see langword="null"/> if it is not in this layout.</summary>
    /// <remarks>
    /// The lookup map is built on the first call, because a painter never asks for it: it walks
    /// <see cref="Cells"/> and already has the bounds of each cell it draws.
    /// </remarks>
    public LayoutRect? BoundsOf(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        _bounds ??= BoundsByCell();

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
    /// <para>
    /// The map holds only what cannot be derived later - the text cells that **wrapped**. A text that stayed
    /// on one line is the text plus its width, which the measure answers, and a cell that draws no text of
    /// its own - a slot waiting for a node, which draws its marker on a single line - has no lines at all
    /// and answers <see langword="null"/>.
    /// </para>
    /// <para>
    /// That is what keeps a per-cell map from describing every cell of the document when only the ones that
    /// wrapped carry information, and it is not a re-measure: a wrapped paragraph, the case that would cost
    /// one, is stored.
    /// </para>
    /// </remarks>
    public LaidOutText? LinesOf(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (_text.TryGetValue(cell, out var lines))
        {
            return lines;
        }

        return cell is TextCell text ? LaidOutText.OneLine(text.Text, text.Emphasis, _measure) : null;
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

    /// <summary>
    /// Where a table's header row is, as one band across the grid's content width, or <see langword="null"/> when
    /// the layout placed no header cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One band rather than one region per header cell, because a column is as wide as its widest cell and a header is
    /// only as wide as its own text: a region per header would stop short of the column it names. The extent across
    /// is therefore the table's own width, minus the room its own decoration takes, and the extent down is the height
    /// of the tallest header cell.
    /// </para>
    /// <para>
    /// This is geometry, not policy: a table with no <see cref="TableCell.HeaderFill"/> still has a header row, and
    /// the caller that wants a band asks for both.
    /// </para>
    /// </remarks>
    public LayoutRect? HeaderBandOf(TableCell table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (BoundsOf(table) is not { } tableBounds)
        {
            return null;
        }

        var top = double.MaxValue;
        var bottom = double.MinValue;

        foreach (var header in table.Headers)
        {
            if (BoundsOf(header) is { } bounds)
            {
                top = Math.Min(top, bounds.Y);
                bottom = Math.Max(bottom, bounds.Bottom);
            }
        }

        if (bottom <= top)
        {
            return null;
        }

        var inset = table.Paint?.Inset ?? CellEdges.Zero;

        return new LayoutRect(
            tableBounds.X + inset.Left,
            top,
            tableBounds.Width - inset.Horizontal,
            bottom - top);
    }

    private Dictionary<EditorCell, LayoutRect> BoundsByCell()
    {
        var bounds = new Dictionary<EditorCell, LayoutRect>(Cells.Count);

        foreach (var placed in Cells)
        {
            bounds[placed.Cell] = placed.Bounds;
        }

        return bounds;
    }
}
