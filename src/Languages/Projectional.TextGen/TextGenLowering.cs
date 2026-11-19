using Projectional.Core;
using Projectional.Language;

namespace Projectional.TextGen;

/// <summary>Turns a model of the generator language into the generators a host runs.</summary>
/// <remarks>
/// <para>
/// This is the half that makes the declared path equal rather than weaker (ADR 0014): every construct here becomes the
/// same <see cref="TemplatePart"/> a C# author writes, so there is no second engine and no declared-only feature.
/// </para>
/// <para>
/// It checks what a compiler would have checked - a generator has a name, two generators do not share one, a mapping
/// names a concept the language has, a part names a slot of that concept and names it as the kind it is - and it
/// deliberately does not check the graph: known concepts and duplicate keys are the concept registry's answers, not a
/// second validator's.
/// </para>
/// </remarks>
public static class TextGenLowering
{
    /// <summary>The generators a language's generator documents declare.</summary>
    /// <param name="generators">
    /// The roots of those documents: one <see cref="TextGenConceptKeys.Generator"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The concepts its mappings may name, which is the target language's registry.</param>
    /// <returns>The generators, in that order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A document cannot be lowered.</exception>
    public static IReadOnlyList<TextGenerator> CreateGenerators(IEnumerable<Node> generators, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(generators);
        ArgumentNullException.ThrowIfNull(concepts);

        var roots = generators as IReadOnlyList<Node> ?? [.. generators];

        List<TextGenerator> lowered = [];
        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (var generator in roots)
        {
            if (generator.Concept.Key != TextGenConceptKeys.Generator)
            {
                throw new ConceptDefinitionException(
                    $"'{generator.Concept.Key}' is not a generator; a generator document holds one '{TextGenConceptKeys.Generator}'.");
            }

            var name = NameOf(generator);

            if (!names.Add(name))
            {
                throw new ConceptDefinitionException(
                    $"Two generators are called '{name}'; a host picks one by name, so a name belongs to one generator.");
            }

            lowered.Add(new TextGenerator(name, CreateTemplates(generator, concepts)));
        }

        return lowered;
    }

    /// <summary>The templates of one generator, keyed by the concept each mapping names.</summary>
    private static TemplateRegistry CreateTemplates(Node generator, ConceptRegistry concepts)
    {
        var templates = TemplateRegistry.Empty;
        HashSet<string> mapped = new(StringComparer.Ordinal);

        foreach (var mapping in generator.GetChildren(TextGenLanguage.MappingsSlot))
        {
            var conceptKey = Required(mapping, TextGenLanguage.ConceptKeySlot, "A mapping names the concept it is about.");

            if (!concepts.TryGet(conceptKey, out var concept))
            {
                throw new ConceptDefinitionException(
                    $"Generator '{NameOf(generator)}' maps '{conceptKey}', which is not a concept of the language it generates for.");
            }

            if (!mapped.Add(conceptKey))
            {
                throw new ConceptDefinitionException(
                    $"Generator '{NameOf(generator)}' maps '{conceptKey}' twice; one concept emits one template.");
            }

            templates = templates.Add(conceptKey, CreateTemplate(Single(mapping, TextGenLanguage.TemplateSlot), concept));
        }

        return templates;
    }

    private static TextTemplate CreateTemplate(Node body, Concept target)
    {
        // "ignore" is the mapping that says the concept leaves no text behind. It is a separate concept rather than an
        // empty template because an author has to be able to say it on purpose: an empty box looks like unfinished
        // work, and a reader of the generator should see the decision.
        if (string.Equals(body.Concept.Key, TextGenConceptKeys.Ignore, StringComparison.Ordinal))
        {
            return TextTemplate.Empty;
        }

        List<TemplatePart> parts = [];

        foreach (var part in body.GetChildren(TextGenLanguage.PartsSlot))
        {
            parts.Add(CreatePart(part, target));
        }

        return new TextTemplate(parts);
    }

