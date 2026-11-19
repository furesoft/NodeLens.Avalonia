using Projectional.Core;
using Projectional.Language;

namespace Projectional.Toml;

/// <summary>TOML as a language: fourteen concepts, one per thing a TOML file is made of.</summary>
/// <remarks>
/// <para>
/// A reference language (docs/architecture/import.md). It references <see cref="Projectional.Language"/> and
/// <c>Projectional.Core</c> like every language does, and it takes one package dependency - the TOML reader - because
/// .NET has none in the box (open question 98).
/// </para>
/// <para>
/// <b>The order is the structure.</b> A TOML file is an ordered sequence of pairs and headers, and a pair belongs to the
/// table whose header was written last. That is why the document is a concept with an <c>entries</c> list instead of a
/// dictionary: a model that normalised the order would rewrite the file on the next save.
/// </para>
/// <para>
/// <b>Every leaf keeps the spelling the file gave it.</b> A string keeps the text between its delimiters and the kind of
/// delimiter is a slot, an integer and a float keep their literal (<c>0x2A</c>, <c>1e3</c>, <c>inf</c>), and a date or a
/// time keeps the text and its kind. That is what makes <c>generate(import(text))</c> exact without an escaping part in
/// the framework's template vocabulary: the language that owns the format spells it, on the way in, and a template emits
/// a slot verbatim. The cost is visible and is the same one JSON and XML pay - a user typing into a string cell types
/// the spelling the file would have.
/// </para>
/// <para>
/// <b>A key is the text it was written as, dotted paths included.</b> <c>a.b = 1</c> keeps <c>a.b</c> as the key and is
/// not resolved into nested tables: resolving it would move the pair on the next save, and nesting is what a header is
/// for.
/// </para>
/// </remarks>
public static class TomlLanguage
{
    /// <summary>The slot of a document, of a table or of an array of tables that holds its entries.</summary>
    public const string EntriesSlot = "entries";

    /// <summary>The slot of a pair that holds its value.</summary>
    public const string ValueSlot = "value";

    /// <summary>The slot of an inline table that holds its members.</summary>
    public const string MembersSlot = "members";

    /// <summary>The slot of an array that holds its items.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The key of a pair or the name of a header, spelled as the file spells it.</summary>
    public const string KeySlot = "key";

    /// <summary>A string's text between its delimiters, spelled as the file spells it.</summary>
    public const string TextSlot = "text";

    /// <summary>Which of the four spellings of a string a value is.</summary>
    public const string KindSlot = "kind";

    /// <summary>A basic string: <c>"escapes"</c>.</summary>
    public const string BasicString = "basic";

    /// <summary>A multi-line basic string: <c>"""</c> and three quotes.</summary>
    public const string MultilineBasicString = "multiline-basic";

    /// <summary>A literal string: <c>'nothing is escaped'</c>.</summary>
    public const string LiteralString = "literal";

    /// <summary>A multi-line literal string: <c>'''</c> and three quotes.</summary>
    public const string MultilineLiteralString = "multiline-literal";

    /// <summary>An offset date-time: <c>1979-05-27T07:32:00Z</c>.</summary>
    public const string OffsetDateTime = "offset-date-time";

    /// <summary>A local date-time: <c>1979-05-27T07:32:00</c>.</summary>
    public const string LocalDateTime = "local-date-time";

    /// <summary>A local date: <c>1979-05-27</c>.</summary>
    public const string LocalDate = "local-date";

    /// <summary>A local time: <c>07:32:00</c>.</summary>
    public const string LocalTime = "local-time";

    /// <summary>The four spellings of a string, in the order the completion list offers them.</summary>
    public static PropertyType StringKinds { get; } = PropertyType.Enum(
        BasicString,
        MultilineBasicString,
        LiteralString,
        MultilineLiteralString);

    /// <summary>The four kinds of date and time, in the order the completion list offers them.</summary>
    public static PropertyType DateTimeKinds { get; } = PropertyType.Enum(
        OffsetDateTime,
        LocalDateTime,
        LocalDate,
        LocalTime);

    /// <summary>The two values a boolean can hold, in the order the completion list shows them.</summary>
    public static PropertyType BoolValue { get; } = PropertyType.Enum("true", "false");

