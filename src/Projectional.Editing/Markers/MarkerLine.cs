using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>
/// One line's worth of marker: the stretch of a drawn line a diagnostic is about, in layout units.
/// </summary>
/// <remarks>
/// <para>
/// A marker is a rectangle on a line, not a range in a value, because a value may be drawn across
/// several lines and the page has to draw one squiggle per line. Everything here is already resolved -
/// which cell, which line of it, and how far across - so a renderer needs no knowledge of text, wrapping
/// or diagnostics and can simply stroke what it is handed.
/// </para>
/// <para>
/// A finding that covers more than one line produces one of these per line, and a finding on a node
/// produces one per text cell it marks. The <see cref="Diagnostic"/> travels along so a host can show
/// the message for the marker under the pointer without keeping a second index.
/// </para>
/// <para>
/// Two findings on the same stretch of text produce two entries rather than one: the collapse to "the
/// worst severity wins" is a painting decision, and dropping a diagnostic here would make it
/// unreachable from a tooltip.
/// </para>
/// </remarks>
/// <param name="Cell">The cell the marker sits in, which is what a hit test would have to resolve.</param>
/// <param name="Diagnostic">The finding this marker shows.</param>
/// <param name="X">The left edge, in the same units as the layout.</param>
/// <param name="Y">The top edge of the line, in the same units as the layout.</param>
/// <param name="Width">How wide the marked stretch is, and never zero.</param>
/// <param name="Height">The height of the line the marker sits on.</param>
/// <param name="Line">Which line of the cell's text the marker is on, counted from zero.</param>
public readonly record struct MarkerLine(
    CellAddress Cell,
    Diagnostic Diagnostic,
    double X,
    double Y,
    double Width,
    double Height,
    int Line);
