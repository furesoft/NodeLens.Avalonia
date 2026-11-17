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
    public void ATypesFileThatCannotBeLoweredIsAMessageNotAnException()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        // A rule naming a type the framework does not have.
        var types = TypeRuleLanguage.NewModel();
        var document = types.NewNode(TypeRuleConceptKeys.Rules);
        var rule = types.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);
        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Pipeline.Stage");
        types.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed)
            .Set(TypeRuleLanguage.TypeNameSlot, "decimal");

        ModelFile.Save(types, Path.Combine(folder.Root, LanguageProject.TypesFile));

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("decimal", error, StringComparison.Ordinal);
    }

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Pipeline.Stage",
            slots: [definition.Property("name", "string", defaultValue: "unnamed")],
            nameSlot: "name");

        ModelFile.Save(definition.Root.Model, Path.Combine(directory, LanguageProject.StructureFile));
    }

    private static void WriteTypes(string directory)
    {
        var types = TypeRuleLanguage.NewModel();
        var document = types.NewNode(TypeRuleConceptKeys.Rules);
        var rule = types.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Pipeline.Stage");
        types.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed)
            .Set(TypeRuleLanguage.TypeNameSlot, "int");

        ModelFile.Save(types, Path.Combine(directory, LanguageProject.TypesFile));
    }
}
