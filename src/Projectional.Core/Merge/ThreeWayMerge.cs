using System.Globalization;
using System.Text;

namespace Projectional.Core;

/// <summary>Merges two edits of one document by node id, against the version both sides started from.</summary>
/// <remarks>
/// <para>
/// The unit is a node with its id, not a line: two branches that each add a rule to the same body added two different
/// nodes, and a text merge calls that a conflict because the lines are adjacent. Here the two additions are two
/// insertions into one list, and both survive.
/// </para>
/// <para>
/// Three models are required, and no two will do. Without the version both sides started from there is no way to tell
/// "ours changed this" from "theirs changed it back", so every question would degrade to "take one side".
/// </para>
/// <para>
/// <b>What it will not do is guess.</b> A question the three versions do not answer - the same property written twice,
/// a node one side edited and the other deleted - is a conflict, and a conflict means no model: the caller is told what
/// could not be decided instead of being handed a document that looks merged. Conflict markers in XML would be exactly
/// the unreadable file this exists to prevent.
/// </para>
/// <para>
/// The result is built node by node rather than by copying one side and patching it, so it is a function of the three
/// inputs: every surviving node is created with its own id, attached where the merge decided it stands, and filled with
/// the values the merge decided it holds.
/// </para>
/// </remarks>
public static class ThreeWayMerge
{
    /// <summary>Merges into a project of its own, made from the registry our side was read with.</summary>
    /// <param name="baseModel">The version both sides started from.</param>
    /// <param name="ours">The branch being merged into.</param>
    /// <param name="theirs">The branch being merged.</param>
    /// <returns>The merged model, or what could not be decided.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MergeResult Merge(Model baseModel, Model ours, Model theirs) =>
        Merge(baseModel, ours, theirs, new Project(ours.Project.Concepts));

    /// <summary>Merges into <paramref name="target"/>, which receives one new model.</summary>
    /// <param name="baseModel">The version both sides started from.</param>
    /// <param name="ours">The branch being merged into.</param>
    /// <param name="theirs">The branch being merged.</param>
    /// <param name="target">
    /// The project the result is built in. It has to hold the concepts of the three models and none of their ids, which
    /// is what makes "a project of its own" the ordinary call rather than a rule the caller has to remember.
    /// </param>
    /// <returns>The merged model, or what could not be decided.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MergeResult Merge(Model baseModel, Model ours, Model theirs, Project target)
    {
        ArgumentNullException.ThrowIfNull(baseModel);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);
        ArgumentNullException.ThrowIfNull(target);

        var origin = Index(baseModel);
        var left = Index(ours);
        var right = Index(theirs);

        // What each side touched, which is the only question `ModelDiff` is asked: a deletion has to know whether the
        // other side changed the node or something below it, and that is a question about ids rather than about values.
        var leftTouched = Touched(baseModel, ours);
        var rightTouched = Touched(baseModel, theirs);

        var conflicts = new List<MergeConflict>();

        // 1. Which nodes survive, and what each one is.
        var alive = new HashSet<NodeId>();
        var concepts = new Dictionary<NodeId, Concept>();

        Survive(origin, left, right, leftTouched, rightTouched, alive, concepts, conflicts);

        if (conflicts.Count > 0)
        {
            return MergeResult.Conflicted(conflicts);
        }

        // 2. What each surviving node holds.
        var properties = new Dictionary<NodeId, Dictionary<string, string>>(EqualityComparer<NodeId>.Default);
        var references = new Dictionary<NodeId, Dictionary<string, NodeReference>>(EqualityComparer<NodeId>.Default);

        Hold(origin, left, right, alive, concepts, properties, references, conflicts);

        // 3. Where each surviving node stands, without deciding the order yet.
        var places = new Dictionary<NodeId, (NodeId? Parent, string? Slot)>();

        Place(origin, left, right, alive, places, conflicts);

        if (conflicts.Count > 0)
        {
            return MergeResult.Conflicted(conflicts);
        }

        // 4. The order inside every slot.
        var orders = Order(origin, left, right, alive, places, conflicts);

        if (conflicts.Count > 0)
        {
            return MergeResult.Conflicted(conflicts);
        }

        // 5. A reference into a node the merge removed is the merge's own doing, and it is not silent.
        Dangling(origin, left, right, alive, references, conflicts);

