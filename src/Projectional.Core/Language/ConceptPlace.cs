namespace Projectional.Core;

/// <summary>One kind of place a node may stand in: a concept, and optionally one of its slots.</summary>
/// <remarks>
/// A place rather than a predicate, because a language is data: the editor edits languages as models from M5
/// on ([language-authoring.md](../../../docs/architecture/language-authoring.md)), and a list of places can be
/// shown and edited there while a delegate cannot. A place is also checkable at registration, which is where
/// this project prefers to catch a language mistake.
/// </remarks>
/// <param name="ConceptKey">
/// The concept the parent must be, or be a kind of. Naming a super concept is how "anywhere inside a
/// declaration" is written.
/// </param>
/// <param name="Slot">
/// One slot of that concept, or <see langword="null"/> for any slot of it - which is what a concept means
/// when it may stand anywhere inside a declaration.
/// </param>
public readonly record struct ConceptPlace(string ConceptKey, string? Slot = null)
{
    /// <summary>Whether a node of the concept this place belongs to may stand in <paramref name="slot"/> of <paramref name="parent"/>.</summary>
    /// <param name="parent">The node that would hold it.</param>
    /// <param name="slot">The child slot of that node, or <see langword="null"/> for "directly in the model".</param>
    public bool Allows(Concept parent, string? slot)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return parent.IsKindOf(ConceptKey)
            && (Slot is null || string.Equals(Slot, slot, StringComparison.Ordinal));
    }

    public override string ToString() => Slot is null ? ConceptKey : $"{ConceptKey}.{Slot}";
}
