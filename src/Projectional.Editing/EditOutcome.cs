namespace Projectional.Editing;

/// <summary>
/// What applying an edit action did to the model.
/// </summary>
/// <remarks>
/// The three cases are worth separating because a caller reacts differently to each: a change has to be
/// repainted and is worth an undo step, no change is invisible, and a refusal is something the host may
/// want to show - a character that is not part of a number - or to pass on to a key binding that knows
/// better. Collapsing them into a boolean would push that distinction into every caller.
/// </remarks>
public enum EditOutcome
{
    /// <summary>The model changed.</summary>
    Applied,

    /// <summary>There was nothing to change, so the model is as it was.</summary>
    Unchanged,

    /// <summary>The edit was refused because the result would not be valid for the model.</summary>
    Rejected,
}