    private static TemplatePart CreatePart(Node part, Concept target) => part.Concept.Key switch
    {
        TextGenConceptKeys.Text => new TextPart(Required(part, TextGenLanguage.TextSlot, "A literal needs its text.")),

        TextGenConceptKeys.Property => new PropertyPart(
            PropertyOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A property part names the property."))),

        TextGenConceptKeys.Reference => new ReferencePart(
            ReferenceOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A reference part names the reference.")),
            part.GetOrNull(TextGenLanguage.UnsetTextSlot) ?? "<none>",
            part.GetOrNull(TextGenLanguage.UnresolvedTextSlot) ?? "<unresolved>"),

        TextGenConceptKeys.Child => new ChildPart(
            ChildSlotOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A child part names the slot."))),

        TextGenConceptKeys.Children => new ChildrenPart(
            ListSlotOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A list part names the slot.")),
            CreateTemplate(Single(part, TextGenLanguage.SeparatorSlot), target)),

        TextGenConceptKeys.Loop => new LoopPart(
            ListSlotOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A loop names the slot.")),
            CreateTemplate(Single(part, TextGenLanguage.BodySlot), target),
            CreateTemplate(Single(part, TextGenLanguage.SeparatorSlot), target)),

        TextGenConceptKeys.Indent => new IndentPart(CreateTemplate(Single(part, TextGenLanguage.BodySlot), target)),

        TextGenConceptKeys.Line => LineBreakPart.Instance,

        TextGenConceptKeys.Conditional => new ConditionalPart(
            PropertyOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A conditional names the property.")),
            Required(part, TextGenLanguage.ValueSlot, "A conditional needs the value it matches."),
            CreateTemplate(Single(part, TextGenLanguage.BodySlot), target),
            CreateTemplate(Single(part, TextGenLanguage.OtherwiseSlot), target)),

        TextGenConceptKeys.Self => SelfPart.Instance,

        TextGenConceptKeys.WhenFilled => new WhenFilledPart(
            SlotOf(part, target, Required(part, TextGenLanguage.ConceptKeySlot, "A filled-check names the slot.")),
            CreateTemplate(Single(part, TextGenLanguage.BodySlot), target),
            CreateTemplate(Single(part, TextGenLanguage.OtherwiseSlot), target)),

        _ => throw new ConceptDefinitionException(
            $"'{part.Concept.Key}' is not a template part this framework generates with."),
    };

    /// <summary>The property slot's name, checked against the concept the template is for.</summary>
    /// <remarks>
    /// The check is done here rather than in the engine so that a typo is a load error naming the concept and the slot,
    /// at the moment the author is looking at the file - which is the rule the notation registry follows too.
    /// </remarks>
    private static string PropertyOf(Node part, Concept target, string slot) =>
        target.FindProperty(slot) is not null
            ? slot
            : throw new ConceptDefinitionException($"'{slot}' is not a property of {target.Key}, so '{part.Concept.Key}' cannot emit it.");

    private static string ReferenceOf(Node part, Concept target, string slot) =>
        target.FindReference(slot) is not null
            ? slot
            : throw new ConceptDefinitionException($"'{slot}' is not a reference of {target.Key}, so '{part.Concept.Key}' cannot emit it.");

    private static string ChildSlotOf(Node part, Concept target, string slot) =>
        target.FindChild(slot) is { } child
            ? child.Cardinality == Cardinality.One
                ? slot
                : throw new ConceptDefinitionException(
                    $"'{slot}' of {target.Key} is a list, so '{part.Concept.Key}' cannot expand it as one child.")
            : throw new ConceptDefinitionException($"'{slot}' is not a child slot of {target.Key}.");

    private static string ListSlotOf(Node part, Concept target, string slot) =>
        target.FindChild(slot) is { } child
            ? child.Cardinality == Cardinality.Many
                ? slot
                : throw new ConceptDefinitionException(
                    $"'{slot}' of {target.Key} holds one child, so '{part.Concept.Key}' cannot walk it as a list.")
            : throw new ConceptDefinitionException($"'{slot}' is not a child slot of {target.Key}.");

    private static string NameOf(Node generator) =>
        Required(generator, TextGenLanguage.NameSlot, "A generator needs a name, because a host picks it by name.");

    /// <summary>The slot a filled-check asks about, which may be of any kind.</summary>
    /// <remarks>
    /// The one check that accepts all four kinds, because "is there anything here" is a question a property, a
    /// reference, a single child and a list can each answer. A name that belongs to no slot is still a load error -
    /// the same one a part that tried to emit the slot would raise, at the same moment.
    /// </remarks>
    private static string SlotOf(Node part, Concept target, string slot) =>
        target.FindProperty(slot) is null && target.FindReference(slot) is null && target.FindChild(slot) is null
            ? throw new ConceptDefinitionException(
                $"'{slot}' is not a slot of {target.Key}, so '{part.Concept.Key}' cannot ask whether it is filled.")
            : slot;

    private static string Required(Node node, string slot, string message) =>
        string.IsNullOrWhiteSpace(node.GetOrNull(slot)) ? throw new ConceptDefinitionException(message) : node.GetOrNull(slot)!;

    private static Node Single(Node node, string slot)
    {
        var children = node.GetChildren(slot);

        return children.Count == 1
            ? children[0]
            : throw new ConceptDefinitionException(
                $"'{slot}' of {node.Concept.Key} must hold exactly one child to be lowered, but holds {children.Count}.");
    }
}
