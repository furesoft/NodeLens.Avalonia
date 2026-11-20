using Projectional.Core;
using Projectional.Language;

namespace Projectional.ImportRules;

/// <summary>The vocabulary a declared importer is written in: one document per importer, one rule per concept.</summary>
/// <remarks>
/// <para>
/// This is the rule layer only. A rule says which concept a run of text becomes; the token layer that says what a
/// token is - a word, a number, a quoted run, a fixed-width body - arrives with a format that needs one, because
/// declaring a lexical class before a file needs it would be the specification the type rules refused to write.
/// </para>
/// <para>
/// The verbs are the ones a template has, borrowed on purpose: a literal is an anchor, a capture is a property, a
/// nest is a child, a repeat is a list. What is <em>not</em> here is a delegate, an expression or an index - a rule
/// that could calculate would be a programming language hiding in a model, and it would need the interpreter this
/// framework has refused for constraints, templates, types and intentions.
/// </para>
/// </remarks>
public static class ImportLanguage
{
    /// <summary>The concept key the rule names, which is the concept the rule reads.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The name of the format, which is the generator it reads back.</summary>
    public const string NameSlot = "name";

    /// <summary>The text a literal part has to find.</summary>
    public const string TextSlot = "text";

    /// <summary>The property a capture fills, or the slot a nest or a repeat reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>What stands between two entries of a repeat.</summary>
    public const string SeparatorSlot = "separator";

    /// <summary>How many entries a repeat needs at least.</summary>
    public const string MinimumSlot = "minimum";

    /// <summary>The name a token class is referred to by.</summary>
    public const string TokenNameSlot = "tokenName";

    /// <summary>The characters a word accepts beyond letters and digits.</summary>
    public const string ExtraSlot = "extra";

    /// <summary>The mark a quoted run stands between.</summary>
    public const string QuoteSlot = "quote";

    /// <summary>The character that makes the next one literal inside a quoted run.</summary>
    public const string EscapeSlot = "escape";

    /// <summary>The text that starts a comment, or empty to skip whitespace instead.</summary>
    public const string PrefixSlot = "prefix";

    /// <summary>What ends a comment, or empty to run to the end of the line.</summary>
    public const string UntilSlot = "until";

    /// <summary>The token class a capture reads, or empty to run to the next literal.</summary>
    public const string TokenSlot = "token";

    /// <summary>The token classes an importer declares.</summary>
    public const string TokensSlot = "tokens";

    /// <summary>The mark a fixed run opens with.</summary>
    public const string OpenSlot = "open";

    /// <summary>The mark a fixed run closes with.</summary>
    public const string CloseSlot = "close";

    /// <summary>The name a bind remembers a run under, or the name a matches requires again.</summary>
    public const string AsSlot = "as";

    /// <summary>The identifier a fail reports under.</summary>
    public const string CodeSlot = "code";

    /// <summary>The sentence a fail gives instead of "expected".</summary>
    public const string MessageSlot = "message";

    /// <summary>The parts of a rule, in the order the text has them.</summary>
    public const string PartsSlot = "parts";

    /// <summary>The rules of an importer.</summary>
    public const string RulesSlot = "rules";

    /// <summary>The concepts of the import language.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(Declarations());

    /// <summary>How those concepts are drawn, so an importer document is readable in the editor.</summary>
    public static NotationRegistry Notations { get; } = CreateNotations();

    /// <summary>A project with the import language's concepts, ready to hold a document.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A new importer document, with its root already in place.</summary>
    public static Model NewModel()
    {
        var model = NewProject().NewModel("import");

        model.NewNode(ImportConceptKeys.Importer);

        return model;
    }

