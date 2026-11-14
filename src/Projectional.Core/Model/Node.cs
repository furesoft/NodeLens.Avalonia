using System.Collections.ObjectModel;

namespace Projectional.Core;

/// <summary>
/// One node of a model: a concept instance plus its slot values. Property and reference values
/// are stored inline; children are stored as ordered lists per child slot.
/// </summary>
/// <remarks>
/// A node is always part of its model, either as a model root or inside exactly one child slot
/// of exactly one parent. Anything else — a node in two places, a node in no place, a node
/// shared between models — is rejected rather than repaired, because a projection could not
/// show such a node unambiguously.
/// </remarks>
public sealed class Node
{
    private readonly Dictionary<string, string?> _properties;
    private readonly Dictionary<string, List<Node>> _children;
    private readonly Dictionary<string, ReadOnlyCollection<Node>> _childViews;
    private readonly Dictionary<string, NodeReference> _references;

    internal Node(NodeId id, Concept concept, Model model)
    {
        Id = id;
        Concept = concept;
        Model = model;

        _properties = new Dictionary<string, string?>(StringComparer.Ordinal);
        _children = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
        _childViews = new Dictionary<string, ReadOnlyCollection<Node>>(StringComparer.Ordinal);
        _references = new Dictionary<string, NodeReference>(StringComparer.Ordinal);

        foreach (var slot in concept.Slots)
        {
            switch (slot)
            {
                case PropertySlot property:
                    _properties.Add(property.Name, property.DefaultValue);
                    break;

                case ChildSlot child:
                    var list = new List<Node>();
                    _children.Add(child.Name, list);
                    _childViews.Add(child.Name, list.AsReadOnly());
                    break;
            }
        }
    }

    public NodeId Id { get; }

    public Concept Concept { get; }

    public Model Model { get; }

    /// <summary>The owning node, or <see langword="null"/> for a model root.</summary>
    public Node? Parent { get; private set; }

    /// <summary>The child slot this node sits in, or <see langword="null"/> for a model root.</summary>
    public string? ParentSlot { get; private set; }

    /// <summary>
    /// Position among siblings, or in the model's roots for a root. <c>-1</c> only while a node
    /// is being moved, which is never observable from outside.
    /// </summary>
    public int IndexInParent { get; internal set; } = -1;

    public bool IsRoot => Parent is null && IndexInParent >= 0;

    /// <summary>The text a reference to this node displays, if its concept declares a name slot.</summary>
    public string? Name => Concept.NameSlot is { } slot ? GetOrNull(slot.Name) : null;

    /// <summary>Nodes of the child slot <paramref name="name"/>, in document order.</summary>
    /// <exception cref="KeyNotFoundException">The concept declares no such slot.</exception>
    public IReadOnlyList<Node> GetChildren(string name)
    {
        RequireChildSlot(name);
        return _childViews[name];
    }

    /// <summary>The single child of a slot declared with <see cref="Cardinality.One"/>.</summary>
    public Node? GetChild(string name)
    {
        RequireChildSlot(name);
        var children = _children[name];
        return children.Count == 0 ? null : children[0];
    }

    public Node AddChild(string name, Node child)
    {
        RequireChildSlot(name);
        return InsertChild(name, _children[name].Count, child);
    }

    public Node InsertChild(string name, int index, Node child)
    {
        var declaration = RequireChildSlot(name);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!ReferenceEquals(child.Model, Model))
        {
            throw new ModelInvariantException(
                $"Node {child.Id} belongs to a different model and cannot be inserted into node {Id}.");
        }

        if (!child.Concept.IsKindOf(declaration.ChildConceptKey))
        {
            throw new ModelInvariantException(
                $"Concept '{child.Concept.Key}' does not conform to slot '{name}', which holds '{declaration.ChildConceptKey}'.");
        }

        if (ReferenceEquals(child, this) || child.IsAncestorOf(this))
        {
            throw new ModelInvariantException($"Node {child.Id} cannot be inserted into its own subtree.");
        }

        var siblings = _children[name];
        if (declaration.Cardinality == Cardinality.One && siblings.Count > 0 && !ReferenceEquals(siblings[0], child))
        {
            throw new ModelInvariantException($"Slot '{name}' of concept '{Concept.Key}' holds at most one child.");
        }

        // Moving a node out of its current place first keeps the "one parent or root" rule true
        // at every intermediate step, and removes it from this very list when it was a sibling.
        child.Detach();
        siblings = _children[name];
        index = Math.Min(index, siblings.Count);

