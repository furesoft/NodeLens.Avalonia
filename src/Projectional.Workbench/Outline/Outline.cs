using Projectional.Core;

namespace Projectional.Workbench;

/// <summary>The active document's structure, as rows a browser can show.</summary>
/// <remarks>
/// <para>
/// It walks the model rather than the projection, which is the opposite of what the find bar does - and for the
/// opposite reason. A browser is about what the document <em>is</em>: a concept nobody drew a notation for is still
/// part of the model, and hiding it here would make the browser disagree with the tree it describes.
/// </para>
/// <para>
/// The order is document order, which is the same order every other list in this app uses, and the depth is counted
/// from the root rather than stored, because depth is a property of a place and a node can be moved.
/// </para>
/// <para>
/// <paramref name="collapsed"/> is the view state the rows are derived from - never mutated here. A node in it is drawn
/// shut and its children are not built at all, which is why a collapsed subtree is one row rather than a tree that is
/// merely hidden.
/// </para>
/// </remarks>
internal static class Outline
{
    /// <summary>The state of a browser that has never closed a node.</summary>
    private static readonly HashSet<NodeId> EmptyCollapsed = [];

    /// <summary>Every node of <paramref name="model"/>, root first, depth first.</summary>
    /// <param name="model">The model to describe.</param>
    internal static IReadOnlyList<OutlineEntry> Of(Model model) => Of(model, EmptyCollapsed);

    /// <summary>Every node of <paramref name="model"/> that is not under a node drawn shut.</summary>
    /// <param name="model">The model to describe.</param>
    /// <param name="collapsed">The nodes drawn shut.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="collapsed"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<OutlineEntry> Of(Model model, IReadOnlySet<NodeId> collapsed)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(collapsed);

        List<OutlineEntry> rows = [];

        foreach (var root in model.Roots)
        {
            Add(root, depth: 0, collapsed, rows);
        }

        return rows;
    }

    /// <summary>Every node of <paramref name="model"/> that has children, which is what "collapse all" shuts.</summary>
    /// <param name="model">The model to walk.</param>
    /// <remarks>
    /// Asked of the model rather than of the rows, because the rows of a tree that is already collapsed do not name the
    /// nodes below them - a command that shut only what it could see would leave the rest open.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal static HashSet<NodeId> NodesWithChildren(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        HashSet<NodeId> nodes = [];

        foreach (var root in model.Roots)
        {
            Collect(root, nodes);
        }

        return nodes;
    }

    private static void Collect(Node node, HashSet<NodeId> nodes)
    {
        if (!node.ChildrenInDocumentOrder().Any())
        {
            return;
        }

        nodes.Add(node.Id);

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Collect(child, nodes);
        }
    }

    /// <summary>One node, then everything under it unless it was drawn shut.</summary>
    private static void Add(Node node, int depth, IReadOnlySet<NodeId> collapsed, List<OutlineEntry> rows)
    {
        var children = node.ChildrenInDocumentOrder().ToArray();
        var expanded = children.Length == 0 || !collapsed.Contains(node.Id);

        rows.Add(new OutlineEntry(node.Id, depth, TextOf(node), node.Concept.Key, children.Length > 0, expanded));

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
    /// <remarks>
    /// The name is what a reader recognises a node by, so it comes last where it is the part that differs between two
    /// rows of the same concept - "Rule \"Approved\"" reads as a list of rules at a glance.
    /// </remarks>
    private static string TextOf(Node node)
    {
        var name = ShortNameOf(node.Concept.Key);

        return node.Name is { Length: > 0 } nodeName ? $"{name} \"{nodeName}\"" : name;
    }

    /// <summary>A concept key without its namespace, which is the name a reader knows it by.</summary>
    private static string ShortNameOf(string conceptKey) =>
        conceptKey[(conceptKey.LastIndexOf('.') + 1)..];
}
