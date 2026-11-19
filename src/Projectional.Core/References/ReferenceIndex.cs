using System.Runtime.CompilerServices;

namespace Projectional.Core;

/// <summary>Answers which nodes point at a given node, across every model of a project.</summary>
/// <remarks>
/// <para>
/// The model stores a reference as a target id on the referring node, so "who points at this node" is the reverse
/// question and has to be indexed: walking every node of every model per query is what a rename would pay once per
/// reference, and a find-usages gesture would pay once per press. The index is built in one pass and answers in
/// document order - models in project order, nodes depth first, and the reference slots of one node in declaration
/// order - so a list of usages reads the way the documents do.
/// </para>
/// <para>
/// It is per <see cref="Project"/> rather than per document because a reference may cross a model boundary but never a
/// project one. An index that answered for one model would have to fall back to a walk for exactly the case a rename
/// cares most about.
/// </para>
/// <para>
/// <strong>It is invalidated by <see cref="Model.Version"/>, never by a change handler.</strong> Subscribing to
/// <see cref="Model.Changed"/> would switch change journalling on for every model in the project - <c>IsRecording</c>
/// becomes true as soon as somebody subscribes - so a consumer that wanted no undo history would start allocating
/// rollback closures for it. Comparing the versions of the project's models costs one read each and sees a new
/// reference, a retargeted one, a cleared one, a deletion and a new node alike, because every one of them passes
/// through <c>MarkMutated</c>. The table that holds one index per project is weak, so a closed project takes its index
/// with it.
/// </para>
/// <para>
/// An unset reference slot is not a usage, and neither is a dangling one: the latter names an id that is no longer in
/// the project, so nothing a caller can ask about - <see cref="UsagesOf(NodeId)"/> takes a live target - can match it.
/// </para>
/// </remarks>
public sealed class ReferenceIndex
{
    private static readonly ConditionalWeakTable<Project, ReferenceIndex> Indexes = new();

    private readonly Project _project;
    private readonly object _gate = new();

    private Model[] _models = [];
    private long[] _versions = [];
    private Dictionary<NodeId, ReferenceUsage[]> _byTarget = [];

    private ReferenceIndex(Project project) => _project = project;

    /// <summary>The index of <paramref name="project"/>, built on first use and rebuilt after a change.</summary>
    /// <param name="project">The project whose references are indexed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="project"/> is <see langword="null"/>.</exception>
    public static ReferenceIndex For(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return Indexes.GetValue(project, candidate => new ReferenceIndex(candidate));
    }

    /// <summary>Every reference to the node with <paramref name="target"/>, in document order.</summary>
    /// <param name="target">The node id the references point at.</param>
    /// <remarks>
    /// The answer is a list, not an iterator: a caller that walks it twice must not pay for the walk twice, and it is
    /// stale the moment the model changes - which is why the next call rebuilds instead of caching between them.
    /// </remarks>
    public IReadOnlyList<ReferenceUsage> UsagesOf(NodeId target)
    {
        lock (_gate)
        {
            Refresh();

            return _byTarget.TryGetValue(target, out var usages) ? usages : [];
        }
    }

    /// <summary>Every reference to <paramref name="target"/>, in document order.</summary>
    /// <param name="target">The node the references point at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="target"/> belongs to another project.</exception>
    /// <remarks>
    /// A node of another project is refused rather than answered with an empty list: two projects are two universes,
    /// so asking one about the other's node is a caller bug - and an empty answer would hide it.
    /// </remarks>
    public IReadOnlyList<ReferenceUsage> UsagesOf(Node target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!ReferenceEquals(target.Model.Project, _project))
        {
            throw new ArgumentException(
                $"Node {target.Id} belongs to another project and has no usages in this one.",
                nameof(target));
        }

        return UsagesOf(target.Id);
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

        var byTarget = new Dictionary<NodeId, List<ReferenceUsage>>();

        foreach (var model in models)
        {
            foreach (var node in model.Nodes)
            {
                foreach (var slot in node.Concept.Slots)
                {
                    if (slot is not ReferenceSlot reference || node.GetReference(reference.Name) is not { } value)
                    {
                        continue;
                    }

                    if (!byTarget.TryGetValue(value.Target, out var usages))
                    {
                        byTarget[value.Target] = usages = [];
                    }

                    usages.Add(new ReferenceUsage(node.Id, reference.Name, value.Target));
                }
            }
        }

        _models = [.. models];
        _versions = versions;
        _byTarget = byTarget.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray());
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
