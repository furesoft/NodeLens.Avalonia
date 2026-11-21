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
/// <para>
/// A text longer than the room left to the page's edge is drawn on several lines, so a paragraph stays
/// readable without the reader scrolling sideways. How the text breaks is decided here, once, and
/// remembered in the layout: the caret and the selection read those lines instead of measuring again.
/// </para>
/// </remarks>
public sealed class CellLayouter
{
    private readonly ITextMeasure _measure;
    private readonly IColumnWidths? _columnWidths;

    /// <summary>Creates a layouter that places cells on a page of <paramref name="wrapWidth"/>.</summary>
    /// <param name="measure">Measures the text of a cell.</param>
    /// <param name="wrapWidth">
    /// Where the page ends, and therefore where a text breaks, or <see langword="null"/> for a page that
    /// never ends - which is what a caller that has no width to give wants.
    /// </param>
    /// <param name="columnWidths">
    /// The widths the reader has given a table's columns, or <see langword="null"/> when nobody has
    /// resized anything and every column is as wide as its content.
    /// </param>
    public CellLayouter(ITextMeasure measure, double? wrapWidth = null, IColumnWidths? columnWidths = null)
    {
        ArgumentNullException.ThrowIfNull(measure);
        _measure = measure;
        WrapWidth = wrapWidth;
        _columnWidths = columnWidths;
    }

    /// <summary>Where the page ends, and therefore where a text breaks, if anywhere.</summary>
    public double? WrapWidth { get; }

    /// <summary>Lays out <paramref name="tree"/> with its top-left corner at the origin.</summary>
    public CellLayout Layout(EditorTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var placed = new List<PlacedCell>();
        var text = new Dictionary<EditorCell, LaidOutText>();
        var size = Arrange(tree.Root, x: 0, y: 0, placed, text);

        return new CellLayout(tree, size, placed, text, _measure);
    }

    private LayoutSize Arrange(EditorCell cell, double x, double y, List<PlacedCell> placed, Dictionary<EditorCell, LaidOutText> text)
    {
        // The cell is claimed before its content is arranged, so that the placement list stays in
        // document order even though a container only knows its size once its children do.
        var index = placed.Count;
        placed.Add(default);

        var size = cell switch
        {
            TextCell line => TextSize(line, x, text),
            ContainerCell { Orientation: CellOrientation.Horizontal } line => ArrangeInline(line.Content, x, y, placed, text),
            ContainerCell stack => ArrangeStacked(stack, x, y, placed, text),
            ChildCell { IsEmpty: true } empty => SingleLineSize(empty.EmptyText),
            ChildCell { IsStacked: true } stacked => ArrangeVertical(stacked.Content, x, x, y, placed, text, stacked.BlankLineBefore),
            ChildCell child => ArrangeInline(child.Content, x, y, placed, text),
            CollectionCell { IsEmpty: true } empty => SingleLineSize(empty.EmptyText),
            CollectionCell collection => ArrangeInline(collection.Content, x, y, placed, text),
            TableCell table => ArrangeTable(table, x, y, placed, text),
            _ => throw new ProjectionException($"The cell {cell} is of a kind that layout does not know."),
        };

        placed[index] = new PlacedCell(cell, new LayoutRect(x, y, size.Width, size.Height));
        return size;
    }

    private LayoutSize ArrangeInline(
        IReadOnlyList<EditorCell> cells,
        double x,
        double y,
        List<PlacedCell> placed,
        Dictionary<EditorCell, LaidOutText> text)
    {
        var cursorX = x;
        var cursorY = y;
        var bottom = y;

        foreach (var cell in cells)
        {
            var size = Arrange(cell, cursorX, cursorY, placed, text);

            cursorX += size.Width;
            bottom = Math.Max(bottom, cursorY + size.Height);

            // A cell that stacks inside a line costs the line its extra rows, and what follows
            // continues on the cell's last line - the way a block nested in a line reads, and the
            // reason a closing brace ends up next to the last statement instead of below the block.
            cursorY += size.Height - _measure.LineHeight;
        }

        return new LayoutSize(cursorX - x, Math.Max(_measure.LineHeight, bottom - y));
    }

    private LayoutSize ArrangeStacked(
        ContainerCell container,
        double x,
        double y,
        List<PlacedCell> placed,
        Dictionary<EditorCell, LaidOutText> text) =>
        ArrangeVertical(
            container.Content,
            container.IsIndented ? x + _measure.IndentWidth : x,
            x,
            y,
            placed,
            text);

