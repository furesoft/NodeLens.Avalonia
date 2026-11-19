using Projectional.Editing;

namespace Projectional.IntentionRules;

/// <summary>One declared intention: the entry, and the concept it is offered on.</summary>
/// <param name="Intention">The entry, built once from the rule.</param>
/// <param name="ConceptKey">The concept it applies to, or <see langword="null"/> for any node.</param>
internal sealed record DeclaredIntention(Intention Intention, string? ConceptKey)
{
    /// <summary>Whether the caret stands on a node this entry is about.</summary>
    internal bool Applies(IntentionContext context) =>
        ConceptKey is null
        || (context.Node is { } node && node.Concept.IsKindOf(ConceptKey));
}