namespace Projectional.Core;

/// <summary>
/// A node became reachable at a place: it was created as a model root, or inserted into a child
/// slot, or moved there.
/// </summary>
/// <param name="Target">The node that appeared or arrived.</param>
/// <param name="Parent">The node it was inserted into, or <see langword="null"/> for a model root.</param>
/// <param name="Role">The child slot it was inserted into, or <see langword="null"/> for a model root.</param>
/// <param name="Index">Its position among its siblings, or among the model roots.</param>
/// <remarks>A move is a <see cref="NodeRemoved"/> followed by a <see cref="NodeAdded"/>.</remarks>
public sealed record NodeAdded(NodeId Target, NodeId? Parent, string? Role, int Index) : IModelChange
{
    /// <inheritdoc />
    public IModelChange Invert() => new NodeRemoved(Target, Parent, Role, Index);
}
