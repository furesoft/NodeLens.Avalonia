using Projectional.Core;
using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>One node the interpreter evaluated: how deep it was, what concept it is, and what came out.</summary>
/// <param name="Depth">The evaluation depth, zero for the root of the run.</param>
/// <param name="Node">The node that was evaluated.</param>
/// <param name="ConceptKey">Its concept, so a trace can be read without the model open.</param>
/// <param name="Value">What it evaluated to, or <see langword="null"/> when it failed.</param>
/// <remarks>
/// Entries are appended in the order the values were <em>finished</em>, which is post-order: a child's entry
/// precedes its parent's, exactly as the values were computed. That is the order a reader needs to follow
/// "why", and it is the order a stepping debugger will replay.
/// </remarks>
public readonly record struct TraceEntry(int Depth, NodeId Node, string ConceptKey, Value? Value)
{
    public override string ToString() =>
        $"{new string(' ', Depth)}{ConceptKey} = {Value?.Display ?? "<failed>"}";
}
