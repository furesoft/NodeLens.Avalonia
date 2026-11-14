namespace Projectional.Core;

/// <summary>
/// A reference slot was written, cleared or re-bound.
/// </summary>
/// <param name="WasResolved">
/// Whether <paramref name="Old"/> still pointed at a live node when it was replaced. A reference
/// that was already dangling is a different case for the subscriber that keeps reference counts,
/// because there is nothing to release.
/// </param>
public sealed record ReferenceChanged(
    NodeId Target,
    string Name,
    NodeId? Old,
    NodeId? New,
    bool WasResolved) : IModelChange
{
    /// <inheritdoc />
    /// <remarks>
    /// The inverse treats the value it puts back as live whenever there is one. A reference that a
    /// deletion cleared comes back together with the subtree that held it, and that deletion reports
    /// its own reference changes, so a wrong guess here would be corrected by a sibling record
    /// rather than by silence.
    /// </remarks>
    public IModelChange Invert() => this with { Old = New, New = Old, WasResolved = New is not null };
}
