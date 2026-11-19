using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>A scope level holding the named children of the nodes given, answered from the project's index.</summary>
/// <remarks>
/// <para>
/// This is the level that costs a document when it is built by walking: "the children of my parent" is everything
/// next to the reference, and in a flat model that is every node. So the entries are materialised only when a
/// caller asks for the list, and <see cref="Resolve"/> - the question a constraint asks once per reference - is one
/// index lookup plus a membership test per candidate.
/// </para>
/// <para>
/// The parents are a sequence rather than one node because the nearest level of the chain is "my own children and
/// my siblings" in that order, and merging them into one level is what makes a name that occurs in both ambiguous
/// rather than shadowed.
/// </para>
/// </remarks>
internal sealed class ChildrenScope : IScope
{
    private readonly NameIndex _index;
    private readonly Node[] _parents;
    private readonly NodeId _subject;
    private readonly string? _conceptKey;

    private ScopeEntry[]? _entries;

    /// <summary>A level holding the named children of <paramref name="parents"/>, in the order given.</summary>
    /// <param name="index">The project's index, which answers a name without walking.</param>
    /// <param name="parents">The nodes whose children this level offers.</param>
    /// <param name="subject">The node being resolved, which is never part of its own scope.</param>
    /// <param name="conceptKey">The concept the entries must be, or <see langword="null"/> for any.</param>
    public ChildrenScope(NameIndex index, Node[] parents, NodeId subject, string? conceptKey)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(parents);
        ArgumentOutOfRangeException.ThrowIfZero(parents.Length);

        _index = index;
        _parents = parents;
        _subject = subject;
        _conceptKey = conceptKey;
    }

    /// <inheritdoc />
    /// <remarks>The list is built on first access, because a caller that only resolves a name never needs it.</remarks>
    public IReadOnlyList<ScopeEntry> Entries => _entries ??= Materialize();

    /// <inheritdoc />
    public ScopeResolution Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        List<ScopeEntry>? matches = null;

        foreach (var parent in _parents)
        {
            foreach (var node in _index.Named(name))
            {
                if (!Accepts(node, parent))
                {
                    continue;
                }

                matches ??= [];
                matches.Add(NameIndex.EntryOf(node, name));
            }
        }

        return matches switch
        {
            null => ScopeResolution.Missing,
            [var only] => ScopeResolution.Found(only),
            _ => ScopeResolution.Ambiguous(matches),
        };
    }

    public override string ToString() => $"{_parents.Length} parent(s)";

    private bool Accepts(Node node, Node parent) =>
        node.Id != _subject
        && ReferenceEquals(node.Parent, parent)
        && NameIndex.Fits(node, _conceptKey)
        && NameIndex.NameOf(node) is not null;

    private ScopeEntry[] Materialize()
    {
        var entries = new List<ScopeEntry>();

        foreach (var parent in _parents)
        {
            foreach (var node in parent.ChildrenInDocumentOrder())
            {
                if (Accepts(node, parent) && NameIndex.NameOf(node) is { } name)
                {
                    entries.Add(NameIndex.EntryOf(node, name));
                }
            }
        }

        return [.. entries];
    }
}
