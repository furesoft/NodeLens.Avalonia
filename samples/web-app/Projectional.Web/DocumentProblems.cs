using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Web;

/// <summary>Derives what is wrong with one document, as rows and as marks.</summary>
/// <remarks>
/// The language's constraints, the framework's own ambiguity rule, and nothing invented: a browser shell that
/// added checks of its own would be a second language definition that no repository test covers.
/// </remarks>
public static class DocumentProblems
{
    /// <summary>Finds everything the language has to say about <paramref name="model"/>.</summary>
    /// <param name="model">The document to check.</param>
    /// <param name="language">The language whose constraints apply.</param>
    /// <param name="scopes">
    /// The scope the editor actually uses, so a finding about a name is about the names the user can see.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> or <paramref name="language"/> is <see langword="null"/>.
    /// </exception>
    public static ProblemReport Of(Model model, WebLanguage language, IScopeProvider? scopes)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(language);

        var rules = (language.Constraints ?? ConstraintSet.Empty).With(ScopeConstraints.All);

        var findings = new ConstraintEngine(rules, scopes: scopes, types: language.Types ?? TypeRegistry.Empty)
            .Check(model);

        return new ProblemReport(Rows(model, findings), new MarkerSet([.. findings]));
    }

    /// <summary>The findings as rows, in the document's order and never the severity's.</summary>
    /// <remarks>
    /// A list sorted by severity would make "the next problem" a meaningless gesture: fixing one row would move
    /// every other row. Reading a document top to bottom is what the panel does too.
    /// </remarks>
    private static IReadOnlyList<ProblemRow> Rows(Model model, IReadOnlyList<Diagnostic> findings)
    {
        Dictionary<NodeId, int> order = [];
        var index = 0;

        foreach (var node in model.Nodes)
        {
            order[node.Id] = index++;
        }

        return
        [
            .. findings
                .OrderBy(finding => order.TryGetValue(finding.Target, out var position) ? position : int.MaxValue)
                .ThenBy(finding => finding.Message, StringComparer.Ordinal)
                .Select(finding => new ProblemRow(finding.Severity, finding.Message, AddressOf(model, finding))),
        ];
    }

    /// <summary>Where a finding's caret goes: its slot when it named one, otherwise its node.</summary>
    private static CellAddress AddressOf(Model model, Diagnostic finding) =>
        finding.Role is { } role
        && model.Project.FindNode(finding.Target)?.Concept is { } concept
        && (concept.FindChild(role) is not null || concept.FindProperty(role) is not null || concept.FindReference(role) is not null)
            ? CellAddress.OfSlot(finding.Target, role)
            : new CellAddress(finding.Target);
}
