using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>The findings under the pointer, the worst first, and the marker they were found on.</summary>
/// <remarks>
/// <para>
/// More than one finding can sit on the same stretch of text - a warning and an error on one word - and the
/// page draws only the worse of them. The tooltip is the only place the other one is reachable at all, so
/// this answers with all of them rather than with the one that was painted.
/// </para>
/// <para>
/// <see cref="Anchor"/> is kept because a caller that received only the diagnostics would have to search
/// the geometry a second time to find out what to place the box against.
/// </para>
/// </remarks>
/// <param name="Diagnostics">Every finding on the marker under the pointer, the worst first.</param>
/// <param name="Anchor">The first marker under the pointer, in layout units.</param>
public readonly record struct MarkerHit(IReadOnlyList<Diagnostic> Diagnostics, MarkerLine Anchor);
