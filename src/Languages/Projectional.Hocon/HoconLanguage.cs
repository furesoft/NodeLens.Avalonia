using Projectional.Core;
using Projectional.Language;

namespace Projectional.Hocon;

/// <summary>HOCON as a language: a JSON superset whose punctuation is optional.</summary>
/// <remarks>
/// <para>
/// A reference language (docs/architecture/import.md). It is the hardest of them to be honest about, because a HOCON
/// file is *evaluated*: keys merge, substitutions resolve, includes pull in other files. This language models the text,
/// not the result, so nothing is merged, resolved or followed - a `${name}` stays a substitution and an
/// <c>include "x.conf"</c> stays an include. A host that wants the resolved configuration evaluates it and imports
/// *that*.
/// </para>
/// <para>
/// <b>Repeated keys are kept, in order.</b> HOCON's merge semantics fold them into one object when the file is read; the
/// text has two, and the model owns the text. That is the opposite of the TOML decision and it follows from the formats
/// rather than from a preference: TOML makes a duplicate key a syntax error, HOCON makes it a feature.
/// </para>
/// <para>
/// <b>A spelling is a slot, not a guess.</b> <c>Hocon.Property.operator</c> keeps <c>=</c>, <c>:</c>, <c>+=</c> or the
/// whitespace separator, and <c>Hocon.String.quoted</c> keeps <c>foo</c>, <c>"foo"</c> and <c>"""foo"""</c> apart - which
/// is what makes a file the user edited read back as the file they wrote.
/// </para>
/// </remarks>
public static class HoconLanguage
{
    /// <summary>The slot of a document or an object that holds its members.</summary>
    public const string MembersSlot = "members";

    /// <summary>The slot of a property that holds its value.</summary>
    public const string ValueSlot = "value";

    /// <summary>The slot of an array that holds its items.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The slot of a concatenation that holds its parts.</summary>
    public const string PartsSlot = "parts";

    /// <summary>A property's key, spelled as the file spells it.</summary>
    public const string KeySlot = "key";

    /// <summary>A string's text between its delimiters, or the unquoted run itself.</summary>
    public const string TextSlot = "text";

    /// <summary>A number's literal, as the file wrote it.</summary>
    public const string LiteralSlot = "text";

    /// <summary>The separator a property was written with.</summary>
    public const string OperatorSlot = "operator";

    /// <summary>Which of the three spellings a string is.</summary>
    public const string QuotedSlot = "quoted";

    /// <summary>The path inside a substitution.</summary>
    public const string PathSlot = "path";

    /// <summary>Whether a substitution may resolve to nothing.</summary>
    public const string OptionalSlot = "optional";

    /// <summary>What an include names, kept as the text it was written as.</summary>
    public const string TargetSlot = "target";

    /// <summary>The marker that introduced a comment.</summary>
    public const string MarkerSlot = "marker";

    /// <summary>The four separators a property can be written with.</summary>
    public const string EqualsOperator = "=";

    /// <summary>A colon instead of an equals sign.</summary>
    public const string ColonOperator = ":";

    /// <summary>An appending assignment.</summary>
    public const string AppendOperator = "+=";

    /// <summary>The whitespace separator: <c>key value</c> and <c>key { ... }</c>.</summary>
    public const string SpaceOperator = "space";

    /// <summary>A string written without quotes.</summary>
    public const string Unquoted = "unquoted";

    /// <summary>A string between one pair of double quotes.</summary>
    public const string Quoted = "quoted";

    /// <summary>A string between three pairs, which may span lines.</summary>
    public const string TripleQuoted = "triple";

    /// <summary>Whether a substitution has to resolve.</summary>
    public const string Required = "required";

    /// <summary>Whether a substitution may resolve to nothing: <c>${?name}</c>.</summary>
    public const string Optional = "optional";

    /// <summary>The four separators, in the order the completion list offers them.</summary>
    public static PropertyType Operators { get; } = PropertyType.Enum(
        EqualsOperator,
        ColonOperator,
        AppendOperator,
        SpaceOperator);

    /// <summary>The three spellings of a string, in the order the completion list offers them.</summary>
    public static PropertyType Quotedness { get; } = PropertyType.Enum(Unquoted, Quoted, TripleQuoted);

    /// <summary>The two kinds of substitution.</summary>
    public static PropertyType Optionality { get; } = PropertyType.Enum(Required, Optional);

    /// <summary>The two comment markers the format has.</summary>
    public static PropertyType Markers { get; } = PropertyType.Enum("#", "//");

    /// <summary>The two values a boolean can hold, in the order the completion list shows them.</summary>
    public static PropertyType BoolValue { get; } = PropertyType.Enum("true", "false");

