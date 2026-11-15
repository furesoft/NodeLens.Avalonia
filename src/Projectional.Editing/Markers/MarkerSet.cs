using System.Collections.ObjectModel;
using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>A batch of diagnostics, kept so that finding the ones for a node does not mean scanning all of them.</summary>
/// <remarks>
/// <para>
/// A marker set is immutable and built once. Every drawing pass asks "what is wrong with this cell", which
/// would be a scan of the whole list per cell; the set answers it from a lookup built in the constructor
/// instead. Nothing can be appended to it afterwards, so a host that has recomputed its checks publishes a
/// whole new set rather than mutating the one the editor is drawing.
/// </para>
/// <para>
/// That immutability is also what makes it safe to hand a set to a control while a background thread
/// computes the next one: the reader sees one set or the other, never a half-filled list. The set is view
/// state, like the choice of variant and the wrap width - it is never part of the model, never persisted
/// and never an undo step, because it describes what somebody thinks of the model rather than the model.
/// </para>
/// <para>
/// Order is preserved: <see cref="Diagnostics"/> answers in the order the diagnostics were added, so a
/// list of findings shown to the user does not shuffle between two runs of the same check.
/// </para>
/// </remarks>
public sealed class MarkerSet
{
    private readonly ReadOnlyCollection<Diagnostic> _all;
    private readonly Dictionary<NodeId, ReadOnlyCollection<Diagnostic>> _byTarget;

    /// <summary>Describes <paramref name="diagnostics"/>, which may be empty.</summary>
    /// <param name="diagnostics">The findings, in the order they should be listed.</param>
    /// <exception cref="ArgumentNullException">One of <paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    public MarkerSet(params ReadOnlySpan<Diagnostic> diagnostics)
    {
        var all = new List<Diagnostic>(diagnostics.Length);
        var byTarget = new Dictionary<NodeId, List<Diagnostic>>();

        foreach (var diagnostic in diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostic, nameof(diagnostics));

            all.Add(diagnostic);

            if (!byTarget.TryGetValue(diagnostic.Target, out var forNode))
            {
                forNode = [];
                byTarget[diagnostic.Target] = forNode;
            }

            forNode.Add(diagnostic);
        }

        _all = all.AsReadOnly();
        _byTarget = new Dictionary<NodeId, ReadOnlyCollection<Diagnostic>>(byTarget.Count);

        foreach (var (target, forNode) in byTarget)
        {
            _byTarget[target] = forNode.AsReadOnly();
        }
    }

    /// <summary>A set with nothing in it, which is what a document with no checks reports.</summary>
    public static MarkerSet Empty { get; } = new();

    /// <summary>How many diagnostics the set holds.</summary>
    public int Count => _all.Count;

    /// <summary>Every diagnostic, in the order the set was built with.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _all;

    /// <summary>Every diagnostic about <paramref name="node"/>, in the order the set was built with.</summary>
    /// <param name="node">The node to ask about.</param>
    /// <remarks>
    /// Empty rather than <see langword="null"/> when the node is fine, because the caller's question is
    /// "what should I draw here" and the answer for a healthy node is "nothing".
    /// </remarks>
    public IReadOnlyList<Diagnostic> For(NodeId node) =>
        _byTarget.TryGetValue(node, out var forNode) ? forNode : [];

    /// <summary>The worst severity reported about <paramref name="node"/>, or <see langword="null"/> if none is.</summary>
    /// <param name="node">The node to ask about.</param>
    /// <remarks>
    /// A node with three warnings and one error answers <see cref="DiagnosticSeverity.Error"/>, which is
    /// what lets a summary, a gutter or a problem list colour a node by its worst finding instead of
    /// showing three marks. The comparison is the enum's declaration order.
    /// </remarks>
    public DiagnosticSeverity? SeverityOf(NodeId node)
    {
        if (!_byTarget.TryGetValue(node, out var forNode))
        {
            return null;
        }

        var worst = forNode[0].Severity;

        for (var i = 1; i < forNode.Count; i++)
        {
            if (forNode[i].Severity > worst)
            {
                worst = forNode[i].Severity;
            }
        }

        return worst;
    }

    /// <summary>This set's diagnostics followed by <paramref name="other"/>'s.</summary>
    /// <param name="other">The set to add, which is not modified.</param>
    /// <remarks>
    /// Combining is the one operation that looks like mutation and is not: a language's own checks and the
    /// host's can be kept apart and joined for display, and neither loses its order. Nothing is deduplicated
    /// - two checks that report the same node are two findings, and merging them would hide one.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public MarkerSet Concat(MarkerSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Count == 0)
        {
            return this;
        }

        return Count == 0 ? other : new MarkerSet([.. _all, .. other._all]);
    }
}
