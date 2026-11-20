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
