namespace Projectional.Language;

/// <summary>Something about the imported text a reader has to be told, placed in that text rather than in a model.</summary>
/// <remarks>
/// <para>
/// Deliberately not a <c>Diagnostic</c>: that one names a <see cref="Core.NodeId"/>, and a syntax error has no node - it
/// is the reason there is nothing to point at. The place is therefore a line and a column in the string the host passed,
/// so a host can put a caret in the user's file.
/// </para>
/// <para>
/// <see cref="Line"/> and <see cref="Column"/> are 1-based, as an editor shows them, and <see cref="Length"/> is the
/// number of characters the construct spans so a host can underline it. A length of zero means "the problem is here"
/// rather than "the problem is this wide".
/// </para>
/// <para>
/// <see cref="Code"/> is a stable short string (<c>json.unexpected-token</c>, <c>json.duplicate-name</c>) and is never
/// translated; <see cref="Message"/> is the sentence a user reads.
/// </para>
/// </remarks>
/// <param name="Code">The stable identifier of the problem.</param>
/// <param name="Message">The sentence a user reads.</param>
/// <param name="Line">The 1-based line in the imported text.</param>
/// <param name="Column">The 1-based column in the imported text.</param>
/// <param name="Length">How many characters the construct spans, or zero.</param>
/// <param name="Severity">Whether the import was refused or merely kept something as raw text.</param>
public sealed record ImportDiagnostic(
    string Code,
    string Message,
    int Line,
    int Column,
    int Length = 0,
    ImportSeverity Severity = ImportSeverity.Error)
{
    /// <summary>Where the problem is, as an editor spells a place: <c>3:14</c>.</summary>
    public override string ToString() => $"{Line}:{Column} {Code} {Message}";
}