    /// <summary>The concepts of this language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            TomlConceptKeys.Document,
            [new ChildSlot(EntriesSlot, TomlConceptKeys.Entry, Cardinality.Many)],
            description: "A TOML file: its entries in the order it has them."),

        new ConceptDeclaration(
            TomlConceptKeys.Entry,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "One entry of a document or of a table."),

        new ConceptDeclaration(
            TomlConceptKeys.KeyValue,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    "key",
                    "The key, spelled as the file spells it - a dotted path included.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(ValueSlot, TomlConceptKeys.Value, Cardinality.One),
            ],
            [TomlConceptKeys.Entry],
            nameSlot: KeySlot,
            description: "A pair in the table that is open where it stands."),

        new ConceptDeclaration(
            TomlConceptKeys.Table,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    "table",
                    "The table's name, spelled as the file spells it.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(EntriesSlot, TomlConceptKeys.Entry, Cardinality.Many),
            ],
            [TomlConceptKeys.Entry],
            nameSlot: KeySlot,
            description: "A header in single brackets, followed by the entries written under it."),

        new ConceptDeclaration(
            TomlConceptKeys.ArrayOfTables,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    "tables",
                    "The array's name, spelled as the file spells it.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(EntriesSlot, TomlConceptKeys.Entry, Cardinality.Many),
            ],
            [TomlConceptKeys.Entry],
            nameSlot: KeySlot,
            description: "A header in double brackets: one element of a list of tables."),

        new ConceptDeclaration(
            TomlConceptKeys.Comment,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                description: "What the note says, without the hash that introduces it.",
                showDescriptionWhenFilled: true)],
            [TomlConceptKeys.Entry],
            nameSlot: TextSlot,
            description: "A note, kept exactly where the file had it."),

        new ConceptDeclaration(
            TomlConceptKeys.Value,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything a pair can hold."),

        new ConceptDeclaration(
            TomlConceptKeys.String,
            [
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    description: "The text between the delimiters, escapes included.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    KindSlot,
                    StringKinds,
                    BasicString,
                    "Which of the four spellings this string is; it decides the delimiters."),
            ],
            [TomlConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A string, in one of the four spellings the format has."),

        new ConceptDeclaration(
            TomlConceptKeys.Integer,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                "0",
                "The integer as the file wrote it, which is what keeps 42, 0x2A and 0o52 apart.",
                showDescriptionWhenFilled: true)],
            [TomlConceptKeys.Value],
            nameSlot: TextSlot,
            description: "An integer, held as its literal."),

        new ConceptDeclaration(
            TomlConceptKeys.Float,
            [new PropertySlot(
                TextSlot,
                PropertyType.String,
                "0.0",
                "The float as the file wrote it, inf and nan included.",
                showDescriptionWhenFilled: true)],
            [TomlConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A float, held as its literal."),

        new ConceptDeclaration(
            TomlConceptKeys.Bool,
            [new PropertySlot(ValueSlot, BoolValue, "true", "The value, offered as the two symbols it has.")],
            [TomlConceptKeys.Value],
            description: "true or false."),

        new ConceptDeclaration(
            TomlConceptKeys.DateTime,
            [
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    "1979-05-27T07:32:00Z",
                    "The date, time or both, exactly as the file wrote it.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    KindSlot,
                    DateTimeKinds,
                    OffsetDateTime,
                    "Which of the four kinds of date and time this value is."),
            ],
            [TomlConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A date, a time or both, held as the text the file had."),

        new ConceptDeclaration(
            TomlConceptKeys.Array,
            [new ChildSlot(ItemsSlot, TomlConceptKeys.Value, Cardinality.Many)],
            [TomlConceptKeys.Value],
            description: "An array of values, in the order the file has them."),

        new ConceptDeclaration(
            TomlConceptKeys.InlineTable,
            [new ChildSlot(MembersSlot, TomlConceptKeys.KeyValue, Cardinality.Many)],
            [TomlConceptKeys.Value],
            description: "A table written in one line, as the value of a pair."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>The importer that turns a TOML string into a document of this language.</summary>
    public static ITextImporter Importer { get; } = new TomlImporter();

    /// <summary>The notation registry of this language.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations(Concepts);

    /// <summary>A registry for these concepts, with this language's notation.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        TomlNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project in which a TOML document can be written.</summary>
    public static Project NewProject() => new(Concepts);
}
