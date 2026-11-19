using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>The one walk that decides which nodes a scope level offers, and in which order.</summary>
/// <remarks>
/// <para>
/// <see cref="DefaultScopeProvider"/> and <see cref="NamedScopeProvider"/> are two answers to the same question, so
/// they share this instead of each walking the tree: a second walk that disagreed with the first would make
/// "everywhere" mean something other than "say nothing", which is exactly the difference a language could not see.
/// </para>
/// <para>
/// The levels are built from <see cref="NameIndex"/> rather than by enumerating children, because the nearest level
/// is "my siblings" and a flat document has one parent for everything - so a walk here is a walk over the whole
/// document, once per reference. Which nodes belong to a level is still this file's decision; only the question "who
/// carries this name" moved into the index.
/// </para>
/// <para>
/// Only named nodes contribute. A node whose concept declares no name slot, or whose name is empty, is not something
/// a name could mean, and offering it would put an entry in a completion list that cannot be typed. The node being
/// resolved is excluded everywhere, because a reference that could point at the thing holding it is a cycle the
/// model already refuses.
/// </para>
/// <para>
/// <paramref name="conceptKey"/> narrows a level to one concept and its subtypes, or is <see langword="null"/> for
/// every named node. It is a filter rather than a second kind of level: which nodes are visible and where they are
/// written are two questions, and a language asks them separately.
/// </para>
/// </remarks>
internal static class ScopeWalk
{
    /// <summary>The chain levels the place contributes, nearest first.</summary>
    /// <param name="node">The node holding the reference.</param>
    /// <param name="place">Which stretch of the document to look in.</param>
    /// <param name="conceptKey">The concept the entries must be, or <see langword="null"/> for any.</param>
    public static IReadOnlyList<IScope> Levels(Node node, ScopePlace place, string? conceptKey)
    {
        ArgumentNullException.ThrowIfNull(node);

        var index = NameIndex.For(node.Model.Project);
        var levels = new List<IScope>();

        if (place is ScopePlace.Local or ScopePlace.Everywhere)
        {
            levels.Add(Local(node, index, conceptKey));
        }

        if (place is ScopePlace.Ancestors or ScopePlace.Everywhere)
        {
            levels.AddRange(Ancestors(node, index, conceptKey));
        }

        if (place is ScopePlace.Model or ScopePlace.Everywhere)
        {
            levels.Add(new RootsScope(index, [node.Model], node.Id, conceptKey));
        }

        if (place is ScopePlace.Project or ScopePlace.Everywhere)
        {
            levels.Add(ProjectRoots(node, index, conceptKey));
        }

        return levels;
    }

    /// <summary>The node's own children and its siblings, which is what "next to the reference" means.</summary>
    private static ChildrenScope Local(Node node, NameIndex index, string? conceptKey) =>
        new ChildrenScope(
            index,
            node.Parent is { } parent ? [node, parent] : [node],
            node.Id,
            conceptKey);

    private static IEnumerable<IScope> Ancestors(Node node, NameIndex index, string? conceptKey)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            yield return new ChildrenScope(index, [ancestor], node.Id, conceptKey);
        }
    }

    private static RootsScope ProjectRoots(Node node, NameIndex index, string? conceptKey) =>
        new RootsScope(
            index,
            [.. node.Model.Project.Models.Where(model => !ReferenceEquals(model, node.Model))],
            node.Id,
            conceptKey);
}
