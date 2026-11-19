using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>Derives what is wrong with one document, in one list, from the language's own rules.</summary>
/// <remarks>
/// <para>
/// This is the join a shell owes its users and had no name for: the checks a language ships produce repairs, the
/// language's constraints report without repairing, and the user must see <em>one</em> set of squiggles. Two lists
/// would be two answers to "what is wrong", so they are merged here and not by each caller.
/// </para>
/// <para>
/// The framework's own ambiguity rule is always added (<see cref="ScopeConstraints.All"/>): a document whose
/// references share a name is wrong in every language, and a language does not opt out of that.
/// </para>
/// </remarks>
public static class DocumentFindings
{
    /// <summary>Finds everything wrong with <paramref name="model"/>.</summary>
    /// <param name="model">The document to check.</param>
    /// <param name="fixes">
    /// What the caller's own checks reported, each carrying the repair that removes it, or <see langword="null"/>
    /// when the host runs no checks of its own.
    /// </param>
    /// <param name="constraints">
    /// The language's constraints, or <see langword="null"/> when it declares none.
    /// </param>
    /// <param name="types">
    /// The language's type rules, which a constraint may ask about, or <see langword="null"/> when it has none.
    /// </param>
    /// <param name="scopes">
    /// The scope provider the editor uses, so a finding about a name is about the scope the user actually has. A
    /// <see langword="null"/> asks the default chain.
    /// </param>
    /// <param name="reports">
    /// Findings the caller has that carry no repair of their own, or <see langword="null"/> when it has none. A
    /// generator that cannot print a concept is the case this exists for: there is nothing an edit action could do
    /// about it, but the user has to see it before pressing the button rather than after.
    /// </param>
    /// <returns>The findings, their marks and their repairs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The diagnostics are the repairs' own findings first, then the constraint findings, then the caller's reports -
    /// the order the sources run in, not the document's. A surface that lists them orders them itself, because "where on
    /// the page" is a question for the surface that knows how to jump there.
    /// </remarks>
    public static FindingSet Of(
        Model model,
        IReadOnlyList<DiagnosticFix>? fixes = null,
        ConstraintSet? constraints = null,
        TypeRegistry? types = null,
        IScopeProvider? scopes = null,
        IReadOnlyList<Diagnostic>? reports = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var repairs = fixes ?? [];

        List<Diagnostic> findings =
        [
            .. repairs.Select(fix => fix.Diagnostic),
            .. new ConstraintEngine(
                (constraints ?? ConstraintSet.Empty).With(ScopeConstraints.All),
                scopes: scopes,
                types: types ?? TypeRegistry.Empty).Check(model),
            .. reports ?? [],
        ];

        return new FindingSet(findings, new MarkerSet([.. findings]), new FixSet([.. repairs]));
    }
}
