using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Workbench;

/// <summary>The place a list row selects, answered once for every panel that has rows.</summary>
/// <remarks>
/// <para>
/// A row is a snapshot: the findings and usages are derived from the model at one moment, and the list outlives that
/// moment until the next derivation. So a row can name a node the user has since deleted, and one that is still there
/// can name a cell the current notation does not draw - the address is a slot, and a variant may hide it. Selecting
/// either is a no-op that looks like a crash: the caret does not move, nothing is said, and the user clicks again.
/// </para>
/// <para>
/// The answer is in three parts, and it is the same three for the problems panel and the usages panel because it is a
/// fact about an address rather than about a kind of row: a node that is gone has no place, a place the tree draws is
/// the place, and a place that is not drawn falls back to the node it belongs to. Only the caller knows what to say,
/// which is why a gone node comes back as <see langword="null"/> rather than as a sentence.
/// </para>
/// </remarks>
internal static class PlaceResolver
{
    /// <summary>The place to select for <paramref name="place"/>, or <see langword="null"/> when its node is gone.</summary>
    /// <param name="session">The session the row belongs to, which holds both the model and the tree.</param>
    /// <param name="place">The address a row carries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    internal static CellAddress? Resolve(EditorSession session, CellAddress place)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Document.Model.Project.FindNode(place.Owner) is not { } owner)
        {
            return null;
        }

        return session.Tree.Find(place) is not null ? place : new CellAddress(owner.Id);
    }
}
