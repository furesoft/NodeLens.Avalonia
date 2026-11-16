using System.Collections.Frozen;

using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Which nodes of a document are folded right now, as an immutable set of node ids.</summary>
/// <remarks>
/// <para>
/// View state, like <see cref="Language.NotationChoice"/>: it says nothing about the document, two readers
/// may disagree, and it is never written to disk. It is keyed by <see cref="NodeId"/> - a stable GUID - so
/// the set survives the rebuild every keystroke triggers and an undo that moves nodes around.
/// </para>
/// <para>
/// Immutable because a rebuild has to see the set that was in force when it started. <see cref="With"/>,
/// <see cref="Without"/> and <see cref="Toggle"/> return a new set instead of changing this one, which makes
/// "fold, then re-project" one expression whose halves cannot come apart.
/// </para>
/// <para>
/// Nesting needs no bookkeeping: folding a node hides its whole subtree, including descendants that are
/// themselves in the set, and unfolding the outer node brings them back still folded because their ids never
/// left. There is no cascade and no counter.
/// </para>
/// <para>
/// The name ends in <c>Set</c> because the type implements <see cref="IReadOnlySet{T}"/>, and the BCL design
/// rule that says so exists for a reason: a reader should be able to tell from the name that membership is
/// the question this type answers.
/// </para>
/// </remarks>
public sealed class CollapsedNodeSet : IReadOnlySet<NodeId>
{
    private readonly FrozenSet<NodeId> _ids;

    private CollapsedNodeSet(FrozenSet<NodeId> ids) => _ids = ids;

    /// <summary>Nothing folded.</summary>
    public static CollapsedNodeSet Empty { get; } = new(FrozenSet<NodeId>.Empty);

    /// <summary>How many nodes are folded.</summary>
    public int Count => _ids.Count;

    /// <summary>Whether nothing is folded.</summary>
    public bool IsEmpty => _ids.Count == 0;

    /// <summary>Whether <paramref name="node"/> is folded.</summary>
    /// <param name="node">The node to ask about.</param>
    public bool Contains(NodeId node) => _ids.Contains(node);

    /// <summary>This set with <paramref name="node"/> folded.</summary>
    /// <param name="node">The node to fold.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    public CollapsedNodeSet With(NodeId node) =>
        Contains(node) ? this : new(_ids.Append(node).ToFrozenSet());

    /// <summary>This set with <paramref name="node"/> unfolded.</summary>
    /// <param name="node">The node to unfold.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    public CollapsedNodeSet Without(NodeId node) =>
        Contains(node) ? new(_ids.Where(id => id != node).ToFrozenSet()) : this;

    /// <summary>This set with <paramref name="node"/> the other way round.</summary>
    /// <param name="node">The node to fold or unfold.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    public CollapsedNodeSet Toggle(NodeId node) => Contains(node) ? Without(node) : With(node);

    /// <summary>Whether every id of <paramref name="other"/> is also in this set.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool IsSubsetOf(IEnumerable<NodeId> other) => _ids.IsSubsetOf(other);

    /// <summary>Whether every id of <paramref name="other"/> is in this set and this set has more.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool IsProperSubsetOf(IEnumerable<NodeId> other) => _ids.IsProperSubsetOf(other);

    /// <summary>Whether this set contains every id of <paramref name="other"/>.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool IsSupersetOf(IEnumerable<NodeId> other) => _ids.IsSupersetOf(other);

    /// <summary>Whether this set contains every id of <paramref name="other"/> and more.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool IsProperSupersetOf(IEnumerable<NodeId> other) => _ids.IsProperSupersetOf(other);

    /// <summary>Whether this set and <paramref name="other"/> hold at least one id in common.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool Overlaps(IEnumerable<NodeId> other) => _ids.Overlaps(other);

    /// <summary>Whether this set holds exactly the ids of <paramref name="other"/>.</summary>
    /// <param name="other">The set to compare with.</param>
    public bool SetEquals(IEnumerable<NodeId> other) => _ids.SetEquals(other);

    /// <inheritdoc />
    public IEnumerator<NodeId> GetEnumerator() => _ids.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() =>
        _ids.Count == 0 ? "nothing folded" : $"{_ids.Count} folded";
}
