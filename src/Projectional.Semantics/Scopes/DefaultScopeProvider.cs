using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The scope chain a language gets when it says nothing: local, then the enclosing blocks, then the
/// model, then the project.
/// </summary>
/// <remarks>
/// <para>
/// The order is the one <c>docs/architecture/language.md</c> fixes, and each level is a rule a language
/// can replace by answering <see cref="IScopeProvider"/> itself:
/// </para>
/// <list type="number">
/// <item>the node's own children and its siblings - what is written next to the reference;</item>
/// <item>the same for every enclosing ancestor, nearest first - lexical nesting, like a block;</item>
/// <item>every root of the model - what the document declares;</item>
/// <item>every root of every model in the project - what the project declares.</item>
/// </list>
/// <para>
/// Only named nodes contribute. A node whose concept declares no name slot, or whose name is empty, is
/// not something a name could mean, and offering it would put an entry in a completion list that
/// cannot be typed.
/// </para>
/// <para>
/// The node itself is not in its own scope: a reference that could point at the thing holding it is a
/// cycle the model already refuses, and offering it would be offering an edit that fails.
/// </para>
/// </remarks>
public sealed class DefaultScopeProvider : IScopeProvider
{
    /// <summary>The shared instance, since the chain holds no state.</summary>
    public static DefaultScopeProvider Instance { get; } = new();

    /// <inheritdoc />
    public IScope ScopeOf(Node node, string referenceSlot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(referenceSlot);

        var levels = new List<IScope>
        {
            Local(node),
        };

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            levels.Add(Children(ancestor, node));
        }

        levels.Add(Roots(node.Model, node));
        levels.Add(ProjectRoots(node));

        return ScopeChain.Of([.. levels]);
    }

    /// <summary>The node's own children and its siblings, which is what "next to the reference" means.</summary>
    private static Scope Local(Node node)
    {
        var entries = new List<ScopeEntry>();

        AddChildren(node, entries, node);

        if (node.Parent is { } parent)
        {
            AddChildren(parent, entries, node);
        }

        return Scope.Of(entries);
    }

    private static Scope Children(Node node, Node subject)
    {
        var entries = new List<ScopeEntry>();

        AddChildren(node, entries, subject);

        return Scope.Of(entries);
    }

    private static Scope Roots(Model model, Node subject)
    {
        var entries = new List<ScopeEntry>();

        foreach (var root in model.Roots)
        {
            Add(root, entries, subject);
        }

        return Scope.Of(entries);
    }

    private static Scope ProjectRoots(Node node)
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
                Add(root, entries, node);
            }
        }

        return Scope.Of(entries);
    }

    private static void AddChildren(Node node, List<ScopeEntry> entries, Node subject)
    {
        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Add(child, entries, subject);
        }
    }

    private static void Add(Node node, List<ScopeEntry> entries, Node subject)
    {
        if (node.Id == subject.Id)
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
