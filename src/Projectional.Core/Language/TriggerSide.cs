namespace Projectional.Core;

/// <summary>
/// Which end of the node under the caret a <see cref="ConceptTrigger"/> grows from.
/// </summary>
/// <remarks>
/// One side per trigger rather than "either", because the side decides the slot the existing node lands in: an
/// operator typed to the right of an operand makes it the left operand, the same operator typed to its left makes
/// it the right one. A trigger that answered "either" would have to name two slots and would have to decide the
/// side itself, which is exactly the one thing the caret already knows.
/// </remarks>
public enum TriggerSide
{
    /// <summary>The caret is at the start of the node, so the new wrapper comes before it.</summary>
    Left,

    /// <summary>The caret is at the end of the node, so the new wrapper continues after it.</summary>
    Right,
}
