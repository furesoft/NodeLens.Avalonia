using System.Runtime.CompilerServices;
using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>Every named node of a project, by name, so that a scope level need not walk a document to answer.</summary>
/// <remarks>
/// <para>
/// The scope model asks two questions of a level - what may I reference here, and what does this name mean - and
/// the second one is the one that fires on every reference: a constraint resolves the target's current name for
/// each reference slot, and a completion list resolves what the user typed. Answering it by enumerating a level's
/// nodes makes a document whose nodes are mostly references quadratic, because the level that holds them is "the
/// children of my parent" and a flat model has one parent for everything.
/// </para>
/// <para>
/// So the index answers <see cref="Named"/> and a level keeps its own membership test. The index is per
/// <see cref="Project"/> rather than per document, because the outermost level of the chain is the project: a
/// reference may cross a model boundary, and an index that answered for one model only would have to fall back to
/// a walk for exactly the case that made the cost visible.
/// </para>
/// <para>
/// <strong>It is invalidated by <see cref="Model.Version"/>, never by a change handler.</strong> Subscribing to
/// <see cref="Model.Changed"/> would be the obvious way to drop a stale index, and it would also switch change
/// journalling on for every model in the project: <c>IsRecording</c> is true as soon as somebody subscribes, so a
/// batch consumer that never wanted an undo history would start allocating rollback closures for it. Comparing a
/// version costs two reads and covers a rename, a move, a deletion and a new node alike, because every one of them
/// passes through <c>MarkMutated</c>.
/// </para>
/// <para>
/// The table that holds one index per project is weak: a document that is closed takes its index with it, and
/// nothing else has to remember to clear it.
/// </para>
/// </remarks>
internal sealed class NameIndex
{
    private static readonly ConditionalWeakTable<Project, NameIndex> Indexes = new();

    private readonly Project _project;
    private readonly object _gate = new();

    private Model[] _models = [];
    private long[] _versions = [];
    private Dictionary<string, Node[]> _byName = new(StringComparer.Ordinal);

    private NameIndex(Project project) => _project = project;

    /// <summary>The index of <paramref name="project"/>, built on first use and rebuilt after a change.</summary>
    public static NameIndex For(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return Indexes.GetValue(project, candidate => new NameIndex(candidate));
    }

    /// <summary>Every node of the project carrying <paramref name="name"/>, in document order.</summary>
    /// <remarks>
    /// The order is the one <see cref="Model.Nodes"/> walks - depth first, models in project order - so a level that
    /// filters this list keeps the order it would have produced by walking, which is what a completion list shows.
    /// </remarks>
    public IReadOnlyList<Node> Named(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            Refresh();

            return _byName.TryGetValue(name, out var nodes) ? nodes : [];
        }
    }

    /// <summary>The name of <paramref name="node"/>, or <see langword="null"/> when it is not a named node.</summary>
    /// <remarks>
    /// A node whose concept declares no name slot, or whose name is empty, is not something a name could mean.
    /// Offering it would put an entry in a completion list that cannot be typed, so the test lives here - beside the
    /// index that uses it - rather than once per caller.
    /// </remarks>
    public static string? NameOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.NameSlot is { } slot && node.GetOrNull(slot.Name) is { Length: > 0 } name ? name : null;
    }

    /// <summary>Whether <paramref name="node"/> is a named node of <paramref name="conceptKey"/> or a subtype.</summary>
    public static bool Fits(Node node, string? conceptKey)
    {
        ArgumentNullException.ThrowIfNull(node);

        return conceptKey is null || node.Concept.IsKindOf(conceptKey);
    }

    /// <summary>The entry a scope offers for <paramref name="node"/>.</summary>
    public static ScopeEntry EntryOf(Node node, string name)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new ScopeEntry(name, node.Id, node.Concept.Key);
    }

    private void Refresh()
    {
        var models = _project.Models;
        var versions = new long[models.Count];

        for (var i = 0; i < versions.Length; i++)
        {
            versions[i] = models[i].Version;
        }

        if (IsCurrent(models, versions))
        {
            return;
        }

        var byName = new Dictionary<string, List<Node>>(StringComparer.Ordinal);

        foreach (var model in models)
        {
            foreach (var node in model.Nodes)
            {
                if (NameOf(node) is not { } name)
                {
                    continue;
                }

                if (!byName.TryGetValue(name, out var nodes))
                {
                    byName[name] = nodes = [];
                }

                nodes.Add(node);
            }
        }

        _models = [.. models];
        _versions = versions;
        _byName = byName.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private bool IsCurrent(IReadOnlyList<Model> models, long[] versions)
    {
        if (versions.Length != _models.Length || _versions.Length != _models.Length)
        {
            return false;
        }

        for (var i = 0; i < _models.Length; i++)
        {
            if (!ReferenceEquals(models[i], _models[i]) || versions[i] != _versions[i])
            {
                return false;
            }
        }

        return true;
    }
}
