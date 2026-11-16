using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A single-valued child slot that holds nothing.</summary>
/// <remarks>
/// <para>
/// A slot declared with <see cref="Cardinality.One"/> promises one child, so an empty one is a hole rather
/// than a choice. This is the finding a user meets most often in a fresh document, and it needs no
/// language knowledge: the slot says which concept belongs there, and
/// <see cref="ConceptRegistry.DefaultInstantiableFor"/> is the language's own answer to which one to
/// create - the same answer <c>Enter</c> uses, because the repair runs
/// <see cref="InsertChildAction"/>, which is what Enter runs.
/// </para>
/// <para>
/// A slot declared over a concept whose every descendant is abstract is a set with nothing in it. It is not
/// reported, because there is no repair: inventing a node the language says cannot exist would be worse
/// than saying nothing.
/// </para>
/// <para>
/// The finding is a <see cref="DiagnosticSeverity.Hint"/> for the same reason <see cref="PlaceholderNameCheck"/>
/// is: a hole is where the user is about to type, not a defect to be told off about.
/// </para>
/// </remarks>
public sealed class EmptyChildSlotCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD010";

    /// <summary>The stable identifier of the repair that fills the slot.</summary>
    public const string FixId = "std.empty-child-slot.insert";

    /// <summary>Every empty single-valued child slot in <paramref name="model"/>.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per empty slot, in document order and, within a node, in slot declaration order. The
    /// diagnostic targets the empty slot, so the marker lands on the placeholder the user can see.
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
                if (declaration is not ChildSlot { Cardinality: Cardinality.One } slot
                    || node.GetChildren(slot.Name).Count > 0
                    || model.Concepts.DefaultInstantiableFor(slot.ChildConceptKey) is not { } concept)
                {
                    continue;
                }

                var id = node.Id;
                var name = slot.Name;
                var label = $"Insert \"{ConceptWords.Word(concept.Key)}\"";

                fixes.Add(new DiagnosticFix(
                    new Diagnostic(
                        DiagnosticSeverity.Hint,
                        id,
                        $"this {ConceptWords.Describe(node.Concept.Key)} has no {name}; insert one.",
                        role: name,
                        code: Code),
                    FixId,
                    label,
                    context => context.Node is null ? null : new InsertChildAction(context.Node.Id, name),
                    "Creates the concept the language offers in this slot, exactly as Enter does."));
            }
        }

        return fixes;
    }
}
