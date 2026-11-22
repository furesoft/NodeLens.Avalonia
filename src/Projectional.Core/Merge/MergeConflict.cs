using System.Globalization;

namespace Projectional.Core;

/// <summary>One thing a merge could not decide, said in both sides' words so a person can decide it.</summary>
/// <remarks>
/// <para>
/// The two sides are text rather than model objects: a conflict outlives the three models it was found in - a report is
/// printed, written to a log and compared by a test - and holding three live projects open to explain one line would
/// be a cost with nothing to show for it. What a caller needs to act is which node, which slot, and what each side says.
/// </para>
/// <para>
/// <see cref="Node"/> is <see langword="null"/> for a conflict about a slot's order, because the thing that could not be
/// decided there is the sequence of a parent's children rather than one node - and <see cref="Slot"/> then names the
/// sequence, with <see cref="Parent"/> naming the node it belongs to.
/// </para>
/// </remarks>
/// <param name="Node">The node the conflict is about, or <see langword="null"/> for a conflict about a slot's order.</param>
/// <param name="Kind">Which question the merge could not answer.</param>
/// <param name="Parent">The node whose slot could not be ordered, for <see cref="MergeConflictKind.ReorderedDifferently"/>.</param>
/// <param name="Slot">The property, reference or child slot it concerns, when it concerns one.</param>
/// <param name="Ours">What the branch being merged into says.</param>
/// <param name="Theirs">What the branch being merged says.</param>
public sealed record MergeConflict(
    NodeId? Node,
    MergeConflictKind Kind,
    string? Parent,
    string? Slot,
    string Ours,
    string Theirs)
{
    /// <summary>One node-level conflict, where both sides say something about the same node.</summary>
    /// <param name="node">The node both sides changed.</param>
    /// <param name="kind">Which question the merge could not answer.</param>
    /// <param name="slot">The slot it concerns, or <see langword="null"/>.</param>
    /// <param name="ours">What our side says.</param>
    /// <param name="theirs">What their side says.</param>
    /// <returns>The conflict.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is empty, or a side is empty.</exception>
    public static MergeConflict Of(NodeId node, MergeConflictKind kind, string? slot, string ours, string theirs)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A conflict is about a node.", nameof(node));
        }

        ArgumentException.ThrowIfNullOrEmpty(ours);
        ArgumentException.ThrowIfNullOrEmpty(theirs);

        return new MergeConflict(node, kind, null, slot, ours, theirs);
    }

    /// <summary>One conflict about the order of a slot's children.</summary>
    /// <param name="parent">The node whose slot could not be ordered, or <see langword="null"/> for the model's roots.</param>
    /// <param name="slot">The child slot, or <c>roots</c> for the model's roots.</param>
    /// <param name="ours">Our side's sequence, as ids.</param>
    /// <param name="theirs">Their side's sequence, as ids.</param>
    /// <returns>The conflict.</returns>
    /// <exception cref="ArgumentException">A side is empty.</exception>
    public static MergeConflict OfOrder(NodeId? parent, string slot, string ours, string theirs)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(ours);
        ArgumentException.ThrowIfNullOrEmpty(theirs);

        return new MergeConflict(
            null,
            MergeConflictKind.ReorderedDifferently,
            parent?.ToString() ?? "the model",
            slot,
            ours,
            theirs);
    }

    /// <summary>The conflict as the line a command prints.</summary>
    /// <returns>One line naming what could not be decided and both sides.</returns>
    public string Describe()
    {
        var where = Node is { } node
            ? Slot is { Length: > 0 } slot ? $"{node}.{slot}" : node.ToString()
            : $"order of {Parent}.{Slot}";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Kind} at {where}: ours {Ours}, theirs {Theirs}");
    }

    public override string ToString() => Describe();
}
