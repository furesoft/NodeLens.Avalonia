using Projectional.Core;
using Projectional.Language;

namespace Projectional.Json;

/// <summary>JSON as a language: eight concepts, one per thing a document can hold.</summary>
/// <remarks>
/// <para>
/// A reference language (docs/architecture/import.md): its model mirrors the file, so a document edited here is the
/// document on disk, and generation is the same language's other direction rather than a second opinion about the
/// syntax. It references <see cref="Projectional.Language"/> only, exactly as the pilot's languages do.
/// </para>
/// <para>
/// <b>A string is held as the file spells it, escapes included.</b> <c>Json.String.text</c> is the text between the
/// quotes, not the value after unescaping, and <c>Json.Property.name</c> is the same. That is what makes a round trip
/// byte-exact without a generator that can escape: a template emits a slot verbatim by design, and there is deliberately
/// no "escape this as JSON" part (that would put a format's spelling into every language that prints one). The cost is
/// visible and is the honest one - a <c>\n</c> in the file reads as <c>\n</c> in the editor rather than as a line break,
/// which is also what an editor should show for text it did not write. JSON forbids a raw control character inside a
/// string, so the stored spelling is always one line.
/// </para>
/// </remarks>
public static class JsonLanguage
{
    /// <summary>The slot of an object that holds its members.</summary>
    public const string MembersSlot = "members";

    /// <summary>The slot of a member that holds its value.</summary>
    public const string ValueSlot = "value";

    /// <summary>The slot of an array that holds its items.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The name of a member, spelled as the file spells it.</summary>
    public const string NameSlot = "name";

    /// <summary>A string's text, spelled as the file spells it.</summary>
    public const string TextSlot = "text";

    /// <summary>The two values a boolean can hold, in the order the completion list shows them.</summary>
    public static PropertyType BoolValue { get; } = PropertyType.Enum("true", "false");

    /// <summary>The concepts of this language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            JsonConceptKeys.Value,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything a JSON document can hold."),

        new ConceptDeclaration(
            JsonConceptKeys.Member,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "One entry of an object: a member, or a note beside one."),

        new ConceptDeclaration(
            JsonConceptKeys.Comment,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                "why",
                "What the note says. JSON has no syntax for it, so generation leaves the note out.",
                showDescriptionWhenFilled: true)],
            [JsonConceptKeys.Member],
            nameSlot: TextSlot,
            description: "A note in the model that the generated document cannot express."),

        new ConceptDeclaration(
            JsonConceptKeys.Object,
            [new ChildSlot(MembersSlot, JsonConceptKeys.Member, Cardinality.Many)],
            [JsonConceptKeys.Value],
            description: "A set of members, drawn in the order the document has them."),

        new ConceptDeclaration(
            JsonConceptKeys.Property,
            [
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "name",
                    "The member's name, spelled as the file spells it.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(ValueSlot, JsonConceptKeys.Value, Cardinality.One),
            ],
            [JsonConceptKeys.Member],
            nameSlot: NameSlot,
            description: "One member: a name and the value it holds."),

        new ConceptDeclaration(
            JsonConceptKeys.Array,
            [new ChildSlot(ItemsSlot, JsonConceptKeys.Value, Cardinality.Many)],
            [JsonConceptKeys.Value],
            description: "An ordered list of values."),

        new ConceptDeclaration(
            JsonConceptKeys.Text,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "The text between the quotes, escapes included.",
                showDescriptionWhenFilled: true)],
            [JsonConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A string."),

        new ConceptDeclaration(
            JsonConceptKeys.Number,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                "0",
                "The number as the file wrote it, which is what keeps 1, 1.0 and 1e3 apart.",
                showDescriptionWhenFilled: true)],
            [JsonConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A number, held as its literal."),

        new ConceptDeclaration(
            JsonConceptKeys.Bool,
            [new PropertySlot(ValueSlot, BoolValue, "true", "The value, offered as the two symbols it has.")],
            [JsonConceptKeys.Value],
            description: "true or false."),

        new ConceptDeclaration(
            JsonConceptKeys.Null,
            [],
            [JsonConceptKeys.Value],
            description: "The null value."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>The importer that turns a JSON string into a document of this language.</summary>
    public static ITextImporter Importer { get; } = new JsonImporter();

    /// <summary>The notation registry of this language.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations(Concepts);

    /// <summary>A registry for these concepts, with this language's notation.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        JsonNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project in which a JSON document can be written.</summary>
    public static Project NewProject() => new(Concepts);
}
