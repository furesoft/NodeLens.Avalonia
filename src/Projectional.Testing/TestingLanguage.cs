using Projectional.Core;
using Projectional.Language;

namespace Projectional.Testing;

/// <summary>
/// The language a language's own tests are written in: concepts whose nodes <em>are</em> an assertion about a
/// document.
/// </summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - so that a language author tests a language
/// in the same way they write one (ADR 0014). The C# twin is <see cref="TestRunner"/>, which turns a document
/// written here into a row of a <see cref="TestReport"/>.
/// </para>
/// <para>
/// <b>The document under test is named by a path, not by a reference.</b> A reference would be the stronger
/// spelling, but a reference slot names a concept key and a document's root is the language's own - the common
/// base concept this framework deliberately does not have. A path relative to the language project is what the
/// corpus already uses, and it is what a reviewer can follow without a session open.
/// </para>
/// </remarks>
public static class TestingLanguage
{
    /// <summary>The slot naming the model file this test is about, relative to the language project.</summary>
    public const string DocumentSlot = "document";

    /// <summary>The single child that says what the test asserts.</summary>
    public const string AssertionSlot = "assertion";

    /// <summary>How many columns a drawing assertion renders into.</summary>
    public const string WidthSlot = "width";

    /// <summary>The lines a drawing assertion expects, one node per line.</summary>
    public const string LinesSlot = "lines";

    /// <summary>The text of one expected line.</summary>
    public const string TextSlot = "text";

    /// <summary>The findings a findings assertion expects, one node per finding.</summary>
    public const string ExpectedSlot = "expected";

    /// <summary>The identifier a finding reports under.</summary>
    public const string CodeSlot = "code";

    /// <summary>How much a finding matters.</summary>
    public const string SeveritySlot = "severity";

    /// <summary>The slot of the node a finding points at, or empty for the node's own text.</summary>
    public const string RoleSlot = "role";

    /// <summary>The name of the generator a generation assertion runs.</summary>
    public const string GeneratorSlot = "generator";

    /// <summary>The slot naming the importer a reads assertion runs.</summary>
    public const string ImporterSlot = "importer";

    /// <summary>The <c>name=value</c> pairs a run is given, as the editor's run box reads them.</summary>
    public const string VariablesSlot = "variables";

    /// <summary>The answer or the failure an evaluation assertion expects, as text.</summary>
    public const string OutcomeSlot = "outcome";

    /// <summary>The steps a script assertion replays, in order.</summary>
    public const string StepsSlot = "steps";

    /// <summary>The text a caret step puts the caret in front of.</summary>
    public const string AtSlot = "at";

    /// <summary>The characters a typing step types at the caret.</summary>
    public const string TypedSlot = "typed";

    /// <summary>The key a press step presses, named the way the editor names it.</summary>
    public const string KeySlot = "key";

    /// <summary>The id of the offer an invoke step applies.</summary>
    public const string IntentionSlot = "intention";

    /// <summary>The name of the type a type assertion expects.</summary>
    public const string TypeSlot = "type";

    /// <summary>The reference slot whose scope a scope assertion reports.</summary>
    public const string ScopeSlot = "slot";

