using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Which concepts could stand where a node stands, given the place it stands in.</summary>
/// <remarks>
/// <para>
/// This is the whole of "replace with X": the framework offers the language's own alternative concepts instead of a
/// fixed list of conversions, and it needs no knowledge of what a step or a literal means. Three questions, each
/// answered from declarations: does the slot this node fills accept the concept, may the concept stand in that slot
/// (its <see cref="Applicability"/>), and can it be created at all (it is concrete).
/// </para>
/// <para>
/// A root has no answer, for the same reason it has no wrapper: replacing it would take the node's place, and a root
/// is not in one. Since the document a session draws <em>is</em> a root, that is also the rule that keeps a
/// replacement from taking the editor's own node away.
/// </para>
/// <para>
/// The node's own concept is left out - replacing a step with a step would delete the node and put an empty one where
/// it was, which is a change nobody asked for. A slot declared over one concrete concept therefore offers no
/// replacement at all, and that emptiness is the correct answer rather than a gap.
/// </para>
/// <para>
/// The order is the registry's declaration order, exactly as for wrappers: it is the language author's ranking, and
/// it never reorders itself between two carets.
/// </para>
/// </remarks>
public static class ReplacementConcepts
{
    /// <summary>Every concept that could take the place of <paramref name="node"/>, in declaration order.</summary>
    /// <param name="node">The node whose place is in question.</param>
    /// <param name="concepts">The language's registry, which is what knows every concept.</param>
    /// <returns>The candidates, empty when nothing else could stand there.</returns>
    public static IReadOnlyList<Concept> Of(Node node, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(concepts);

        if (node.Parent is not { } parent || node.ParentSlot is not { } place)
        {
            return [];
        }

        if (parent.Concept.FindChild(place) is not { } declared)
        {
            return [];
        }

        List<Concept> candidates = [];

        foreach (var concept in concepts.Concepts)
        {
            if (concept.IsAbstract
                || string.Equals(concept.Key, node.Concept.Key, StringComparison.Ordinal)
                || !concept.IsKindOf(declared.ChildConceptKey)
                || !concept.Applicability.Allows(parent.Concept, place))
            {
                continue;
            }

            candidates.Add(concept);
        }

        return candidates;
    }
}
