using Projectional.Core;

namespace Projectional.Language;

/// <summary>The name of the one child in a slot - how a composite name is written.</summary>
/// <param name="Slot">The single-valued child slot to read.</param>
/// <remarks>
/// The child's own concept answers its name, so <c>Class1.method2</c> is a literal, this node's name slot and this
/// part, and the child's concept decides what the second half looks like. An empty slot answers the empty string.
/// </remarks>
public sealed record NameChild : NameBody
{
    /// <summary>Creates a part that answers the name of <paramref name="slot"/>'s child.</summary>
    /// <param name="slot">The single-valued child slot to read.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public NameChild(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The child slot to read.</summary>
    public string Slot { get; }

    private protected override string Evaluate(Node node)
    {
        if (node.Concept.FindChild(Slot) is not { } child)
        {
            throw new InvalidOperationException(
                $"'{Slot}' is not a child slot of {node.Concept.Key}, so a name body cannot read its name. The declared form refuses this at load.");
        }

        if (child.Cardinality != Cardinality.One)
        {
            throw new InvalidOperationException(
                $"'{Slot}' of {node.Concept.Key} holds {child.Cardinality} children, and a name can read one. The declared form refuses this at load.");
        }

        return node.GetChild(Slot)?.Name ?? string.Empty;
    }

    public override string ToString() => $"child {Slot}";
}
