using Projectional.Core;
using Projectional.Projection;

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

    /// <summary>
    /// An open list for <paramref name="slot"/>, whichever kind of slot it is, or <see langword="null"/>
    /// when it has nothing to offer.
    /// </summary>
    /// <remarks>
    /// The one entry point that turns a <see cref="CompletionSlot"/> into a list, so the list a host sees
    /// and the list the accepting action reads are built by the same question. A property slot is answered
    /// from its type; an empty slot from the concepts below the declared child concept.
    /// </remarks>
    internal static CompletionList? Open(EditContext context, CompletionSlot slot)
    {
        ArgumentNullException.ThrowIfNull(context);

        return slot.Property is { } property
            ? Values(property)
            : Open(context, slot.Owner, slot.Slot);
    }

    /// <summary>
    /// The values <paramref name="property"/> allows, in declaration order, or <see langword="null"/> when
    /// its type has no closed set of values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value the cell already holds is selected, so opening the list and accepting without choosing
    /// leaves the document alone. Without that, arriving in a <c>Review</c> cell and pressing Enter would
    /// silently turn it into the first symbol of the type - a completion that edits when the user asked
    /// for nothing.
    /// </para>
    /// <para>
    /// The list starts with an empty prefix, so it is complete: filtering happens when the user types, one
    /// <see cref="NarrowCompletionAction"/> at a time.
    /// </para>
    /// </remarks>
    internal static CompletionList? Values(PropertyCell property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var symbols = property.Type.Symbols;

        if (symbols.Count == 0)
        {
            return null;
        }

        List<CompletionItem> items = [];

        foreach (var symbol in symbols)
        {
            items.Add(CompletionItem.ForValue(symbol));
        }

        var list = new CompletionList(items);
        var current = IndexOf(symbols, property.Text);

        return current > 0 ? list.Move(current) : list;
    }

    private static int IndexOf(IReadOnlyList<string> symbols, string text)
    {
        for (var index = 0; index < symbols.Count; index++)
        {
            if (string.Equals(symbols[index], text, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
