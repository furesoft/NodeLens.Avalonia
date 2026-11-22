using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A list slot that holds nothing while its declaration asks for at least one child.</summary>
/// <remarks>
/// <para>
/// The list half of <see cref="EmptyChildSlotCheck"/>, and the reason a slot carries a minimum of its own: a list
/// written <c>[1..n]</c> promises at least one element, while <c>[0..n]</c> promises nothing - the same storage
/// shape, two different statements. The finding names the slot the user can see, and the repair runs
/// <see cref="InsertChildAction"/>, which is what Enter fills an empty slot with.
/// </para>
/// <para>
/// It is a <see cref="DiagnosticSeverity.Hint"/> for the same reason the other structural findings are: an empty
/// slot is where the user is about to type, not a defect to be told off about.
/// </para>
/// <para>
/// A list whose declared concept has nothing concrete below it is not reported, because there is no repair:
/// inventing a node the language says cannot exist would be worse than saying nothing.
/// </para>
/// </remarks>
public sealed class MinimumChildCountCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD011";

    string ICheck.Code => Code;

    string ICheck.Title => "A list slot that holds nothing while its declaration asks for at least one child.";

    /// <summary>The stable identifier of the repair that adds the first child.</summary>
    public const string FixId = "std.minimum-child-count.add";

    /// <summary>Every list slot in <paramref name="model"/> that holds fewer children than it requires.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per list below its minimum, in document order and, within a node, in slot declaration order.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            foreach (var declaration in node.Concept.Slots)
            {
                if (declaration is not ChildSlot { Cardinality: Cardinality.Many, Minimum: > 0 } slot
                    || node.GetChildren(slot.Name).Count > 0
                    || model.Concepts.DefaultInstantiableFor(slot.ChildConceptKey, node.Concept, slot.Name) is not { } concept)
                {
                    continue;
                }

                var id = node.Id;
                var name = slot.Name;

                fixes.Add(new DiagnosticFix(
                    new Diagnostic(
                        DiagnosticSeverity.Hint,
                        id,
                        $"this {ConceptWords.Describe(node.Concept.Key)} needs at least {slot.Minimum} {name}; add one.",
                        role: name,
                        code: Code),
                    FixId,
                    $"Add \"{ConceptWords.Word(concept.Key)}\"",
                    context => context.Node is null ? null : new InsertChildAction(context.Node.Id, name),
                    "Creates the first concept the language offers in this list, exactly as Enter does."));
            }
        }

        return fixes;
    }
}
