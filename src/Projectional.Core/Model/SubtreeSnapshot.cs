namespace Projectional.Core;

/// <summary>
/// Everything a deleted subtree needs in order to come back exactly as it was.
/// </summary>
/// <remarks>
/// Deletion reaches into four places at once — the project index, the model roots, the parent's
/// child list and the tree below — so undoing it from the public change records alone is impossible:
/// they describe a <see cref="NodeRemoved"/> as one node leaving one slot, not as a whole tree being
/// unregistered. Capturing the state at the moment of deletion keeps the undo honest and the public
/// record small.
/// </remarks>
internal sealed class SubtreeSnapshot
{
    private SubtreeSnapshot(Node root, Node? parent, string? slot, int index, IReadOnlyList<Node> nodes)
    {
        Root = root;
        Parent = parent;
        Slot = slot;
        Index = index;
        Nodes = nodes;
    }

    /// <summary>The node that was deleted; the top of the subtree.</summary>
    public Node Root { get; }

    /// <summary>The node it hung under, or <see langword="null"/> if it was a model root.</summary>
    public Node? Parent { get; }

    /// <summary>The child slot it hung in, or <see langword="null"/> if it was a model root.</summary>
    public string? Slot { get; }

    /// <summary>Its position among its siblings, or among the model roots.</summary>
    public int Index { get; }

    /// <summary>Every deleted node, in document order.</summary>
    public IReadOnlyList<Node> Nodes { get; }

    /// <summary>Captures the placement of <paramref name="root"/> before the caller unlinks it.</summary>
    public static SubtreeSnapshot Capture(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new SubtreeSnapshot(root, root.Parent, root.ParentSlot, root.IndexInParent, [.. root.SelfAndDescendants()]);
    }

    /// <summary>Puts the subtree back: registered, re-linked and at its old index.</summary>
    public void Restore()
    {
        foreach (var node in Nodes)
        {
            node.Model.Project.Register(node);
        }

        if (Parent is { } parent)
        {
            parent.InsertChildAt(Slot!, Index, Root);
            return;
        }

        Root.Model.InsertRootAt(Index, Root);
    }
}
