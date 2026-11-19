using Projectional.Core;
using Projectional.Language;

namespace Projectional.Yaml;

/// <summary>YAML as a language: block and flow collections, three scalar spellings, aliases and comments.</summary>
/// <remarks>
/// <para>
/// A reference language (docs/architecture/import.md). YAML is the format whose layout *is* part of its syntax, so this
/// language keeps the two collections apart by concept rather than by a flag: a block mapping is one
/// <c>key: value</c> per line and a flow mapping is braces, so each has its own notation and its own template, and a
/// flow collection's slots are typed over <see cref="YamlConceptKeys.FlowValue"/> because YAML forbids a block
/// collection inside one.
/// </para>
/// <para>
/// <b>One document is one root.</b> <c>Yaml.Document</c> holds the file's lines, because a YAML file is a stream: a
/// note above a mapping, a <c>---</c> marker and the node itself are three entries of one document, and a host projects
/// one root. That is also what keeps the notes the file wrote - a comment is an entry, not a discarding detail.
/// </para>
/// <para>
/// <b>A scalar keeps the whole spelling.</b> <c>Yaml.Scalar.text</c> is the text as the file wrote it - quotes, escapes,
/// an anchor, a tag, a block scalar's indicator and its body - so a template emits it verbatim and the round trip needs
/// no escaping part. That is the same decision JSON, XML, TOML and HOCON make, taken one step further because YAML has
/// more ways to spell one value than any of them.
/// </para>
/// <para>
/// <b>A pair says where its value stands.</b> A block collection cannot be written on its key's line, and a template
/// cannot ask what kind of node its child is, so <c>Yaml.Pair.layout</c> records whether the value was written inline or
/// on the next line - a fact of the file, like TOML's table-versus-inline distinction.
/// </para>
/// <para>
/// <b>Anchored collections are raw.</b> An anchor or a tag in front of a block or flow collection keeps the whole node as
/// <see cref="YamlConceptKeys.Raw"/> text, which is this language's tier 2: the file survives and the construct is not
/// pretended to be modelled. An anchor or tag in front of a scalar stays part of the scalar's spelling, because there it
/// is one run of text.
/// </para>
/// </remarks>
public static class YamlLanguage
{
    /// <summary>The slot of a document that holds its lines.</summary>
    public const string EntriesSlot = "entries";

    /// <summary>The slot of a block or flow mapping that holds its members.</summary>
    public const string MembersSlot = "members";

    /// <summary>The slot of a block sequence that holds its items.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The slot of a sequence item that holds the node after the dash.</summary>
    public const string ValueSlot = "value";

    /// <summary>The slot of a pair that holds its key.</summary>
    public const string KeySlot = "key";

    /// <summary>A scalar's whole spelling, or a raw construct's text.</summary>
    public const string TextSlot = "text";

    /// <summary>The name an alias refers to.</summary>
    public const string NameSlot = "name";

    /// <summary>Where a pair's value stands.</summary>
    public const string LayoutSlot = "layout";

    /// <summary>The value is written on the key's own line.</summary>
    public const string Inline = "inline";

    /// <summary>The value is written on the line below, which a block collection requires.</summary>
    public const string Nested = "nested";

    /// <summary>The two placements a value can have.</summary>
    public static PropertyType Layouts { get; } = PropertyType.Enum(Inline, Nested);

    /// <summary>The concepts of this language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            YamlConceptKeys.Node,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything that is a YAML node."),

