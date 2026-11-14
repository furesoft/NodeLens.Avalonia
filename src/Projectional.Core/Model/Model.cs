namespace Projectional.Core;

/// <summary>
/// One document: an ordered set of root nodes written in the project's language. A model is the
/// unit that is saved to a file, opened in an editor tab and put under version control.
/// </summary>
/// <remarks>
/// <see cref="Version"/> counts mutations so that the layers above — projections, snapshots,
/// dirty tracking — can tell "nothing changed" from "changed and changed back" without
/// comparing trees.
/// </remarks>
public sealed class Model
{
    private readonly List<Node> _roots = [];

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

    public ModelId Id { get; }

    public string? Name { get; private set; }

    public Project Project { get; }

    public ConceptRegistry Concepts => Project.Concepts;

    public IReadOnlyList<Node> Roots => field ??= _roots.AsReadOnly();

    /// <summary>Increases with every mutation of this model.</summary>
    public long Version { get; private set; }

    public void Rename(string? name) => Name = name;

    /// <summary>
    /// Creates a node of the given concept and makes it a root. Roots are the only place a node
    /// can be created: a node that belonged to nothing would break the one-parent-or-root rule.
    /// </summary>
    public Node NewNode(string conceptKey) => NewNode(Project.Concepts.Get(conceptKey));

    public Node NewNode(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (!Project.Concepts.TryGet(concept.Key, out var known) || !ReferenceEquals(known, concept))
        {
            throw new ModelInvariantException(
                $"Concept '{concept.Key}' is not part of the language of this project.");
        }

        var node = new Node(NodeId.New(), concept, this);
        Project.Register(node);
        _roots.Add(node);
        node.IndexInParent = _roots.Count - 1;
        MarkMutated();
        return node;
    }

    /// <summary>Creates a node and immediately hangs it into <paramref name="parent"/>.</summary>
    public Node NewChild(Node parent, string slot, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var node = NewNode(conceptKey);
        try
        {
            parent.AddChild(slot, node);
        }
        catch
        {
            DeleteSubtree(node);
            throw;
        }

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

        node.Detach();
        _roots.Add(node);
        node.IndexInParent = _roots.Count - 1;
        MarkMutated();
    }

    public override string ToString() => Name ?? Id.ToString();

    internal void RemoveRootInternal(Node node)
    {
        var index = _roots.IndexOf(node);
        if (index < 0)
        {
            throw new ModelInvariantException($"Node {node.Id} is not a root of model {Id}.");
        }

        _roots.RemoveAt(index);
        Node.ReindexFrom(_roots, index);
    }

    /// <summary>
    /// Deletes a node and its subtree from the project and clears the references that pointed at
    /// them, so that a deletion never leaves a dangling target behind.
    /// </summary>
    internal void DeleteSubtree(Node node)
    {
        var deleted = node.SelfAndDescendants().ToList();
        var ids = new HashSet<NodeId>(deleted.Count);
        foreach (var each in deleted)
        {
            ids.Add(each.Id);
            Project.Unregister(each);
            RemoveRootIfPresent(each);
        }

        foreach (var survivor in Project.Nodes)
        {
            survivor.ClearReferencesTo(ids);
        }
    }

    internal void MarkMutated() => Version++;

    private void RemoveRootIfPresent(Node node)
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
}
