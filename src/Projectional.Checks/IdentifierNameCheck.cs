using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A name a concept declares must be spelled the way the concept says, and is not.</summary>
/// <remarks>
/// <para>
/// The rule is read from the concept's own declaration (<see cref="Concept.NameRule"/>), so a language that never
/// asked for a spelling is never reported: a name that reads like a sentence is what "any text" means, and a
/// catalogue that reported it would be a style opinion rather than a rule. The rule may be an identifier or any of
/// the case conventions the language offers, and the finding quotes which one it is.
/// </para>
/// <para>
/// An empty name is not reported, and neither is a name that is still the concept's declared placeholder. "Nobody
/// has named this yet" is <see cref="PlaceholderNameCheck"/>'s business, and a name that has to be repaired and a
/// name that has to be chosen are two different things to tell a user - <c>machine</c> on a fresh machine is not a
/// spelling mistake, it is an unnamed node.
/// </para>
/// <para>
/// A name with whitespace around it is <see cref="SurroundingWhitespaceCheck"/>'s finding, and is left to it: the
/// spelling of <c>"Spaced "</c> is a question about the space, and answering it twice would put two squiggles under
/// one word with two different repairs. Trimming the name first is what makes the second finding appear, which is
/// the ordinary repair chain.
/// </para>
/// <para>
/// The repair is <see cref="NameRule.Propose"/>, which is mechanical: an identifier's illegal characters become
/// underscores and a leading digit gains one in front, while a case rule respells the words the name already
/// carries. It never invents words, so the author sees what changed and can edit from there - and the proposal is
/// computed from the value the node holds, so a name that was already repaired proposes itself and the fix
/// disappears.
/// </para>
/// </remarks>
public sealed class IdentifierNameCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD004";

    /// <summary>The stable identifier of the repair that rewrites the name.</summary>
    public const string FixId = "std.identifier-name.rewrite";

    /// <summary>Every node whose name breaks the spelling rule its concept declares.</summary>
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
                || IsPlaceholder(nameSlot, name)
                || HasSurroundingWhitespace(name)
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
                    $"the name \"{name}\" is not {rule.Requirement}, which this {ConceptWords.Describe(node.Concept.Key)} requires.",
                    role: nameSlot.Name,
                    code: Code),
                FixId,
                $"Use \"{proposal}\"",
                context => context.Node is null ? null : new SetPropertyAction(target, proposal),
                "Respells the name the way the language asks for, without inventing a word."));
        }

        return fixes;
    }

    /// <summary>Whether <paramref name="name"/> is the placeholder the concept starts its name slot with.</summary>
    /// <remarks>
    /// The same exclusion <see cref="DuplicateNameCheck"/> makes, and for the same reason: a value the model
    /// materialized from a declaration is not something the user typed, so repairing its spelling would be answering
    /// a question nobody asked. <see cref="PlaceholderNameCheck"/> has a finding of its own for that state.
    /// </remarks>
    private static bool IsPlaceholder(PropertySlot nameSlot, string name) =>
        nameSlot.DefaultValue is { Length: > 0 } placeholder
        && string.Equals(name, placeholder, StringComparison.Ordinal);

    /// <summary>Whether whitespace stands in front of or behind <paramref name="name"/>.</summary>
    /// <remarks>
    /// A name that is <em>only</em> whitespace is not one of these: nothing else would report it, so its spelling -
    /// which is no word at all - stays this check's question. The span is trimmed rather than the string copied,
    /// because a check runs after every change.
    /// </remarks>
    private static bool HasSurroundingWhitespace(string name) =>
        name.AsSpan().Trim().Length > 0
        && (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]));
}
