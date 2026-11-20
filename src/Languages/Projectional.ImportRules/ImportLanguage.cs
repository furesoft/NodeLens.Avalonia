using Projectional.Core;

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

    /// <summary>The parts of a rule, in the order the text has them.</summary>
    public const string PartsSlot = "parts";

    /// <summary>The rules of an importer.</summary>
    public const string RulesSlot = "rules";

    /// <summary>The concepts of the import language.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(Declarations());

    /// <summary>A project with the import language's concepts, ready to hold a document.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A new importer document, with its root already in place.</summary>
    public static Model NewModel()
    {
        var model = NewProject().NewModel("import");

        model.NewNode(ImportConceptKeys.Importer);

        return model;
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
                    "\n",
                    "What ends a comment, or empty to run to the end of the input."),
            ],
            [ImportConceptKeys.Token],
            nameSlot: TokenNameSlot,
            description: "Text that produces no node: whitespace, or a comment the reader steps over.");
    }
}