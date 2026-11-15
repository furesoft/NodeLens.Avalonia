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

    private LayoutSize TextSize(string text, TextEmphasis emphasis)
    {
        // An empty text still occupies one space: a value nobody has typed yet, or the marker for an
        // empty slot, has to be a place the caret can stand on, and a zero-width cell is not.
        var width = _measure.Measure(text.Length == 0 ? " " : text, emphasis);

        return new LayoutSize(width, _measure.LineHeight);
    }
}
