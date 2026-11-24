using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Web;

/// <summary>What the problems panel shows, together with the marks the editor paints for it.</summary>
/// <param name="Rows">The findings, in document order.</param>
/// <param name="Markers">The same findings as the frame's view state.</param>
/// <remarks>
/// The two travel together because they are one answer: a panel built from a second run of the rules could
/// disagree with the squiggles about what is wrong, which is the defect the desktop shell's <c>DocumentFindings</c>
/// join exists to prevent. The join is repeated here rather than referenced because that type lives in the
/// Avalonia assembly, which a browser host cannot load - the rules it runs are the same ones.
/// </remarks>
public sealed record ProblemReport(IReadOnlyList<ProblemRow> Rows, MarkerSet Markers)
{
    /// <summary>Nothing found: what a document nobody has broken shows.</summary>
    public static ProblemReport Empty { get; } = new([], MarkerSet.Empty);
}
