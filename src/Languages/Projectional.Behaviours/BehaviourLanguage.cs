using Projectional.Core;
using Projectional.Language;

namespace Projectional.Behaviours;

/// <summary>The language a concept's behaviour is written in: one document per concept, one function per name.</summary>
/// <remarks>
/// <para>
/// The one definition language whose lowering must reach the concept registry rather than a registry beside it: a name is
/// asked by consumers that have no session (a reference cell, a sort), so it is a property of the concept
/// (docs/architecture/behaviours.md). The <c>behaviour</c> folder is read before the registry is built, which is why
/// <see cref="BehaviourLowering"/> answers the delegates rather than installing them.
/// </para>
/// <para>
/// The vocabulary is closed: a part reads a slot, names a child or a target, joins, falls back or chooses. There is no
/// arithmetic, no loop and no variable, because a name that needs one is a name that should be stored.
/// </para>
/// </remarks>
public static class BehaviourLanguage
{
    /// <summary>The name the shell and the documentation give this language.</summary>
    public const string Name = "Behaviours";

    private static PropertyType KindType { get; } = PropertyType.Enum([BehaviourConceptKeys.NameKind, BehaviourConceptKeys.DescribeKind]);

    /// <summary>The concepts of the language, in declaration order.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = Declarations();

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project whose language is <see cref="Concepts"/>.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model in <see cref="NewProject"/>, holding one document root.</summary>
    /// <param name="name">The document's model name, which is what the tab shows.</param>
    public static Model NewModel(string name = "behaviour")
    {
        var model = NewProject().NewModel(name);
        model.AddRoot(model.NewNode(BehaviourConceptKeys.Document));

        return model;
    }

    private static List<ConceptDeclaration> Declarations() =>
    [
        new ConceptDeclaration(
            BehaviourConceptKeys.Document,
            [
                new PropertySlot(
                    BehaviourConceptKeys.ConceptSlot,
                    PropertyType.String,
                    description: "The key of the concept this behaviour is for."),
                new ChildSlot(BehaviourConceptKeys.FunctionsSlot, BehaviourConceptKeys.Function, Cardinality.Many),
            ],
            nameSlot: BehaviourConceptKeys.ConceptSlot,
            isRoot: true,
            description: "One concept's behaviour: the concept it is for and the functions it declares."),

        new ConceptDeclaration(
            BehaviourConceptKeys.Function,
            [
                new PropertySlot(
                    BehaviourConceptKeys.KindSlot,
                    KindType,
                    BehaviourConceptKeys.NameKind,
                    "Which function this is: the name, or the sentence."),
                new ChildSlot(BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Part, Cardinality.One),
            ],
            isRoot: false,
            description: "One function: which of the closed set it is, and the body that answers it."),

        new ConceptDeclaration(
            BehaviourConceptKeys.Part,
            isAbstract: true,
            isRoot: false,
            description: "A part of a body: it reads a slot, names a node, joins, falls back or chooses."),

        new ConceptDeclaration(
            BehaviourConceptKeys.Literal,
            [new PropertySlot(BehaviourConceptKeys.TextSlot, PropertyType.String, description: "The fixed word.")],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "literal <text>"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Property,
            [new PropertySlot(BehaviourConceptKeys.SlotSlot, PropertyType.String, description: "The property to read.")],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "property <slot>"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Child,
            [new PropertySlot(BehaviourConceptKeys.SlotSlot, PropertyType.String, description: "The child slot to name.")],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "child <slot>"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Reference,
            [new PropertySlot(BehaviourConceptKeys.SlotSlot, PropertyType.String, description: "The reference slot to follow.")],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "reference <slot>"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Join,
            [
                new PropertySlot(
                    BehaviourConceptKeys.SeparatorSlot,
                    PropertyType.String,
                    " ",
                    "The text between two parts, empty ones left out."),
                new ChildSlot(BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Part, Cardinality.Many),
            ],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "join <separator> (<parts...>)"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Fallback,
            [new ChildSlot(BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Part, Cardinality.Many)],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "fallback (<parts...>)"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Children,
            [
                new PropertySlot(BehaviourConceptKeys.SlotSlot, PropertyType.String, description: "The child slot whose names to join."),
                new PropertySlot(BehaviourConceptKeys.SeparatorSlot, PropertyType.String, ", ", "The text between two names, empty ones left out."),
            ],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "children <slot> with <separator>"),

        new ConceptDeclaration(
            BehaviourConceptKeys.Choose,
            [
                new PropertySlot(BehaviourConceptKeys.SlotSlot, PropertyType.String, description: "The property to read."),
                new PropertySlot(BehaviourConceptKeys.ValueSlot, PropertyType.String, description: "The value that picks the then part."),
                new ChildSlot(BehaviourConceptKeys.ThenSlot, BehaviourConceptKeys.Part, Cardinality.One),
                new ChildSlot(BehaviourConceptKeys.OtherwiseSlot, BehaviourConceptKeys.Part, Cardinality.One),
            ],
            superConcepts: [BehaviourConceptKeys.Part],
            isRoot: false,
            description: "choose <slot> = <value> ? <then> : <otherwise>"),
    ];
}
