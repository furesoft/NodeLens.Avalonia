using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>A scope level holding the named roots of the models given, answered from the project's index.</summary>
/// <remarks>
/// The model level and the project level are the same rule over different sets of documents: what a document
/// declares at its top, and what the other documents of the project declare. A root is a node without a parent, so
/// which model it belongs to is the only thing that tells the two levels apart.
/// </remarks>
internal sealed class RootsScope : IScope
{
    private readonly NameIndex _index;
    private readonly Model[] _models;
    private readonly NodeId _subject;
    private readonly string? _conceptKey;

    private ScopeEntry[]? _entries;

    /// <summary>A level holding the named roots of <paramref name="models"/>, in the order given.</summary>
    /// <param name="index">The project's index, which answers a name without walking.</param>
    /// <param name="models">The models whose roots this level offers.</param>
    /// <param name="subject">The node being resolved, which is never part of its own scope.</param>
    /// <param name="conceptKey">The concept the entries must be, or <see langword="null"/> for any.</param>
    public RootsScope(NameIndex index, Model[] models, NodeId subject, string? conceptKey)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(models);

        _index = index;
        _models = models;
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

        foreach (var node in _index.Named(name))
        {
            if (!Accepts(node))
            {
                continue;
            }

            matches ??= [];
            matches.Add(NameIndex.EntryOf(node, name));
        }

        return matches switch
        {
            null => ScopeResolution.Missing,
            [var only] => ScopeResolution.Found(only),
            _ => ScopeResolution.Ambiguous(matches),
        };
    }

    public override string ToString() => $"{_models.Length} model(s)";

    private bool Accepts(Node node) =>
        node.Id != _subject
        && node.Parent is null
        && _models.Contains(node.Model)
        && NameIndex.Fits(node, _conceptKey)
        && NameIndex.NameOf(node) is not null;

    private ScopeEntry[] Materialize()
    {
        var entries = new List<ScopeEntry>();

        foreach (var model in _models)
        {
            foreach (var root in model.Roots)
            {
                if (Accepts(root) && NameIndex.NameOf(root) is { } name)
                {
                    entries.Add(NameIndex.EntryOf(root, name));
                }
            }
        }

        return [.. entries];
    }
}
