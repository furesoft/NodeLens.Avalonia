using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>Runs a document and answers the lines the output panel shows.</summary>
/// <remarks>
/// <para>
/// The shell's whole contribution to interpretation is this: one document, one registry, the variables the user typed,
/// and a report. Budget, cancellation, the trace and the failure vocabulary belong to
/// <see cref="Interpreter"/>, which is why nothing here re-states them.
/// </para>
/// <para>
/// A document whose language declares no evaluation rules is not special-cased: the interpreter fails with "declares
/// no evaluation rule" and the panel says so, which is a truer answer than a command that greys itself out for reasons
/// the user cannot see.
/// </para>
/// </remarks>
public static class DocumentRun
{
    /// <summary>Runs <paramref name="document"/> and reports what came out.</summary>
    /// <param name="document">The root the session shows, which is the node the run starts at.</param>
    /// <param name="rules">The evaluation rules of the document's language.</param>
    /// <param name="inputs">The variables the user typed, or <see langword="null"/> when there are none.</param>
    /// <param name="budget">How many node evaluations the run may spend.</param>
    /// <returns>The answer or the failure, with the trace in both cases.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RunReport Of(
        Node document,
        EvaluationRegistry rules,
        string? inputs = null,
        int budget = Interpreter.DefaultBudget)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rules);

        if (!RunInputs.TryParse(inputs, out var variables, out var error))
        {
            // A line the user mistyped is about the panel, not about the document, so there is nothing to trace.
            return new RunReport(false, error!, []);
        }

        var result = new Interpreter(rules).Run(document, variables, budget);

        return new RunReport(
            result.Succeeded,
            result.Succeeded ? $"{result.Value!.Display} in {result.Steps} step(s)" : result.Failure!.Message,
            RowsOf(result))
        {
            // The cursor and the rows are the same trace: a step marks a row and moves the caret to the node that wrote
            // it. A run that stopped before evaluating anything has an empty trace, so the cursor stands at the end of
            // nothing and every step answers "no" - which is the truth rather than a special case.
            Steps = new TraceCursor(result),
        };
    }

    /// <summary>The trace as rows, each pointing at the node whose value it records.</summary>
    private static List<RunRow> RowsOf(InterpretationResult result)
    {
        List<RunRow> rows = [];

        foreach (var entry in result.Trace)
        {
            rows.Add(new RunRow(entry.ToString(), new CellAddress(entry.Node)));
        }

        return rows;
    }
}