    private static NotationRegistry CreateNotations()
    {
        var notations = new NotationRegistry(Concepts);

        ImportNotations.AddTo(notations);

        return notations;
    }
    private static IEnumerable<ConceptDeclaration> Declarations()
    {
        yield return new ConceptDeclaration(
            ImportConceptKeys.Importer,
            [
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    description: "The name of the format, which is also the generator this importer reads back."),
                new ChildSlot(TokensSlot, ImportConceptKeys.Token, Cardinality.Many),
                new ChildSlot(RulesSlot, ImportConceptKeys.Rule, Cardinality.Many),
            ],
            nameSlot: NameSlot,
            description: "One way to read a format: the rule per concept.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Rule,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The concept this rule reads."),
                new ChildSlot(PartsSlot, ImportConceptKeys.Part, Cardinality.Many),
            ],
            nameSlot: ConceptKeySlot,
            description: "What one concept looks like in the text, as an ordered run of parts.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Part,
            isAbstract: true,
            description: "One piece of a rule: a literal, a capture, a nest or a repeat.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Literal,
            [
                new PropertySlot(TextSlot, PropertyType.String, description: "The text this part has to find."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: TextSlot,
            description: "Text that has to stand here, which is what an anchor is.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Capture,
            [
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The property the run up to the next literal is written into."),
                new PropertySlot(
                    TokenSlot,
                    PropertyType.String,
                    description: "The token class to read instead, or empty to run to the next literal."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: SlotSlot,
            description: "The run up to the next literal, filled into a property as it was written.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Line,
            [
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The property the rest of the line is written into."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: SlotSlot,
            description: "The rest of the line, filled into a property, with the line break read and discarded.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Nest,
            [
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The child slot the text is read into."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: SlotSlot,
            description: "A child, read by whichever rule the slot allows.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Repeat,
            [
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The many-valued child slot the entries are read into."),
                new PropertySlot(
                    SeparatorSlot,
                    PropertyType.String,
                    description: "The literal that stands between two entries, or empty for none."),
                new PropertySlot(
                    MinimumSlot,
                    PropertyType.Integer,
                    "0",
                    "How many entries the text has to hold at least."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: SlotSlot,
            description: "The entries of a list, separated by a literal.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Token,
            [
                new PropertySlot(
                    TokenNameSlot,
                    PropertyType.String,
                    description: "The name a rule refers to this class by."),
            ],
            isAbstract: true,
            nameSlot: TokenNameSlot,
            description: "A lexical class a capture may read: a word, a number, a quoted run, or text that produces no node.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Word,
            [
                new PropertySlot(
                    ExtraSlot,
                    PropertyType.String,
                    description: "The characters a word accepts beyond letters, digits and underscore."),
            ],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "A run of letters, digits and underscore, plus the characters the declaration adds.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Number,
            [],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "A number: an optional sign, digits, an optional fraction and an optional exponent.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Quoted,
            [
                new PropertySlot(
                    QuoteSlot,
                    PropertyType.String,
                    "\"",
                    "The mark the run stands between."),
                new PropertySlot(
                    EscapeSlot,
                    PropertyType.String,
                    "\\",
                    "The character that makes the next one literal, or empty to resolve no escapes."),
            ],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "A run between two marks, with the escapes the declaration names resolved.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Skipped,
            [
                new PropertySlot(
                    PrefixSlot,
                    PropertyType.String,
                    "",
                    "The text that starts a comment, or empty to skip whitespace instead."),
                new PropertySlot(
                    UntilSlot,
                    PropertyType.String,
                    "\\n",
                    "What ends a comment, or empty to run to the end of the input."),
            ],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "Text that produces no node: whitespace, or a comment the reader steps over.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Fixed,
            [
                new PropertySlot(OpenSlot, PropertyType.String, description: "The mark the run opens with."),
                new PropertySlot(CloseSlot, PropertyType.String, description: "The mark the run closes with."),
            ],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "A run between two marks in which nothing is escaped, because a section that must not resolve escapes is a class rather than a flag.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Raw,
            [
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The property the run is kept in, exactly as written."),
                new PropertySlot(
                    UntilSlot,
                    PropertyType.String,
                    description: "The text that ends the run."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: SlotSlot,
            description: "Tier 2: a run the model does not map, kept verbatim so it generates back unchanged.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Bind,
            [
                new PropertySlot(
                    AsSlot,
                    PropertyType.String,
                    description: "The name the run is remembered under."),
                new PropertySlot(
                    TokenSlot,
                    PropertyType.String,
                    description: "The token class to read, or empty to run to the next literal."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: AsSlot,
            description: "A run remembered under a name and filling no slot, so a later part can require it again.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Matches,
            [
                new PropertySlot(
                    AsSlot,
                    PropertyType.String,
                    description: "The name a bind earlier in this rule remembered."),
                new PropertySlot(
                    TokenSlot,
                    PropertyType.String,
                    description: "The token class to read, or empty to run to the next literal."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: AsSlot,
            description: "The next run has to be the one a bind remembered: the closing tag that must equal the opening one.");

        yield return new ConceptDeclaration(
            ImportConceptKeys.Fail,
            [
                new PropertySlot(CodeSlot, PropertyType.String, description: "The identifier the refusal reports under."),
                new PropertySlot(MessageSlot, PropertyType.String, description: "The sentence a reader gets instead of \"expected\"."),
            ],
            [ImportConceptKeys.Part],
            nameSlot: CodeSlot,
            description: "The message a rule gives when it is the one that should have matched: \"a trailing comma is not allowed\".");
    }
}