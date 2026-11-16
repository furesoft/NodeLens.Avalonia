using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Which fold arrow, if any, a point in the gutter hits.</summary>
/// <remarks>
/// The only new reader of gutter geometry, and it is a separate type for the same reason
/// <see cref="MarkerHitTest"/> is: the control asks it before it asks where the caret would go, and a press
/// that misses must fall through to the behaviour the gutter already had - nothing.
/// </remarks>
public static class FoldHitTest
{
    /// <summary>The mark under a point, or <see langword="null"/> when the point is in another column.</summary>
    /// <param name="marks">The marks of the document, as <see cref="FoldMarks.Of"/> answered them.</param>
    /// <param name="left">The left edge of the twisty column, in the same units as the marks.</param>
    /// <param name="width">How wide the column is.</param>
    /// <param name="x">The point's x coordinate.</param>
    /// <param name="y">The point's y coordinate.</param>
    /// <returns>The mark, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The column is tested first, so a point over a row number can never be mistaken for a point over an
    /// arrow even if a mark's box were wider than the column.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="marks"/> is <see langword="null"/>.</exception>
    public static FoldMark? At(IReadOnlyList<FoldMark> marks, double left, double width, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(marks);

        if (x < left || x >= left + width)
        {
            return null;
        }

        foreach (var mark in marks)
        {
            if (mark.Contains(x, y))
            {
                return mark;
            }
        }

        return null;
    }
}
