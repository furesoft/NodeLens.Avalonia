namespace Projectional.Testing;

/// <summary>One finding a test expects its document to report.</summary>
/// <param name="Code">The identifier the check reports under.</param>
/// <param name="Severity">How much it matters: <c>error</c>, <c>warning</c> or <c>hint</c>.</param>
/// <param name="Role">The slot the finding points at, or empty for the node's own text.</param>
/// <remarks>
/// A value rather than the node it came from, so a row can be compared and printed without a document
/// around it. The three parts are the ones a person reads in a report: what fired, how loudly, and where.
/// </remarks>
public sealed record ExpectedFinding(string Code, string Severity, string Role)
{
    /// <summary>The finding as one line, in the same shape the runner prints an actual finding.</summary>
    public string ToLine() => string.Join(' ', new[] { Code, Severity, Role }.Where(part => part.Length > 0));
}
