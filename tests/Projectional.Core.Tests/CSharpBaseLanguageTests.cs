using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Generation;
using Projectional.Editors;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Structures;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A declared project that names the shipped C# language as a dependency, which is what makes C# a *base* language
/// rather than a language beside the others.
/// </summary>
/// <remarks>
/// <para>
/// The MPS case in one test: a DSL needs logic, so it declares a slot over <c>CSharp.Expression</c> instead of growing
/// an expression language of its own, and the user writes a call or a comparison in that slot. Nothing of C# is
/// declared in the folder - the concepts arrive through the name the <c>language.mps</c> writes.
/// </para>
/// <para>
/// The folder is written at test time from the same builders a language author uses, because what is under test is
/// the resolution and not a checked-in sample: a sample would have to be kept in step with the language, and this
/// cannot drift.
/// </para>
/// </remarks>
public sealed class CSharpBaseLanguageTests
{
    private const string RuleConcept = "Decisions.Rule";

    [Fact]
    public void ADeclaredProjectResolvesTheShippedBaseLanguage()
    {
        var folder = Directory.CreateTempSubdirectory("declared-decisions");

        try
        {
            Write(folder.FullName);

            var shell = new WorkbenchLanguage();
            var language = LanguageProject.Open(folder.FullName, shell, out var error);

            Assert.Null(error);
            Assert.NotNull(language);

            // The base answered the name, so its vocabulary is in this language's registry - which is the whole
            // point: a slot below is declared over a concept this folder never declares.
            Assert.Equal(CSharpLanguage.Name, Assert.Single(language.Resolved).Name);
            Assert.True(language.Concepts.TryGet(CSharpConceptKeys.Expression, out _));
            Assert.True(language.Concepts.TryGet(CSharpConceptKeys.Binary, out _));

            // The vocabulary is the project's, the drawing is the shell's: a base language's notation stays its own
            // (ADR 0026), so the project draws nothing of C# and the shell composes one registry from every language
            // it holds - which is what makes the slot drawable without the project re-declaring how C# looks.
            Assert.False(language.Notations.TryGet(CSharpConceptKeys.Binary, out _));

            Assert.True(shell.TryAdd(language, out var refusal), refusal);
            Assert.True(shell.Notations.TryGet(CSharpConceptKeys.Binary, out _));
            Assert.True(shell.Notations.TryGet(RuleConcept, out _));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void ADocumentOfThatProjectHoldsCSharpLogic()
    {
        var folder = Directory.CreateTempSubdirectory("declared-decisions");

        try
        {
            Write(folder.FullName);

            var shell = new WorkbenchLanguage();
            var language = Assert.IsType<LanguageProject>(
                LanguageProject.Open(folder.FullName, shell, out var error) ?? Fail(error));

            var model = language.NewModel("decisions");
            var rule = model.NewNode(RuleConcept);
            var comparison = model.NewChild(rule, "when", CSharpConceptKeys.Binary);
            var left = model.NewChild(comparison, "left", CSharpConceptKeys.Name);
            var right = model.NewChild(comparison, "right", CSharpConceptKeys.IntLiteral);

            rule.Set("name", "enough");
            comparison.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.GreaterThan);
            // A dangling reference that keeps the text it was given: the DSL declares no `total`, and the base
            // language draws the resolveInfo exactly as a resolved name would be drawn.
            left.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "total");
            right.Set(CSharpLanguage.ValueProperty, "0");

            // The DSL's own notation and the base language's are one drawing - the rule around it is declared here,
            // the expression inside it is drawn by C# - and the registry that holds both is the shell's, which is the
            // registry the workbench opens every document with.
            Assert.True(shell.TryAdd(language, out var refusal), refusal);

            Assert.Equal(
                "rule enough when total > 0 -> accept",
                LayoutSnapshot.Render(model, shell.Notations, width: 120));

            // And the base language's generator turns the same node back into the C# that a compiler accepts.
            var generated = new Projectional.Semantics.Generator()
                .Generate(comparison, CSharpTextGenerators.Generator)
                .Text;

            Assert.Equal("total > 0", generated);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static LanguageProject Fail(string? error) => throw new InvalidOperationException(error);

    /// <summary>Writes a one-concept DSL that depends on the shipped C# language.</summary>
    private static void Write(string root)
    {
        foreach (var name in new[] { "structure", "editor" })
        {
            Directory.CreateDirectory(Path.Combine(root, name));
        }

        var definitions = new DefinitionBuilder();

        definitions.Concept(
            RuleConcept,
            slots:
            [
                definitions.Property("name", "string", "The rule's name."),
                definitions.Child("when", CSharpConceptKeys.Expression, "1"),
                definitions.Property("outcome", "string", "accept", "What the rule decides."),
            ],
            nameSlot: "name",
            nameRule: "identifier",
            description: "A rule that decides when some C# holds.");

        foreach (var document in definitions.Documents)
        {
            var key = document.Roots[0].GetOrNull(StructureLanguage.KeySlot)!;

            ModelFile.Save(document, Path.Combine(root, "structure", $"{key}.mps"));
        }

        var notation = new NotationBuilder();
        var declared = notation.Notation(RuleConcept);
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Keyword, ("text", "rule"));
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Cell(line, EditorConceptKeys.Property, ("slot", "name"));
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Cell(line, EditorConceptKeys.Keyword, ("text", "when"));
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Cell(line, EditorConceptKeys.ChildInline, ("slot", "when"));
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Cell(line, EditorConceptKeys.Punct, ("text", "->"));
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Cell(line, EditorConceptKeys.Property, ("slot", "outcome"));

        ModelFile.Save(notation.Documents[0], Path.Combine(root, "editor", $"{RuleConcept}.mps"));

        var languageRoot = StructureLanguage.NewModel("language");
        var language = languageRoot.NewNode(StructureConceptKeys.Language);

        language.Set("name", "Decisions");
        language.Set(StructureLanguage.VersionSlot, "1.0.0");

        languageRoot
            .NewChild(language, StructureLanguage.DependenciesSlot, StructureConceptKeys.Dependency)
            .Set("name", CSharpLanguage.Name);

        ModelFile.Save(languageRoot, Path.Combine(root, LanguageProject.LanguageFile));
    }

    [Fact]
    public void ANewDocumentOfTheBaseLanguageStartsAsAFile()
    {
        // The path from "new document" to the first keystroke: the shell asks the language which concept may start a
        // document. For C# the answer has to be `File`, because a document that began as a type reference or a
        // literal would be a fragment of C# rather than a file of it.
        var shell = new WorkbenchLanguage();
        var language = shell.Find(CSharpLanguage.Name);

        Assert.NotNull(language);

        var model = WorkbenchLanguage.NewDocument(language);

        Assert.NotNull(model);
        Assert.Equal(CSharpConceptKeys.File, Assert.Single(model.Roots).Concept.Key);

        // The document is drawable the moment it exists: the namespace slot is a placeholder the user fills, so the
        // first keystroke lands in a document rather than in an error.
        Assert.Equal("<no namespace>", LayoutSnapshot.Render(model, shell.Notations, width: 80));

        // And filling it is one child, which is where "new document, type a class" begins.
        var ns = model.NewChild(model.Roots[0], CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        ns.Set(CSharpLanguage.NameSlot, "Sample");

        Assert.DoesNotContain("<no namespace>", LayoutSnapshot.Render(model, shell.Notations, width: 80), StringComparison.Ordinal);
    }
}
