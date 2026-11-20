using Projectional.Core;

namespace Projectional.Structures;

/// <summary>
/// The structure language as ordinary <see cref="ConceptDeclaration"/>s: the C# twin of a declared language.
/// </summary>
/// <remarks>
/// <para>
/// A language definition is a model, and a model needs a language. This is that language, written the way every
/// other one in the repository is written, which is what keeps the two authoring paths of
/// [ADR 0014](../../docs/adr/0014-both-authoring-paths-are-permanent.md) at the same standing: the structure
/// language is not a special case with its own syntax, it is declarations plus nodes.
/// </para>
/// <para>
/// The shape is the provisional one (slice P1). It names what a concept <em>is</em> and nothing about how it is
/// drawn, because presentation is the editor language and mixing the two is what makes a definition unreadable.
/// </para>
/// </remarks>
public static class StructureLanguage
{
    /// <summary>The slot naming a concept or an enumeration: the key other concepts refer to it by.</summary>
    public const string KeySlot = "key";

    /// <summary>What a concept declares: the super types it extends.</summary>
    public const string SuperTypeSlot = "supertypes";

    /// <summary>What a concept declares: the interfaces it implements.</summary>
    public const string ImplementsSlot = "implements";

    /// <summary>The slots holding one primitive value.</summary>
    public const string PropertiesSlot = "properties";

    /// <summary>The slots holding owned children.</summary>
    public const string ChildrenSlot = "children";

    /// <summary>The slots pointing at nodes owned elsewhere.</summary>
    public const string ReferencesSlot = "references";

    /// <summary>
    /// Whether a concept is abstract.
    /// </summary>
    /// <remarks>
    /// Named here rather than spelled out at each use because three packages read it - the declarations, the
    /// notation and the intentions - and a name typed three times is a name that can be typed two ways. It is a
    /// flag and not a word: the notation decides that an abstract concept <em>says</em> <c>abstract</c>, which is
    /// why the property holds a truth value and not the text.
    /// </remarks>
    public const string IsAbstractProperty = "isAbstract";

    /// <summary>
    /// Whether a concept is an interface.
    /// </summary>
    /// <remarks>
    /// A second flag rather than a value of <see cref="IsAbstractProperty"/>, because an interface <em>is</em>
    /// abstract - the two are joined by an implication, not by an enumeration - and because `isAbstract` is already
    /// a persisted property of every definition document. The lowering refuses a concept that is concrete and an
    /// interface at once, which is the combination nothing could mean.
    /// </remarks>
    public const string IsInterfaceProperty = "isInterface";

    /// <summary>The concepts of the structure language itself.</summary>
    /// <remarks>
    /// There is no container concept: a document's root <em>is</em> the definition it holds - one concept, or one
    /// enumeration - the way MPS stores one root per file. A language is the set of its documents, which is why
    /// nothing here knows how many definitions a project has.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            StructureConceptKeys.Concept,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    description: "The key every other definition, notation and reference names this concept by.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    IsAbstractProperty,
                    PropertyType.Boolean,
                    "false",
                    "Whether the concept can only be extended: an abstract concept is never a node.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    IsInterfaceProperty,
                    PropertyType.Boolean,
                    "false",
                    "Whether this concept is an interface: abstract by definition, and a source of members.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "isOfferedInCompletion",
                    PropertyType.Boolean,
                    "true",
                    "Whether completion lists the concept. A hidden one can still be created with Enter.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "description",
                    PropertyType.String,
                    description: "One sentence about what the concept is, shown on hover and beside its completion entry.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "nameSlot",
                    PropertyType.String,
                    description: "The name of the slot that holds this concept's name, or empty when it has none.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "collapseSlot",
                    PropertyType.String,
                    description: "The name of the boolean property that says whether a node may be folded.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "nameRule",
                    PropertyType.Enum([.. NameRule.All.Select(rule => rule.Text)]),
                    description: "How a name of this concept is spelled: any text, an identifier, or a case convention such as PascalCase or snake_case. Empty means inherit.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(SuperTypeSlot, StructureConceptKeys.SuperType, Cardinality.Many),

                // Two slots rather than one with a kind flag: the header writes two words, and a kind stored on
                // the reference would be a third spelling of the same fact.
                new ChildSlot(ImplementsSlot, StructureConceptKeys.SuperType, Cardinality.Many),

