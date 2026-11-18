using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>Everything a shell shows about what is wrong with one document, derived in one place.</summary>
/// <param name="Diagnostics">The findings in document order, which is the order a reader fixes them in.</param>
/// <param name="Markers">The same findings as the marks the control draws.</param>
/// <param name="Fixes">The repairs that came with the findings, which is what a quick-fix list offers.</param>
/// <remarks>
/// <para>
/// The three are one value because they must not be three answers to one question: a panel that ran the checks
/// again could report a finding the squiggles do not draw, and a repair list built from a second run could offer an
/// edit for a finding nobody can see.
/// </para>
/// <para>
/// A constraint finding is in <see cref="Diagnostics"/> and <see cref="Markers"/> but never in
/// <see cref="Fixes"/>: a constraint reports and never repairs, so it has no repair to offer. That is not a gap to
/// fill later - a repair invented for a rule nobody wrote would be worse than a finding without one.
/// </para>
/// </remarks>
public sealed record FindingSet(
    IReadOnlyList<Diagnostic> Diagnostics,
    MarkerSet Markers,
    FixSet Fixes);
