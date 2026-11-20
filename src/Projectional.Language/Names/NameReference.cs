using Projectional.Core;

namespace Projectional.Language;

/// <summary>The name of a reference's target, so a node can be called after what it points at.</summary>
/// <param name="Slot">The reference slot to read.</param>
/// <remarks>
/// Read from the model, not from a cell: an unset reference and a dangling one both answer the empty string, because a
/// name is about what the node is called right now and a stand-in would be part of every name that recited it.
/// </remarks>
public sealed record NameReference : NameBody
{
    /// <summary>Creates a part that answers the target's name of <paramref name="slot"/>.</summary>
    /// <param name="slot">The reference slot to read.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public NameReference(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The reference slot to read.</summary>
    public string Slot { get; }

    private protected override string Evaluate(Node node) =>
        node.Concept.FindReference(Slot) is not null
            ? node.GetReferenceTarget(Slot)?.Name ?? string.Empty
            : throw new InvalidOperationException(
                $"'{Slot}' is not a reference of {node.Concept.Key}, so a name body cannot read its target. The declared form refuses this at load.");

    public override string ToString() => $"reference {Slot}";
}
