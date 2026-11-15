using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>
/// One stretch of one line that is stroked once, with the severity it is stroked in.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="MarkerLine"/> says what one finding covers; a run says what is actually drawn. The
/// difference matters where two findings cover the same characters: two squiggles on one baseline is a
/// smear, not more information, so the overlapping stretches are merged into one run that carries the
/// worse of the two severities. The findings themselves are untouched - a tooltip still lists both - which
/// is why the merge happens here, on the way to the renderer, and not in the geometry.
/// </para>
/// <para>
/// Everything is already resolved: a run knows where it starts, how far it reaches and what it means. A
/// renderer draws it and asks nothing else.
/// </para>
/// </remarks>
/// <param name="X">The left edge, in the same units as the layout.</param>
/// <param name="Y">The top edge of the line the run sits on, in the same units as the layout.</param>
/// <param name="Width">How wide the run is, and never zero.</param>
/// <param name="Height">The height of the line the run sits on.</param>
/// <param name="Severity">The worst severity of the findings merged into this run.</param>
public readonly record struct MarkerRun(
    double X,
    double Y,
    double Width,
    double Height,
    DiagnosticSeverity Severity);
