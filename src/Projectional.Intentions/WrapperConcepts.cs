using Projectional.Core;

namespace Projectional.Intentions;

/// <summary>Which concepts could surround a node, given where the node stands and what it is.</summary>
/// <remarks>
/// <para>
/// This is the whole of "wrap in X": the framework offers the language's own compositions instead of a fixed list
/// of refactorings, and it needs no knowledge of what a rule or a loop means. Three questions, each answered from
/// declarations: can the wrapper stand where this node stands (the parent's slot accepts its concept, and its
/// <see cref="Applicability"/> allows that place), can it hold this node (some single-child slot of it accepts the
/// node's concept), and can it be created at all (it is concrete).
/// </para>
/// <para>
/// A root has no answer: the wrapper would have to take the node's place, and a root is not in one. That is why the
/// search starts with the parent rather than with the node.
/// </para>
/// <para>
/// The order is the registry's declaration order, which is the language author's answer to "what would the user
/// most likely want here" everywhere else in this framework. It is never sorted, because a menu that reorders
/// itself between two carets in one document is a menu nobody can learn.
/// </para>
/// </remarks>
internal static class WrapperConcepts
{
    /// <summary>Every concept that could wrap <paramref name="node"/>, in declaration order.</summary>
    /// <param name="node">The node to surround.</param>
    /// <param name="concepts">The language's registry, which is what knows every concept.</param>
    /// <returns>The candidates, empty when the node cannot be wrapped at all.</returns>
    internal static IReadOnlyList<WrapperCandidate> Of(Node node, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(concepts);

        if (node.Parent is not { } parent || node.ParentSlot is not { } place)
        {
            return [];
        }

        // The slot says what may stand there; a concept that names its places has to be asked as well.
        if (parent.Concept.FindChild(place) is not { } target)
        {
            return [];
        }

        List<WrapperCandidate> candidates = [];

        foreach (var wrapper in concepts.Concepts)
        {
            if (wrapper.IsAbstract
                || !wrapper.Applicability.Allows(parent.Concept, place)
                || !wrapper.IsKindOf(target.ChildConceptKey))
            {
                continue;
            }

            if (SlotFor(wrapper, node.Concept) is { } slot)
            {
                candidates.Add(new WrapperCandidate(wrapper, slot));
            }
        }

        return candidates;
    }

    /// <summary>The first single-child slot of <paramref name="wrapper"/> that accepts <paramref name="wrapped"/>.</summary>
    /// <remarks>
    /// Only a single-child slot can be the one a wrapper is born with: a many-valued slot has no "the one child" to
    /// put there, and filling it would be an insert rather than a wrap.
    /// </remarks>
    private static ChildSlot? SlotFor(Concept wrapper, Concept wrapped) =>
        wrapper.Slots
            .OfType<ChildSlot>()
            .FirstOrDefault(slot => slot.Cardinality == Cardinality.One && wrapped.IsKindOf(slot.ChildConceptKey));
}
