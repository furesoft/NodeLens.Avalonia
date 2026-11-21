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
                definition.Child("detail", "Pipeline.Step", "1"),
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

    /// <summary>The rules the language in a dependent folder carries, standing on a base by one of the two lists.</summary>
    /// <remarks>
    /// `extends` adopts the base language, so its constraints about a shared concept are in force; `depends on` lends
    /// the vocabulary only, so they are not ([ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).
    /// </remarks>
    private static ConstraintSet ConstraintsOf(string? extends = null, string? dependency = null)
    {
        using var coreFolder = new TemporaryDirectory();
        using var dependentFolder = new TemporaryDirectory();

        WriteStructure(coreFolder.Root);
        WriteConstraint(coreFolder.Root, "Pipeline.Stage", "detail");
        WriteRoot(coreFolder.Root, "Core");

        WriteOtherStructure(dependentFolder.Root);
        WriteRoot(dependentFolder.Root, "Rules", extends, dependency);

        var core = LanguageProject.Open(coreFolder.Root, out _)!;

        return (LanguageProject.Open(dependentFolder.Root, new Answers(core), out var error) ?? throw new InvalidOperationException(error)).Constraints;
    }

    [Fact]
    public void AnExtendedLanguagesConstraintsAreInherited()
    {
        Assert.Equal(1, ConstraintsOf(extends: "Core").Count);
    }

    [Fact]
    public void ADependedOnLanguagesConstraintsAreNotInherited()
    {
        Assert.Equal(0, ConstraintsOf(dependency: "Core").Count);
    }

    /// <summary>The host's answer: the languages it was given, by name.</summary>
    private sealed class Answers(params LanguageProject[] languages) : ILanguageResolver
    {
        public LanguageProject? Find(string name) =>
            languages.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.Ordinal));
    }

    /// <summary>A concept of the dependent language, so its folder declares something of its own.</summary>
    private static void WriteOtherStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Pipeline.Other", slots: [definition.Property("name", "string")], nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Pipeline.Other.mps"));
    }

    /// <summary>The language root, saying what it stands on.</summary>
    private static void WriteRoot(string directory, string name, string? extends = null, string? dependency = null)
    {
        var model = StructureLanguage.NewModel("language");
        var root = model.NewNode(StructureConceptKeys.Language);

        root.Set("name", name);

        if (extends is not null)
        {
            model.NewChild(root, StructureLanguage.ExtendsSlot, StructureConceptKeys.Dependency).Set("name", extends);
        }

        if (dependency is not null)
        {
            model.NewChild(root, StructureLanguage.DependenciesSlot, StructureConceptKeys.Dependency).Set("name", dependency);
        }

        ModelFile.Save(model, Path.Combine(directory, LanguageProject.LanguageFile));
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
