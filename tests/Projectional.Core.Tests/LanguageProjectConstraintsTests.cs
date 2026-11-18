using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Semantics;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language project's fourth file: <c>constraints.mps</c> becomes the rules the language states about itself.
/// </summary>
/// <remarks>
/// The folder is optional in the same way the other three are: a language being written has no rules yet, and
/// refusing to load it would make "write the structure first" impossible.
/// </remarks>
public sealed class LanguageProjectConstraintsTests
{
    [Fact]
    public void AProjectWithConstraintsLoadsThem()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteConstraint(folder.Root, "Pipeline.Stage", "detail");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.True(language.HasConstraints);
        Assert.Equal(1, language.Constraints.Count);

        // The rule the folder declared is what reports: a stage without a detail is the example the sample uses.
        var model = language.NewModel("document");

        model.NewNode("Pipeline.Stage");

        var finding = Assert.Single(new ConstraintEngine(language.Constraints).Check(model));

        Assert.Equal("detail", finding.Role);
        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal("a stage needs a detail", finding.Message);
    }

    [Fact]
    public void AProjectWithoutConstraintsLoadsAndReportsNothing()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.False(language.HasConstraints);
        Assert.True(language.Constraints.IsEmpty);

        var model = language.NewModel("document");

        model.NewNode("Pipeline.Stage");

        Assert.Empty(new ConstraintEngine(language.Constraints).Check(model));
    }

    [Fact]
    public void AConstraintNamingAnUnknownConceptIsAMessageNotAnException()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteConstraint(folder.Root, "Pipeline.Nothing", "detail");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("Pipeline.Nothing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AConstraintReadingASlotTheConceptDoesNotDeclareIsRefused()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteConstraint(folder.Root, "Pipeline.Stage", "nothing");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("nothing", error, StringComparison.Ordinal);
    }

    /// <summary>Two concepts: a stage that owns a detail, and the detail it owns.</summary>
    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Pipeline.Stage",
            slots:
            [
                definition.Property("name", "string", defaultValue: "unnamed"),
                definition.Child("detail", "Pipeline.Step", "one"),
            ],
            nameSlot: "name");

        definition.Concept(
            "Pipeline.Step",
            slots: [definition.Property("name", "string", defaultValue: "unnamed")],
            nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        foreach (var document in definition.Documents)
        {
            ModelFile.Save(document, Path.Combine(structure, $"{document.Roots[0].GetOrNull(StructureLanguage.KeySlot)}.mps"));
        }
    }

    /// <summary>One rule, as its own document, the way a language project stores it.</summary>
    private static void WriteConstraint(string directory, string conceptKey, string slot)
    {
        var model = ConstraintLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(ConstraintLanguage.ConceptKeySlot, conceptKey);
        rule.Set(ConstraintLanguage.SeveritySlot, ConstraintSeverities.Hint);
        rule.Set(ConstraintLanguage.MessageSlot, "a stage needs a detail");

        model.NewChild(rule, ConstraintLanguage.BodySlot, ConstraintConceptKeys.EmptySlot)
            .Set(ConstraintLanguage.TargetSlot, slot);

        var constraints = Path.Combine(directory, LanguageProject.ConstraintsFolder);

        Directory.CreateDirectory(constraints);
        ModelFile.Save(model, Path.Combine(constraints, "stage-detail.mps"));
    }
}
