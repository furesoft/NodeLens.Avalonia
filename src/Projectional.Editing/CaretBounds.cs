using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Where the caret is drawn: the zero-width bar at the character boundary a caret position names.
/// </summary>
/// <remarks>
/// <para>
/// A caret is a boundary, not a box, so the rectangle has no width and the renderer gives it thickness
/// when it paints. Keeping the raw boundary here rather than a widened rect is what lets a control choose
/// a width that matches its zoom and DPI without the frame knowing about either.
/// </para>
/// <para>
/// The height is one line, not the height of the cell the caret stands in: a cell with several lines of
/// text is several lines tall, and a bar covering all of them would say the caret is on all of them at
/// once. A caret on a slot rather than on text stands on the slot's own line, which is where its marker
/// is drawn.
/// </para>
/// </remarks>
public static class CaretBounds
{
    /// <summary>Where <paramref name="position"/> is drawn, or <see langword="null"/> when it names no cell.</summary>
    /// <param name="tree">The tree whose places are searched.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="position">The position to locate.</param>
    /// <param name="measure">The measure that turns the text before the caret into a distance.</param>
    public static LayoutRect? Of(EditorTree tree, CellLayout layout, CaretPosition position, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return Of(CaretPlaces.Of(tree), layout, position, measure);
    }

    /// <summary>Where <paramref name="position"/> is drawn, reusing places that are already known.</summary>
    /// <param name="places">The places of one tree, in document order.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="position">The position to locate.</param>
    /// <param name="measure">The measure that turns the text before the caret into a distance.</param>
    /// <remarks>
    /// A position outlives the cells it names - an undo, or a re-projection, can leave it pointing at
    /// something that is no longer there - so a missing cell answers <see langword="null"/> rather than
    /// guessing a nearby one.
    /// </remarks>
    public static LayoutRect? Of(
        IReadOnlyList<CaretPlace> places,
        CellLayout layout,
        CaretPosition position,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        if (CaretPlaces.PlaceAt(places, position) is not { } place)
        {
            return null;
        }

        if (layout.BoundsOf(place.Cell) is not { } bounds)
        {
            return null;
        }

        var lines = CaretGeometry.LinesOf(place, layout, measure);
        var lineIndex = CaretGeometry.LineIndexOf(lines, position.Offset);

        return new LayoutRect(
            bounds.X + CaretGeometry.LeftOn(place, lines, lineIndex, position.Offset, measure),
            CaretGeometry.TopOf(bounds, lineIndex, measure),
            0,
            measure.LineHeight);
    }
}