        if (conflicts.Count > 0)
        {
            return MergeResult.Conflicted(conflicts);
        }

        return MergeResult.Merged(Build(target, ours.Name, alive, concepts, properties, references, orders));
    }

    /// <summary>Which nodes the merge keeps, and the concept each one has.</summary>
    private static void Survive(
        Dictionary<NodeId, Node> origin,
        Dictionary<NodeId, Node> left,
        Dictionary<NodeId, Node> right,
        HashSet<NodeId> leftTouched,
        HashSet<NodeId> rightTouched,
        HashSet<NodeId> alive,
        Dictionary<NodeId, Concept> concepts,
        List<MergeConflict> conflicts)
    {
        foreach (var id in origin.Keys.Union(left.Keys).Union(right.Keys))
        {
            var inOrigin = origin.ContainsKey(id);
            var onLeft = left.TryGetValue(id, out var ourNode);
            var onRight = right.TryGetValue(id, out var theirNode);

            if (onLeft && onRight)
            {
                // Both sides have it. Two different concepts would be a document nothing can be built from, so the
                // merge says so instead of picking one and producing a node whose slots do not match its content.
                if (!string.Equals(ourNode!.Concept.Key, theirNode!.Concept.Key, StringComparison.Ordinal))
                {
                    conflicts.Add(MergeConflict.Of(
                        id,
                        MergeConflictKind.ConceptChanged,
                        null,
                        ourNode.Concept.Key,
                        theirNode.Concept.Key));

                    continue;
                }

                alive.Add(id);
                concepts[id] = ourNode.Concept;

                continue;
            }

            if (onLeft || onRight)
            {
                var node = onLeft ? ourNode! : theirNode!;

                if (!inOrigin)
                {
                    // One side added it, which is simply part of the result.
                    alive.Add(id);
                    concepts[id] = node.Concept;

                    continue;
                }

                // One side deleted it. If the other side changed it - or anything below it - the merge will not decide
                // which of the two intents wins. It is the side that *kept* the node whose changes matter: the
                // deleting side's own journal holds the removal, so asking it would make every deletion a conflict.
                var changed = onLeft ? leftTouched : rightTouched;

                if (Touches(id, origin, changed))
                {
                    conflicts.Add(MergeConflict.Of(
                        id,
                        MergeConflictKind.DeletedAndEdited,
                        null,
                        onLeft ? "kept" : "deleted",
                        onLeft ? "deleted" : "kept"));

                    continue;
                }

                continue;
            }

            // Neither side has it: both deleted it, which is not a conflict.
        }
    }

    /// <summary>What every surviving node holds, asked property by property and reference by reference.</summary>
    private static void Hold(
        Dictionary<NodeId, Node> origin,
        Dictionary<NodeId, Node> left,
        Dictionary<NodeId, Node> right,
        HashSet<NodeId> alive,
        Dictionary<NodeId, Concept> concepts,
        Dictionary<NodeId, Dictionary<string, string>> properties,
        Dictionary<NodeId, Dictionary<string, NodeReference>> references,
        List<MergeConflict> conflicts)
    {
        foreach (var id in alive)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var targets = new Dictionary<string, NodeReference>(StringComparer.Ordinal);

            var onLeft = left.TryGetValue(id, out var ourNode);
            var onRight = right.TryGetValue(id, out var theirNode);
            var inOrigin = origin.TryGetValue(id, out var baseNode);

            foreach (var slot in concepts[id].Slots)
            {
                switch (slot)
                {
                    case PropertySlot property:
                    {
                        var ourValue = onLeft ? ourNode!.GetOrNull(property.Name) : null;
                        var theirValue = onRight ? theirNode!.GetOrNull(property.Name) : null;
                        var baseValue = inOrigin ? baseNode!.GetOrNull(property.Name) : null;

                        if (Decide(
                                id,
                                property.Name,
                                MergeConflictKind.PropertyWrittenDifferently,
                                ourValue,
                                theirValue,
                                baseValue,
                                conflicts) is string value)
                        {
                            values[property.Name] = value;
                        }

                        break;
                    }

                    case ReferenceSlot reference:
                    {
                        var ourValue = onLeft ? ourNode!.GetReference(reference.Name) : null;
                        var theirValue = onRight ? theirNode!.GetReference(reference.Name) : null;
                        var baseValue = inOrigin ? baseNode!.GetReference(reference.Name) : null;

                        if (Decide(
                                id,
                                reference.Name,
                                MergeConflictKind.ReferenceRetargeted,
                                ourValue,
                                theirValue,
                                baseValue,
                                conflicts) is NodeReference target)
                        {
                            targets[reference.Name] = target;
                        }

                        break;
                    }
                }
            }

            properties[id] = values;
            references[id] = targets;
        }
    }

    /// <summary>The three-way question for one value, or <see langword="null"/> when it does not survive.</summary>
    /// <remarks>
    /// Boxed rather than generic, because the two things it is asked about are different kinds - a property value is
    /// text and a reference is a small struct - and one question written once is worth a box per value compared.
    /// </remarks>
    private static object? Decide(
        NodeId id,
        string slot,
        MergeConflictKind kind,
        object? ours,
        object? theirs,
        object? origin,
        List<MergeConflict> conflicts)
    {
        if (ours is null && theirs is null)
        {
            return null;
        }

        if (Equals(ours, theirs))
        {
            return ours;
        }

        if (Equals(ours, origin))
        {
            return theirs;
        }

        if (Equals(theirs, origin))
        {
            return ours;
        }

        conflicts.Add(MergeConflict.Of(id, kind, slot, Text(ours), Text(theirs)));

        return null;
    }

    /// <summary>Where each surviving node stands, before the order inside a slot is decided.</summary>
    private static void Place(
        Dictionary<NodeId, Node> origin,
        Dictionary<NodeId, Node> left,
        Dictionary<NodeId, Node> right,
        HashSet<NodeId> alive,
        Dictionary<NodeId, (NodeId? Parent, string? Slot)> places,
        List<MergeConflict> conflicts)
    {
        foreach (var id in alive)
        {
            var onLeft = left.TryGetValue(id, out var ourNode);
            var onRight = right.TryGetValue(id, out var theirNode);
            var inOrigin = origin.TryGetValue(id, out var baseNode);

            var ourPlace = onLeft ? Place(ourNode!) : default;
            var theirPlace = onRight ? Place(theirNode!) : default;
            var basePlace = inOrigin ? Place(baseNode!) : default;

            if (onLeft && onRight)
            {
                // "Moved" is a structural question here: another parent or another slot. A different index inside the
                // same slot is a reordering, and the order phase answers that for the whole sequence at once.
                var ourMoved = inOrigin && !SameParent(ourPlace, basePlace);
                var theirMoved = inOrigin && !SameParent(theirPlace, basePlace);

                if (ourMoved && theirMoved && !SameParent(ourPlace, theirPlace))
                {
                    conflicts.Add(MergeConflict.Of(
                        id,
                        MergeConflictKind.MovedDifferently,
                        null,
                        Describe(ourPlace),
                        Describe(theirPlace)));

                    continue;
                }

                places[id] = ourMoved ? ourPlace : theirPlace;

                continue;
            }

            places[id] = onLeft ? ourPlace : theirPlace;
        }
    }

    /// <summary>The merged sequence of every slot that holds surviving children.</summary>
    private static Dictionary<(NodeId? Parent, string? Slot), List<NodeId>> Order(
        Dictionary<NodeId, Node> origin,
        Dictionary<NodeId, Node> left,
        Dictionary<NodeId, Node> right,
        HashSet<NodeId> alive,
        Dictionary<NodeId, (NodeId? Parent, string? Slot)> places,
        List<MergeConflict> conflicts)
    {
        var groups = alive
            .GroupBy(id => places[id])
            .OrderBy(group => group.Key.Parent?.ToString(), StringComparer.Ordinal)
            .ThenBy(group => group.Key.Slot, StringComparer.Ordinal);

        var orders = new Dictionary<(NodeId? Parent, string? Slot), List<NodeId>>();

        foreach (var group in groups)
        {
            var surviving = group.ToHashSet();
            var oursInSlot = Sequence(left, group.Key, surviving);
            var theirsInSlot = Sequence(right, group.Key, surviving);

            // Both sides put the nodes they share in a different order, which is a question only a person answers.
            if (Reordered(oursInSlot, theirsInSlot, origin, surviving))
            {
                conflicts.Add(MergeConflict.OfOrder(
                    group.Key.Parent,
                    group.Key.Slot ?? "roots",
                    Join(oursInSlot),
                    Join(theirsInSlot)));

                continue;
            }

            // The base order, with what each side put here that the base did not have here - an addition, or a move
            // into this slot - emitted after the base id it follows in its own sequence. Ours first and then theirs at
            // one anchor, each in its own order, so two unrelated additions to one list both land.
            var baseSequence = Sequence(origin, group.Key, surviving);
            var fromOurs = Insertions(oursInSlot, baseSequence);
            var fromTheirs = Insertions(theirsInSlot, baseSequence);

            var order = new List<NodeId>();

            Emit(order, null, fromOurs, fromTheirs);

            foreach (var id in baseSequence)
            {
                order.Add(id);
                Emit(order, id, fromOurs, fromTheirs);
            }

            orders[group.Key] = order;
        }

        return orders;
    }

    /// <summary>The ids one side put here that the base did not have here, each with the base id it follows.</summary>
    /// <remarks>
    /// A node that stands here on one side but stood elsewhere in the base is an insertion here: this is the same list
    /// arithmetic for "added" and "moved in", which is what keeps a move from being a second kind of problem.
    /// </remarks>
    private static List<(NodeId Id, NodeId? Anchor)> Insertions(List<NodeId> sequence, List<NodeId> baseSequence)
    {
        var insertions = new List<(NodeId Id, NodeId? Anchor)>();
        NodeId? anchor = null;

        foreach (var id in sequence)
        {
            if (baseSequence.Contains(id))
            {
                anchor = id;

                continue;
            }

            insertions.Add((id, anchor));
        }

        return insertions;
    }

    /// <summary>Appends the insertions of both sides that follow one anchor, ours before theirs.</summary>
    private static void Emit(
        List<NodeId> order,
        NodeId? anchor,
        List<(NodeId Id, NodeId? Anchor)> ours,
        List<(NodeId Id, NodeId? Anchor)> theirs)
    {
        for (var side = 0; side < 2; side++)
        {
            foreach (var (id, at) in side == 0 ? ours : theirs)
            {
                if (at == anchor)
                {
                    order.Add(id);
                }
            }
        }
    }

    /// <summary>The ids of one slot in one model, restricted to what survives.</summary>
    private static List<NodeId> Sequence(
        Dictionary<NodeId, Node> model,
        (NodeId? Parent, string? Slot) slot,
        HashSet<NodeId> surviving)
    {
        var sequence = new List<NodeId>();

        if (slot.Parent is not { } parent)
        {
            if (!model.Values.Any(node => node.IsRoot))
            {
                return sequence;
            }

            foreach (var root in Roots(model))
            {
                if (surviving.Contains(root))
                {
                    sequence.Add(root);
                }
            }

            return sequence;
        }

        if (model.TryGetValue(parent, out var owner))
        {
            foreach (var child in owner.GetChildren(slot.Slot!))
            {
                if (surviving.Contains(child.Id))
                {
                    sequence.Add(child.Id);
                }
            }
        }

        return sequence;
    }

    /// <summary>Whether the two sides disagree about the order of the nodes they share.</summary>
    private static bool Reordered(
        List<NodeId> ours,
        List<NodeId> theirs,
        Dictionary<NodeId, Node> origin,
        HashSet<NodeId> surviving)
    {
        var shared = ours.Where(id => origin.ContainsKey(id) && surviving.Contains(id)).ToList();
        var other = theirs.Where(id => origin.ContainsKey(id) && surviving.Contains(id)).ToList();

        // Only the same set of base ids asks the same question. When one side moved a node out of this slot and another
        // in, the two lists hold different nodes and comparing their order would be a guess dressed as a conflict.
        return shared.Count > 1
            && shared.Count == other.Count
            && shared.ToHashSet().SetEquals(other)
            && !shared.SequenceEqual(other);
    }

    /// <summary>A reference that the merge would leave pointing at a node it removed.</summary>
    private static void Dangling(
        Dictionary<NodeId, Node> origin,
        Dictionary<NodeId, Node> left,
        Dictionary<NodeId, Node> right,
        HashSet<NodeId> alive,
        Dictionary<NodeId, Dictionary<string, NodeReference>> references,
        List<MergeConflict> conflicts)
    {
        foreach (var (id, targets) in references)
        {
            foreach (var (slot, reference) in targets)
            {
                if (alive.Contains(reference.Target) || !origin.ContainsKey(reference.Target))
                {
                    continue;
                }

                // Only when the removal is this merge's doing: a node both sides deleted left the same dangling
                // reference behind before the merge ran, and refusing that would refuse a state both branches accepted.
                if (!left.ContainsKey(reference.Target) && !right.ContainsKey(reference.Target))
                {
                    continue;
                }

                conflicts.Add(MergeConflict.Of(
                    id,
                    MergeConflictKind.DanglingReference,
                    slot,
                    reference.ResolveInfo ?? reference.Target.ToString(),
                    $"an id the merge removed ({reference.Target})"));
            }
        }
    }

    /// <summary>Builds the merged model from what the phases decided.</summary>
    private static Model Build(
        Project target,
        string? name,
        HashSet<NodeId> alive,
        Dictionary<NodeId, Concept> concepts,
        Dictionary<NodeId, Dictionary<string, string>> properties,
        Dictionary<NodeId, Dictionary<string, NodeReference>> references,
        Dictionary<(NodeId? Parent, string? Slot), List<NodeId>> orders)
    {
        // The transaction is the model's, and it is opened before the first node exists so the whole build is one
        // change event - a merge is one edit to whoever is listening, the same rule every other bulk edit follows.
        var model = target.NewModel(name);

        using var transaction = model.BeginTransaction();

        var created = alive.ToDictionary(id => id, id => model.CreateDetached(id, concepts[id]));

        foreach (var ((parent, slot), sequence) in orders)
        {
            foreach (var id in sequence)
            {
                if (parent is { } owner)
                {
                    created[owner].AddChild(slot!, created[id]);
                }
                else
                {
                    model.AddRoot(created[id]);
                }
            }
        }

        foreach (var (id, values) in properties)
        {
            foreach (var (slot, value) in values)
            {
                created[id].Set(slot, value);
            }
        }

        foreach (var (id, targets) in references)
        {
            foreach (var (slot, reference) in targets)
            {
                created[id].SetReference(slot, reference.Target, reference.ResolveInfo);
            }
        }

        transaction.Complete();

        return model;
    }

    /// <summary>Every node of a model by id, which is what every question here is asked about.</summary>
    private static Dictionary<NodeId, Node> Index(Model model) =>
        model.Nodes.ToDictionary(node => node.Id);

    /// <summary>Every node one side changed, compared with the version both sides started from.</summary>
    private static HashSet<NodeId> Touched(Model origin, Model side) =>
        [.. ModelDiff.Of(origin, side).Select(difference => difference.Node)];

    /// <summary>Whether <paramref name="id"/> or something below it is in <paramref name="touched"/>.</summary>
    private static bool Touches(
        NodeId id,
        Dictionary<NodeId, Node> origin,
        HashSet<NodeId> touched)
    {
        if (touched.Contains(id))
        {
            return true;
        }

        if (!origin.TryGetValue(id, out var node))
        {
            return false;
        }

        foreach (var child in node.SelfAndDescendants())
        {
            if (touched.Contains(child.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<NodeId> Roots(Dictionary<NodeId, Node> model) =>
        model.Values.Where(node => node.IsRoot).OrderBy(node => node.IndexInParent).Select(node => node.Id);

    private static (NodeId? Parent, string? Slot) Place(Node node) =>
        (node.Parent?.Id, node.ParentSlot);

    private static bool SameParent((NodeId? Parent, string? Slot) one, (NodeId? Parent, string? Slot) other) =>
        one.Parent == other.Parent && string.Equals(one.Slot, other.Slot, StringComparison.Ordinal);

    private static string Describe((NodeId? Parent, string? Slot) place) =>
        place.Parent is { } parent ? $"{parent}.{place.Slot}" : "a root";

    private static string Join(List<NodeId> ids) =>
        string.Join(", ", ids.Select(id => id.ToString()));

    private static string Text(object? value) => value switch
    {
        null => "<none>",
        NodeReference reference => reference.ResolveInfo ?? reference.Target.ToString(),
        string text => text.Length == 0 ? "<empty>" : text,
        _ => value.ToString() ?? "<none>",
    };
}
