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

        var levels = new List<IScope>();

        if (place is ScopePlace.Local or ScopePlace.Everywhere)
        {
            levels.Add(Local(node, conceptKey));
        }

        if (place is ScopePlace.Ancestors or ScopePlace.Everywhere)
        {
            levels.AddRange(Ancestors(node, conceptKey));
        }

        if (place is ScopePlace.Model or ScopePlace.Everywhere)
        {
            levels.Add(Roots(node, conceptKey));
        }

        if (place is ScopePlace.Project or ScopePlace.Everywhere)
        {
            levels.Add(ProjectRoots(node, conceptKey));
        }

        return levels;
    }

    /// <summary>The node's own children and its siblings, which is what "next to the reference" means.</summary>
    private static Scope Local(Node node, string? conceptKey)
    {
        var entries = new List<ScopeEntry>();

        AddChildren(node, entries, node, conceptKey);

        if (node.Parent is { } parent)
        {
            AddChildren(parent, entries, node, conceptKey);
        }

        return Scope.Of(entries);
    }

    private static IEnumerable<Scope> Ancestors(Node node, string? conceptKey)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            var entries = new List<ScopeEntry>();

            AddChildren(ancestor, entries, node, conceptKey);

            yield return Scope.Of(entries);
        }
    }

    private static Scope Roots(Node node, string? conceptKey)
    {
        var entries = new List<ScopeEntry>();

        foreach (var root in node.Model.Roots)
        {
            Add(root, entries, node, conceptKey);
        }

        return Scope.Of(entries);
    }

    private static Scope ProjectRoots(Node node, string? conceptKey)
    {
        var entries = new List<ScopeEntry>();

        foreach (var model in node.Model.Project.Models)
        {
            if (ReferenceEquals(model, node.Model))
            {
                continue;
            }

            foreach (var root in model.Roots)
            {
                Add(root, entries, node, conceptKey);
            }
        }

        return Scope.Of(entries);
    }

    private static void AddChildren(Node node, List<ScopeEntry> entries, Node subject, string? conceptKey)
    {
        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Add(child, entries, subject, conceptKey);
        }
    }

    private static void Add(Node node, List<ScopeEntry> entries, Node subject, string? conceptKey)
    {
        if (node.Id == subject.Id)
        {
            return;
        }

        if (conceptKey is not null && !node.Concept.IsKindOf(conceptKey))
        {
            return;
        }

        if (node.Concept.NameSlot is not { } nameSlot)
        {
            return;
        }

        if (node.GetOrNull(nameSlot.Name) is not { Length: > 0 } name)
        {
            return;
        }

        entries.Add(new ScopeEntry(name, node.Id, node.Concept.Key));
    }
}
