using Projectional.Core;
using Projectional.Language;

namespace Projectional.TextGen;

/// <summary>
/// The language a text generator is written in: concepts whose nodes <em>are</em> a generator.
/// </summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - because that is what makes the two authoring
/// paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal. The C# twin and the
/// lowering are the two halves: <c>Projectional.Samples.RulesTextGenerators</c> builds these generators by hand, and
/// <see cref="TextGenLowering"/> builds the same ones from a model.
/// </para>
/// <para>
/// <b>A document holds many generators.</b> MPS puts a generation plan in front of them; this framework stops at the
/// generators, so the host picks one by name. That is the smaller and more honest answer while nothing here needs
/// generation phases or a reference-resolution stage: an author who wants two targets writes two generators, not two
/// language projects.
/// </para>
/// </remarks>
public static class TextGenLanguage
{
    /// <summary>The slot holding a generator's mappings.</summary>
    public const string MappingsSlot = "mappings";

    /// <summary>The slot holding a mapping's template.</summary>
    public const string TemplateSlot = "template";

    /// <summary>The slot holding a template's parts.</summary>
    public const string PartsSlot = "parts";

    /// <summary>The slot holding what a list part or a loop puts between two children.</summary>
    public const string SeparatorSlot = "separator";

    /// <summary>The slot holding the body of an indent or a loop, or the selected template of a conditional.</summary>
    public const string BodySlot = "body";

    /// <summary>The slot holding what a conditional emits when its property does not match.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>A generator's name, unique within the document.</summary>
    public const string NameSlot = "name";

    /// <summary>The concept a mapping is about, or the slot a part reads.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The text a literal emits.</summary>
    public const string TextSlot = "text";

    /// <summary>The value a conditional matches.</summary>
    public const string ValueSlot = "value";

    /// <summary>The concept declarations of the language.</summary>
    /// <remarks>
    /// A generator is a document of its own, so a language's generator aspect is a folder with one generator per
    /// file - the same shape the other definition languages use since one root per document became the rule.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            TextGenConceptKeys.Generator,
            [
                new PropertySlot(NameSlot, PropertyType.String, description: "The name a host picks this generator by, such as Json."),
                new ChildSlot(MappingsSlot, TextGenConceptKeys.Mapping, Cardinality.Many),
            ],
            nameSlot: NameSlot,
            description: "One way to turn this language's model into text."),

        new ConceptDeclaration(
            TextGenConceptKeys.Mapping,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The concept whose text this is."),
                new ChildSlot(TemplateSlot, TextGenConceptKeys.Template, Cardinality.One),
            ],
            nameSlot: ConceptKeySlot,
            description: "What one concept emits."),

        new ConceptDeclaration(
            TextGenConceptKeys.Template,
            [new ChildSlot(PartsSlot, TextGenConceptKeys.Part, Cardinality.Many)],
            description: "An ordered list of parts; an empty one means this concept emits nothing."),

        new ConceptDeclaration(
            TextGenConceptKeys.Part,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "One piece of a template, as one of the kinds below."),

        new ConceptDeclaration(
            TextGenConceptKeys.Text,
            [new PropertySlot(TextSlot, PropertyType.String, description: "The text, exactly as written.")],
            [TextGenConceptKeys.Part],
            nameSlot: TextSlot,
            description: "Literal text, such as a keyword or punctuation."),

        new ConceptDeclaration(
            TextGenConceptKeys.Property,
            [new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The property to emit.")],
            [TextGenConceptKeys.Part],
            nameSlot: ConceptKeySlot,
            description: "A property's value, emitted as it is stored."),

        new ConceptDeclaration(
            TextGenConceptKeys.Child,
            [new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The child slot to expand.")],
            [TextGenConceptKeys.Part],
            nameSlot: ConceptKeySlot,
            description: "The generated text of the single child in a slot."),

        new ConceptDeclaration(
            TextGenConceptKeys.Children,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The list slot to expand."),
                new ChildSlot(SeparatorSlot, TextGenConceptKeys.Template, Cardinality.One),
            ],
            [TextGenConceptKeys.Part],
            nameSlot: ConceptKeySlot,
            description: "Every child of a list slot, in order, separated by a template."),

        new ConceptDeclaration(
            TextGenConceptKeys.Loop,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The list slot to walk."),
                new ChildSlot(BodySlot, TextGenConceptKeys.Template, Cardinality.One),
                new ChildSlot(SeparatorSlot, TextGenConceptKeys.Template, Cardinality.One),
            ],
            [TextGenConceptKeys.Part],
            nameSlot: ConceptKeySlot,
            description: "One repetition per child, with the body applied to that child."),

        new ConceptDeclaration(
            TextGenConceptKeys.Indent,
            [new ChildSlot(BodySlot, TextGenConceptKeys.Template, Cardinality.One)],
            [TextGenConceptKeys.Part],
            description: "A template emitted one indentation level deeper."),

        new ConceptDeclaration(
            TextGenConceptKeys.Line,
            [],
            [TextGenConceptKeys.Part],
            description: "A line break, followed by the indentation of the current level."),

        new ConceptDeclaration(
            TextGenConceptKeys.Conditional,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The property to read."),
                new PropertySlot(ValueSlot, PropertyType.String, description: "The value that selects the body."),
                new ChildSlot(BodySlot, TextGenConceptKeys.Template, Cardinality.One),
                new ChildSlot(OtherwiseSlot, TextGenConceptKeys.Template, Cardinality.One),
            ],
            [TextGenConceptKeys.Part],
            nameSlot: ConceptKeySlot,
            description: "A choice between two templates, decided by a property's value."),

        new ConceptDeclaration(
            TextGenConceptKeys.Self,
            [],
            [TextGenConceptKeys.Part],
            description: "The node's own template, applied to itself - what a loop body uses to place the child."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a generator can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty generator, ready to be written.</summary>
    public static Model NewModel(string name = "generator")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(TextGenConceptKeys.Generator);

        return model;
    }
}
