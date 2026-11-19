using Projectional.Core;
using Projectional.LanguageExport;
using Projectional.Languages;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The export turns a declared language into the C# a hand-written one is: the same declarations, written as calls.
/// </summary>
/// <remarks>
/// The shape is the one `language-authoring.md` documents as the code path a host ships, so the tests pin the text
/// rather than a data structure: what a language author receives is source, and a diff in it is what they review.
/// </remarks>
public sealed class LanguageExportTests
{
    [Fact]
    public void ConceptsBecomeDeclarationCalls()
    {
        var declarations = new List<ConceptDeclaration>
        {
            new(
                "Rules.Rule",
                [
                    new PropertySlot("name", PropertyType.String),
                    new PropertySlot(
                        "outcome",
                        PropertyType.Enum("accept", "review"),
                        "review",
                        "How it ends.",
                        showDescriptionWhenFilled: true),
                    new ChildSlot("steps", "Rules.Step", Cardinality.Many, minimum: 1),
                    new ReferenceSlot("condition", "Rules.Condition", allowOwnSubtree: true),
                ],
                superConcepts: ["Rules.Element"],
                nameSlot: "name",
                description: "One rule."),
        };

        var source = new SourceWriter();

        DeclarationWriter.Write(source, declarations);

        Assert.Equal(
            string.Join(
                '\n',
                "internal static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =",
                "[",
                "    new ConceptDeclaration(",
                "        \"Rules.Rule\",",
                "        [",
                "            new PropertySlot(\"name\", PropertyType.String),",
                "            new PropertySlot(\"outcome\", PropertyType.Enum(\"accept\", \"review\"), \"review\", \"How it ends.\", showDescriptionWhenFilled: true),",
                "            new ChildSlot(\"steps\", \"Rules.Step\", Cardinality.Many, minimum: 1),",
                "            new ReferenceSlot(\"condition\", \"Rules.Condition\", allowOwnSubtree: true),",
                "        ],",
                "        superConcepts: [\"Rules.Element\"],",
                "        nameSlot: \"name\",",
                "        description: \"One rule.\"",
                "    ),",
                "];",
                ""),
            source.ToString());
    }

    [Fact]
    public void AnInterfaceIsEmittedAsTheFlagsItIs()
    {
        var declarations = new List<ConceptDeclaration>
        {
            new("Framework.Named", isAbstract: true, isInterface: true, isOfferedInCompletion: false),
        };

        var source = new SourceWriter();

        DeclarationWriter.Write(source, declarations);

        var text = source.ToString();

        Assert.Contains("isAbstract: true,", text, StringComparison.Ordinal);
        Assert.Contains("isInterface: true,", text, StringComparison.Ordinal);
        Assert.Contains("isOfferedInCompletion: false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredSampleNotationsBecomeBuilderCalls()
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var source = new SourceWriter();

        NotationWriter.Write(source, language.Notations.Notations);

        Assert.Equal(
            """
            notations.Add("Pipeline.Pipeline", ed => ed.Sequence(
                ed.Line(ed.Keyword("pipeline"), ed.Space, ed.Property("name")),
                ed.Indent(
                    ed.Table("stages", "+ stage", ed.Column("name", ed.Keyword("stage")), ed.Column("command", ed.Keyword("run")), ed.Column("retries", ed.Keyword("retries")))
                )
            ));
            notations.Add("Pipeline.Stage", ed => ed.Line(ed.Property("name"), ed.Space, ed.Property("command"), ed.Space, ed.Property("retries")));

            """,
            source.ToString(),
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void TheDeclaredSampleOpensAndItsConceptsAreExported()
    {
        // The language a user opens is the export's first customer, so it is the fixture: the folder the app reads
        // is the folder this test exports.
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var source = new SourceWriter();

        DeclarationWriter.Write(source, language.Declarations);

        var text = source.ToString();

        foreach (var concept in language.Declarations)
        {
            Assert.Contains($"\"{concept.Key}\"", text, StringComparison.Ordinal);
        }
    }
}
