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

    public override string ToString() => Name ?? Id.ToString();

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
    }
}
