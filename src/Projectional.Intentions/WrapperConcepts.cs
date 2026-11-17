using Projectional.Core;

namespace Projectional.Intentions;

/// <summary>Which concepts could surround a node, or a run of siblings, given where they stand and what they are.</summary>
/// <remarks>
/// <para>
/// This is the whole of "wrap in X": the framework offers the language's own compositions instead of a fixed list
/// of refactorings, and it needs no knowledge of what a rule or a loop means. Three questions, each answered from
/// declarations: can the wrapper stand where these nodes stand (the parent's slot accepts its concept, and its
/// <see cref="Applicability"/> allows that place), can it hold them (a child slot of it accepts every one of their
/// concepts - a single-valued one preferred for one node, a many-valued one required for a run), and can it be
/// created at all (it is concrete).
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

        return Candidates(parent, place, node.Concept, concepts);
    }

    /// <summary>
    /// Every concept that could wrap the run <paramref name="nodes"/>, in declaration order.
    /// </summary>
    /// <param name="nodes">The nodes a selection covers, in document order.</param>
    /// <param name="concepts">The language's registry, which is what knows every concept.</param>
    /// <returns>The candidates, empty when the nodes are not something one concept could surround.</returns>
    /// <remarks>
    /// <para>
    /// A run has one more question than a single node: the wrapper has to hold <em>all</em> of them at once,
    /// so its slot must be many-valued. A concept whose only candidate slot is single-valued is not offered
    /// for a selection, because accepting it would have to throw away every node but one.
    /// </para>
    /// <para>
    /// The nodes must be consecutive siblings, which is the same rule
    /// <see cref="Model.Wrap(IReadOnlyList{Node}, string, string, IReadOnlyDictionary{string, string})"/>
    /// enforces: an offer that the edit would refuse is worse than no offer, so the search answers the
    /// question the edit asks. Fewer than two nodes is the per-node entry's business and answers empty here,
    /// which is what keeps one operation from having two rows.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<WrapperCandidate> Of(IReadOnlyList<Node> nodes, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(concepts);

        if (nodes.Count < 2 || nodes[0] is not { Parent: { } parent, ParentSlot: { } place })
        {
            return [];
        }

        var ordered = new List<Node>(nodes);
        ordered.Sort((left, right) => left.IndexInParent.CompareTo(right.IndexInParent));

        for (var index = 0; index < ordered.Count; index++)
        {
            var node = ordered[index];

            if (!ReferenceEquals(node.Parent, parent)
                || !string.Equals(node.ParentSlot, place, StringComparison.Ordinal)
                || (index > 0 && node.IndexInParent != ordered[index - 1].IndexInParent + 1))
            {
                return [];
            }
        }

        return Candidates(parent, place, ordered[0].Concept, concepts, ordered);
    }

    /// <summary>The candidates for one place, asking what the wrapper's slots can hold.</summary>
    /// <remarks>
    /// A single node is asked of its own concept and any slot of the right cardinality; a run is asked of
    /// every node's concept and a many-valued slot only, which is what <paramref name="run"/> carries.
    /// </remarks>
    private static List<WrapperCandidate> Candidates(
        Node parent,
        string place,
        Concept wrapped,
        ConceptRegistry concepts,
        IReadOnlyList<Node>? run = null)
    {
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

            if (SlotFor(wrapper, wrapped, run) is { } slot)
            {
                candidates.Add(new WrapperCandidate(wrapper, slot));
            }
        }

        return candidates;
    }

    /// <summary>The first slot of <paramref name="wrapper"/> that accepts everything being wrapped.</summary>
    /// <remarks>
    /// <para>
    /// A run needs a many-valued slot: anything else can hold only part of it. A single node is offered the
    /// one-slot first, because that is the shape a wrapper of one was thought for - a parenthesis around an
    /// expression - and falls back to a many-valued slot only when the concept declares no one-slot that
    /// accepts it. Without that fallback "put this item in a group" would not be offered at all, even though
    /// the model can do it: the wrapper is created empty, so the first child of a list is the same operation
    /// as the only child of a slot.
    /// </para>
    /// <para>
    /// The fallback is what makes the two forms agree: a single node and a run of two are the same edit with
    /// a different number of children, so a language whose only grouping is a list gains both entries or
    /// neither.
    /// </para>
    /// </remarks>
    private static ChildSlot? SlotFor(Concept wrapper, Concept wrapped, IReadOnlyList<Node>? run)
    {
        var slots = wrapper.Slots.OfType<ChildSlot>();

        if (run is not null)
        {
            return slots.FirstOrDefault(slot =>
                slot.Cardinality == Cardinality.Many
                && run.All(node => node.Concept.IsKindOf(slot.ChildConceptKey)));
        }

        return slots.FirstOrDefault(slot =>
                slot.Cardinality == Cardinality.One && wrapped.IsKindOf(slot.ChildConceptKey))
            ?? slots.FirstOrDefault(slot => wrapped.IsKindOf(slot.ChildConceptKey));
    }
}
