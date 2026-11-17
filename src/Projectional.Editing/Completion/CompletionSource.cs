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
            if (!concept.IsAbstract
                && concept.IsOfferedInCompletion
                && concept.IsKindOf(declared)
                && concept.Applicability.Allows(owner.Concept, slot))
            {
                items.Add(new CompletionItem(concept.Key, CompletionKind.Node, concept.Description));
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

        if (slot.Reference is { } reference)
        {
            return References(context, slot.Owner, reference.Slot);
        }

        if (slot.Property is { } property && Values(property) is { } values)
        {
            return values;
        }

        // A filled slot offers what could stand in the child's place instead. It is the one list whose
        // accepting is destructive, so CompletionSlot decides whether the child is worth protecting - a
        // node with a subtree is never offered.
        return slot.Replace is { } replace
            ? Replacements(context, replace)
            : Open(context, slot.Owner, slot.Slot);
    }

    /// <summary>
    /// The concepts that could take the place of <paramref name="node"/>, or <see langword="null"/> when
    /// nothing else could stand there.
    /// </summary>
    /// <remarks>
    /// The candidates come from <see cref="ReplacementConcepts"/>, which the intention catalogue's
    /// "replace with" entry uses too - one answer to "what can stand here", so a list and a menu cannot
    /// offer different concepts for one place.
    /// </remarks>
    internal static CompletionList? Replacements(EditContext context, Node node)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(node);

        List<CompletionItem> items = [];

        foreach (var concept in ReplacementConcepts.Of(node, context.Concepts))
        {
            items.Add(new CompletionItem(concept.Key, CompletionKind.Node, concept.Description));
        }

        return items.Count == 0 ? null : new CompletionList(items);
    }

    /// <summary>
    /// The things the reference in <paramref name="slot"/> of <paramref name="owner"/> may point at, or
    /// <see langword="null"/> when the scope holds nothing that fits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scope is the aspect's answer, so this is the same list a typed name resolves against - one
    /// implementation, two consumers. Entries are filtered by the slot's declared concept, because a scope
    /// describes what is <em>visible</em> and the slot says what is <em>legal</em>; offering a visible node
    /// the slot refuses would be a list whose entries cannot be accepted.
    /// </para>
    /// <para>
    /// A name the scope resolves ambiguously is offered once per candidate: the list is where the user sees
    /// that two things are called the same, and picking one is exactly the decision the model refuses to
    /// make for them.
    /// </para>
    /// </remarks>
    internal static CompletionList? References(EditContext context, Node owner, string slot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (owner.Concept.FindReference(slot) is not { } reference)
        {
            return null;
        }

        List<CompletionItem> items = [];

        foreach (var entry in context.Scopes.ScopeOf(owner, slot).Entries)
        {
            if (entry.Fits(reference.TargetConceptKey, context.Concepts))
            {
                items.Add(CompletionItem.ForReference(entry.Name));
            }
        }

        return items.Count == 0 ? null : new CompletionList(items);
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
