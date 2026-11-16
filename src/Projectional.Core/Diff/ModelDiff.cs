namespace Projectional.Core;

/// <summary>
/// What changed between two models, node by node.
/// </summary>
/// <remarks>
/// <para>
/// A structural diff, not a text diff: nodes are matched by <see cref="NodeId"/>, so a rename is one changed
/// property, a reordering is a set of moves, and a delete is the node id disappearing. Text comparison cannot
/// answer any of those - two documents can read identically and be different trees, and one node can move
/// without a character changing.
/// </para>
/// <para>
/// A node present in both models with a different concept is reported as <see cref="NodeDifferenceKind.Removed"/>
/// and <see cref="NodeDifferenceKind.Added"/> rather than compared: the ids coincide across two unrelated
/// models, and pretending one is the other would produce a diff describing an edit nobody made. Within one
/// project that cannot happen - an id names one node - so this is a guard for a caller that diffed the wrong
/// pair of models.
/// </para>
/// </remarks>
public static class ModelDiff
{
    /// <summary>The differences between <paramref name="before"/> and <paramref name="after"/>.</summary>
    /// <param name="before">The earlier state.</param>
    /// <param name="after">The later state.</param>
    /// <returns>
    /// One entry per change, in document order of <paramref name="after"/>, followed by the removals in the
    /// document order they had in <paramref name="before"/>. An identical copy answers with no entry at all.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either model is <see langword="null"/>.</exception>
    public static IReadOnlyList<NodeDifference> Of(Model before, Model after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var earlier = Index(before);
        var later = Index(after);
        var differences = new List<NodeDifference>();

        foreach (var node in Walk(after))
        {
            if (earlier.TryGetValue(node.Id, out var was) && SameConcept(was, node))
            {
                Compare(was, node, differences);

                continue;
            }

            differences.Add(new NodeDifference(
                node.Id,
                NodeDifferenceKind.Added,
                node.ParentSlot,
                After: NodePlace.Of(node)));
        }

        foreach (var node in Walk(before))
        {
            if (!later.TryGetValue(node.Id, out var now) || !SameConcept(now, node))
            {
                differences.Add(Removal(node));
            }
        }

        return differences.AsReadOnly();
    }

    /// <summary>Whether two nodes with one id are the same node rather than a coincidence across projects.</summary>
    private static bool SameConcept(Node one, Node other) =>
        one.Concept.Key.Equals(other.Concept.Key, StringComparison.Ordinal);

    /// <summary>Reports a placement change and every changed property and reference of one node.</summary>
    private static void Compare(Node was, Node now, List<NodeDifference> differences)
    {
        var before = NodePlace.Of(was);
        var after = NodePlace.Of(now);

        if (before != after)
        {
            differences.Add(new NodeDifference(
                now.Id,
                NodeDifferenceKind.Moved,
                now.ParentSlot,
                before,
                after));
        }

        foreach (var declaration in now.Concept.Slots)
        {
            switch (declaration)
            {
                case PropertySlot property:
                    CompareProperty(was, now, property.Name, differences);

                    break;

                case ReferenceSlot reference:
                    CompareReference(was, now, reference.Name, differences);

                    break;
            }
        }
    }

    private static void CompareProperty(Node was, Node now, string slot, List<NodeDifference> differences)
    {
        var oldValue = was.GetOrNull(slot);
        var newValue = now.GetOrNull(slot);

        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            differences.Add(new NodeDifference(
                now.Id,
                NodeDifferenceKind.PropertyChanged,
                slot,
                OldValue: oldValue,
                NewValue: newValue));
        }
    }

    private static void CompareReference(Node was, Node now, string slot, List<NodeDifference> differences)
    {
        var oldReference = was.GetReference(slot);
        var newReference = now.GetReference(slot);

        if (oldReference != newReference)
        {
            differences.Add(new NodeDifference(
                now.Id,
                NodeDifferenceKind.ReferenceChanged,
                slot,
                OldValue: oldReference?.ToString(),
                NewValue: newReference?.ToString()));
        }
    }

    private static NodeDifference Removal(Node node) => new(
        node.Id,
        NodeDifferenceKind.Removed,
        node.ParentSlot,
        NodePlace.Of(node),
        OldValue: node.Name);

    /// <summary>Every node of one model, in document order: each root, then its whole subtree.</summary>
    /// <remarks>
    /// The roots of the model rather than the project's node index: a project holds several models, and a diff
    /// that walked the index would report nodes of documents that are not being compared.
    /// </remarks>
    private static IEnumerable<Node> Walk(Model model)
    {
        foreach (var root in model.Roots)
        {
            foreach (var node in SelfAndDescendants(root))
            {
                yield return node;
            }
        }
    }

    /// <summary>One node, then everything below it, depth first.</summary>
    /// <remarks>
    /// <see cref="Node.ChildrenInDocumentOrder"/> answers with the direct children only, so the descent is
    /// explicit - the same shape <see cref="EditorSession"/> uses to find a node by id.
    /// </remarks>
    private static IEnumerable<Node> SelfAndDescendants(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static Dictionary<NodeId, Node> Index(Model model) =>
        Walk(model).ToDictionary(node => node.Id);
}
