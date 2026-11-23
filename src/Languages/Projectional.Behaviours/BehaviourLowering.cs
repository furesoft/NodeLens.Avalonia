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
/// throws and the one <c>LanguageProject</c> turns into the message it reports: a body naming a slot the concept does
/// not have - or one of the wrong kind - is refused while the language loads rather than at the first reference cell
/// that asks. Each message names the concept, and the slot where there is one, because that is what the author has to
/// change.
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
    /// twice, or a body part whose slot does not exist or has the wrong kind.
    /// </exception>
    public static IReadOnlyDictionary<string, Func<Node, string?>> Lower(
        IEnumerable<Node> documents,
        ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        Dictionary<string, Func<Node, string?>> names = new(StringComparer.Ordinal);

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

                if (names.ContainsKey(key))
                {
                    throw new ConceptDefinitionException($"'{key}' declares more than one name function.");
                }

                var body = function.GetChild(BehaviourConceptKeys.BodySlot)
                    ?? throw new ConceptDefinitionException($"the name function of '{key}' must say what the node is called.");

                names[key] = Part(body, concept).ToFunction();
            }
        }

        return names;
    }

    /// <summary>One part of a body, as the vocabulary record it lowers to.</summary>
    private static NameBody Part(Node part, Concept concept) => part.Concept.Key switch
    {
        BehaviourConceptKeys.Literal => new NameLiteral(part.GetOrNull(BehaviourConceptKeys.TextSlot) ?? string.Empty),
        BehaviourConceptKeys.Property => new NameProperty(Slot(part, concept, typeof(PropertySlot))),
        BehaviourConceptKeys.Child => new NameChild(Slot(part, concept, typeof(ChildSlot))),
        BehaviourConceptKeys.Reference => new NameReference(Slot(part, concept, typeof(ReferenceSlot))),
        BehaviourConceptKeys.Join => new NameJoin(
            part.GetOrNull(BehaviourConceptKeys.SeparatorSlot) ?? " ",
            [.. part.GetChildren(BehaviourConceptKeys.PartsSlot).Select(each => Part(each, concept))]),
        BehaviourConceptKeys.Fallback => new NameFallback(
            [.. part.GetChildren(BehaviourConceptKeys.PartsSlot).Select(each => Part(each, concept))]),
        BehaviourConceptKeys.Choose => new NameChoice(
            Slot(part, concept, typeof(PropertySlot)),
            part.GetOrNull(BehaviourConceptKeys.ValueSlot) ?? string.Empty,
            Part(Required(part, BehaviourConceptKeys.ThenSlot), concept),
            Part(Required(part, BehaviourConceptKeys.OtherwiseSlot), concept)),
        _ => throw new ConceptDefinitionException($"'{part.Concept.Key}' is not a body part."),
    };

    /// <summary>The slot a part reads, refused unless the concept declares it as the kind the part needs.</summary>
    private static string Slot(Node part, Concept concept, Type kind)
    {
        var name = part.GetOrNull(BehaviourConceptKeys.SlotSlot);

        if (name is not { Length: > 0 })
        {
            throw new ConceptDefinitionException($"a body part of '{concept.Key}' must name the slot it reads.");
        }

        if (!concept.TryGetSlot(name, out var slot) || slot.GetType() != kind)
        {
            throw new ConceptDefinitionException($"'{name}' is not a {Word(kind)} of {concept.Key}.");
        }

        return name;
    }

    private static string Word(Type kind) =>
        kind == typeof(PropertySlot) ? "property" : kind == typeof(ChildSlot) ? "child" : "reference";

    private static Node Required(Node part, string slot) =>
        part.GetChild(slot) ?? throw new ConceptDefinitionException($"a '{part.Concept.Key}' needs its '{slot}' part.");
}
