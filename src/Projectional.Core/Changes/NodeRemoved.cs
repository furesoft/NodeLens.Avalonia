namespace Projectional.Core;

/// <summary>
/// A node left the place it was in — either because it was deleted with its subtree, or because it
/// moved to another slot.
/// </summary>
/// <param name="Target">The node that left.</param>
/// <param name="Parent">The node it was taken out of, or <see langword="null"/> for a model root.</param>
/// <param name="Role">The child slot it was taken out of, or <see langword="null"/> for a model root.</param>
/// <param name="Index">The position it had among its siblings, or among the model roots.</param>
/// <remarks>
/// Deleting a subtree is one change, not one change per descendant: nothing below the node can be
/// reached any more, so there is nothing incremental to gain from naming those nodes. A subscriber
/// that needs them walks the tree before the change is applied, which it has to do anyway.
/// </remarks>
public sealed record NodeRemoved(NodeId Target, NodeId? Parent, string? Role, int Index) : IModelChange
{
    /// <inheritdoc />
    public IModelChange Invert() => new NodeAdded(Target, Parent, Role, Index);
}
