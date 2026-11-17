using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// One thing a name can mean in a scope: the name, the node it names, and the concept that node is.
/// </summary>
/// <remarks>
/// <para>
/// The concept key travels with the entry because a scope is asked two different questions - "what may
/// I reference here" for a completion list, and "what does this name mean" for a typed name - and the
/// first one has to filter by the slot's declared concept without resolving every node again.
/// </para>
/// <para>
/// Two entries with the same name in one scope are not a mistake to be deduplicated: they are exactly
/// what <see cref="ScopeResolutionKind.Ambiguous"/> reports. A scope that quietly kept the first would
/// be picking for the user.
/// </para>
/// </remarks>
public readonly record struct ScopeEntry(string Name, NodeId Node, string ConceptKey)
{
    /// <summary>Whether this entry can stand in a slot declared over <paramref name="conceptKey"/>.</summary>
    /// <param name="conceptKey">The concept the slot accepts.</param>
    /// <param name="registry">The registry that answers what a concept is a kind of.</param>
    public bool Fits(string conceptKey, ConceptRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.TryGet(ConceptKey, out var concept) && concept.IsKindOf(conceptKey);
    }

    public override string ToString() => $"{Name} ({ConceptKey})";
}
