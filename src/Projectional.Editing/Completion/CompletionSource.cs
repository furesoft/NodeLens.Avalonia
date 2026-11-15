using Projectional.Core;

namespace Projectional.Editing;

/// <summary>
/// Works out what could fill a slot, from the concepts the language declares.
/// </summary>
/// <remarks>
/// <para>
/// Proposals are concept-driven, not word-driven: the slot names a child concept, and every concrete
/// concept below it is a candidate, in declaration order. That order is the language author's ranking,
/// so the default Enter would create without a list comes first here too.
/// </para>
/// <para>
/// A concept the language marks as <see cref="Concept.IsOfferedInCompletion"/> = <see langword="false"/>
/// is left out: the list is the menu the DSL author designed, not a dump of the concept set. Hiding one
/// changes nothing about the model - it stays a legal child of the slot - so a slot whose every concept
/// is hidden yields no proposal at all and is still filled by the ordinary Enter path.
/// </para>
/// <para>
/// The source never mutates anything and never caches across edits - it reads the registry it is handed
/// and answers with an item list, so a language that gains a concept needs no rebuild of the session.
/// </para>
/// </remarks>
internal static class CompletionSource
{
    /// <summary>
    /// The concepts that could fill <paramref name="slot"/> of <paramref name="owner"/>, or an empty list.
    /// </summary>
    /// <remarks>
    /// An abstract concept is excluded because no node can be an instance of it; a concept the language
    /// hides from completion is excluded because the language asked for that. The first rule is the same
    /// one <see cref="ConceptRegistry.DefaultInstantiableFor"/> applies. The second deliberately is not:
    /// hiding a concept from the menu must not also make it impossible to create, so Enter in an empty
    /// slot still reaches it.
    /// </remarks>
    internal static IReadOnlyList<CompletionItem> Proposals(
        ConceptRegistry concepts,
        Node owner,
        string slot)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        var declared = owner.Concept.FindChild(slot)?.ChildConceptKey;

        if (declared is null)
        {
            return [];
        }

        List<CompletionItem> items = [];

        foreach (var concept in concepts.Concepts)
        {
            if (!concept.IsAbstract && concept.IsOfferedInCompletion && concept.IsKindOf(declared))
            {
                items.Add(new CompletionItem(concept.Key, CompletionKind.Node));
            }
        }

        return items;
    }

    /// <summary>An open list for <paramref name="slot"/>, or <see langword="null"/> when nothing could fill it.</summary>
    internal static CompletionList? Open(EditContext context, Node owner, string slot)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = Proposals(context.Concepts, owner, slot);

        return items.Count == 0 ? null : new CompletionList(items);
    }
}
