namespace Projectional.Semantics;

/// <summary>How much a diagnostic matters, declared from least to most.</summary>
/// <remarks>
/// <para>
/// The declaration order <em>is</em> the ordering: <see cref="Hint"/> is zero and <see cref="Error"/> is
/// the largest value, so the severity to draw where several diagnostics share a stretch of text is the
/// larger of the two and nothing else. Do not reorder the members to group them by meaning, and do not
/// give them explicit values - both would turn "the worst one wins" into a lookup instead of a
/// comparison.
/// </para>
/// <para>
/// There is no <c>None</c>: a diagnostic always says something, and "nothing to report" is the absence of
/// a diagnostic rather than a severity. A caller that needs to say so answers with a
/// <see langword="null"/> severity instead.
/// </para>
/// </remarks>
public enum DiagnosticSeverity
{
    /// <summary>Worth knowing, and not necessarily wrong - a suggestion the user may ignore.</summary>
    Hint,

    /// <summary>An observation about the model that is not a defect.</summary>
    Info,

    /// <summary>Probably wrong: the model is accepted, but likely does not mean what was intended.</summary>
    Warning,

    /// <summary>Wrong: the model cannot mean what it says, and whatever consumes it will fail.</summary>
    Error,
}