                // Three collections rather than one `slots`: it is how MPS writes a structure model, it makes the
                // three sections of the notation a fact about the model instead of a grouping the renderer invents,
                // and it means a child slot cannot be written where a property belongs.
                new ChildSlot(PropertiesSlot, StructureConceptKeys.PropertyDecl, Cardinality.Many),
                new ChildSlot(ChildrenSlot, StructureConceptKeys.ChildDecl, Cardinality.Many),
                new ChildSlot(ReferencesSlot, StructureConceptKeys.ReferenceDecl, Cardinality.Many),
            ],
            nameSlot: KeySlot,
            description: "One concept: what it is called, what it extends and what it holds."),

        new ConceptDeclaration(
            StructureConceptKeys.SuperType,
            [
                new PropertySlot(
                    "conceptKey",
                    PropertyType.String,
                    description: "The key of the concept this one extends.",
                    showDescriptionWhenFilled: true),
            ],
            nameSlot: "conceptKey",
            description: "A super concept, named by key."),

        new ConceptDeclaration(
            StructureConceptKeys.Slot,
            [
                new PropertySlot(
                    "name",
                    PropertyType.String,
                    description: "The slot's name, which is how the notation and the model address it.",
                    showDescriptionWhenFilled: true),
            ],
            nameSlot: "name",
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What a concept holds, as one of the three kinds below."),

        new ConceptDeclaration(
            StructureConceptKeys.PropertyDecl,
            [
                new PropertySlot(
                    "typeName",
                    PropertyType.String,
                    description: "The type of the value: string, int, float, bool, date, or the key of a declared enumeration.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "defaultValue",
                    PropertyType.String,
                    description: "The value a fresh node starts with. Empty when the slot has no default.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "description",
                    PropertyType.String,
                    description: "One sentence about what belongs in this slot, shown as a tooltip.",
                    showDescriptionWhenFilled: true),

                // The sentence a hover shows over a *filled* value, as a flag: the description alone is shown
                // while the slot is empty, and a language that wants it over its text says so here.
                new PropertySlot(
                    "showDescriptionWhenFilled",
                    PropertyType.Boolean,
                    "false",
                    "Whether that sentence is also shown on hover while the slot holds a value.",
                    showDescriptionWhenFilled: true),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot holding one primitive value: a built-in type name or a declared enumeration."),

        new ConceptDeclaration(
            StructureConceptKeys.ChildDecl,
            [
                new PropertySlot(
                    "childConceptKey",
                    PropertyType.String,
                    description: "The key of the concept a child of this slot has to conform to.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "cardinality",
                    PropertyType.Enum(["0..1", "0..n", "1", "1..n"]),
                    "1",
                    "How many children the slot holds: none or one, zero or more, exactly one, or one or more.",
                    showDescriptionWhenFilled: true),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot holding children of a declared concept."),

        new ConceptDeclaration(
            StructureConceptKeys.ReferenceDecl,
            [
                new PropertySlot(
                    "targetConceptKey",
                    PropertyType.String,
                    description: "The key of the concept this slot points at.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "allowOwnSubtree",
                    PropertyType.Boolean,
                    "false",
                    "Whether a node may point into its own subtree, which is a cycle and usually refused.",
                    showDescriptionWhenFilled: true),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot pointing at a node owned elsewhere."),

        new ConceptDeclaration(
            StructureConceptKeys.Enumeration,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    description: "The key a property names as its type.",
                    showDescriptionWhenFilled: true),
                new ChildSlot("symbols", StructureConceptKeys.EnumSymbol, Cardinality.Many),
            ],
            nameSlot: KeySlot,
            description: "A named set of symbols, which a property can be declared over."),

        new ConceptDeclaration(
            StructureConceptKeys.EnumSymbol,
            [
                new PropertySlot(
                    "symbol",
                    PropertyType.String,
                    description: "One symbol of the enumeration, in the order the values are offered.",
                    showDescriptionWhenFilled: true),
            ],
            nameSlot: "symbol",
            description: "One symbol of an enumeration."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a language definition can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model of the structure language, ready for one definition.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    /// <remarks>One definition per model: a document holds the concept or the enumeration it is named after.</remarks>
    public static Model NewModel(string name = "concept") => NewProject().NewModel(name);
}
