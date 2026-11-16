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
    /// <summary>The slot holding the concepts of a language.</summary>
    public const string ConceptSlot = "concepts";

    /// <summary>The slot holding the enumerations a property may be declared over.</summary>
    public const string EnumerationSlot = "enumerations";

    /// <summary>The slot naming a concept or an enumeration: the key other concepts refer to it by.</summary>
    public const string KeySlot = "key";

    /// <summary>What a concept declares: the super types it extends.</summary>
    public const string SuperTypeSlot = "supertypes";

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

    /// <summary>The concepts of the structure language itself.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            StructureConceptKeys.Language,
            [
                new ChildSlot(ConceptSlot, StructureConceptKeys.Concept, Cardinality.Many),
                new ChildSlot(EnumerationSlot, StructureConceptKeys.Enumeration, Cardinality.Many),
            ],
            description: "One language definition: the concepts it declares and the symbols its properties accept."),

        new ConceptDeclaration(
            StructureConceptKeys.Concept,
            [
                new PropertySlot(KeySlot, PropertyType.String),
                new PropertySlot(IsAbstractProperty, PropertyType.Boolean, "false"),
                new PropertySlot("isOfferedInCompletion", PropertyType.Boolean, "true"),
                new PropertySlot("description", PropertyType.String),
                new PropertySlot("nameSlot", PropertyType.String),
                new PropertySlot("collapseSlot", PropertyType.String),
                new ChildSlot(SuperTypeSlot, StructureConceptKeys.SuperType, Cardinality.Many),

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
            [new PropertySlot("conceptKey", PropertyType.String)],
            nameSlot: "conceptKey",
            description: "A super concept, named by key."),

        new ConceptDeclaration(
            StructureConceptKeys.Slot,
            [new PropertySlot("name", PropertyType.String)],
            nameSlot: "name",
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What a concept holds, as one of the three kinds below."),

        new ConceptDeclaration(
            StructureConceptKeys.PropertyDecl,
            [
                new PropertySlot("typeName", PropertyType.String),
                new PropertySlot("defaultValue", PropertyType.String),
                new PropertySlot("description", PropertyType.String),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot holding one primitive value: a built-in type name or a declared enumeration."),

        new ConceptDeclaration(
            StructureConceptKeys.ChildDecl,
            [
                new PropertySlot("childConceptKey", PropertyType.String),
                new PropertySlot("cardinality", PropertyType.Enum(["one", "many"]), "one"),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot holding children of a declared concept."),

        new ConceptDeclaration(
            StructureConceptKeys.ReferenceDecl,
            [
                new PropertySlot("targetConceptKey", PropertyType.String),
                new PropertySlot("allowOwnSubtree", PropertyType.Boolean, "false"),
            ],
            [StructureConceptKeys.Slot],
            description: "A slot pointing at a node owned elsewhere."),

        new ConceptDeclaration(
            StructureConceptKeys.Enumeration,
            [
                new PropertySlot(KeySlot, PropertyType.String),
                new ChildSlot("symbols", StructureConceptKeys.EnumSymbol, Cardinality.Many),
            ],
            nameSlot: KeySlot,
            description: "A named set of symbols, which a property can be declared over."),

        new ConceptDeclaration(
            StructureConceptKeys.EnumSymbol,
            [new PropertySlot("symbol", PropertyType.String)],
            nameSlot: "symbol",
            description: "One symbol of an enumeration."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a language definition can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model of the structure language, ready for one definition.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "structure") => NewProject().NewModel(name);
}
