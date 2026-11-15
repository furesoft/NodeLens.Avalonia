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
/// The height is the height of the cell the caret stands in, not of the line, because a slot may be
/// taller than the text it holds - and a caret that only covered the glyph would look misplaced in an
/// indented block.
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

        return new LayoutRect(bounds.X + PrefixWidth(place, position.Offset, measure), bounds.Y, 0, bounds.Height);
    }

    /// <summary>How wide the first <paramref name="length"/> characters of a place's text are.</summary>
    /// <param name="place">The place whose text is measured.</param>
    /// <param name="length">How many characters to measure, clamped to the text length.</param>
    /// <param name="measure">The measure to use.</param>
    /// <remarks>
    /// Offsets are clamped rather than rejected: a caret past the end of a shortened property would
    /// otherwise draw outside its cell, and drawing it at the end is both harmless and what the user
    /// expects while a deletion is being typed through.
    /// </remarks>
    internal static double PrefixWidth(CaretPlace place, int length, ITextMeasure measure)
    {
        var clamped = Math.Clamp(length, 0, place.Text.Length);

        return clamped == place.Text.Length ? measure.Measure(place.Text) : measure.Measure(place.Text[..clamped]);
    }
}
