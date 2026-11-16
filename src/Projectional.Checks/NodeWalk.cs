using Projectional.Core;

namespace Projectional.Checks;

/// <summary>The document order the checks walk in, defined once.</summary>
/// <remarks>
/// <see cref="Node.ChildrenInDocumentOrder"/> answers one level only, and every standard check wants the
/// same whole-document walk. Writing it once here is what keeps two checks from reporting the same
/// document in two different orders - the order a user reads the findings in is the order they were
/// produced.
/// </remarks>
internal static class NodeWalk
{
    /// <summary>Every root and every node below it, in the order the persistence format writes them.</summary>
    /// <param name="model">The document to walk.</param>
    internal static IEnumerable<Node> Of(Model model)
    {
        foreach (var root in model.Roots)
        {
            foreach (var node in SelfAndDescendants(root))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<Node> SelfAndDescendants(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
