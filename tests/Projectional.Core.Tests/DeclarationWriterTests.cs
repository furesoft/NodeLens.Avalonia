using Projectional.Core;
using Projectional.LanguageExport;
using Projectional.Languages;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The declaration half of the export: a language's concepts written as the calls a hand-written language makes.
/// </summary>
/// <remarks>
/// The tests pin text rather than a data structure, because source is what a language author receives and a diff in
/// it is what they review.
/// </remarks>
public sealed class DeclarationWriterTests
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
                "public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =",
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
