namespace Projectional.Core;

/// <summary>One place where a node is pointed at: the node holding the reference, the slot, and the target.</summary>
/// <param name="Owner">The node whose reference slot is set.</param>
/// <param name="Slot">The name of that reference slot on the owner's concept.</param>
/// <param name="Target">The node the slot points at.</param>
/// <remarks>
/// A usage is a fact about the model, not about a page: a reference the notation hides is still a usage, and a rename
/// has to rewrite it. Where a reader can be sent to is derived from <see cref="Owner"/> and <see cref="Slot"/> by
/// whoever draws the document - a reference slot's cell is addressed by its owner and its slot name, so a usage and a
/// cell name the same place without either layer knowing the other.
/// </remarks>
public readonly record struct ReferenceUsage(NodeId Owner, string Slot, NodeId Target)
{
    public override string ToString() => $"{Owner}:{Slot} -> {Target}";
}
