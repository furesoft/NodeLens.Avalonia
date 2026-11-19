using Projectional.Core;

namespace Projectional.Language;

/// <summary>What one import did: the roots it added, or why it refused the text.</summary>
/// <remarks>
/// <para>
/// A refusal has no roots and one error, and the model is untouched - the importer runs inside one transaction that is
/// rolled back. Keeping the two outcomes in one type is what lets a host write one branch: either there is a document,
/// or there is something to show the user.
/// </para>
/// <para>
/// Warnings travel with a successful import, because "it worked, and this construct is only raw text" is a state a host
/// has to be able to report without treating the import as failed.
/// </para>
/// </remarks>
public sealed record ImportResult
{
    private ImportResult(bool succeeded, IReadOnlyList<Node> roots, IReadOnlyList<ImportDiagnostic> diagnostics)
    {
        Succeeded = succeeded;
        Roots = roots;
        Diagnostics = diagnostics;
    }

    /// <summary>Whether the text became a model.</summary>
    public bool Succeeded { get; }

    /// <summary>The roots that were added to the model, in the order the text had them.</summary>
    public IReadOnlyList<Node> Roots { get; }

    /// <summary>The error that refused the text, or the warnings of a successful import.</summary>
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }

    /// <summary>Whether anything was kept as raw text, or otherwise reported without failing.</summary>
    public bool HasWarnings => Diagnostics.Any(diagnostic => diagnostic.Severity == ImportSeverity.Warning);

    /// <summary>The text did not parse; nothing was imported.</summary>
    /// <param name="diagnostic">Why.</param>
    public static ImportResult Refused(ImportDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new ImportResult(false, [], [diagnostic]);
    }

    /// <summary>The text became <paramref name="roots"/>, with <paramref name="diagnostics"/> beside them.</summary>
    /// <param name="roots">The roots that were added.</param>
    /// <param name="diagnostics">What is worth saying about the result; may be empty.</param>
    public static ImportResult Imported(
        IReadOnlyList<Node> roots,
        IReadOnlyList<ImportDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(roots);

        return new ImportResult(true, roots, diagnostics ?? []);
    }
}
