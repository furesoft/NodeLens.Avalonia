using Projectional.Core;

namespace Projectional.Intentions;

/// <summary>One concept that can surround a node or a run of siblings, and the slot that would receive it.</summary>
/// <remarks>
/// The slot is part of the candidate rather than something the caller looks up again: a concept may declare more
/// than one slot of the right shape, and which one is chosen is a decision of the search (the first in declaration
/// order) that must not be repeated differently at apply time.
/// </remarks>
/// <param name="Wrapper">The concept the wrapper node would be.</param>
/// <param name="Slot">Its child slot that accepts what is wrapped - single-valued for a node, many-valued for a run.</param>
internal readonly record struct WrapperCandidate(Concept Wrapper, ChildSlot Slot);
