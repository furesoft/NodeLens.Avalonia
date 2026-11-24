using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Web;

/// <summary>One finding in the problems panel, as a row a user can click.</summary>
/// <param name="Severity">How bad it is, which is what the dot's colour says.</param>
/// <param name="Message">What the rule said, in the language's own words.</param>
/// <param name="Address">Where the caret goes when the row is clicked.</param>
public sealed record ProblemRow(DiagnosticSeverity Severity, string Message, CellAddress Address)
{
    public override string ToString() => $"{Severity}: {Message}";
}