    /// <summary>The concepts of this language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            HoconConceptKeys.Document,
            [new ChildSlot(MembersSlot, HoconConceptKeys.Member, Cardinality.Many)],
            description: "A HOCON file: the object every file is, written without braces."),

        new ConceptDeclaration(
            HoconConceptKeys.Member,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "One entry of a document or of an object."),

        new ConceptDeclaration(
            HoconConceptKeys.Value,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything a property can hold."),

        new ConceptDeclaration(
            HoconConceptKeys.Comment,
            [
                new PropertySlot(
                    MarkerSlot,
                    Markers,
                    "#",
                    "Which of the two markers the note was written with."),
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    description: "What the note says, without the marker.",
                    showDescriptionWhenFilled: true),
            ],
            [HoconConceptKeys.Member],
            nameSlot: TextSlot,
            description: "A note, kept exactly where the file had it."),

        new ConceptDeclaration(
            HoconConceptKeys.Include,
            [new PropertySlot(
                TargetSlot,
                PropertyType.String,
                "\"application.conf\"",
                "What the include names, spelled as the file spells it. It is never followed.",
                showDescriptionWhenFilled: true)],
            [HoconConceptKeys.Member],
            nameSlot: TargetSlot,
            description: "An include, kept as text: reading another file is the host's decision, not an import's."),

        new ConceptDeclaration(
            HoconConceptKeys.Property,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    "key",
                    "The key, spelled as the file spells it - a dotted path included.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    OperatorSlot,
                    Operators,
                    EqualsOperator,
                    "The separator the property was written with, which the drawing shows as it was."),
                new ChildSlot(ValueSlot, HoconConceptKeys.Value, Cardinality.One),
            ],
            [HoconConceptKeys.Member],
            nameSlot: KeySlot,
            description: "A key, the separator it was written with, and its value."),

        new ConceptDeclaration(
            HoconConceptKeys.Object,
            [new ChildSlot(MembersSlot, HoconConceptKeys.Member, Cardinality.Many)],
            [HoconConceptKeys.Value],
            description: "An object, written between braces."),

        new ConceptDeclaration(
            HoconConceptKeys.Array,
            [new ChildSlot(ItemsSlot, HoconConceptKeys.Value, Cardinality.Many)],
            [HoconConceptKeys.Value],
            description: "An array of values, in the order the file has them."),

        new ConceptDeclaration(
            HoconConceptKeys.String,
            [
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    description: "The text between the delimiters, escapes included, or the unquoted run itself.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    QuotedSlot,
                    Quotedness,
                    Unquoted,
                    "Which of the three spellings this string is; it decides the delimiters."),
            ],
            [HoconConceptKeys.Value],
            nameSlot: TextSlot,
            description: "A string, in one of the three spellings the format has."),

        new ConceptDeclaration(
            HoconConceptKeys.Number,
            [new PropertySlot(
                LiteralSlot,
                PropertyType.String,
                "0",
                "The number as the file wrote it, which is what keeps 1, 1.0 and 1e3 apart.",
                showDescriptionWhenFilled: true)],
            [HoconConceptKeys.Value],
            nameSlot: LiteralSlot,
            description: "A number, held as its literal."),

        new ConceptDeclaration(
            HoconConceptKeys.Bool,
            [new PropertySlot(ValueSlot, BoolValue, "true", "The value, offered as the two symbols it has.")],
            [HoconConceptKeys.Value],
            description: "true or false."),

        new ConceptDeclaration(
            HoconConceptKeys.Null,
            [],
            [HoconConceptKeys.Value],
            description: "The null value."),

        new ConceptDeclaration(
            HoconConceptKeys.Concat,
            [new ChildSlot(PartsSlot, HoconConceptKeys.Value, Cardinality.Many)],
            [HoconConceptKeys.Value],
            description: "Values written next to each other, which HOCON concatenates."),

        new ConceptDeclaration(
            HoconConceptKeys.Substitution,
            [
                new PropertySlot(
                    PathSlot,
                    PropertyType.String,
                    "name",
                    "The path inside the substitution, spelled as the file spells it.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    OptionalSlot,
                    Optionality,
                    Required,
                    "Whether the substitution may resolve to nothing, which is what the question mark means."),
            ],
            [HoconConceptKeys.Value],
            nameSlot: PathSlot,
            description: "A name resolved when the configuration is read; never resolved here."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>The importer that turns a HOCON string into a document of this language.</summary>
    public static ITextImporter Importer { get; } = new HoconImporter();

    /// <summary>The notation registry of this language.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations(Concepts);

    /// <summary>A registry for these concepts, with this language's notation.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        HoconNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project in which a HOCON document can be written.</summary>
    public static Project NewProject() => new(Concepts);
}
