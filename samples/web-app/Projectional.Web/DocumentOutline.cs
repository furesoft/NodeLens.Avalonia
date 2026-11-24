using Projectional.Core;

namespace Projectional.Web;

/// <summary>The active document's structure as rows, which is what a model browser shows.</summary>
/// <remarks>
/// <para>
/// It walks the model rather than the projection, which is the opposite of what the painted page shows and for the
/// opposite reason: a browser is about what the document <em>is</em>. A concept nobody drew a notation for is still
/// part of the model, and hiding it here would make the browser disagree with the tree it describes.
/// </para>
/// <para>
/// The order is document order and the depth is counted while walking rather than stored, because depth is a property
/// of a place and a node can be moved. A collapsed node's children are not in the list at all, so a rebuilt list can
/// never disagree with the tree the user sees.
/// </para>
/// </remarks>
public static class DocumentOutline
{
    /// <summary>Every node of <paramref name="model"/> that is not under a node drawn shut.</summary>
    /// <param name="model">The model to describe.</param>
    /// <param name="collapsed">The nodes drawn shut.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> or <paramref name="collapsed"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<OutlineRow> Of(Model model, IReadOnlySet<NodeId> collapsed)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(collapsed);

        List<OutlineRow> rows = [];

        foreach (var root in model.Roots)
        {
            Add(root, depth: 0, collapsed, rows);
        }

        return rows;
    }

    /// <summary>Every node with children, which is what "collapse all" shuts.</summary>
    /// <param name="model">The model to walk.</param>
    /// <remarks>
    /// Asked of the model rather than of the rows, because the rows of a tree that is already collapsed do not name
    /// the nodes below them - a command that shut only what it could see would leave the rest open.
    /// </remarks>
    public static HashSet<NodeId> NodesWithChildren(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        HashSet<NodeId> nodes = [];

        foreach (var root in model.Roots)
        {
            Collect(root, nodes);
        }

        return nodes;
    }

    /// <summary>The nodes above <paramref name="id"/>, nearest first, without the node itself.</summary>
    /// <param name="model">The model the node belongs to.</param>
    /// <param name="id">The node whose path is wanted.</param>
    public static IReadOnlyList<NodeId> AncestorsOf(Model model, NodeId id)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<NodeId> ancestors = [];

        for (var node = model.Project.FindNode(id)?.Parent; node is not null; node = node.Parent)
        {
            ancestors.Add(node.Id);
        }

        return ancestors;
    }

    private static void Collect(Node node, HashSet<NodeId> nodes)
    {
        var children = node.ChildrenInDocumentOrder().ToArray();

        if (children.Length == 0)
        {
            return;
        }

        nodes.Add(node.Id);

        foreach (var child in children)
        {
            Collect(child, nodes);
        }
    }

    private static void Add(Node node, int depth, IReadOnlySet<NodeId> collapsed, List<OutlineRow> rows)
    {
        var children = node.ChildrenInDocumentOrder().ToArray();
        var expanded = children.Length == 0 || !collapsed.Contains(node.Id);

        rows.Add(new OutlineRow(node.Id, depth, TextOf(node), node.Concept.Key, children.Length > 0, expanded));

        if (!expanded)
        {
            return;
        }

        foreach (var child in children)
        {
            Add(child, depth + 1, collapsed, rows);
        }
    }

    /// <summary>What a row says: the concept's own name, plus the node's name when it has one.</summary>
    private static string TextOf(Node node)
    {
        var name = ShortNameOf(node.Concept.Key);

        return node.Name is { Length: > 0 } nodeName ? $"{name} \"{nodeName}\"" : name;
    }

    /// <summary>A concept key without its namespace, which is the name a reader knows it by.</summary>
    private static string ShortNameOf(string conceptKey)
    {
        var dot = conceptKey.LastIndexOf('.');

        return dot >= 0 && dot < conceptKey.Length - 1 ? conceptKey[(dot + 1)..] : conceptKey;
    }
}
