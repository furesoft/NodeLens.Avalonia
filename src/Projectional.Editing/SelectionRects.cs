using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The rectangles that show what is selected, one per cell the selection touches.
/// </summary>
/// <remarks>
/// <para>
/// A selection is two caret positions, but a highlight is geometry: a control wants boxes to fill, not
/// two offsets it has to measure itself. Splitting the range per cell is what makes a selection across
/// cell boundaries paint correctly in a notation that puts cells side by side - the cells are separate
/// boxes, and a single rectangle spanning them would highlight the gaps between them too.
/// </para>
/// <para>
/// The ends are trimmed to the character boundaries the selection names, so a selection never appears to
/// include a character the caret has not reached; the cells in between are highlighted whole.
/// </para>
/// </remarks>
public static class SelectionRects
{
    /// <summary>The highlight boxes of <paramref name="selection"/>.</summary>
    /// <param name="tree">The tree the selection lives in.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="selection">The selection to draw.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    public static IReadOnlyList<LayoutRect> Of(
        EditorTree tree,
        CellLayout layout,
        Selection selection,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return Of(CaretPlaces.Of(tree), layout, selection, measure);
    }

    /// <summary>The highlight boxes of <paramref name="selection"/>, reusing places that are already known.</summary>
    /// <param name="places">The places of one tree, in document order.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="selection">The selection to draw.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    /// <remarks>
    /// An empty selection, or one whose ends name cells that are no longer in the tree, produces no
    /// boxes at all. That is the only safe answer: there is nothing to highlight, and highlighting
    /// something adjacent would be worse than highlighting nothing.
    /// </remarks>
    public static IReadOnlyList<LayoutRect> Of(
        IReadOnlyList<CaretPlace> places,
        CellLayout layout,
        Selection selection,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        if (selection.IsEmpty)
        {
            return [];
        }

        var anchor = CaretPlaces.Resolve(places, selection.Anchor);
        var caret = CaretPlaces.Resolve(places, selection.Caret);
        var (first, last) = anchor <= caret ? (anchor, caret) : (caret, anchor);

        if (first < 0)
        {
            return [];
        }

        var start = anchor <= caret ? selection.Anchor.Offset : selection.Caret.Offset;
        var end = anchor <= caret ? selection.Caret.Offset : selection.Anchor.Offset;
        var rects = new List<LayoutRect>(last - first + 1);

        for (var index = first; index <= last; index++)
        {
            var place = places[index];

            if (layout.BoundsOf(place.Cell) is not { } bounds)
            {
                continue;
            }

            var from = index == first ? start : 0;
            var to = index == last ? end : place.Text.Length;

            if (first == last && to < from)
            {
                (from, to) = (to, from);
            }

            AddLines(rects, place, layout, bounds, from, to, measure);
        }

        return rects;
    }

    /// <summary>Adds one box per line the range touches, which is one box for a place of one line.</summary>
    /// <remarks>
    /// The ends are trimmed to the line they fall on rather than to the cell, so a selection that starts
    /// in the middle of a wrapped value highlights only the rest of that line and not the box around it.
    /// A place that is selected whole is highlighted as a block, keeping its full width on every line,
    /// because a selection is a range of cells and a ragged right edge would misread as a ragged range.
    /// </remarks>
    private static void AddLines(
        List<LayoutRect> rects,
        CaretPlace place,
        CellLayout layout,
        LayoutRect bounds,
        int from,
        int to,
        ITextMeasure measure)
    {
        var lines = CaretGeometry.LinesOf(place, layout, measure);
        var whole = from <= 0 && to >= place.Text.Length;

        for (var index = 0; index < lines.LineCount; index++)
        {
            var line = lines.Lines[index];

            if (to < line.Start || from > line.ContentEnd)
            {
                continue;
            }

            var left = whole || from <= line.Start
                ? 0
                : CaretGeometry.LeftOn(place, lines, index, from, measure);
            var right = whole
                ? bounds.Width
                : to >= line.ContentEnd
                    ? line.Width
                    : CaretGeometry.LeftOn(place, lines, index, to, measure);

            if (right > left)
            {
                rects.Add(
                    new LayoutRect(
                        bounds.X + left,
                        CaretGeometry.TopOf(bounds, index, measure),
                        right - left,
                        measure.LineHeight));
            }
        }
    }
}
