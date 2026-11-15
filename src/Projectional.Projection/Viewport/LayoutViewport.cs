namespace Projectional.Projection;

/// <summary>
/// The slice of a layout that a scrolling control is showing: which cells it has to draw, and where
/// they land once the scroll offset is subtracted.
/// </summary>
/// <remarks>
/// <para>
/// Layout answers how much room a model takes; a viewport answers how much of that the user can see.
/// Keeping the two apart is what makes virtualization a filter instead of a second layout: the whole
/// tree is laid out once, and only the cells that hit-test into the visible rectangle are ever drawn.
/// That is sufficient in M1 because none of the built cells arranges itself differently with width -
/// when that changes, this type becomes the place that lays out a subtree on demand.
/// </para>
/// <para>
/// The offset is clamped on the way in, so a viewport always describes a position the control can
/// actually be scrolled to. Content smaller than the viewport therefore has exactly one offset, zero,
/// which is what stops a control from asking to scroll past the end of a short document.
/// </para>
/// </remarks>
public sealed class LayoutViewport
{
    private LayoutViewport(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        IReadOnlyList<PlacedCell> cells)
    {
        Layout = layout;
        ViewportSize = viewport;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Cells = cells;
    }

    /// <summary>The layout being looked at.</summary>
    public CellLayout Layout { get; }

    /// <summary>How big the window onto the layout is.</summary>
    public LayoutSize ViewportSize { get; }

    /// <summary>The horizontal scroll offset, already clamped to the scrollable range.</summary>
    public double OffsetX { get; }

    /// <summary>The vertical scroll offset, already clamped to the scrollable range.</summary>
    public double OffsetY { get; }

    /// <summary>
    /// The cells that intersect the window, in document order, parents before children.
    /// </summary>
    /// <remarks>
    /// Drawing these in order is enough, and drawing in order is the only requirement a renderer has, so
    /// a container that happens to be in the list is harmless: containers carry no text.
    /// </remarks>
    public IReadOnlyList<PlacedCell> Cells { get; }

    /// <summary>The area of the layout that is on screen, in layout coordinates.</summary>
    public LayoutRect Window => new(OffsetX, OffsetY, ViewportSize.Width, ViewportSize.Height);

    /// <summary>
    /// How far the control may scroll: the content size minus the viewport, floored at zero on each
    /// axis, so a document narrower or shorter than the window reports no scrollable range rather than a
    /// negative one.
    /// </summary>
    public static LayoutSize ScrollableExtent(CellLayout layout, LayoutSize viewport)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return new LayoutSize(
            Math.Max(0, layout.Size.Width - viewport.Width),
            Math.Max(0, layout.Size.Height - viewport.Height));
    }

    /// <summary>
    /// Builds the viewport for a scroll offset, clamping that offset to the range
    /// <see cref="ScrollableExtent"/> allows.
    /// </summary>
    public static LayoutViewport Of(CellLayout layout, LayoutSize viewport, double offsetX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var (x, y) = Clamp(layout, viewport, offsetX, offsetY);
        var window = new LayoutRect(x, y, viewport.Width, viewport.Height);
        var cells = new List<PlacedCell>();

        foreach (var placed in layout.Cells)
        {
            if (placed.Bounds.Intersects(window))
            {
                cells.Add(placed);
            }
        }

        return new LayoutViewport(layout, viewport, x, y, cells);
    }

    /// <summary>Trims a scroll offset to the range the content actually has.</summary>
    public static (double X, double Y) Clamp(CellLayout layout, LayoutSize viewport, double offsetX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var extent = ScrollableExtent(layout, viewport);

        return (Math.Clamp(offsetX, 0, extent.Width), Math.Clamp(offsetY, 0, extent.Height));
    }

    /// <summary>
    /// The smallest scroll offset change that brings <paramref name="target"/> fully into view, starting
    /// from the given offset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Smallest" is what keeps a caret from jumping to the middle of the window on every keystroke: a
    /// target already inside the window produces no movement at all, and one outside moves exactly far
    /// enough to touch the nearest edge.
    /// </para>
    /// <para>
    /// A target taller or wider than the window is aligned to its start rather than its end, so a block
    /// that does not fit is shown from its first line - reading order beats showing the tail.
    /// </para>
    /// <para>
    /// An axis whose window has no extent is left alone. That is the state of a control that has not been
    /// arranged yet, and it has no answer to give: no part of the layout is on screen, so no offset shows
    /// more than another. Treating the target as "wider than the window" - which is what the start
    /// alignment would do - would instead park the layout at the caret's own position, and a document set
    /// on a control before its first arrange would open scrolled to the caret rather than to its start.
    /// </para>
    /// </remarks>
    public static (double X, double Y) ScrollIntoView(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        LayoutRect target)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return Clamp(
            layout,
            viewport,
            ScrollAxis(offsetX, viewport.Width, target.X, target.Right),
            ScrollAxis(offsetY, viewport.Height, target.Y, target.Bottom));
    }

    /// <summary>
    /// Converts a rectangle from layout coordinates to control coordinates.
    /// </summary>
    /// <remarks>
    /// Every drawing call on the control side goes through this, so there is one place that knows the
    /// scroll offset and no code that subtracts it by hand.
    /// </remarks>
    public LayoutRect ToViewport(LayoutRect layoutRect) => layoutRect.Offset(-OffsetX, -OffsetY);

    /// <summary>Converts a point from control coordinates to layout coordinates.</summary>
    public (double X, double Y) ToLayout(double x, double y) => (x + OffsetX, y + OffsetY);

    private static double ScrollAxis(double offset, double extent, double start, double end) =>
        extent <= 0 ? offset
        : end - start >= extent || start < offset ? start
        : end > offset + extent ? end - extent
        : offset;

    public override string ToString() =>
        $"viewport {ViewportSize} at ({OffsetX}, {OffsetY}) showing {Cells.Count} of {Layout.Cells.Count} cells";
}
