using Projectional.Editing;

namespace Projectional.IntentionRules;

/// <summary>One declared intention: the entry, the concept it is offered on, and any extra place it needs.</summary>
/// <param name="Intention">The entry, built once from the rule.</param>
/// <param name="ConceptKey">The concept it applies to, or <see langword="null"/> for any node.</param>
/// <param name="Place">
/// An extra condition on the caret, for a body that is only meaningful at one kind of place - an insert needs an
/// empty slot, and offering it anywhere else would be an offer that comes back <c>Rejected</c>.
/// </param>
internal sealed record DeclaredIntention(
    Intention Intention,
    string? ConceptKey,
    Func<IntentionContext, bool>? Place = null)
{
    /// <summary>Whether the caret is somewhere this entry means something.</summary>
    internal bool Applies(IntentionContext context) =>
        (ConceptKey is null || (context.Node is { } node && node.Concept.IsKindOf(ConceptKey)))
        && (Place is null || Place(context));
}
