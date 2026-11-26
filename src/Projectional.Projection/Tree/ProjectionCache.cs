using Projectional.Core;
using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// The projections an <see cref="EditorTreeBuilder"/> may hand to a later build instead of building them again.
/// </summary>
/// <remarks>
/// <para>
/// A projection is reusable when the change that triggered the rebuild did not name its node or one of its
/// ancestors, when its address has not moved, and when nothing in its subtree shows a value the node does not own
/// — a reference to another node or a type annotation. Those two are the reason a projection can depend on a part
/// of the model outside its own subtree, so a subtree that shows one is never stored and its ancestors are rebuilt
/// instead. That is the honest trade: a document with neither gets the whole win, and a document with them gets the
/// win wherever the dependency does not reach.
/// </para>
/// <para>
/// Reuse moves a cell out of the tree it was built for: the cell is detached from its parent and the new tree claims
/// it. That is sound only because the tree it came from is discarded in the same rebuild, which is why the cache
/// belongs to a builder that one session owns.
/// </para>
/// </remarks>
internal sealed class ProjectionCache
{
    private readonly Dictionary<NodeId, Entry> _entries = [];

    /// <summary>
    /// Takes the projection stored for <paramref name="node"/> when it may still stand, and detaches it from the
    /// tree it was built for so the new one can claim it.
    /// </summary>
    /// <param name="node">The node whose projection is wanted.</param>
    /// <param name="address">The address the projection would have in this build.</param>
    /// <param name="emphasis">The emphasis the projection would be drawn with in this build.</param>
    /// <param name="projection">The stored projection, when one is reusable.</param>
    /// <returns><see langword="true"/> when the stored projection was handed back.</returns>
    internal bool TryReuse(NodeId node, CellAddress address, TextEmphasis emphasis, out EditorCell projection)
    {
        projection = null!;

        if (!_entries.TryGetValue(node, out var entry) ||
            entry.Address != address ||
            entry.Emphasis != emphasis)
        {
            return false;
        }

        projection = entry.Projection;
        projection.Parent = null;

        return true;
    }

    /// <summary>Keeps a projection for a later build, replacing whatever was stored for the node.</summary>
    internal void Store(NodeId node, CellAddress address, TextEmphasis emphasis, EditorCell projection) =>
        _entries[node] = new Entry(address, emphasis, projection);

    /// <summary>
    /// Forgets the projection stored for <paramref name="node"/>, because rebuilding it now shows a value the
    /// node does not own and a projection kept from before would go stale.
    /// </summary>
    internal void Forget(NodeId node) => _entries.Remove(node);

    /// <summary>Forgets every projection, so the next build starts from notation.</summary>
    internal void Clear() => _entries.Clear();

    private readonly record struct Entry(CellAddress Address, TextEmphasis Emphasis, EditorCell Projection);
}
