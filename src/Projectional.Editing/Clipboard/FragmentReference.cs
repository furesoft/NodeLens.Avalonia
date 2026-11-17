using Projectional.Core;

namespace Projectional.Editing;

/// <summary>One reference of a copied node: the slot it sits in, and what it points at.</summary>
/// <remarks>
/// Both halves of the stored reference travel - the target id and its <c>resolveInfo</c> - because a paste
/// has to keep what the reference meant even when the target is gone from the model it lands in. Dropping
/// either half would turn "points at a rule that no longer exists" into "points at nothing", which is
/// exactly the degradation the model was designed to avoid (ADR 0004).
/// </remarks>
/// <param name="Slot">The reference slot of the node the reference belongs to.</param>
/// <param name="Target">The id of the node it pointed at, in the model it was copied from.</param>
/// <param name="ResolveInfo">The name it was stored with, or <see langword="null"/>.</param>
public readonly record struct FragmentReference(string Slot, NodeId Target, string? ResolveInfo);