    /// <summary>Stacks <paramref name="cells"/> one above the other, starting at <paramref name="y"/>.</summary>
    /// <param name="cells">The cells to stack, in document order.</param>
    /// <param name="left">The x every cell starts at.</param>
    /// <param name="x">The x the resulting box starts at, which differs from <paramref name="left"/> when indented.</param>
    /// <param name="y">The top of the box.</param>
    /// <param name="placed">The placement list being filled, in document order.</param>
    /// <param name="text">The laid-out text being filled.</param>
    /// <param name="blankLineBefore">
    /// The positions that start a group and therefore get one empty line above them, or <see langword="null"/> for
    /// none.
    /// </param>
    /// <remarks>
    /// One implementation for both a vertical container and a stacked child slot: an indented container
    /// and a slot told to stack differ in where the box starts, not in how the stack behaves, and a
    /// second copy would be a second chance to disagree about line height.
    /// </remarks>
    private LayoutSize ArrangeVertical(
        IReadOnlyList<EditorCell> cells,
        double left,
        double x,
        double y,
        List<PlacedCell> placed,
        Dictionary<EditorCell, LaidOutText> text,
        IReadOnlySet<int>? blankLineBefore = null)
    {
        var cursorY = y;
        var right = x;

        for (var index = 0; index < cells.Count; index++)
        {
            // A group boundary is one empty row, and it is space rather than a cell: nothing is drawn there, so
            // there is nothing to hit-test and no caret to stop on.
            if (blankLineBefore is { } starts && starts.Contains(index))
            {
                cursorY += _measure.LineHeight;
            }

            var size = Arrange(cells[index], left, cursorY, placed, text);

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
    private LayoutSize ArrangeTable(TableCell table, double x, double y, List<PlacedCell> placed, Dictionary<EditorCell, LaidOutText> text)
    {
        var gap = _measure.Measure(" ", TextEmphasis.None);
        var columnCount = table.ColumnCount;

        // The drawn grid, not the model's rows: an empty table draws one affordance row, and layout
        // must place exactly the cells the tree walks or the caret would stand in a cell nobody drew.
        var rowCount = (table.Content.Count / columnCount) - 1;
        var columns = new double[columnCount];
        var rowHeights = new double[rowCount + 1];
        var grid = new List<PlacedCell>[(rowCount + 1) * columnCount];

        for (var c = 0; c < columnCount; c++)
        {
            grid[c] = ArrangeDetached(table.Headers[c], columns, rowHeights, c, 0, text);
        }

        for (var r = 0; r < rowCount; r++)
        {
            for (var c = 0; c < columnCount; c++)
            {
                grid[((r + 1) * columnCount) + c] = ArrangeDetached(
                    table.Content[((r + 1) * columnCount) + c],
                    columns,
                    rowHeights,
                    c,
                    r + 1,
                    text);
            }
        }

        var columnLeft = new double[columnCount];
        var cursorX = x;

        for (var c = 0; c < columnCount; c++)
        {
            // A width the reader dragged is a floor, never a ceiling: a column narrower than its content
            // would clip a value or overlap its neighbour, and the model is what the reader came to read.
            if (_columnWidths?.WidthOf(table.Address.Owner, table.Slot, c) is { } asked)
            {
                columns[c] = Math.Max(columns[c], asked);
            }

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
        int row,
        Dictionary<EditorCell, LaidOutText> text)
    {
        var detached = new List<PlacedCell>();
        var size = Arrange(cell, x: 0, y: 0, detached, text);

        columns[column] = Math.Max(columns[column], size.Width);
        rowHeights[row] = Math.Max(rowHeights[row], size.Height);

        return detached;
    }

    /// <summary>How far a text cell at <paramref name="x"/> may run before it reaches the page's edge.</summary>
    /// <remarks>
    /// A cell that already starts at or past the edge is not wrapped at all: it has no room to wrap into,
    /// and a line one character wide is worse than a line that sticks out.
    /// </remarks>
    private double? LimitAt(double x) => WrapWidth is { } width && width > x ? width - x : null;

    private LayoutSize TextSize(TextCell cell, double x, Dictionary<EditorCell, LaidOutText> text)
    {
        // An empty text still occupies one space: a value nobody has typed yet has to be a place the
        // caret can stand on, and a zero-width cell is not. The wrapper keeps that floor for the width.
        if (TextWrapper.SingleLineWidth(cell.Text, cell.Emphasis, _measure, LimitAt(x)) is { } width)
        {
            // One line is the text plus the measure's height, and CellLayout answers it from the measure, so
            // nothing is remembered for it. A property showing a placeholder instead of its value is drawn
            // where the (empty) value would go, so the box has to be as wide as the stand-in: it is one token
            // and never wrapped, and the lines that are not stored still describe the value, which is what
            // the caret keeps standing in.
            return cell is PropertyCell { Text.Length: 0, EmptyText.Length: > 0 } empty
                ? new LayoutSize(_measure.Measure(empty.EmptyText, empty.Emphasis), _measure.LineHeight)
                : new LayoutSize(width, _measure.LineHeight);
        }

        var lines = LaidOutText.Wrap(cell.Text, cell.Emphasis, _measure, LimitAt(x));
        text[cell] = lines;

        if (cell is PropertyCell { Text.Length: 0, EmptyText.Length: > 0 } placeholder)
        {
            return new LayoutSize(_measure.Measure(placeholder.EmptyText, placeholder.Emphasis), lines.Height);
        }

        return new LayoutSize(lines.Width, lines.Height);
    }

    private LayoutSize SingleLineSize(string text)
    {
        // An empty text still occupies one space: a value nobody has typed yet, or the marker for an
        // empty slot, has to be a place the caret can stand on, and a zero-width cell is not.
        var width = _measure.Measure(text.Length == 0 ? " " : text, TextEmphasis.None);

        return new LayoutSize(width, _measure.LineHeight);
    }
}