    /// <summary>The concept declarations of the language.</summary>
    /// <remarks>
    /// The document is also the name slot, so a test reads as the document it is about in a tree, a reference and
    /// a report. An unnamed test would be a row nobody can find.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            TestingConceptKeys.Test,
            [
                new PropertySlot(
                    DocumentSlot,
                    PropertyType.String,
                    description: "The model file this test asserts about, relative to the language project."),
                new ChildSlot(AssertionSlot, TestingConceptKeys.Assertion, Cardinality.One),
            ],
            nameSlot: DocumentSlot,
            description: "One assertion about one document of a language, written as data."),

        new ConceptDeclaration(
            TestingConceptKeys.Assertion,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What a test asserts about its document, as one of the kinds below."),

        new ConceptDeclaration(
            TestingConceptKeys.RoundTrips,
            [],
            [TestingConceptKeys.Assertion],
            description: "The document is written and read back to the same text."),

        new ConceptDeclaration(
            TestingConceptKeys.Draws,
            [
                new PropertySlot(
                    WidthSlot,
                    PropertyType.Integer,
                    "80",
                    description: "How many columns the drawing has."),
                new ChildSlot(LinesSlot, TestingConceptKeys.Line, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            description: "The document draws as the text the test spells out, one line per entry."),

        new ConceptDeclaration(
            TestingConceptKeys.Line,
            [
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    description: "One line of the drawing, with the indentation the renderer gives it."),
            ],
            nameSlot: TextSlot,
            description: "One line of a drawn expectation."),

        new ConceptDeclaration(
            TestingConceptKeys.Findings,
            [
                new ChildSlot(ExpectedSlot, TestingConceptKeys.ExpectedFinding, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            description: "The document reports exactly the findings the test lists, in order."),

        new ConceptDeclaration(
            TestingConceptKeys.ExpectedFinding,
            [
                new PropertySlot(
                    CodeSlot,
                    PropertyType.String,
                    description: "The identifier the check reports under, such as pipeline.stage.command."),
                new PropertySlot(
                    SeveritySlot,
                    PropertyType.Enum("error", "warning", "hint"),
                    "error",
                    description: "How much it matters: error, warning or hint."),
                new PropertySlot(
                    RoleSlot,
                    PropertyType.String,
                    description: "The slot the finding points at, or empty for the node's own text."),
            ],
            nameSlot: CodeSlot,
            description: "One finding a test expects the document to report."),

        new ConceptDeclaration(
            TestingConceptKeys.Generates,
            [
                new PropertySlot(
                    GeneratorSlot,
                    PropertyType.String,
                    description: "The name of the generator the document is generated with."),
                new ChildSlot(LinesSlot, TestingConceptKeys.Line, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            description: "The document generates as the text the test spells out, one line per entry."),

        new ConceptDeclaration(
            TestingConceptKeys.Imports,
            [
                new PropertySlot(
                    GeneratorSlot,
                    PropertyType.String,
                    description: "The name of the generator whose text is read back."),
            ],
            [TestingConceptKeys.Assertion],
            nameSlot: GeneratorSlot,
            description: "The text the document generates reads back, through that generator's inverse, to a document that generates the same text."),

        new ConceptDeclaration(
            TestingConceptKeys.Reads,
            [
                new PropertySlot(
                    ImporterSlot,
                    PropertyType.String,
                    description: "The name of the importer that reads the file."),
                new PropertySlot(
                    GeneratorSlot,
                    PropertyType.String,
                    description: "The name of the generator the imported model is printed with."),
                new ChildSlot(LinesSlot, TestingConceptKeys.Line, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            nameSlot: ImporterSlot,
            description: "The text file reads, through that importer, into a model that generates as the lines the test spells out."),
        new ConceptDeclaration(
            TestingConceptKeys.Evaluates,
            [
                new PropertySlot(
                    VariablesSlot,
                    PropertyType.String,
                    description: "The name=value pairs the run is given, or empty when it needs none."),
                new PropertySlot(
                    OutcomeSlot,
                    PropertyType.String,
                    description: "The answer the run produces, or the message of the failure that stops it."),
            ],
            [TestingConceptKeys.Assertion],
            description: "The document evaluates to the answer the test spells out, or to the failure it names."),

        new ConceptDeclaration(
            TestingConceptKeys.Script,
            [
                new PropertySlot(
                    WidthSlot,
                    PropertyType.Integer,
                    "80",
                    description: "How many columns the drawing after the script has."),
                new ChildSlot(StepsSlot, TestingConceptKeys.Step, Cardinality.Many),
                new ChildSlot(LinesSlot, TestingConceptKeys.Line, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            description: "The document is edited by the steps and then draws as the text the test spells out."),

        new ConceptDeclaration(
            TestingConceptKeys.Step,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "One thing a user does to the document, as one of the kinds below."),

        new ConceptDeclaration(
            TestingConceptKeys.Caret,
            [
                new PropertySlot(
                    AtSlot,
                    PropertyType.String,
                    description: "The text whose first occurrence the caret goes in front of."),
            ],
            [TestingConceptKeys.Step],
            nameSlot: AtSlot,
            description: "Puts the caret in front of the text the step names."),

        new ConceptDeclaration(
            TestingConceptKeys.Type,
            [
                new PropertySlot(
                    TypedSlot,
                    PropertyType.String,
                    description: "The characters typed at the caret, as one keystroke each."),
            ],
            [TestingConceptKeys.Step],
            nameSlot: TypedSlot,
            description: "Types the characters the step names at the caret."),

        new ConceptDeclaration(
            TestingConceptKeys.Press,
            [
                new PropertySlot(
                    KeySlot,
                    PropertyType.String,
                    description: "The key, named the way the editor's routing table names it - Enter, Tab, Delete."),
            ],
            [TestingConceptKeys.Step],
            nameSlot: KeySlot,
            description: "Presses the key the step names at the caret."),

        new ConceptDeclaration(
            TestingConceptKeys.Invoke,
            [
                new PropertySlot(
                    IntentionSlot,
                    PropertyType.String,
                    description: "The stable id of the offer to apply, such as workflow.add-first-step."),
            ],
            [TestingConceptKeys.Step],
            nameSlot: IntentionSlot,
            description: "Applies the offer whose id the step names, at the caret."),

        new ConceptDeclaration(
            TestingConceptKeys.TypeOf,
            [
                new PropertySlot(
                    AtSlot,
                    PropertyType.String,
                    description: "The text the document shows at the node whose type is asserted."),
                new PropertySlot(
                    TypeSlot,
                    PropertyType.String,
                    description: "The type the node is expected to have, written the way the framework writes it."),
            ],
            [TestingConceptKeys.Assertion],
            nameSlot: AtSlot,
            description: "The node the test names has the type the test spells out."),

        new ConceptDeclaration(
            TestingConceptKeys.ScopeOf,
            [
                new PropertySlot(
                    AtSlot,
                    PropertyType.String,
                    description: "The text the document shows at the node whose reference slot is asked."),
                new PropertySlot(
                    ScopeSlot,
                    PropertyType.String,
                    description: "The reference slot to ask, such as onFailure."),
                new ChildSlot(LinesSlot, TestingConceptKeys.Line, Cardinality.Many),
            ],
            [TestingConceptKeys.Assertion],
            nameSlot: AtSlot,
            description: "The reference slot the test names sees exactly the names the test lists, in order."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a test can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty test, ready to be written.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "test")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(TestingConceptKeys.Test);

        return model;
    }
}
