using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>One stretch of generated text, and the node (and slot) it came from.</summary>
/// <param name="Node">The node whose text this is.</param>
/// <param name="Slot">
/// The property or child slot the text was read from, or <see langword="null"/> for a literal or a child expansion.
/// </param>
/// <param name="Start">Where the stretch begins in the generated text.</param>
/// <param name="Length">How long it is, in characters.</param>
/// <remarks>
/// This is the traceability map, and it is what makes "go to source" from a generated file possible without a second
/// generator: a host that shows the text can map a caret offset back to a node by looking for the region that covers
/// it. Whitespace is not in a region - it belongs to no node - which is why regions need not tile the output.
/// </remarks>
public readonly record struct GeneratedRegion(NodeId Node, string? Slot, int Start, int Length)
{
    /// <summary>Whether this region covers <paramref name="offset"/>.</summary>
    /// <param name="offset">A zero-based offset into the generated text.</param>
    public bool Covers(int offset) => offset >= Start && offset < Start + Length;

    public override string ToString() => $"{Node}[{Slot}] @{Start}+{Length}";
}
