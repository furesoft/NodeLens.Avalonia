using Projectional.Core;
using Projectional.Editing;
using Projectional.Persistence;
using Projectional.Samples;
using Projectional.Structures;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The standalone editor knows the languages it is written with, so a definition can be written in it.
/// </summary>
/// <remarks>
/// This is the M2 claim in the smallest verifiable form: the app's one registry declares the sample language *and*
/// the two definition languages, so a <c>structure.mps</c> opens, is edited and is saved like any other document -
/// no mode, no switching, no second editor. Without them the same file would fail to load with "unknown concept",
/// which is why the registries are asserted rather than assumed.
/// </remarks>
public sealed class WorkbenchLanguageTests
{
    [Fact]
    public void TheAppKnowsAllThreeLanguages()
    {
        var concepts = WorkbenchLanguage.Concepts;

        Assert.True(concepts.TryGet(RulesConceptKeys.Ruleset, out _));
        Assert.True(concepts.TryGet(StructureConceptKeys.Language, out _));
        Assert.True(concepts.TryGet(Projectional.Editors.EditorConceptKeys.Notations, out _));
    }

    [Fact]
    public void EveryConceptOfAllThreeHasANotation()
    {
        // A registered concept with no notation still opens, as a "missing notation" cell - which is the one thing
        // a user cannot work around, so the app's composition is checked rather than trusted.
        foreach (var concept in WorkbenchLanguage.Concepts.Concepts.Where(each => !each.IsAbstract))
        {
            Assert.True(
                WorkbenchLanguage.Notations.TryGet(concept.Key, out _),
                $"'{concept.Key}' has no notation in the app's registry");
        }
    }

    [Fact]
    public void ANewLanguageDefinitionOpensInASessionAndCanBeWritten()
    {
        var model = WorkbenchLanguage.NewLanguageDefinition();

        using var session = new EditorSession(
            model.Roots[0],
            WorkbenchLanguage.Notations,
            typing: WorkbenchLanguage.Typing);

        // The definition is editable with the same actions every document uses: the user types the concept's key.
        var concept = model.NewChild(model.Roots[0], StructureLanguage.ConceptSlot, StructureConceptKeys.Concept);

        concept.Set(StructureLanguage.KeySlot, "Rules.Rule");
        session.Refresh();

        Assert.Contains("Rules.Rule", Projectional.Projection.LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void ADefinitionSurvivesARoundTripThroughTheAppsLanguage()
    {
        // Loading is the part that would fail without the composition: the reader resolves every concept against
        // the project's registry, so a forgotten language is a file that cannot be opened.
        var model = WorkbenchLanguage.NewLanguageDefinition();

        model.NewChild(model.Roots[0], StructureLanguage.ConceptSlot, StructureConceptKeys.Concept)
            .Set(StructureLanguage.KeySlot, "Rules.Rule");

        var path = Path.Combine(Path.GetTempPath(), $"definition-{Guid.NewGuid():N}.mps");

        try
        {
            ModelFile.Save(model, path);

            var loaded = DocumentFile.TryLoad(path, out var error);

            Assert.Null(error);
            Assert.NotNull(loaded);

            var concept = Assert.Single(loaded.Roots[0].GetChildren(StructureLanguage.ConceptSlot));

            Assert.Equal("Rules.Rule", concept.GetOrNull(StructureLanguage.KeySlot));
            Assert.Equal(
                ModelXml.Serialize(model),
                ModelXml.Serialize(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ANotationDefinitionOpensToo()
    {
        var model = WorkbenchLanguage.NewNotationDefinition();

        using var session = new EditorSession(model.Roots[0], WorkbenchLanguage.Notations);

        Assert.Contains(
            "<no notations>",
            Projectional.Projection.LayoutSnapshot.Render(session.Layout, width: 120),
            StringComparison.Ordinal);
    }
}
