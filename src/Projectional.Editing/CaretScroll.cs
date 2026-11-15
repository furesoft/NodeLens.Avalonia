using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The scroll offset that keeps the caret on screen.
/// </summary>
/// <remarks>
/// <para>
/// Scrolling the caret into view is the one scroll a control does on its own, on every keystroke, so it
/// has to be cheap and it has to be calm: the offset only changes when the caret would otherwise be
/// invisible, and then only by as much as it takes to touch the nearest edge.
/// </para>
/// <para>
/// A caret that names no cell leaves the offset alone rather than jumping somewhere: positions outlive
/// the cells they name, and a stale one during a re-projection should not move the view. It is still
/// clamped, so a window that grew keeps a legal offset.
/// </para>
/// </remarks>
public static class CaretScroll
{
    /// <summary>The offset to scroll to so that <paramref name="caret"/> is visible.</summary>
    /// <param name="layout">The layout that says where cells sit.</param>
    /// <param name="viewport">How much of the layout fits on screen.</param>
    /// <param name="offsetX">The current horizontal offset.</param>
    /// <param name="offsetY">The current vertical offset.</param>
    /// <param name="caret">The position to bring into view.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    public static (double X, double Y) Offset(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        CaretPosition caret,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        return CaretBounds.Of(layout.Tree, layout, caret, measure) is { } target
            ? LayoutViewport.ScrollIntoView(layout, viewport, offsetX, offsetY, target)
            : LayoutViewport.Clamp(layout, viewport, offsetX, offsetY);
    }
}
