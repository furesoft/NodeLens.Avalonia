namespace Projectional.Projection;

/// <summary>The three states a reference can be in, which differ in what the cell shows and what the
/// user can do next.</summary>
/// <remarks>
/// Unresolved is deliberately its own state rather than "unset with a stale name": an unset reference
/// waits for a target, an unresolved one already has one and has to be repaired, and offering the
/// same actions for both would make a dangling reference look like an empty slot.
/// </remarks>
public enum ReferenceState
{
    /// <summary>The slot holds nothing.</summary>
    Unset,

    /// <summary>The slot holds a target id that is not in the project.</summary>
    Unresolved,

    /// <summary>The slot holds a target id that resolves.</summary>
    Resolved,
}
