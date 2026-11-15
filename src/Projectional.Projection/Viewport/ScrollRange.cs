namespace Projectional.Projection;

/// <summary>
/// The numbers a scroll host asks a scrolling control for: how much there is, how much is on screen,
/// where the window sits, and what one notch and one page move it by.
/// </summary>
/// <remarks>
/// <para>
/// These are the numbers <c>IScrollable</c>/<c>ILogicalScrollable</c> want, kept in the projection layer
/// rather than in the control so they can be tested without a window: the Avalonia control cannot be
/// constructed headlessly (its palette needs platform services), so arithmetic left inside it would only
/// ever be verified by a probe. The control computes a range and reads it back, so everything here has a
/// test that runs on every build.
/// </para>
/// <para>
/// <see cref="Extent"/> is the raw laid-out size and is deliberately not raised to at least
/// <see cref="Viewport"/>: a document shorter than the window has to report that, or a bar that shows
/// itself only when there is something to scroll would appear over a document that fits.
/// </para>
/// <para>
/// A step is one indent column and one line of the <em>layout's</em> own units, so a notch of the wheel
/// or an arrow on a bar moves by a cell rather than by a fixed number of pixels. A page is the window
/// less one step, so PageDown keeps the line the reader was on.
/// </para>
/// </remarks>
public readonly record struct ScrollRange(
    LayoutSize Extent,
    LayoutSize Viewport,
    LayoutSize Offset,
    LayoutSize Step,
    LayoutSize Page)
{
    /// <summary>Builds the range of a window at (<paramref name="offsetX"/>, <paramref name="offsetY"/>).</summary>
    /// <param name="layout">The laid-out document.</param>
    /// <param name="viewport">How much of it is on screen.</param>
    /// <param name="offsetX">The requested horizontal offset, clamped to what the content allows.</param>
    /// <param name="offsetY">The requested vertical offset, clamped to what the content allows.</param>
    /// <param name="measure">The measure the layout was produced with, which defines the step.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="layout"/> or <paramref name="measure"/> is <see langword="null"/>.
    /// </exception>
    public static ScrollRange Of(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        var (x, y) = LayoutViewport.Clamp(layout, viewport, offsetX, offsetY);
        var step = new LayoutSize(measure.IndentWidth, measure.LineHeight);

        // A page is a window minus one step, but never less than a step: a window one line tall (or a
        // control that has not been laid out yet) would otherwise ask a bar to page by nothing.
        return new ScrollRange(
            layout.Size,
            viewport,
            new LayoutSize(x, y),
            step,
            new LayoutSize(
                Math.Max(step.Width, viewport.Width - step.Width),
                Math.Max(step.Height, viewport.Height - step.Height)));
    }

    public override string ToString() =>
        $"{Extent} in {Viewport} at ({Offset.Width}, {Offset.Height}), step {Step}, page {Page}";
}
