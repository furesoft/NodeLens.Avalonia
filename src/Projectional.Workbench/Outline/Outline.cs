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
/// </remarks>
internal static class Outline
{
    /// <summary>Every node of <paramref name="model"/>, root first, depth first.</summary>
    /// <param name="model">The model to describe.</param>
    internal static IReadOnlyList<OutlineEntry> Of(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<OutlineEntry> rows = [];

        foreach (var root in model.Roots)
        {
            Add(root, depth: 0, rows);
        }

        return rows;
    }

    /// <summary>One node, then everything under it.</summary>
    private static void Add(Node node, int depth, List<OutlineEntry> rows)
    {
        rows.Add(new OutlineEntry(node.Id, depth, TextOf(node)));

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Add(child, depth + 1, rows);
        }
    }

    /// <summary>What a row says: the concept's own name, plus the node's name when it has one.</summary>
    /// <remarks>
    /// The name is what a reader recognises a node by, so it comes last where it is the part that differs between two
    /// rows of the same concept - "Rule \"Approved\"" reads as a list of rules at a glance.
    /// </remarks>
    private static string TextOf(Node node)
    {
        var concept = node.Concept.Key;

        if (concept.LastIndexOf('.') is var dot and >= 0)
        {
            concept = concept[(dot + 1)..];
        }

        return node.Name is { Length: > 0 } name ? $"{concept} \"{name}\"" : concept;
    }
}