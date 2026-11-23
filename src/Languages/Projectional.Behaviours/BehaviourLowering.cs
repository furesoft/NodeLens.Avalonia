using Projectional.Core;
using Projectional.Language;

namespace Projectional.Behaviours;

/// <summary>Turns behaviour documents into the delegates a concept declaration takes.</summary>
/// <remarks>
/// <para>
/// The lowering answers a map rather than installing anything, because a name must be part of the concept registry and
/// the registry is built after every folder is lowered: the caller lowers the <c>behaviour</c> folder, then builds the
/// registry over declarations carrying the answers (docs/architecture/behaviours.md).
/// </para>
/// <para>
/// A refusal is a <see cref="ConceptDefinitionException"/>, the one type every aspect lowering in this repository
/// throws and the one <c>LanguageProject</c> turns into the message it reports. Every refusal is a body that could not
/// answer what its author meant, and each is caught while the language loads rather than at the first reference cell
/// that asks: a slot the concept does not have or one of the wrong kind, a <c>child</c> part naming a list (which would
/// answer its first element), and a <c>choose</c> whose value the property would store differently from how it was
/// written (which would answer the otherwise arm for ever). Each message names the concept, and the slot where there is
/// one, because that is what the author has to change.
/// </para>
/// </remarks>
public static class BehaviourLowering
{
    /// <summary>Lowers <paramref name="documents"/> against <paramref name="concepts"/>.</summary>
    /// <param name="documents">The behaviour documents, one per concept.</param>
    /// <param name="concepts">The concepts the names will be attached to, which is what the slots are checked against.</param>
    /// <returns>The name function of every concept that declares one, by concept key.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// A document names no concept, a concept the language does not have, a function that is not lowered yet, a concept
    /// twice, a body part naming a slot that does not exist or has the wrong kind, a <c>child</c> part naming a list,
    /// or a <c>choose</c> whose value the property cannot store in that spelling.
    /// </exception>
    public static IReadOnlyDictionary<string, Func<Node, string?>> Lower(
        IEnumerable<Node> documents,
        ConceptRegistry concepts)
    {
        Dictionary<string, Func<Node, string?>> names = new(StringComparer.Ordinal);

        foreach (var (key, body) in Bodies(documents, concepts))
        {
            names[key] = body.ToFunction();
        }

        return names;
    }

    /// <summary>The body each concept's name is written by, by concept key.</summary>
    /// <param name="documents">The behaviour documents, one per concept.</param>
    /// <param name="concepts">The concepts the names will be attached to, which is what the slots are checked against.</param>
    /// <returns>The body of every concept that declares one, by concept key.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// A document names no concept, a concept the language does not have, a function that is not lowered yet, a concept
    /// twice, a body part naming a slot that does not exist or has the wrong kind, a <c>child</c> part naming a list,
    /// or a <c>choose</c> whose value the property cannot store in that spelling.
    /// </exception>
    /// <remarks>
    /// The records rather than the delegates, because a delegate cannot be written out again: an export turns a folder
    /// into source, and the vocabulary is the one form of a name that both runs and can be spelled as C#
    /// (<c>Projectional.LanguageExport</c>).
    /// </remarks>
    public static IReadOnlyDictionary<string, NameBody> Bodies(
        IEnumerable<Node> documents,
        ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        Dictionary<string, NameBody> bodies = new(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            var key = document.GetOrNull(BehaviourConceptKeys.ConceptSlot);

            if (key is not { Length: > 0 })
            {
                throw new ConceptDefinitionException("a behaviour document must name the concept it is for.");
            }

            if (!concepts.TryGet(key, out var concept))
            {
                throw new ConceptDefinitionException($"'{key}' is not a concept of this language.");
            }

            foreach (var function in document.GetChildren(BehaviourConceptKeys.FunctionsSlot))
            {
                var kind = function.GetOrNull(BehaviourConceptKeys.KindSlot) ?? BehaviourConceptKeys.NameKind;

                if (!string.Equals(kind, BehaviourConceptKeys.NameKind, StringComparison.Ordinal))
                {
                    throw new ConceptDefinitionException(
                        $"'{key}' declares a '{kind}' function, and only '{BehaviourConceptKeys.NameKind}' is lowered yet.");
                }

                if (bodies.ContainsKey(key))
                {
                    throw new ConceptDefinitionException($"'{key}' declares more than one name function.");
                }

                var body = function.GetChild(BehaviourConceptKeys.BodySlot)
                    ?? throw new ConceptDefinitionException($"the name function of '{key}' must say what the node is called.");

                bodies[key] = Part(body, concept);
            }
        }

        return bodies;
    }

