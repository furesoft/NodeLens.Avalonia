using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A name a concept declares must be an identifier, and is not one.</summary>
/// <remarks>
/// <para>
/// The rule is read from the concept's own declaration (<see cref="Concept.NameRule"/>), so a language that never
/// asked for identifiers is never reported: a name that reads like a sentence is what "any text" means, and a
/// catalogue that reported it would be a style opinion rather than a rule.
/// </para>
/// <para>
/// An empty name is not reported. "Nobody has named this yet" is <see cref="PlaceholderNameCheck"/>'s business, and
/// a name that has to be repaired and a name that has to be chosen are two different things to tell a user.
/// </para>
/// <para>
/// The repair is <see cref="NameRule.Propose"/>, which is mechanical: every illegal character becomes an underscore
/// and a leading digit gains one in front. It never invents words, so the author sees what changed and can edit from
/// there - and the proposal is computed from the value the node holds, so a name that was already repaired proposes
/// itself and the fix disappears.
/// </para>
/// </remarks>
public sealed class IdentifierNameCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD004";

    /// <summary>The stable identifier of the repair that rewrites the name.</summary>
    public const string FixId = "std.identifier-name.rewrite";

    /// <summary>Every node whose name breaks the identifier rule its concept declares.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per affected name, in document order, targeting the name property so the marker lands under the
    /// text rather than under the node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            if (node.Concept.NameSlot is not { } nameSlot
                || node.Concept.NameRule is not { } rule
                || node.GetOrNull(nameSlot.Name) is not { Length: > 0 } name
                || rule.IsValid(name))
            {
                continue;
            }

            var proposal = rule.Propose(name);
            var target = CellAddress.OfSlot(node.Id, nameSlot.Name);

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Warning,
                    node.Id,
                    $"the name \"{name}\" is not an identifier, which this {ConceptWords.Describe(node.Concept.Key)} requires.",
                    role: nameSlot.Name,
                    code: Code),
                FixId,
                $"Use \"{proposal}\"",
                context => context.Node is null ? null : new SetPropertyAction(target, proposal),
                "Rewrites the illegal characters as underscores, which is what the language asks for."));
        }

        return fixes;
    }
}
