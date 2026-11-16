namespace Projectional.Editing;

/// <summary>How much an offer can take away when it is applied.</summary>
/// <remarks>
/// <para>
/// The classification belongs to the <em>offer</em> rather than to the fix, because a second catalogue needs
/// the same answer: an intention that deletes a node and a repair that clears a reference are the same
/// hazard, and one enum for both keeps a surface from having to decide twice. It therefore lives beside
/// <see cref="EditAction"/> at the root of this layer rather than in the quick-fix folder it was
/// introduced with.
/// </para>
/// <para>
/// <b>Nothing in this layer reads it as a rule any more.</b> It used to decide whether the offers gesture
/// might apply a lone offer without showing it; the gesture now always shows the list, so there is nothing
/// left for it to gate. It stays because it is true and a surface may want it - a host that draws the list can
/// mark a destructive row, and one that binds a single offer to a key of its own should ask first. Do not read
/// that as an invitation to reintroduce the shortcut: the classification is advice, not permission.
/// </para>
/// <para>
/// <see cref="Plain"/> is the default, which is the safe direction for a new kind of offer: a category that
/// forgot to classify itself must not be the one a host assumes is harmless.
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
