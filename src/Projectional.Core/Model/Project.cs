namespace Projectional.Core;

/// <summary>
/// Everything the editor keeps in memory for one open piece of work: the language it is written
/// in, the models that belong to it, and the live index of every node by id.
/// </summary>
/// <remarks>
/// The index is what makes identity cheap to trust. It is built as nodes appear and cleared as
/// they are deleted, so "an id is unique per project" is not a rule that has to be checked — it
/// is a property of the data structure.
/// </remarks>
public sealed class Project
{
    private readonly Dictionary<NodeId, Node> _nodes = [];
    private readonly List<Model> _models = [];

    public Project(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        Concepts = concepts;
    }

    public ConceptRegistry Concepts { get; }

    /// <summary>Models in the order they were opened, which is also their order on disk.</summary>
    public IReadOnlyList<Model> Models => field ??= _models.AsReadOnly();

    /// <summary>
    /// Every live node. Enumerating this is how a rule that has to look at the whole project —
    /// such as clearing the references of a deleted subtree — stays correct without a second index.
    /// </summary>
    public IEnumerable<Node> Nodes => _nodes.Values;

    public int NodeCount => _nodes.Count;

    public Model NewModel(string? name = null) => NewModel(ModelId.New(), name);

    public Node? FindNode(NodeId id) => _nodes.TryGetValue(id, out var node) ? node : null;

    internal Model NewModel(ModelId id, string? name)
    {
        var model = new Model(this, id, name);
        _models.Add(model);
        return model;
    }

    /// <summary>
    /// Removes a model that never became usable, as a load that failed halfway does. The nodes are
    /// expected to be gone already; this only takes the model out of the order on disk.
    /// </summary>
    internal void DiscardModel(Model model) => _models.Remove(model);

    internal void Register(Node node)
    {
        if (!_nodes.TryAdd(node.Id, node))
        {
            throw new ModelInvariantException($"Node id {node.Id} is already in use in this project.");
        }
    }

    internal void Unregister(Node node) => _nodes.Remove(node.Id);
}
