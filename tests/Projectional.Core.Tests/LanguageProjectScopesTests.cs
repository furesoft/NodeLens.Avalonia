using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.ScopeRules;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language folder declares its scopes: the documents load, lower, and answer from the same provider a session
/// installs.
/// </summary>
/// <remarks>
/// The folder is written here rather than copied from <c>samples/</c>, because the sample languages have no reference
/// in the one document a scope rule needs - this test is the first place a language both declares a reference and
/// says where its names come from.
/// </remarks>
public sealed class LanguageProjectScopesTests
{
    [Fact]
    public void AScopesFolderBecomesTheLanguagesProviders()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteScope(folder.Root, conceptKey: "Doc.Root", slot: "link", place: ScopePlaces.Project, target: "Doc.Item");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.True(language.HasScopes);
        Assert.Equal(1, language.Scopes.Count);

        // And the provider answers: an item of another model is visible, which is what "in the project" means.
        var project = language.NewProject();
        var here = project.NewModel("here");
        var root = here.NewNode("Doc.Root");
        root.Set("name", "Here");

        var there = project.NewModel("there");
        var item = there.NewNode("Doc.Item");
        item.Set("name", "There");

        Assert.Equal(item.Id, language.Scopes.ScopeOf(root, "link").Resolve("There").Entry.Node);

        // The same concept's other reference slots were never named, so they keep the default chain.
        Assert.Equal(
            DefaultScopeProvider.Instance.ScopeOf(root, "other").Entries,
            language.Scopes.ScopeOf(root, "other").Entries);
    }

    [Fact]
    public void ARuleThatNamesAPropertyIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        // `name` is a property; only a reference has a scope, and the folder is the earliest moment both halves exist.
        WriteScope(folder.Root, conceptKey: "Doc.Root", slot: "name", place: ScopePlaces.Model);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("reference slot", error, StringComparison.Ordinal);
    }

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Doc.Root",
            slots: [definition.Property("name", "string"), definition.Reference("link", "Doc.Item")],
            nameSlot: "name");

        definition.Concept("Doc.Item", slots: [definition.Property("name", "string")], nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Root.mps"));
        ModelFile.Save(definition.Documents[1], Path.Combine(structure, "Doc.Item.mps"));

    }

    private static void WriteScope(string directory, string conceptKey, string slot, string place, string? target = null)
    {
        var model = ScopeLanguage.NewModel("link");
        var rule = model.Roots[0];

        rule.Set(ScopeLanguage.ConceptKeySlot, conceptKey);
        rule.Set(ScopeLanguage.SlotSlot, slot);
        rule.Set(ScopeLanguage.PlaceSlot, place);

        if (target is not null)
        {
            rule.Set(ScopeLanguage.TargetSlot, target);
        }

        var scopes = Path.Combine(directory, LanguageProject.ScopesFolder);

        Directory.CreateDirectory(scopes);
        ModelFile.Save(model, Path.Combine(scopes, "link.mps"));
    }
}
