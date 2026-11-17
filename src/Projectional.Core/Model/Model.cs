namespace Projectional.Core;

/// <summary>
/// One document: an ordered set of root nodes written in the project's language. A model is the
/// unit that is saved to a file, opened in an editor tab and put under version control.
/// </summary>
/// <remarks>
/// <see cref="Version"/> counts mutations so that the layers above — projections, snapshots, dirty
/// tracking — can tell "nothing changed" from "changed and changed back" without comparing trees.
/// Every mutation is also journalled, so that it can be described to subscribers in a
/// <see cref="ModelChangedEventArgs"/> and taken back if the transaction around it never commits.
/// </remarks>
public sealed class Model
{
    private readonly List<Node> _roots = [];
    private readonly ChangeJournal _journal = new();
    private int _depth;
    private EventHandler<ModelChangedEventArgs>? _changed;

    internal Model(Project project, ModelId id, string? name)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (id.IsEmpty)
        {
            throw new ArgumentException("A model id must not be empty.", nameof(id));
        }

        Project = project;
        Id = id;
        Name = name;
    }

    /// <summary>Raised once per committed transaction, with everything it changed.</summary>
    public event EventHandler<ModelChangedEventArgs>? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }

    public ModelId Id { get; }

    public string? Name { get; private set; }

    public Project Project { get; }

    public ConceptRegistry Concepts => Project.Concepts;

    public IReadOnlyList<Node> Roots => field ??= _roots.AsReadOnly();

    /// <summary>Every node of this model, depth first: each root, then its whole subtree.</summary>
    /// <remarks>
    /// <see cref="Project.Nodes"/> is the whole project, which is a different question: a project holds several
    /// models, so a walk over the index answers with nodes of documents that are not being looked at. This is
    /// the walk a diff, a copy budget or a "find this node" pass wants.
    /// </remarks>
    public IEnumerable<Node> Nodes
    {
        get
        {
            foreach (var root in Roots)
            {
                foreach (var node in SelfAndDescendants(root))
                {
                    yield return node;
                }
            }
        }
    }

    /// <summary>Increases with every mutation of this model.</summary>
    public long Version { get; private set; }

    /// <summary>Whether a transaction is open, so that the next edit joins it instead of publishing.</summary>
    public bool HasTransaction => _depth > 0;

    /// <summary>
    /// Whether edits have to be journalled: either an open transaction needs to be able to roll them
    /// back, or a subscriber will want to hear about them.
    /// </summary>
    internal bool IsRecording => _depth > 0 || _changed is not null;

    internal int JournalCount => _journal.Count;

    /// <summary>
    /// Opens a transaction that groups the edits made inside it into one <see cref="ModelChangedEventArgs"/>
    /// and that is rolled back if it is disposed without <see cref="ModelTransaction.Complete"/>.
    /// </summary>
    public ModelTransaction BeginTransaction()
    {
        var transaction = new ModelTransaction(this, _journal.Count, Version);
        _depth++;
        return transaction;
    }

    public void Rename(string? name) => Name = name;

    /// <summary>
    /// Creates a node of the given concept and makes it a root. Roots are the only place a node
    /// can be created: a node that belonged to nothing would break the one-parent-or-root rule.
    /// </summary>
    public Node NewNode(string conceptKey) => NewNode(Project.Concepts.Get(conceptKey));

    public Node NewNode(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);
        RequireKnownConcept(concept);

        using var transaction = BeginTransaction();
        var node = CreateDetached(concept);
        InsertRootAt(_roots.Count, node);
        RecordAdded(node, parent: null, role: null, index: node.IndexInParent);
        MarkMutated();
        transaction.Complete();
        return node;
    }

    /// <summary>Creates a node and immediately hangs it into <paramref name="parent"/>.</summary>
    /// <remarks>
    /// The node is created detached and only then placed, so that the whole operation is one
    /// <see cref="NodeAdded"/>: a caller creating and inserting should not have to subscribe to the
    /// intermediate state where the node was briefly a root.
    /// </remarks>
    public Node NewChild(Node parent, string slot, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(parent);

        using var transaction = BeginTransaction();
        var node = CreateDetached(Project.Concepts.Get(conceptKey));
        parent.AddChild(slot, node);
        MarkMutated();
        transaction.Complete();
        return node;
    }

    /// <summary>Creates a node and hangs it into <paramref name="parent"/> at <paramref name="index"/>.</summary>
    /// <param name="parent">The node that gains the child.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/>.</param>
    /// <param name="index">Where among the slot's children the new node goes.</param>
    /// <param name="conceptKey">The concept of the new child.</param>
    /// <returns>The node that was created.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ModelInvariantException">
    /// The concept is unknown, does not conform to the slot, or is not allowed there.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The same operation as <see cref="NewChild"/>, except that the new node is placed rather than appended -
    /// which is what pressing <c>Enter</c> in the middle of a list means. Appending there puts the thing the
    /// user just made at the far end of a list they were not looking at.
    /// </para>
    /// <para>
    /// An <paramref name="index"/> past the end appends, because that is what the position means once the list
    /// is shorter than the caller expected. One <see cref="NodeAdded"/> is published either way, so undo takes
    /// the node out of the place it actually went into.
    /// </para>
    /// </remarks>
    public Node NewChildAt(Node parent, string slot, int index, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        using var transaction = BeginTransaction();
        var node = CreateDetached(Project.Concepts.Get(conceptKey));
        parent.InsertChild(slot, index, node);
        MarkMutated();
        transaction.Complete();
        return node;
    }

    /// <summary>Puts <paramref name="node"/> inside a new node of <paramref name="wrapperConceptKey"/>.</summary>
    /// <param name="node">The node to surround, which keeps its identity and its subtree.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around it.</param>
    /// <param name="wrapperSlot">The single-child slot of that concept which receives <paramref name="node"/>.</param>
    /// <param name="presets">
    /// Values the wrapper is born with, such as the operator that makes a unary a negation, or
    /// <see langword="null"/> for a wrapper holding only its defaults.
    /// </param>
    /// <returns>The node that was created.</returns>
    /// <remarks>
    /// The one-node case of <see cref="Wrap(IReadOnlyList{Node}, string, string, IReadOnlyDictionary{string, string})"/>,
    /// which is where the reasoning lives: a wrapper that already holds something is the same operation with a
    /// run of siblings instead of one node.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="wrapperConceptKey"/> or <paramref name="wrapperSlot"/> is empty.
    /// </exception>
    /// <exception cref="ModelInvariantException">
    /// <paramref name="node"/> is not part of this model, has no place, cannot be wrapped by that concept
    /// in that slot, the wrapper declares no child slot of that name that can hold it, or a preset names a
    /// property the wrapper does not declare or a value its type refuses.
    /// </exception>
    public Node Wrap(
        Node node,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        return Wrap([node], wrapperConceptKey, wrapperSlot, presets);
    }

    /// <summary>
    /// Puts a run of sibling nodes inside one new node of <paramref name="wrapperConceptKey"/>.
    /// </summary>
    /// <param name="nodes">
    /// The nodes to surround, which keep their identity, their subtrees and their order. They must be siblings
    /// standing next to each other in one slot.
    /// </param>
    /// <param name="wrapperConceptKey">The concept of the node to create around them.</param>
    /// <param name="wrapperSlot">
    /// The slot of that concept which receives them - a many-valued one unless there is a single node.
    /// </param>
    /// <param name="presets">
    /// Values the wrapper is born with, or <see langword="null"/> for a wrapper holding only its defaults.
    /// </param>
    /// <returns>The node that was created.</returns>
    /// <remarks>
    /// <para>
    /// Wrapping cannot be written as "create the wrapper, then move the nodes into it": a single-valued slot
    /// holds at most one child, so there is no moment at which both a node and its wrapper could be in
    /// the slot together. The nodes therefore leave first and return inside the wrapper, and all steps run
    /// in one transaction so that the user sees one undo step and so that a failure cannot leave the nodes
    /// outside the document.
    /// </para>
    /// <para>
    /// The nodes keep their ids, which is what makes the operation safe to undo and what makes several nodes
    /// one operation: every reference that points into their subtrees and every caret address that names one
    /// of their cells still resolves afterwards.
    /// </para>
    /// <para>
    /// A run, not a set: the nodes must name the place each other stands in, so a selection that skipped one
    /// of their siblings is refused rather than silently leaving that sibling outside the wrapper. Nothing
    /// here decides what a user selected - <c>SelectionRanges.NodesIn</c> answers in document order, and the
    /// order this operation keeps is the order a reader sees.
    /// </para>
    /// <para>
    /// <paramref name="presets"/> exists because some wrappers only mean something with a value:
    /// "negate" is a <c>Unary</c> whose operator is <c>!</c>, and a caller cannot write that value
    /// afterwards, because the wrapper's id does not exist until it is created. Every preset is validated
    /// against the wrapper's declarations before anything is touched, so a wrong one fails without leaving
    /// the nodes detached.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="nodes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="nodes"/> is empty, or <paramref name="wrapperConceptKey"/> or
    /// <paramref name="wrapperSlot"/> is empty.
    /// </exception>
    /// <exception cref="ModelInvariantException">
    /// A node is not part of this model, has no place, the nodes are not consecutive siblings, they cannot be
    /// wrapped by that concept in that slot, the wrapper declares no slot of that name that can hold them all,
    /// or a preset names a property the wrapper does not declare or a value its type refuses.
    /// </exception>
    public Node Wrap(
        IReadOnlyList<Node> nodes,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        if (nodes.Count == 0)
        {
            throw new ArgumentException("A wrap needs at least one node.", nameof(nodes));
        }

        var first = nodes[0];
        ArgumentNullException.ThrowIfNull(first);

        if (!ReferenceEquals(first.Model, this))
        {
            throw new ModelInvariantException(
                $"Node {first.Id} belongs to a different model and cannot be wrapped by this one's concepts.");
        }

        if (first.Parent is not { } parent || first.ParentSlot is not { } slot)
        {
            throw new ModelInvariantException(
                $"Node {first.Id} has no place, so there is nothing to wrap it in.");
        }

        if (!Project.Concepts.TryGet(wrapperConceptKey, out var wrapper))
        {
            throw new ModelInvariantException(
                $"Concept '{wrapperConceptKey}' is not part of the language of this project.");
        }

        if (parent.Concept.FindChild(slot) is not { } place)
        {
            throw new ModelInvariantException(
                $"Slot '{slot}' of concept '{parent.Concept.Key}' holds no children, so nothing can be wrapped in it.");
        }

        if (wrapper.IsAbstract || !wrapper.IsKindOf(place.ChildConceptKey))
        {
            throw new ModelInvariantException(
                $"Concept '{wrapperConceptKey}' cannot stand in slot '{slot}' of concept '{parent.Concept.Key}'.");
        }

        if (wrapper.FindChild(wrapperSlot) is not { } inner)
        {
            throw new ModelInvariantException(
                $"Concept '{wrapperConceptKey}' declares no child slot named '{wrapperSlot}'.");
        }

        if (inner.Cardinality == Cardinality.One && nodes.Count > 1)
        {
            throw new ModelInvariantException(
                $"Slot '{wrapperSlot}' of concept '{wrapperConceptKey}' holds one child, so it cannot take {nodes.Count} nodes.");
        }

        var seen = new HashSet<NodeId>();
        var ordered = new Node[nodes.Count];

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            ArgumentNullException.ThrowIfNull(node);

            if (!ReferenceEquals(node.Model, this))
            {
                throw new ModelInvariantException(
                    $"Node {node.Id} belongs to a different model and cannot be wrapped by this one's concepts.");
            }

            if (!seen.Add(node.Id))
            {
                throw new ModelInvariantException($"Node {node.Id} was named twice, so it cannot be wrapped once.");
            }

            if (!ReferenceEquals(node.Parent, parent) || !string.Equals(node.ParentSlot, slot, StringComparison.Ordinal))
            {
                throw new ModelInvariantException(
                    $"Node {node.Id} does not stand in slot '{slot}' of the same parent as node {first.Id}, so the nodes are not consecutive siblings.");
            }

            if (!node.Concept.IsKindOf(inner.ChildConceptKey))
            {
                throw new ModelInvariantException(
                    $"Concept '{node.Concept.Key}' does not conform to slot '{wrapperSlot}' of concept '{wrapperConceptKey}'.");
            }

            ordered[index] = node;
        }

        // Sorted by the order a reader sees, then required to be a run: a selection that left a sibling in
        // between would otherwise put the wrapper over some nodes and leave that sibling outside it.
        Array.Sort(ordered, (left, right) => left.IndexInParent.CompareTo(right.IndexInParent));

        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].IndexInParent != ordered[index - 1].IndexInParent + 1)
            {
                throw new ModelInvariantException(
                    $"Node {ordered[index].Id} does not stand directly after node {ordered[index - 1].Id}, so the nodes are not consecutive siblings.");
            }
        }

        // Validated before anything moves: a preset the wrapper refuses must not leave the nodes detached.
        if (presets is not null)
        {
            foreach (var (presetSlot, value) in presets)
            {
                if (wrapper.FindProperty(presetSlot) is not { } property)
                {
                    throw new ModelInvariantException(
                        $"Concept '{wrapperConceptKey}' declares no property named '{presetSlot}', so it cannot be born with it.");
                }

                if (!property.Type.IsValid(value))
                {
                    throw new ModelInvariantException(
                        $"\"{value}\" is not a valid {property.Type.Name} value for '{presetSlot}' of concept '{wrapperConceptKey}'.");
                }
            }
        }

        var at = ordered[0].IndexInParent;

        using var transaction = BeginTransaction();

        foreach (var node in ordered)
        {
            node.Detach();
        }

        var created = CreateDetached(wrapper);
        parent.InsertChild(slot, Math.Min(at, parent.GetChildren(slot).Count), created);

        for (var index = 0; index < ordered.Length; index++)
        {
            created.InsertChild(wrapperSlot, index, ordered[index]);
        }

        if (presets is not null)
        {
            foreach (var (presetSlot, value) in presets)
            {
                created.Set(presetSlot, value);
            }
        }

        MarkMutated();

        transaction.Complete();

        return created;
    }

    /// <summary>Puts a node of <paramref name="conceptKey"/> where <paramref name="node"/> stands.</summary>
    /// <param name="node">The node whose place is taken; it is deleted with its whole subtree.</param>
    /// <param name="conceptKey">The concept of the node that takes its place.</param>
    /// <returns>The node that was created, already attached where <paramref name="node"/> was.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="conceptKey"/> is empty.</exception>
    /// <exception cref="KeyNotFoundException">The concept is not part of the language of this project.</exception>
    /// <exception cref="ModelInvariantException">
    /// <paramref name="node"/> belongs to a different model, the concept is abstract, it does not conform to the
    /// slot <paramref name="node"/> stands in, or it may not stand there.
    /// </exception>
    /// <remarks>
    /// <para>
    /// A replacement is a deletion and a creation at one place, and one transaction around both: the user sees one
    /// undo step, and undo puts the <em>old</em> node back with its identity and its whole subtree rather than a
    /// rebuilt copy of it.
    /// </para>
    /// <para>
    /// The two steps cannot be swapped or overlapped. A single-valued slot holds at most one child, so the old node
    /// has to leave before the new one arrives; and the new node is born detached so that it is never briefly a
    /// root, which would also make one replacement two change sets.
    /// </para>
    /// <para>
    /// What the old node held does not travel. Its properties, references and children are gone with it, and the
    /// references that pointed into its subtree are cleared, exactly as a deletion clears them - a "replace" that
    /// carried values over would need a per-concept correspondence between old and new slots, which no declaration
    /// describes. The answer is an empty node at the place the user pointed at, which is what they then fill.
    /// </para>
    /// <para>
    /// Everything a placement could refuse is refused before anything moves, so a concept that cannot stand there
    /// cannot leave the document missing a node.
    /// </para>
    /// </remarks>
    public Node Replace(Node node, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        if (!ReferenceEquals(node.Model, this))
        {
            throw new ModelInvariantException(
                $"Node {node.Id} belongs to a different model and cannot be replaced by this one's concepts.");
        }

        var concept = Project.Concepts.Get(conceptKey);
        var parent = node.Parent;
        var slot = node.ParentSlot;
        var index = node.IndexInParent;

        if (parent is { } place && slot is not null)
        {
            if (place.Concept.FindChild(slot) is not { } declared)
            {
                throw new ModelInvariantException(
                    $"Slot '{slot}' of concept '{place.Concept.Key}' holds no children, so nothing can be replaced in it.");
            }

            if (!concept.IsKindOf(declared.ChildConceptKey))
            {
                throw new ModelInvariantException(
                    $"Concept '{conceptKey}' does not conform to slot '{slot}' of concept '{place.Concept.Key}', which holds '{declared.ChildConceptKey}'.");
            }
        }

        using var transaction = BeginTransaction();

        var created = CreateDetached(concept);

        // The model's own answer to "may this concept stand here", asked before anything moves. It covers the
        // root case as well, which is the one a replacement at the top of a document asks.
        RequireApplicable(created, parent, slot);

        DeleteSubtree(node);

        if (parent is { } host && slot is not null)
        {
            // Journals the NodeAdded itself, so the pair of entries is the whole replacement.
            host.InsertChild(slot, index, created);
        }
        else
        {
            InsertRootAt(index, created);
            RecordAdded(created, parent: null, role: null, index: created.IndexInParent);
        }

        MarkMutated();

        transaction.Complete();

        return created;
    }

    /// <summary>Puts the children of <paramref name="slot"/> into <paramref name="order"/>.</summary>
    /// <param name="parent">The node whose children are reordered.</param>
    /// <param name="slot">A child slot declared with <see cref="Cardinality.Many"/>.</param>
    /// <param name="order">
    /// Every child of that slot, exactly once, in the order they should have afterwards.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="parent"/> or <paramref name="order"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ModelInvariantException">
    /// <paramref name="parent"/> belongs to a different model, the slot holds no children or holds one child only, or
    /// <paramref name="order"/> is not a permutation of that slot's children.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The model reorders; it does not decide the order. Sorting by a value, by a declaration or by anything else is a
    /// rule of whoever asks, and a model that knew one of them would have to grow a second and a third. What the model
    /// owns is the part that must not vary: the slot ends up holding the same children, no more and no fewer, and the
    /// whole rearrangement is one transaction.
    /// </para>
    /// <para>
    /// It is written as "take them all out, put them all back" rather than as the shortest sequence of moves, because
    /// every entry that produces is a pair the journal already knows how to undo: the removals come out in reverse,
    /// each putting a node back at the index it left, and the original order falls out of that on its own. A
    /// minimal permutation would be cheaper to look at and would need its own reasoning about reversibility - and it
    /// is the reversibility, not the number of entries, that a user sees when they press undo.
    /// </para>
    /// <para>
    /// An order that already holds is accepted and journals nothing, because "sort what is sorted" is not an edit and
    /// must not become an undo step the user has to press through.
    /// </para>
    /// </remarks>
    public void ReorderChildren(Node parent, string slot, IReadOnlyList<Node> order)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(order);

        if (!ReferenceEquals(parent.Model, this))
        {
            throw new ModelInvariantException(
                $"Node {parent.Id} belongs to a different model and cannot be reordered by this one.");
        }

        if (parent.Concept.FindChild(slot) is not { } declared)
        {
            throw new ModelInvariantException(
                $"Concept '{parent.Concept.Key}' declares no child slot named '{slot}'.");
        }

        if (declared.Cardinality != Cardinality.Many)
        {
            throw new ModelInvariantException(
                $"Slot '{slot}' of concept '{parent.Concept.Key}' holds one child, so it has no order to change.");
        }

        if (!IsPermutation(parent.GetChildren(slot), order))
        {
            throw new ModelInvariantException(
                $"The given order is not a rearrangement of the children of slot '{slot}' of concept '{parent.Concept.Key}'; it must name each of them exactly once.");
        }

        if (SameOrder(parent, slot, order))
        {
            return;
        }

        using var transaction = BeginTransaction();

        var children = new List<Node>(parent.GetChildren(slot));

        foreach (var child in children)
        {
            child.Detach();
        }

        for (var index = 0; index < order.Count; index++)
        {
            parent.InsertChild(slot, index, order[index]);
        }

        MarkMutated();

        transaction.Complete();
    }

    /// <summary>Turns an existing node of this model into a root.</summary>
    public void AddRoot(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!ReferenceEquals(node.Model, this))
        {
            throw new ModelInvariantException($"Node {node.Id} belongs to a different model.");
        }

        if (node.IsRoot)
        {
            return;
        }

        using var scope = MutationScope.Begin(this);
        node.Detach();
        InsertRootAt(_roots.Count, node);
        RecordAdded(node, parent: null, role: null, index: node.IndexInParent);
        MarkMutated();
    }

    /// <summary>
    /// Copies this model into <paramref name="target"/> as a new model, keeping every node's identity.
    /// </summary>
    /// <param name="target">The project that receives the copy; it must not already hold these node ids.</param>
    /// <param name="name">The name of the new model, or <see langword="null"/> to copy this model's.</param>
    /// <returns>The new model, which shares no node with this one.</returns>
    /// <exception cref="ModelInvariantException">
    /// <paramref name="target"/> already holds one of these node ids, which is what happens when a copy is
    /// taken into the project it came from.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The copy is exact: same ids, concepts, property values, reference targets and resolve info, same child
    /// order and the same roots in the same order. That is what makes an edit action - a value addressing ids
    /// and slots rather than positions - mean the same thing in the copy as in the original, which is the whole
    /// reason a preview can be "do it to a copy" rather than "describe what you would do".
    /// </para>
    /// <para>
    /// The ids have to be preserved for that to work, and that is also why the target project must be a
    /// different one: <see cref="Project"/> indexes nodes by id, so a copy carrying the original's ids could
    /// only live where those ids are still free. Nothing is shared afterwards - mutating the copy touches no
    /// node of this model.
    /// </para>
    /// <para>
    /// A failure leaves nothing behind: the nodes built so far are deleted without recording and the
    /// half-built model is discarded from <paramref name="target"/>, the way a failed load cleans up.
    /// </para>
    /// </remarks>
    public Model CopyTo(Project target, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var copy = target.NewModel(name ?? Name);

        try
        {
            foreach (var root in Roots)
            {
                CopySubtree(root, copy, parent: null, slot: null);
            }
        }
        catch
        {
            Abandon(copy, target);

            throw;
        }

        return copy;
    }

    /// <summary>Inserts a copy of <paramref name="source"/> at a place in this model, with new identities.</summary>
    /// <param name="source">The node to copy; it stays where it is.</param>
    /// <param name="parent">The node that receives the copy.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/> the copy goes into.</param>
    /// <param name="index">Where in that slot it goes; past the end appends, as <see cref="NewChildAt"/> does.</param>
    /// <returns>The copied node, which is the root of a copied subtree.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ModelInvariantException">
    /// Either node belongs to another model, the slot does not hold children of the copy's concept, the copy may not
    /// stand there, or the slot is single-valued and already full.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is <see cref="CopyTo"/>'s walk with the one difference that makes a second copy possible: the nodes are
    /// <em>new</em>. <see cref="CopyTo"/> has to preserve identities, because a preview addresses nodes by id and an
    /// action has to mean the same thing in the copy; a copy that stays in the same model cannot, because ids are
    /// unique per project. The two therefore cannot share one routine without a flag that says "same ids or new
    /// ones", and the flag would be the only thing either caller cares about - so they are siblings instead.
    /// </para>
    /// <para>
    /// The copy is exact in everything else: concepts, property values, reference targets and resolve info, child
    /// order, and the whole subtree. A reference is copied as it stands, so a subtree that referred to something
    /// outside itself still does; a reference from one copied node to another also still points at the
    /// <em>original</em>, because "the copy refers to the same thing" is the only reading that does not need to know
    /// what the language means by its references.
    /// </para>
    /// <para>
    /// Everything runs in one transaction, and each node is hung into its place before it is filled - the order
    /// <see cref="CopyTo"/> uses, so a failure can never leave a registered node that nothing points at. One undo
    /// step takes the whole copy back out.
    /// </para>
    /// </remarks>
    public Node InsertCopy(Node source, Node parent, string slot, int index)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!ReferenceEquals(source.Model, this) || !ReferenceEquals(parent.Model, this))
        {
            throw new ModelInvariantException("A copy is taken inside one model: both nodes must belong to it.");
        }

        using var transaction = BeginTransaction();

        var copy = CopyFresh(source, parent, slot, index);

        MarkMutated();

        transaction.Complete();

        return copy;
    }

    /// <summary>Copies one node with a new identity into its place, then everything below it.</summary>
    /// <remarks>
    /// Attached before it is filled, for the reason <see cref="CopySubtree"/> gives: a node that is reachable from
    /// the roots at every step is one a rollback can always find.
    /// </remarks>
    private Node CopyFresh(Node source, Node parent, string slot, int index)
    {
        var copy = CreateDetached(source.Concept);

        parent.InsertChild(slot, index, copy);

        foreach (var declaration in source.Concept.Slots)
        {
            switch (declaration)
            {
                case PropertySlot property when source.GetOrNull(property.Name) is { } value:
                    copy.Set(property.Name, value);

                    break;

                case ReferenceSlot reference when source.GetReference(reference.Name) is { } target:
                    copy.SetReference(reference.Name, target.Target, target.ResolveInfo);

                    break;

                case ChildSlot child:
                    var position = 0;

                    foreach (var sourceChild in source.GetChildren(child.Name))
                    {
                        CopyFresh(sourceChild, copy, child.Name, position++);
                    }

                    break;
            }
        }

        return copy;
    }

    public override string ToString() => Name ?? Id.ToString();

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="copy"/>, hanging the new node into its place
    /// before filling it.
    /// </summary>
    /// <remarks>
    /// The order matters for the failure path: a node that is attached first and filled afterwards is always
    /// reachable from the copy's roots, so <see cref="Abandon"/> can delete everything that was built. A node
    /// left detached for a moment - a deep copy that builds bottom-up would do that - would be registered in
    /// the target project with nothing pointing at it.
    /// </remarks>
    private static void CopySubtree(Node source, Model copy, Node? parent, string? slot)
    {
        var node = copy.CreateDetached(source.Id, source.Concept);

        if (parent is null)
        {
            copy.AddRoot(node);
        }
        else
        {
            parent.AddChild(slot!, node);
        }

        foreach (var declaration in source.Concept.Slots)
        {
            switch (declaration)
            {
                case PropertySlot property when source.GetOrNull(property.Name) is { } value:
                    node.Set(property.Name, value);

                    break;

                case ReferenceSlot reference when source.GetReference(reference.Name) is { } target:
                    node.SetReference(reference.Name, target.Target, target.ResolveInfo);

                    break;

                case ChildSlot child:
                    foreach (var sourceChild in source.GetChildren(child.Name))
                    {
                        CopySubtree(sourceChild, copy, node, child.Name);
                    }

                    break;
            }
        }
    }

    /// <summary>Removes what a failed copy built and takes the model out of the target project.</summary>
    private static void Abandon(Model copy, Project target)
    {
        foreach (var root in copy.Roots)
        {
            copy.DeleteSubtree(root, record: false);
        }

        target.DiscardModel(copy);
    }

    /// <summary>One node, then everything below it, depth first.</summary>
    /// <remarks>
    /// <see cref="Node.ChildrenInDocumentOrder"/> answers with the direct children only, so the descent is
    /// explicit.
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

    /// <summary>Whether <paramref name="order"/> names each child of <paramref name="current"/> exactly once.</summary>
    private static bool IsPermutation(IReadOnlyList<Node> current, IReadOnlyList<Node> order)
    {
        if (current.Count != order.Count)
        {
            return false;
        }

        var remaining = new List<Node>(current);

        foreach (var child in order)
        {
            var index = remaining.IndexOf(child);

            if (index < 0)
            {
                return false;
            }

            remaining.RemoveAt(index);
        }

        return true;
    }

    /// <summary>Whether the slot already holds its children in <paramref name="order"/>.</summary>
    private static bool SameOrder(Node parent, string slot, IReadOnlyList<Node> order)
    {
        var current = parent.GetChildren(slot);

        if (current.Count != order.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!ReferenceEquals(current[index], order[index]))
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// Deletes a node and its subtree from the project and clears the references that pointed at
    /// them, so that a deletion never leaves a dangling target behind.
    /// </summary>
    /// <param name="node">The subtree to take out.</param>
    /// <param name="record">
    /// Whether to report the deletion. Redoing one repeats the removal without reporting it, because
    /// the undo stack publishes the change set for a restoration itself.
    /// </param>
    /// <returns>What it would take to put the subtree back.</returns>
    internal SubtreeSnapshot DeleteSubtree(Node node, bool record = true)
    {
        ArgumentNullException.ThrowIfNull(node);

        var snapshot = SubtreeSnapshot.Capture(node);
        node.UnlinkFromPlace();

        var ids = new HashSet<NodeId>(snapshot.Nodes.Count);
        foreach (var each in snapshot.Nodes)
        {
            ids.Add(each.Id);
            Project.Unregister(each);
            RemoveRootIfPresent(each);
        }

        if (record)
        {
            RecordRemoval(snapshot, ids);
        }
        else
        {
            foreach (var survivor in Project.Nodes)
            {
                survivor.ClearReferencesTo(ids);
            }
        }

        return snapshot;
    }

    /// <summary>Puts <paramref name="node"/> at <paramref name="index"/> among the model roots.</summary>
    internal void InsertRootAt(int index, Node node)
    {
        RequireApplicable(node, parent: null, slot: null);

        index = Math.Min(index, _roots.Count);
        _roots.Insert(index, node);
        node.Parent = null;
        node.ParentSlot = null;
        Node.ReindexFrom(_roots, index);
    }

    /// <summary>
    /// Refuses a placement the concept's <see cref="Applicability"/> does not allow.
    /// </summary>
    /// <param name="node">The node about to be placed.</param>
    /// <param name="parent">The node that would hold it, or <see langword="null"/> for a root.</param>
    /// <param name="slot">That node's child slot, or <see langword="null"/> for a root.</param>
    /// <exception cref="ModelInvariantException">The concept may not stand there.</exception>
    /// <remarks>
    /// Called by the three primitives every placement goes through, so it holds for a new node, a move, a wrap, a
    /// load and a replay alike. That is what makes "a concept is not valid here" a property of the model rather
    /// than a rule each caller has to remember - and it is why the editor's completion and insertion filters are
    /// a courtesy, not the guarantee.
    /// </remarks>
    internal static void RequireApplicable(Node node, Node? parent, string? slot)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Concept.Applicability.Allows(parent?.Concept, slot))
        {
            return;
        }

        throw new ModelInvariantException(
            parent is null
                ? $"Concept '{node.Concept.Key}' may not be a model root; it may stand in {node.Concept.Applicability}."
                : $"Concept '{node.Concept.Key}' may not stand in slot '{slot}' of concept '{parent.Concept.Key}'; it may stand in {node.Concept.Applicability}.");
    }

    internal void MarkMutated() => Version++;

    /// <summary>Opens the implicit transaction a single public edit runs in.</summary>
    internal MutationScope BeginMutation() => MutationScope.Begin(this);

    /// <summary>
    /// Registers a node that is not in the tree yet and remembers to unregister it again, so that a
    /// placement which never happens leaves no orphan in the project index.
    /// </summary>
    internal Node CreateDetached(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        var node = CreateDetached(NodeId.New(), concept);

        if (IsRecording)
        {
            _journal.Add(change: null, undo: () => Project.Unregister(node));
        }

        return node;
    }

    /// <summary>
    /// Creates a node whose identity comes from outside, which is what loading a stored model
    /// needs: the id in the file is the id in the project, so that references and caret positions
    /// in that file keep pointing where they did.
    /// </summary>
    /// <remarks>
    /// Loading is the only caller, which is why nothing is journaled here: a model that is being
    /// built has no subscribers yet, and a half-built one is taken out of the project again rather
    /// than undone.
    /// </remarks>
    internal Node CreateDetached(NodeId id, Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (id.IsEmpty)
        {
            throw new ArgumentException("A node id must not be empty.", nameof(id));
        }

        RequireKnownConcept(concept);

        var node = new Node(id, concept, this);
        Project.Register(node);
        return node;
    }

    /// <summary>Journals a step so that it reaches subscribers and can be undone and redone.</summary>
    internal void Record(IModelChange change, Action undo, Action? redo = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(undo);

        if (IsRecording)
        {
            _journal.Add(change, undo, redo);
        }
    }

    internal void RecordAdded(Node node, Node? parent, string? role, int index)
    {
        if (!IsRecording)
        {
            return;
        }

        _journal.Add(
            new NodeAdded(node.Id, parent?.Id, role, index),
            node.MakeUnlinkRollback(),
            () => AttachPlacement(node, parent, role, index));
    }

    /// <summary>
    /// Puts a node back at a known place, registering it again if a step took it out of the project
    /// index, and reports nothing — this is the redo of a placement, and the undo stack publishes
    /// the change set for it.
    /// </summary>
    internal void AttachPlacement(Node node, Node? parent, string? role, int index)
    {
        if (Project.FindNode(node.Id) is null)
        {
            Project.Register(node);
        }

        if (parent is null)
        {
            InsertRootAt(index, node);
        }
        else
        {
            parent.InsertChildAt(role!, index, node);
        }
    }

    /// <summary>Publishes a change set that was not produced by the journal, as undo and redo do.</summary>
    internal void PublishRestored(IReadOnlyList<IModelChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count > 0 && _changed is { } handler)
        {
            handler(this, new ModelChangedEventArgs(Id, Version, changes, []));
        }
    }

    internal void BeginImplicitTransaction() => _depth++;

    /// <summary>
    /// Ends one level of nesting. The outermost commit publishes; a rollback undoes the journal
    /// back to the mark and puts the version counter back, because a rolled-back edit is not a
    /// version the rest of the system should ever have seen.
    /// </summary>
    internal void EndTransaction(int mark, long versionBefore, bool commit)
    {
        _depth--;

        if (!commit)
        {
            _journal.RollbackTo(mark);
            Version = versionBefore;
            return;
        }

        if (_depth > 0)
        {
            return;
        }

        var changes = _journal.ChangesFrom(mark);
        var steps = _journal.StepsFrom(mark);
        _journal.Clear();

        if (changes.Count > 0 && _changed is { } handler)
        {
            handler(this, new ModelChangedEventArgs(Id, Version, changes, steps));
        }
    }

    internal void RemoveRootIfPresent(Node node)
    {
        var index = _roots.IndexOf(node);
        if (index < 0)
        {
            return;
        }

        _roots.RemoveAt(index);
        Node.ReindexFrom(_roots, index);
        node.IndexInParent = -1;
    }

    private void RecordRemoval(SubtreeSnapshot snapshot, HashSet<NodeId> deleted)
    {
        if (!IsRecording)
        {
            foreach (var survivor in Project.Nodes)
            {
                survivor.ClearReferencesTo(deleted);
            }

            return;
        }

        Record(
            new NodeRemoved(snapshot.Root.Id, snapshot.Parent?.Id, snapshot.Slot, snapshot.Index),
            snapshot.Restore,
            () => DeleteSubtree(snapshot.Root, record: false));

        foreach (var survivor in Project.Nodes)
        {
            if (survivor.ClearReferencesTo(deleted) is not { } cleared)
            {
                continue;
            }

            foreach (var (slot, reference) in cleared)
            {
                Record(
                    new ReferenceChanged(survivor.Id, slot, reference.Target, null, WasResolved: true),
                    () => survivor.RestoreReference(slot, reference),
                    () => survivor.UnsetReference(slot));
            }
        }
    }

    private void RequireKnownConcept(Concept concept)
    {
        if (!Project.Concepts.TryGet(concept.Key, out var known) || !ReferenceEquals(known, concept))
        {
            throw new ModelInvariantException(
                $"Concept '{concept.Key}' is not part of the language of this project.");
        }

        if (concept.IsAbstract)
        {
            throw new ModelInvariantException(
                $"Concept '{concept.Key}' is abstract, so no node can be an instance of it.");
        }
    }
}