    /// <summary>One part of a body, as the vocabulary record it lowers to.</summary>
    private static NameBody Part(Node part, Concept concept) => part.Concept.Key switch
    {
        BehaviourConceptKeys.Literal => new NameLiteral(part.GetOrNull(BehaviourConceptKeys.TextSlot) ?? string.Empty),
        BehaviourConceptKeys.Property => new NameProperty(Slot<PropertySlot>(part, concept).Name),
        BehaviourConceptKeys.Child => new NameChild(SingleChild(Slot<ChildSlot>(part, concept), concept).Name),
        BehaviourConceptKeys.Reference => new NameReference(Slot<ReferenceSlot>(part, concept).Name),
        BehaviourConceptKeys.ResolveInfo => new NameResolveInfo(Slot<ReferenceSlot>(part, concept).Name),
        BehaviourConceptKeys.Join => new NameJoin(
            part.GetOrNull(BehaviourConceptKeys.SeparatorSlot) ?? " ",
            [.. part.GetChildren(BehaviourConceptKeys.PartsSlot).Select(each => Part(each, concept))]),
        BehaviourConceptKeys.Fallback => new NameFallback(
            [.. part.GetChildren(BehaviourConceptKeys.PartsSlot).Select(each => Part(each, concept))]),
        BehaviourConceptKeys.Children => new NameChildren(
            Slot<ChildSlot>(part, concept).Name,
            part.GetOrNull(BehaviourConceptKeys.SeparatorSlot) ?? ", "),
        BehaviourConceptKeys.WhenFilled => new NameWhenFilled(
            AnySlot(part, concept).Name,
            Part(Required(part, BehaviourConceptKeys.PartSlot), concept)),
        BehaviourConceptKeys.Choose => Choice(part, concept),
        _ => throw new ConceptDefinitionException($"'{part.Concept.Key}' is not a body part."),
    };

    /// <summary>One of two parts, by the value a property stores.</summary>
    private static NameChoice Choice(Node part, Concept concept)
    {
        var slot = Slot<PropertySlot>(part, concept);

        return new NameChoice(
            slot.Name,
            Value(part, slot),
            Part(Required(part, BehaviourConceptKeys.ThenSlot), concept),
            Part(Required(part, BehaviourConceptKeys.OtherwiseSlot), concept));
    }

    /// <summary>The slot a part reads, refused unless the concept declares it as the kind the part needs.</summary>
    private static T Slot<T>(Node part, Concept concept)
        where T : SlotDeclaration
    {
        var name = part.GetOrNull(BehaviourConceptKeys.SlotSlot);

        if (name is not { Length: > 0 })
        {
            throw new ConceptDefinitionException($"a body part of '{concept.Key}' must name the slot it reads.");
        }

        if (!concept.TryGetSlot(name, out var slot) || slot is not T typed)
        {
            throw new ConceptDefinitionException($"'{name}' is not a {Word(typeof(T))} of {concept.Key}.");
        }

        return typed;
    }

    /// <summary>The slot a part asks about, of any kind, refused unless the concept declares it.</summary>
    private static SlotDeclaration AnySlot(Node part, Concept concept)
    {
        var name = part.GetOrNull(BehaviourConceptKeys.SlotSlot);

        if (name is not { Length: > 0 })
        {
            throw new ConceptDefinitionException($"a body part of '{concept.Key}' must name the slot it reads.");
        }

        if (!concept.TryGetSlot(name, out var slot))
        {
            throw new ConceptDefinitionException($"'{name}' is not a slot of {concept.Key}.");
        }

        return slot;
    }

    /// <summary>
    /// The child slot a <c>child</c> part names, refused when it holds a list.
    /// </summary>
    /// <remarks>
    /// A list has no single name: reading the slot would silently answer with the first element, which is a name the
    /// author did not ask for and cannot see in the document. Refusing it at the load says so once instead of producing
    /// the wrong word at every reference cell.
    /// </remarks>
    private static ChildSlot SingleChild(ChildSlot slot, Concept concept) =>
        slot.Cardinality == Cardinality.One
            ? slot
            : throw new ConceptDefinitionException(
                $"'{slot.Name}' of {concept.Key} holds a list of children, and a 'child' part names the one child in a slot; a list has no single name.");

    /// <summary>
    /// The value a choice compares against, refused unless the property can hold it in exactly that spelling.
    /// </summary>
    /// <remarks>
    /// A choice compares what the property <em>stores</em>, which is the canonical spelling its type writes, so a value
    /// the type refuses - or one it would store differently, <c>01</c> for a number - would answer the otherwise arm for
    /// ever without anything being visibly wrong. That is the kind of mistake a language should not let an author keep.
    /// </remarks>
    private static string Value(Node part, PropertySlot slot)
    {
        var text = part.GetOrNull(BehaviourConceptKeys.ValueSlot);

        if (text is not { Length: > 0 })
        {
            throw new ConceptDefinitionException(
                $"a 'choose' on '{slot.Name}' must name the value that picks the then part.");
        }

        if (!slot.Type.TryParse(text, out var parsed))
        {
            throw new ConceptDefinitionException($"'{text}' is not a value of '{slot.Type.Name}', the type of '{slot.Name}'.");
        }

        var stored = slot.Type.Format(parsed!);

        return string.Equals(stored, text, StringComparison.Ordinal)
            ? text
            : throw new ConceptDefinitionException(
                $"'{text}' is a '{slot.Type.Name}' value, but '{slot.Name}' stores it as '{stored}', and a choice compares what is stored.");
    }

    private static string Word(Type kind) =>
        kind == typeof(PropertySlot) ? "property" : kind == typeof(ChildSlot) ? "child" : "reference";

    private static Node Required(Node part, string slot) =>
        part.GetChild(slot) ?? throw new ConceptDefinitionException($"a '{part.Concept.Key}' needs its '{slot}' part.");
}
