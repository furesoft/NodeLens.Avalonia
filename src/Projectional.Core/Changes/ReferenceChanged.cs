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
    bool WasResolved) : IModelChange;
