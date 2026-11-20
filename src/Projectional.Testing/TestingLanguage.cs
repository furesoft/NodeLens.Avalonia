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

    /// <summary>The <c>name=value</c> pairs a run is given, as the editor's run box reads them.</summary>
    public const string VariablesSlot = "variables";

    /// <summary>The answer or the failure an evaluation assertion expects, as text.</summary>
    public const string OutcomeSlot = "outcome";

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
