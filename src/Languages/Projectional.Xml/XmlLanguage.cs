using Projectional.Core;
using Projectional.Language;

namespace Projectional.Xml;

/// <summary>XML as a language: six concepts, one per kind of thing a document is made of.</summary>
/// <remarks>
/// <para>
/// A reference language (docs/architecture/import.md). It references <see cref="Projectional.Language"/> only, as the
/// pilot's languages do, and its model mirrors the file: an element's attributes and children are in the order the file
/// has them, and whitespace-only text is a node, because in XML whitespace between markup is content.
/// </para>
/// <para>
/// <b>Text and attribute values are held in the file's spelling, escaped.</b> <c>Xml.Text.text</c> is what sat between
/// the markup, with <c>&amp;amp;</c> still written as <c>&amp;amp;</c>; the importer decodes the parser's value and escapes
/// it again, canonically, so the model holds exactly what generation must emit. The alternative - holding the decoded
/// value - would need a template part that escapes as XML, and a format's spelling inside the framework's template
/// vocabulary is what the reference languages exist to avoid. The cost is visible and is the same one the JSON language
/// pays: a user typing into a text cell writes the spelling the file would have.
/// </para>
/// </remarks>
public static class XmlLanguage
{
    /// <summary>The slot of an element that holds its attributes.</summary>
    public const string AttributesSlot = "attributes";

    /// <summary>The slot of an element that holds its content.</summary>
    public const string ChildrenSlot = "children";

    /// <summary>The name of an element or an attribute.</summary>
    public const string NameSlot = "name";

    /// <summary>The value of an attribute, or the character data of a text node.</summary>
    public const string ValueSlot = "value";

    /// <summary>The character data of a text node or a comment, or the source of a raw node.</summary>
    public const string TextSlot = "text";

    /// <summary>The concepts of this language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            XmlConceptKeys.Node,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything an element can hold."),

        new ConceptDeclaration(
            XmlConceptKeys.Element,
            [
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "element",
                    "The element's name as the file spells it, a namespace prefix included.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(AttributesSlot, XmlConceptKeys.Attribute, Cardinality.Many),
                new ChildSlot(ChildrenSlot, XmlConceptKeys.Node, Cardinality.Many),
            ],
            [XmlConceptKeys.Node],
            nameSlot: NameSlot,
            description: "An element: a name, its attributes, and what it holds."),

        new ConceptDeclaration(
            XmlConceptKeys.Attribute,
            [
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "name",
                    "The attribute's name as the file spells it, a namespace prefix included.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    ValueSlot,
                    PropertyType.String,
                    description: "The value, escaped as the file writes it.",
                    showDescriptionWhenFilled: true),
            ],
            nameSlot: NameSlot,
            description: "One attribute of an element."),

        new ConceptDeclaration(
            XmlConceptKeys.Text,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "The character data, escaped as the file spells it. Whitespace counts.",
                showDescriptionWhenFilled: true)],
            [XmlConceptKeys.Node],
            nameSlot: TextSlot,
            description: "Character data between the markup."),

        new ConceptDeclaration(
            XmlConceptKeys.Comment,
            [new PropertySlot(TextSlot, PropertyType.String, description: "What the comment says.")],
            [XmlConceptKeys.Node],
            nameSlot: TextSlot,
            description: "A comment, kept where it was written."),

        new ConceptDeclaration(
            XmlConceptKeys.Raw,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "A construct this language does not model, kept exactly as written.")],
            [XmlConceptKeys.Node],
            nameSlot: TextSlot,
            description: "A CDATA section or a processing instruction, kept as text."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>The importer that turns an XML string into a document of this language.</summary>
    public static ITextImporter Importer { get; } = new XmlImporter();

    /// <summary>The notation registry of this language.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations(Concepts);

    /// <summary>A registry for these concepts, with this language's notation.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        XmlNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project in which an XML document can be written.</summary>
    public static Project NewProject() => new(Concepts);
}
