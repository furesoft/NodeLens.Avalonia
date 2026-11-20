using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Semantics;
using Projectional.Structures;
using Projectional.TypeRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language project's third file: <c>types.mps</c> becomes the registry a constraint asks for types.
/// </summary>
public sealed class LanguageProjectTypesTests
{
    [Fact]
    public void AProjectWithTypesLoadsThem()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteTypes(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.True(language.HasTypes);

        var model = language.NewModel("document");
        var node = model.NewNode("Pipeline.Stage");

        Assert.Equal("int", language.Types.TypeOf(node).Name);
    }

    [Fact]
    public void AProjectWithoutTypesLoadsAndAnswersUnknown()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.False(language.HasTypes);

        var model = language.NewModel("document");

        // Nobody said what a stage evaluates to, so nothing is known - which is the honest answer.
        Assert.Same(UnknownType.Instance, language.Types.TypeOf(model.NewNode("Pipeline.Stage")));
    }

    [Fact]
    public void ATypesDocumentThatCannotBeLoweredIsAMessageNotAnException()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        // A rule naming a type the framework does not have.
        var types = TypeRuleLanguage.NewModel();
        var rule = types.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Pipeline.Stage");

        var body = types.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);

        types.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "decimal");

        var rules = Path.Combine(folder.Root, LanguageProject.TypesFolder);

        Directory.CreateDirectory(rules);
        ModelFile.Save(types, Path.Combine(rules, "Pipeline.Stage.mps"));

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("decimal", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AComplaintReachesTheLanguagesConstraints()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteComplaint(folder.Root, "Pipeline.Stage", "a stage is not typed", "warning");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        // A complaint is a diagnostic, not a type: the language has a constraint and still no type.
        Assert.True(language.HasConstraints);
        Assert.False(language.HasTypes);

        var model = language.NewModel("document");

        model.NewNode("Pipeline.Stage");

        var finding = Assert.Single(new ConstraintEngine(language.Constraints).Check(model));

        Assert.Equal("a stage is not typed", finding.Message);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void AComplaintAndATypeLiveSideBySide()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteTypes(folder.Root);
        WriteComplaint(folder.Root, "Pipeline.Stage", "a stage is not typed", "error");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.True(language.HasTypes);
        Assert.True(language.HasConstraints);

        var model = language.NewModel("document");
        var node = model.NewNode("Pipeline.Stage");

        // The type the rule answers is unchanged by the complaint, which only adds a finding beside it.
        Assert.Equal("int", language.Types.TypeOf(node).Name);
        Assert.Equal("a stage is not typed", Assert.Single(new ConstraintEngine(language.Constraints).Check(model)).Message);
    }

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Pipeline.Stage",
            slots: [definition.Property("name", "string", defaultValue: "unnamed")],
            nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Pipeline.Stage.mps"));
    }

    private static void WriteTypes(string directory)
    {
        var types = TypeRuleLanguage.NewModel();
        var rule = types.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Pipeline.Stage");

        var body = types.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);

        types.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        var rules = Path.Combine(directory, LanguageProject.TypesFolder);

        Directory.CreateDirectory(rules);
        ModelFile.Save(types, Path.Combine(rules, "Pipeline.Stage.mps"));
    }

    /// <summary>One complaint, as its own document, the way a language project stores it.</summary>
    private static void WriteComplaint(string directory, string conceptKey, string message, string severity)
    {
        var types = TypeRuleLanguage.NewModel();
        var rule = types.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, conceptKey);

        var body = types.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Complain);

        body.Set(TypeRuleLanguage.MessageSlot, message);
        body.Set(TypeRuleLanguage.SeveritySlot, severity);

        var rules = Path.Combine(directory, LanguageProject.TypesFolder);

        Directory.CreateDirectory(rules);
        ModelFile.Save(types, Path.Combine(rules, "stage-complaint.mps"));
    }
}
