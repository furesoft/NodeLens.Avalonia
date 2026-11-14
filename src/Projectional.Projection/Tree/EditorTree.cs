using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// The projection of one node: the cell tree a view renders, tied to the model version it was built
/// from.
/// </summary>
/// <remarks>
/// <para>
/// A tree is built, used and thrown away. It is not observed by the model and holds no subscription,
/// so the only safe assumption is that it describes the model as it was at build time; the layer that
/// caches trees is the layer that decides when to drop them.
/// </para>
/// <para>
/// There is one tree per projected node rather than one for the whole model, because that is the unit
/// the editor invalidates on: a change to a node rebuilds that node's tree and leaves the trees of
/// every other node in the model alone. The trees form a hierarchy through the child cells that
/// appear inside them, which is why <see cref="Root"/> of a child's own tree is the same cell object
/// as the child cell's content here.
/// </para>
/// </remarks>
public sealed class EditorTree
{
    internal EditorTree(Node source, EditorCell root)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new ProjectionException($"The root cell of the tree for {source} already has a parent.");
        }

        Source = source;
        Root = root;
    }

    /// <summary>The node this tree projects.</summary>
    public Node Source { get; }

    /// <summary>The outermost cell, which is the projection of the node's notation root.</summary>
    public EditorCell Root { get; }

    /// <summary>Every cell of the tree, in document order.</summary>
    public IEnumerable<EditorCell> Cells() => EditorTreeWalker.SelfAndDescendants(Root);

    /// <summary>
    /// The cell at <paramref name="address"/>, or <see langword="null"/> when the tree shows no such
    /// place.
    /// </summary>
    /// <remarks>
    /// The first match in document order wins, and that matters: layout containers carry the address
    /// of the node they arrange, so a node's address matches its outermost container before it matches
    /// the containers nested inside it. A caller that really wants every cell at an address has to
    /// walk the tree itself.
    /// </remarks>
    public EditorCell? Find(CellAddress address)
    {
        foreach (var cell in Cells())
        {
            if (cell.Address == address)
            {
                return cell;
            }
        }

        return null;
    }

    public override string ToString() => $"editor tree of {Source}";
}