        siblings.Insert(index, child);
        child.Parent = this;
        child.ParentSlot = name;
        ReindexFrom(siblings, index);
        Model.MarkMutated();
        return child;
    }

    /// <summary>
    /// Fills or clears the single child of a slot declared with <see cref="Cardinality.One"/>.
    /// The replaced child is deleted with its subtree, because a child is owned.
    /// </summary>
    public void SetChild(string name, Node? child)
    {
        var declaration = RequireChildSlot(name);
        if (declaration.Cardinality != Cardinality.One)
        {
            throw new InvalidOperationException(
                $"Slot '{name}' of concept '{Concept.Key}' holds many children; use InsertChild or AddChild.");
        }

        var children = _children[name];
        if (ReferenceEquals(children.Count > 0 ? children[0] : null, child))
        {
            return;
        }

        if (children.Count > 0)
        {
            RemoveChild(name, children[0]);
        }

        if (child is not null)
        {
            AddChild(name, child);
        }
    }

    /// <summary>Unlinks a child and deletes it together with its subtree.</summary>
    public void RemoveChild(string name, Node child)
    {
        RequireChildSlot(name);
        ArgumentNullException.ThrowIfNull(child);

        var siblings = _children[name];
        var index = siblings.IndexOf(child);
        if (index < 0)
        {
            throw new ModelInvariantException($"Node {child.Id} is not a child in slot '{name}' of node {Id}.");
        }

        siblings.RemoveAt(index);
        ReindexFrom(siblings, index);
        child.Parent = null;
        child.ParentSlot = null;
        child.IndexInParent = -1;
        Model.DeleteSubtree(child);
        Model.MarkMutated();
    }

    /// <summary>The stored reference, or <see langword="null"/> while the slot is unset.</summary>
    public NodeReference? GetReference(string name)
    {
        RequireReferenceSlot(name);
        return _references.TryGetValue(name, out var reference) ? reference : null;
    }

    /// <summary>The resolved target of a reference slot, or <see langword="null"/> if unset or dangling.</summary>
    public Node? GetReferenceTarget(string name)
    {
        RequireReferenceSlot(name);
        return _references.TryGetValue(name, out var reference) ? Model.Project.FindNode(reference.Target) : null;
    }

    public void SetReference(string name, Node? target)
    {
        var declaration = RequireReferenceSlot(name);

        if (target is null)
        {
            if (_references.Remove(name))
            {
                Model.MarkMutated();
            }

            return;
        }

        if (!ReferenceEquals(target.Model, Model))
        {
            throw new ModelInvariantException(
                $"Node {target.Id} belongs to a different model and cannot be referenced from node {Id}.");
        }

        if (!target.Concept.IsKindOf(declaration.TargetConceptKey))
        {
            throw new ModelInvariantException(
                $"Concept '{target.Concept.Key}' does not conform to reference '{name}', which holds '{declaration.TargetConceptKey}'.");
        }

        if (!declaration.AllowOwnSubtree && target.IsSelfOrDescendantOf(this))
        {
            throw new ModelInvariantException(
                $"Reference '{name}' of node {Id} must not point into its own subtree.");
        }

        SetReference(name, target.Id, target.Name);
    }

    /// <summary>
    /// Writes a reference to an id that may not be in this project yet, which is what loading a
    /// file with a dangling reference needs.
    /// </summary>
    public void SetReference(string name, NodeId target, string? resolveInfo = null)
    {
        RequireReferenceSlot(name);
        if (target.IsEmpty)
        {
            throw new ArgumentException("A reference target must not be empty.", nameof(target));
        }

        var reference = new NodeReference(target, resolveInfo);
        if (_references.TryGetValue(name, out var existing) && existing.Equals(reference))
        {
            return;
        }

        _references[name] = reference;
        Model.MarkMutated();
    }

    /// <summary>Whether this node is a strict ancestor of <paramref name="node"/>.</summary>
    public bool IsAncestorOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether this node is <paramref name="node"/> or lies below it.</summary>
    public bool IsSelfOrDescendantOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ReferenceEquals(this, node) || node.IsAncestorOf(this);
    }

    /// <summary>
    /// Children in the order the persistence format writes them: slot declaration order, then
    /// the order inside the slot.
    /// </summary>
    public IEnumerable<Node> ChildrenInDocumentOrder()
    {
        foreach (var slot in Concept.Slots)
        {
            if (slot is not ChildSlot child)
            {
                continue;
            }

            foreach (var node in _children[child.Name])
            {
                yield return node;
            }
        }
    }

    public string? GetOrNull(string name)
    {
        RequireProperty(name);
        return _properties[name];
    }

    public bool IsSet(string name)
    {
        RequireProperty(name);
        return _properties[name] is not null;
    }

    public string Get(string name)
    {
        RequireProperty(name);
        return _properties[name]
            ?? throw new ModelInvariantException(
                $"Property '{name}' of node {Id} has no value yet, but the slot has no default either.");
    }

    public int GetInt(string name) => GetTyped<int>(name);

    public bool GetBool(string name) => GetTyped<bool>(name);

    public DateOnly GetDate(string name) => GetTyped<DateOnly>(name);

    public void Set(string name, string value)
    {
        var property = RequireProperty(name);
        ArgumentNullException.ThrowIfNull(value);
        SetCore(property, value);
    }

    public void Set(string name, int value) => SetFormatted(name, PropertyType.Integer, value);

    public void Set(string name, bool value) => SetFormatted(name, PropertyType.Boolean, value);

    public void Set(string name, DateOnly value) => SetFormatted(name, PropertyType.Date, value);

    public override string ToString() => $"{Concept.Key} {Id}";

    internal IEnumerable<Node> SelfAndDescendants()
    {
        yield return this;

        foreach (var child in ChildrenInDocumentOrder())
        {
            foreach (var node in child.SelfAndDescendants())
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Unlinks this node from its parent or from the model roots without deleting it. Version
    /// bookkeeping is left to the public operation that called it, so one move counts once.
    /// </summary>
    internal void Detach()
    {
        if (Parent is { } parent)
        {
            var name = ParentSlot!;
            var siblings = parent._children[name];
            var index = siblings.IndexOf(this);
            if (index < 0)
            {
                throw new ModelInvariantException($"Node {Id} claims to be a child of {parent.Id} but is not.");
            }

            siblings.RemoveAt(index);
            ReindexFrom(siblings, index);
            Parent = null;
            ParentSlot = null;
            IndexInParent = -1;
            return;
        }

        if (IsRoot)
        {
            Model.RemoveRootInternal(this);
            IndexInParent = -1;
        }
    }

    internal void ClearReferencesTo(HashSet<NodeId> deleted)
    {
        List<string>? dangling = null;

        foreach (var pair in _references)
        {
            if (deleted.Contains(pair.Value.Target))
            {
                (dangling ??= []).Add(pair.Key);
            }
        }

        if (dangling is null)
        {
            return;
        }

        foreach (var name in dangling)
        {
            _references.Remove(name);
        }
    }

    internal static void ReindexFrom(List<Node> siblings, int start)
    {
        for (var i = start; i < siblings.Count; i++)
        {
            siblings[i].IndexInParent = i;
        }
    }

    private void SetFormatted(string name, PropertyType expected, object value)
    {
        var property = RequireProperty(name);
        if (!property.Type.Equals(expected))
        {
            throw new ArgumentException(
                $"Slot '{name}' of concept '{Concept.Key}' is of type {property.Type.Name}, not {expected.Name}.",
                nameof(value));
        }

        SetCore(property, expected.Format(value));
    }

    private void SetCore(PropertySlot property, string value)
    {
        if (!property.Type.IsValid(value))
        {
            throw new ModelInvariantException(
                $"'{value}' is not a valid {property.Type.Name} value for slot '{property.Name}'.");
        }

        if (_properties[property.Name] == value)
        {
            return;
        }

        _properties[property.Name] = value;
        Model.MarkMutated();
    }

    private T GetTyped<T>(string name)
    {
        var property = RequireProperty(name);
        if (property.Type.TryParse(_properties[name], out var parsed) && parsed is T typed)
        {
            return typed;
        }

        throw new ModelInvariantException(
            $"Property '{name}' of node {Id} does not hold a {typeof(T).Name} value.");
    }

    private PropertySlot RequireProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Concept.GetSlot(name) as PropertySlot
            ?? throw new KeyNotFoundException(
                $"Concept '{Concept.Key}' declares no property slot named '{name}'.");
    }

    private ChildSlot RequireChildSlot(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Concept.GetSlot(name) as ChildSlot
            ?? throw new KeyNotFoundException(
                $"Concept '{Concept.Key}' declares no child slot named '{name}'.");
    }

    private ReferenceSlot RequireReferenceSlot(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Concept.GetSlot(name) as ReferenceSlot
            ?? throw new KeyNotFoundException(
                $"Concept '{Concept.Key}' declares no reference slot named '{name}'.");
    }
}
