namespace Projectional.Workbench;

/// <summary>What one run of a document produced, as rows the output panel shows.</summary>
/// <param name="Succeeded">Whether the run answered a value.</param>
/// <param name="Headline">The answer or the reason there is none, in one line.</param>
/// <param name="Rows">The trace, post-order, which is the order the values were finished.</param>
/// <remarks>
/// A report is the run's snapshot, so it is held on the tab and dropped when the document changes - the same rule the
/// findings follow. It carries no model and no interpreter: running again is the caller's decision, and a report that
/// could re-run itself would make the panel a second interpreter.
/// </remarks>
internal sealed record RunReport(bool Succeeded, string Headline, IReadOnlyList<RunRow> Rows);
