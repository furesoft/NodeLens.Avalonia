using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Where the quick-fix bulb sits: the square just left of the page, level with the caret's line.
/// </summary>
/// <remarks>
/// <para>
/// Geometry only, no pixels: the answer is a <see cref="LayoutRect"/> in the layout's own coordinates,
/// with a <em>negative</em> <see cref="LayoutRect.X"/> because the bulb is outside the page. A renderer
/// translates it into whatever margin it reserved - the control adds its gutter width - so a second
/// renderer can draw its own glyph without sharing this type's idea of what a bulb looks like.
/// </para>
/// <para>
/// The box is one line square, so it is exactly as tall as the line it belongs to and cannot bleed into
/// the line above or below. Its height comes from the measure rather than from the caret's rectangle,
/// because a caret standing in a cell whose text is several lines tall would otherwise produce a bulb
/// that covers all of them.
/// </para>
/// </remarks>
public static class QuickFixBulb
{
    /// <summary>The blank between the bulb and the page's left edge, in layout units.</summary>
    /// <remarks>
    /// Not zero: a glyph touching the first character of a line reads as part of the document, and the
    /// whole point of the margin is that it is not.
    /// </remarks>
    public const double Gap = 2;

    /// <summary>Where the bulb is drawn for <paramref name="position"/>.</summary>
    /// <param name="tree">The tree whose places are searched.</param>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="position">The caret the bulb belongs to.</param>
    /// <param name="measure">The measure that decides how large one line is.</param>
    /// <returns>The box, or <see langword="null"/> when the position names no cell.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    public static LayoutRect? BoundsOf(
        EditorTree tree,
        CellLayout layout,
        CaretPosition position,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return BoundsOf(CaretBounds.Of(tree, layout, position, measure), measure);
    }

    /// <summary>Where the bulb is drawn for a caret rectangle that is already known.</summary>
    /// <param name="caret">The caret's rectangle, or <see langword="null"/> for a caret that is not drawn.</param>
    /// <param name="measure">The measure that decides how large one line is.</param>
    /// <returns>The box, or <see langword="null"/> when there is no caret to align with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> is <see langword="null"/>.</exception>
    public static LayoutRect? BoundsOf(LayoutRect? caret, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(measure);

        if (caret is not { } line)
        {
            return null;
        }

        var side = measure.LineHeight;

        return new LayoutRect(-(side + Gap), line.Y, side, side);
    }
}
