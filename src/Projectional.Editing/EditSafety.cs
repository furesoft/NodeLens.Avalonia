namespace Projectional.Editing;

/// <summary>How much an offer can take away if it runs without being asked about first.</summary>
/// <remarks>
/// <para>
/// The classification belongs to the <em>offer</em> rather than to the fix, because a second catalogue needs
/// the same answer: an intention that deletes a node and a repair that clears a reference are the same
/// hazard, and one enum for both keeps a surface from having to decide twice. It is one of the two things the
/// gesture reads before it runs an offer unasked, so the rule cannot be written once for fixes and once for
/// intentions and then drift apart. It therefore lives beside <see cref="EditAction"/> at the root of this
/// layer rather than in the quick-fix folder it was introduced with.
/// </para>
/// <para>
/// <see cref="Plain"/> is the default, which is the safe direction for a new kind of offer: a category that
/// forgot to classify itself runs only from the list, where the user picked it, instead of from the single
/// keystroke that was about to do something else.
/// </para>
/// </remarks>
public enum EditSafety
{
    /// <summary>Applying the offer adds or rewrites something; nothing the user already had is lost.</summary>
    Plain,

    /// <summary>
    /// Applying the offer removes content - a node, its subtree, a reference - so it may not run from a
    /// gesture that only asked what is available.
    /// </summary>
    Destructive,
}
