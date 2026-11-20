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
    }
}
