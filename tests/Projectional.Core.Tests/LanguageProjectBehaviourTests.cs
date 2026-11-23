using Projectional.Behaviours;
using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Structures;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language folder declares what its concepts are called: the documents load, lower, and the name a concept computes is
/// the name its nodes answer with.
/// </summary>
/// <remarks>
/// The folder is written here rather than copied from <c>samples/</c>, because the sample languages name their nodes
/// through a name slot: the shape under test - a concept with no name slot, called after what it points at - needs a
/// structure this test declares itself. The sample's own behaviour is asserted where the sample is, in
/// <c>DeclaredAtlasTests</c>.
/// </remarks>
public sealed class LanguageProjectBehaviourTests
{
    [Fact]
    public void ABehaviourFolderNamesAConceptThatHasNoNameSlot()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Single(language.BehaviourDocuments);

        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        var link = project.NewModel("links").NewNode("Doc.Link");
        link.SetReference("to", item);

        // `Doc.Link` has no name slot, so the only answer there can be is the one its folder declares.
        Assert.False(link.IsNamed);
        Assert.Equal("Bedrock", link.Name);
    }

    [Fact]
    public void ABehaviourThatNamesAConceptTheStructureDoesNotDeclareIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Nope", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("is not a concept of this language", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageWithoutTheFolderNamesItsNodesThroughTheNameSlotItAlwaysHad()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Empty(language.BehaviourDocuments);

        var item = language.NewProject().NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        Assert.Equal("Bedrock", item.Name);
    }

    [Fact]
    public void ABehaviourOnAnAbstractConceptNamesEverySubtype()
    {
        // A name belongs to a concept, and a concept's subtypes inherit it - which is what makes one document enough for
        // a whole family of nodes.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Spot", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        var place = project.NewModel("places").NewNode("Doc.Place");
        place.SetReference("to", item);

        Assert.Equal("Bedrock", place.Name);
    }

    [Fact]
    public void ADependentLanguageKeepsTheNamesItsBaseDeclares()
    {
        // A name is part of a declaration now, so a language that depends on another has to receive the named ones - the
        // declaration the registry was built from, not the one the structure folder wrote.
        using var folder = new TemporaryDirectory();
        var baseFolder = Path.Combine(folder.Root, "Base");
        var dependentFolder = Path.Combine(folder.Root, "Dependent");

        WriteStructure(baseFolder);
        WriteBehaviour(baseFolder, conceptKey: "Doc.Link", slot: "to");
        WriteLanguageRoot(baseFolder, "Base");

        WriteLanguageRoot(dependentFolder, "Dependent", dependsOn: "Base");

        var structure = Path.Combine(dependentFolder, LanguageProject.StructureFolder);
        Directory.CreateDirectory(structure);

        var definition = new DefinitionBuilder();
        definition.Concept("Dep.Thing", slots: [definition.Property("name", "string")], nameSlot: "name");
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Dep.Thing.mps"));

        var baseLanguage = LanguageProject.Open(baseFolder, out var baseError);

        Assert.Null(baseError);
        Assert.NotNull(baseLanguage);

        var dependent = LanguageProject.Open(dependentFolder, new ResolvedLanguages(baseLanguage), out var error);

        Assert.Null(error);
        Assert.NotNull(dependent);
        Assert.NotNull(dependent.Concepts.Get("Doc.Link").NameFunction);
    }

    private static void WriteLanguageRoot(string directory, string name, string? dependsOn = null)
    {
        var model = new Project(StructureLanguage.Concepts).NewModel("language");
        var root = model.NewNode(StructureConceptKeys.Language);

        root.Set("name", name);
        model.AddRoot(root);

        if (dependsOn is not null)
        {
            model.NewChild(root, "dependencies", StructureConceptKeys.Dependency).Set("name", dependsOn);
        }

        Directory.CreateDirectory(directory);
        ModelFile.Save(model, Path.Combine(directory, LanguageProject.LanguageFile));
    }

    [Fact]
    public void ANameThatShadowsTheOneItInheritsIsWarnedAboutRatherThanRefused()
    {
        // The language is usable - the subtype's own body wins - so this is not an error. It is also almost never what
        // an author means, which is why the loaded project carries a warning rather than staying quiet.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Spot", slot: "to", file: "spot");
        WriteBehaviour(folder.Root, conceptKey: "Doc.Place", slot: "to", file: "place");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var warning = Assert.Single(language.Warnings);

        Assert.Equal(LanguageProject.BehaviourFolder, warning.Aspect);
        Assert.Contains("'Doc.Place' declares a name it inherits from 'Doc.Spot'", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageThatNamesEachConceptOnceWarnsAboutNothing()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Empty(language.Warnings);
    }

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Doc.Item", slots: [definition.Property("name", "string")], nameSlot: "name");
        definition.Concept("Doc.Link", slots: [definition.Reference("to", "Doc.Item")]);
        definition.Concept("Doc.Spot", slots: [definition.Reference("to", "Doc.Item")], isAbstract: true);
        definition.Concept("Doc.Place", supers: ["Doc.Spot"]);

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Item.mps"));
        ModelFile.Save(definition.Documents[1], Path.Combine(structure, "Doc.Link.mps"));
        ModelFile.Save(definition.Documents[2], Path.Combine(structure, "Doc.Spot.mps"));
        ModelFile.Save(definition.Documents[3], Path.Combine(structure, "Doc.Place.mps"));
    }

    private static void WriteBehaviour(string directory, string conceptKey, string slot, string file = "link")
    {
        var model = BehaviourLanguage.NewModel("link");
        var document = model.Roots[0];

        document.Set(BehaviourConceptKeys.ConceptSlot, conceptKey);

        var function = model.NewChild(document, BehaviourConceptKeys.FunctionsSlot, BehaviourConceptKeys.Function);
        function.Set(BehaviourConceptKeys.KindSlot, BehaviourConceptKeys.NameKind);

        var body = model.NewChild(function, BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Reference);
        body.Set(BehaviourConceptKeys.SlotSlot, slot);

        var behaviour = Path.Combine(directory, LanguageProject.BehaviourFolder);

        Directory.CreateDirectory(behaviour);
        ModelFile.Save(model, Path.Combine(behaviour, file + ".mps"));
    }
}
