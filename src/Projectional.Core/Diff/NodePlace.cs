namespace Projectional.Core;

/// <summary>Where a node stands: the parent that holds it, the slot in that parent, and its position there.</summary>
/// <remarks>
/// A place rather than a node, because "the same node, somewhere else" is exactly what a move is - and a
/// difference that reported only the node id could not tell a move from an unchanged node.
/// </remarks>
/// <param name="Parent">The node that holds it, or <see langword="null"/> when it is a root.</param>
/// <param name="Slot">The child slot of <paramref name="Parent"/>, or <see langword="null"/> for a root.</param>
/// <param name="Index">Its position among that slot's children, or among the roots.</param>
public readonly record struct NodePlace(NodeId? Parent, string? Slot, int Index)
{
    /// <summary>Where <paramref name="node"/> stands.</summary>
    /// <param name="node">The node to locate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static NodePlace Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new NodePlace(node.Parent?.Id, node.ParentSlot, node.IndexInParent);
    }

    /// <summary>Whether the place is a position among the model's roots.</summary>
    public bool IsRoot => Parent is null;

    public override string ToString() =>
        Parent is { } parent ? $"{parent}/{Slot}[{Index}]" : $"root[{Index}]";
}
