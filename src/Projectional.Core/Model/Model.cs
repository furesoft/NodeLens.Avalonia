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
    /// <para>
    /// Wrapping cannot be written as "create the wrapper, then move the node into it": a single-valued slot
    /// holds at most one child, so there is no moment at which both the node and its wrapper could be in
    /// the slot together. The node therefore leaves first and returns inside the wrapper, and all three
    /// steps run in one transaction so that the user sees one undo step and so that a failure cannot leave
    /// the node outside the document.
    /// </para>
    /// <para>
    /// The node keeps its id, which is what makes the operation safe to undo: every reference that points
    /// into its subtree and every caret address that names one of its cells still resolves afterwards.
    /// </para>
    /// <para>
    /// <paramref name="presets"/> exists because some wrappers only mean something with a value:
    /// "negate" is a <c>Unary</c> whose operator is <c>!</c>, and a caller cannot write that value
    /// afterwards, because the wrapper's id does not exist until it is created. Every preset is validated
    /// against the wrapper's declarations before anything is touched, so a wrong one fails without leaving
    /// the node detached.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="wrapperConceptKey"/> or <paramref name="wrapperSlot"/> is empty.
    /// </exception>
    /// <exception cref="ModelInvariantException">
    /// <paramref name="node"/> is not part of this model, has no place, cannot be wrapped by that concept
    /// in that slot, the wrapper declares no single-child slot of that name, or a preset names a property
    /// the wrapper does not declare or a value its type refuses.
    /// </exception>
    public Node Wrap(
        Node node,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        if (!ReferenceEquals(node.Model, this))
        {
            throw new ModelInvariantException(
                $"Node {node.Id} belongs to a different model and cannot be wrapped by this one's concepts.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            throw new ModelInvariantException(
                $"Node {node.Id} has no place, so there is nothing to wrap it in.");
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

        if (wrapper.FindChild(wrapperSlot) is not { Cardinality: Cardinality.One } inner)
        {
            throw new ModelInvariantException(
                $"Concept '{wrapperConceptKey}' declares no single-child slot named '{wrapperSlot}'.");
        }

        if (!node.Concept.IsKindOf(inner.ChildConceptKey))
        {
            throw new ModelInvariantException(
                $"Concept '{node.Concept.Key}' does not conform to slot '{wrapperSlot}' of concept '{wrapperConceptKey}'.");
        }

        // Validated before anything moves: a preset the wrapper refuses must not leave the node detached.
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

        var index = node.IndexInParent;

        using var transaction = BeginTransaction();

        node.Detach();

        var created = CreateDetached(wrapper);
        parent.InsertChild(slot, Math.Min(index, parent.GetChildren(slot).Count), created);
        created.InsertChild(wrapperSlot, 0, node);

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
        index = Math.Min(index, _roots.Count);
        _roots.Insert(index, node);
        node.Parent = null;
        node.ParentSlot = null;
        Node.ReindexFrom(_roots, index);
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
