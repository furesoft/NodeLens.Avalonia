using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Turns a point in the layout into the caret position a click means.
/// </summary>
/// <remarks>
/// <para>
/// A projectional editor has no text to put a caret between, only cells, so a click has to answer two
/// questions: which cell was hit, and where inside it. The first is the innermost cell covering the
/// point - the layout already answers that - and the second is the character boundary of that cell
/// closest to the point, so that clicking the right half of a letter puts the caret after it, exactly
/// as it would in a text editor.
/// </para>
/// <para>
/// The point usually lands on something that is not a caret stop: the gap right of the last cell on a
/// line, the padding below a block, a container that only arranges other cells. Then the nearest stop
/// wins, compared row first and column second, which is what makes clicking into the empty area past
/// the end of a line put the caret at the end of that line rather than at the start of the next one.
/// </para>
/// <para>
/// Snapping measures the same prefixes <see cref="CaretBounds"/> measures, so a click always lands the
/// caret on a boundary the bar is actually drawn at; a second, cheaper approximation here would drift
/// from the caret the moment the measure stops being monospaced.
/// </para>
/// </remarks>
public static class HitTest
{
    /// <summary>The caret position a click at <paramref name="x"/>, <paramref name="y"/> means.</summary>
    /// <param name="tree">The tree whose places are searched.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="x">The horizontal coordinate, in layout coordinates.</param>
    /// <param name="y">The vertical coordinate, in layout coordinates.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    /// <returns>The position, or <see langword="null"/> when the tree has no cell to stand in.</returns>
    public static CaretPosition? Caret(EditorTree tree, CellLayout layout, double x, double y, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return Caret(CaretPlaces.Of(tree), layout, x, y, measure);
    }

    /// <summary>The caret position a click means, reusing places that are already known.</summary>
    /// <param name="places">The places of one tree, in document order.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="x">The horizontal coordinate, in layout coordinates.</param>
    /// <param name="y">The vertical coordinate, in layout coordinates.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    public static CaretPosition? Caret(
        IReadOnlyList<CaretPlace> places,
        CellLayout layout,
        double x,
        double y,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        if (places.Count == 0)
        {
            return null;
        }

        var place = Under(places, layout, x, y) ?? Nearest(places, layout, x, y);

        return place is { } found && layout.BoundsOf(found.Cell) is { } bounds
            ? CaretPosition.At(found, OffsetAt(found, bounds, x, measure))
            : null;
    }

    private static CaretPlace? Under(IReadOnlyList<CaretPlace> places, CellLayout layout, double x, double y) =>
        layout.Find(x, y) is { } placed ? PlaceOf(places, placed.Cell) : null;

    private static CaretPlace? PlaceOf(IReadOnlyList<CaretPlace> places, EditorCell cell)
    {
        foreach (var place in places)
        {
            if (ReferenceEquals(place.Cell, cell))
            {
                return place;
            }
        }

        return null;
    }

    private static CaretPlace? Nearest(IReadOnlyList<CaretPlace> places, CellLayout layout, double x, double y)
    {
        CaretPlace? nearest = null;
        var nearestRow = double.MaxValue;
        var nearestColumn = double.MaxValue;

        foreach (var place in places)
        {
            if (layout.BoundsOf(place.Cell) is not { } bounds)
            {
                continue;
            }

            var row = Distance(bounds.Y, bounds.Bottom, y);
            var column = Distance(bounds.X, bounds.Right, x);

            if (row > nearestRow || (row == nearestRow && column >= nearestColumn))
            {
                continue;
            }

            nearest = place;
            nearestRow = row;
            nearestColumn = column;
        }

        return nearest;
    }

    private static double Distance(double start, double end, double value) =>
        value < start ? start - value
        : value > end ? value - end
        : 0;

    private static int OffsetAt(CaretPlace place, LayoutRect bounds, double x, ITextMeasure measure)
    {
        var nearest = 0;
        var nearestDistance = double.MaxValue;

        for (var offset = 0; offset <= place.Length; offset++)
        {
            var distance = Math.Abs(bounds.X + CaretBounds.PrefixWidth(place, offset, measure) - x);

            if (distance < nearestDistance)
            {
                nearest = offset;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
