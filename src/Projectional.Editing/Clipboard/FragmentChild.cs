namespace Projectional.Editing;

/// <summary>One child of a copied node: the slot it stood in, and the copied subtree itself.</summary>
/// <remarks>
/// The slot is recorded per child rather than as a slot per list, because a node's children can be spread over
/// several slots and the order across them is the order a reader sees. Pasting appends each child to its own
/// slot, which puts every slot's contents back in the order they were copied in.
/// </remarks>
/// <param name="Slot">The child slot of the parent this child stood in.</param>
/// <param name="Node">The copied subtree.</param>
public readonly record struct FragmentChild(string Slot, FragmentNode Node);