        new ConceptDeclaration(
            YamlConceptKeys.Entry,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything a block sequence item or a document root can be."),

        new ConceptDeclaration(
            YamlConceptKeys.Member,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything one line of a block mapping can be."),

        new ConceptDeclaration(
            YamlConceptKeys.FlowValue,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything that may stand inside a flow collection, where no block collection may."),

        new ConceptDeclaration(
            YamlConceptKeys.Document,
            [new ChildSlot(EntriesSlot, YamlConceptKeys.Entry, Cardinality.Many)],
            description: "A YAML file: its notes, its markers and its node, in the order the file has them."),

        new ConceptDeclaration(
            YamlConceptKeys.Scalar,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "The scalar exactly as the file wrote it: quotes, escapes, an anchor, a tag, a block body.",
                showDescriptionWhenFilled: true)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry, YamlConceptKeys.FlowValue],
            nameSlot: TextSlot,
            description: "A scalar, in whatever spelling the file used."),

        new ConceptDeclaration(
            YamlConceptKeys.Alias,
            [new PropertySlot(
                NameSlot,
                PropertyType.String,
                "name",
                "The anchor this alias refers to. It is kept as text and never resolved.",
                showDescriptionWhenFilled: true)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry, YamlConceptKeys.FlowValue],
            nameSlot: NameSlot,
            description: "A reference to an anchor: it is never followed."),

        new ConceptDeclaration(
            YamlConceptKeys.Comment,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "What the note says, without the hash that introduces it.",
                showDescriptionWhenFilled: true)],
            [YamlConceptKeys.Member, YamlConceptKeys.Entry],
            nameSlot: TextSlot,
            description: "A note, kept exactly where the file had it."),

        new ConceptDeclaration(
            YamlConceptKeys.Raw,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "A construct this language does not model, kept exactly as written.",
                showDescriptionWhenFilled: true)],
            [YamlConceptKeys.Node, YamlConceptKeys.Member, YamlConceptKeys.Entry],
            nameSlot: TextSlot,
            description: "A directive, a document marker, a complex key - or an anchored collection, kept verbatim."),

        new ConceptDeclaration(
            YamlConceptKeys.Mapping,
            [new ChildSlot(MembersSlot, YamlConceptKeys.Member, Cardinality.Many)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry],
            description: "A block mapping: its members are one per line."),

        new ConceptDeclaration(
            YamlConceptKeys.Sequence,
            [new ChildSlot(ItemsSlot, YamlConceptKeys.Entry, Cardinality.Many)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry],
            description: "A block sequence: its items are one per line."),

        new ConceptDeclaration(
            YamlConceptKeys.SequenceItem,
            [new ChildSlot(ValueSlot, YamlConceptKeys.Node, Cardinality.One)],
            [YamlConceptKeys.Entry],
            description: "One item of a block sequence: the dash and the node it introduces."),

        new ConceptDeclaration(
            YamlConceptKeys.FlowMapping,
            [new ChildSlot(MembersSlot, YamlConceptKeys.FlowPair, Cardinality.Many)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry, YamlConceptKeys.FlowValue],
            description: "A mapping written between braces."),

        new ConceptDeclaration(
            YamlConceptKeys.FlowSequence,
            [new ChildSlot(ItemsSlot, YamlConceptKeys.FlowValue, Cardinality.Many)],
            [YamlConceptKeys.Node, YamlConceptKeys.Entry, YamlConceptKeys.FlowValue],
            description: "A sequence written between brackets."),

        new ConceptDeclaration(
            YamlConceptKeys.Pair,
            [
                new ChildSlot(KeySlot, YamlConceptKeys.Node, Cardinality.One),
                new ChildSlot(ValueSlot, YamlConceptKeys.Node, Cardinality.One),
                new PropertySlot(
                    LayoutSlot,
                    Layouts,
                    Inline,
                    "Whether the value is written on the key's line; a block collection needs the next one."),
            ],
            [YamlConceptKeys.Member],
            description: "One line of a block mapping: a key and the node it holds."),

        new ConceptDeclaration(
            YamlConceptKeys.FlowPair,
            [
                new ChildSlot(KeySlot, YamlConceptKeys.FlowValue, Cardinality.One),
                new ChildSlot(ValueSlot, YamlConceptKeys.FlowValue, Cardinality.One),
            ],
            description: "One member of a flow mapping."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>The importer that turns a YAML string into a document of this language.</summary>
    public static ITextImporter Importer { get; } = new YamlImporter();

    /// <summary>The notation registry of this language.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations(Concepts);

    /// <summary>A registry for these concepts, with this language's notation.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        YamlNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project in which a YAML document can be written.</summary>
    public static Project NewProject() => new(Concepts);
}
