using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Assigns every cell of an editor tree a place on the page.
/// </summary>
/// <remarks>
/// <para>
/// Arrangement is one pass over the tree that returns each cell's size on the way out, and it is
/// purely a function of the tree plus a measure: nothing here reads the model, and nothing is cached
/// between calls. A tree that has changed is a different tree, so a stale placement cannot exist, and
/// the caller decides when to lay out again.
/// </para>
/// <para>
/// The result is a flat list of placed cells in document order rather than a second tree of boxes.
/// Both of the things layout exists for - drawing the visible region and answering "what did the user
/// point at" - want absolute coordinates, and a flat list is exactly that. Nesting is still visible in
/// the list, because a container is placed before the cells it arranges and covers the same area.
/// </para>
/// </remarks>
public sealed class CellLayouter
{
    private readonly ITextMeasure _measure;

    public CellLayouter(ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        _measure = measure;
    }

    /// <summary>Lays out <paramref name="tree"/> with its top-left corner at the origin.</summary>
    public CellLayout Layout(EditorTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var placed = new List<PlacedCell>();
        var size = Arrange(tree.Root, x: 0, y: 0, placed);

        return new CellLayout(tree, size, placed);
    }

    private LayoutSize Arrange(EditorCell cell, double x, double y, List<PlacedCell> placed)
    {
        // The cell is claimed before its content is arranged, so that the placement list stays in
        // document order even though a container only knows its size once its children do.
        var index = placed.Count;
        placed.Add(default);

        var size = cell switch
        {
            TextCell text => TextSize(text.Text, text.Emphasis),
            ContainerCell { Orientation: CellOrientation.Horizontal } line => ArrangeInline(line.Content, x, y, placed),
            ContainerCell stack => ArrangeStacked(stack, x, y, placed),
            ChildCell { IsEmpty: true } empty => TextSize(empty.EmptyText, TextEmphasis.None),
            ChildCell child => ArrangeInline(child.Content, x, y, placed),
            CollectionCell { IsEmpty: true } empty => TextSize(empty.EmptyText, TextEmphasis.None),
            CollectionCell collection => ArrangeInline(collection.Content, x, y, placed),
            TableCell { IsEmpty: true } empty => TextSize(empty.EmptyText, TextEmphasis.None),
            TableCell table => ArrangeTable(table, x, y, placed),
            _ => throw new ProjectionException($"The cell {cell} is of a kind that layout does not know."),
        };

        placed[index] = new PlacedCell(cell, new LayoutRect(x, y, size.Width, size.Height));
        return size;
    }

    private LayoutSize ArrangeInline(IReadOnlyList<EditorCell> cells, double x, double y, List<PlacedCell> placed)
    {
        var cursorX = x;
        var cursorY = y;
        var bottom = y;

        foreach (var cell in cells)
        {
            var size = Arrange(cell, cursorX, cursorY, placed);

            cursorX += size.Width;
            bottom = Math.Max(bottom, cursorY + size.Height);

            // A cell that stacks inside a line costs the line its extra rows, and what follows
            // continues on the cell's last line - the way a block nested in a line reads, and the
            // reason a closing brace ends up next to the last statement instead of below the block.
            cursorY += size.Height - _measure.LineHeight;
        }

        return new LayoutSize(cursorX - x, Math.Max(_measure.LineHeight, bottom - y));
    }

    private LayoutSize ArrangeStacked(ContainerCell container, double x, double y, List<PlacedCell> placed)
    {
        var left = container.IsIndented ? x + _measure.IndentWidth : x;
        var cursorY = y;
        var right = x;

        foreach (var child in container.Content)
        {
            var size = Arrange(child, left, cursorY, placed);

            cursorY += size.Height;
            right = Math.Max(right, left + size.Width);
        }

        return new LayoutSize(right - x, Math.Max(_measure.LineHeight, cursorY - y));
    }

    /// <summary>
    /// Lays a table out as a grid: every column as wide as its widest cell, every row as tall as its
    /// tallest one.
    /// </summary>
    /// <remarks>
    /// The widths are only known once every cell has been measured, and arranging is also what
    /// measures, so each cell is first arranged on its own at the origin and then moved to the place
    /// the finished grid gives it. Moving a cell moves everything below it by the same amount, which
    /// keeps the placement list in document order and the subtree intact.
    /// </remarks>
    private LayoutSize ArrangeTable(TableCell table, double x, double y, List<PlacedCell> placed)
    {
        var gap = _measure.Measure(" ", TextEmphasis.None);
        var columnCount = table.ColumnCount;
        var rowCount = table.Rows.Count;
        var columns = new double[columnCount];
        var rowHeights = new double[rowCount + 1];
        var grid = new List<PlacedCell>[(rowCount + 1) * columnCount];

        for (var c = 0; c < columnCount; c++)
        {
            grid[c] = ArrangeDetached(table.Headers[c], columns, rowHeights, c, 0);
        }

        for (var r = 0; r < rowCount; r++)
        {
            var row = table.Rows[r];

            for (var c = 0; c < columnCount; c++)
            {
                grid[((r + 1) * columnCount) + c] = ArrangeDetached(row[c], columns, rowHeights, c, r + 1);
            }
        }

        var columnLeft = new double[columnCount];
        var cursorX = x;

        for (var c = 0; c < columnCount; c++)
        {
            columnLeft[c] = cursorX;
            cursorX += columns[c] + gap;
        }

        var rowTop = new double[rowCount + 1];
        var cursorY = y;

        for (var r = 0; r <= rowCount; r++)
        {
            rowTop[r] = cursorY;
            cursorY += rowHeights[r];
        }

        for (var r = 0; r <= rowCount; r++)
        {
            for (var c = 0; c < columnCount; c++)
            {
                var dx = columnLeft[c];
                var dy = rowTop[r];

                foreach (var cell in grid[(r * columnCount) + c])
                {
                    placed.Add(cell with { Bounds = cell.Bounds.Offset(dx, dy) });
                }
            }
        }

        return new LayoutSize(cursorX - gap - x, Math.Max(_measure.LineHeight, cursorY - y));
    }

    /// <summary>
    /// Arranges one grid cell at the origin, into a list of its own, and records how wide and how tall
    /// it turned out so that the grid can size its column and its row.
    /// </summary>
    private List<PlacedCell> ArrangeDetached(
        EditorCell cell,
        double[] columns,
        double[] rowHeights,
        int column,
        int row)
    {
        var detached = new List<PlacedCell>();
        var size = Arrange(cell, x: 0, y: 0, detached);

        columns[column] = Math.Max(columns[column], size.Width);
        rowHeights[row] = Math.Max(rowHeights[row], size.Height);

        return detached;
    }

    private LayoutSize TextSize(string text, TextEmphasis emphasis)
    {
        // An empty text still occupies one space: a value nobody has typed yet, or the marker for an
        // empty slot, has to be a place the caret can stand on, and a zero-width cell is not.
        var width = _measure.Measure(text.Length == 0 ? " " : text, emphasis);

        return new LayoutSize(width, _measure.LineHeight);
    }
}
