using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>What one run of a document produced, as rows the output panel shows.</summary>
/// <param name="Succeeded">Whether the run answered a value.</param>
/// <param name="Headline">The answer or the reason there is none, in one line.</param>
/// <param name="Rows">The trace, post-order, which is the order the values were finished.</param>
/// <remarks>
/// A report is the run's snapshot, so it is held on the tab and dropped when the document changes - the same rule the
/// findings follow. It carries no model and no interpreter: running again is the caller's decision, and a report that
/// could re-run itself would make the panel a second interpreter.
/// </remarks>
public sealed record RunReport(bool Succeeded, string Headline, IReadOnlyList<RunRow> Rows)
{
    /// <summary>
    /// The place a step-by-step reader stands in <see cref="Rows"/>, or <see langword="null"/> when the report is not
    /// a run's.
    /// </summary>
    /// <remarks>
    /// A generated text also arrives as a report, and stepping through it is not a thing: there is no order of
    /// evaluation to replay, only bytes put one after another. So the cursor is part of the report rather than
    /// something a caller guesses at, and a null one is the honest answer to "can this be stepped" - the panel enables
    /// its step command from it instead of from the command that produced the report.
    /// <para>
    /// The cursor walks the trace; the rows are the same trace rendered, index for index, which is what lets a step
    /// both mark a row and move the caret to the node that row came from.
    /// </para>
    /// </remarks>
    public TraceCursor? Steps { get; init; }
}
