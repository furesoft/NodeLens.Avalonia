using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language ships library nodes: an <c>accessory</c> folder whose models every project of it contains.</summary>
/// <remarks>
/// The point is not that the folder loads - that is one assertion - but that a document can <em>point at</em> what it
/// holds. A reference stores its target's id, so an accessory has to arrive in every project with the same identities;
/// the second test is what would fail if the copy renumbered them.
/// </remarks>
public sealed class LanguageProjectAccessoryTests
{
    [Fact]
    public void AnAccessoryFolderBecomesTheLanguagesLibrary()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteAccessory(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var palette = Assert.Single(language.Accessory);

        Assert.Equal("palette", palette.Name);

        // And every project of the language holds it, with the same node, not merely the same name.
        Assert.Contains(language.NewProject().Models, model => model.Name == "palette");
    }

    [Fact]
    public void ADocumentCanReferenceALibraryNodeAndStillResolveAfterLoading()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteAccessory(folder.Root);

        var opening = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);

        // One project, because a reference never crosses one: the document and the library it points at are loaded
        // into the same project, which is exactly how a host opens a file of a language that ships accessories.
        var project = opening!.NewProject();
        var palette = Assert.Single(project.Models, model => model.Name == "palette");
        var red = Assert.Single(palette.Roots);

        // A document of the language, referring to the palette entry by identity - which is what the writer stores.
        var document = project.NewModel("figure");
        var figure = document.NewNode("Doc.Root");
        figure.Set("name", "Figure");
        document.AddRoot(figure);
        figure.SetReference("link", red);

        var path = Path.Combine(folder.Root, "figure.mps");

        ModelFile.Save(document, path);

        // A fresh project, the way a host loads a file: the accessory has to be there for the reference to resolve.
        var reopened = LanguageProject.Open(folder.Root, out error);

        Assert.Null(error);

        var loaded = ModelFile.Load(reopened!.NewProject(), path);
        var loadedFigure = Assert.Single(loaded.Roots);

        Assert.Equal(red.Id, loadedFigure.GetReference("link")!.Value.Target);
        Assert.Equal("red", loadedFigure.GetReference("link")!.Value.ResolveInfo);

        // And the target is a node of the accessory, not a copy that happens to share an id with nothing else.
        Assert.Equal(red.Id, reopened.NewProject().FindNode(red.Id)!.Id);
    }

    [Fact]
    public void TwoProjectsDoNotShareOneAccessoryNode()
    {
        // Each project gets its own copy, so an edit in one document cannot reach into another document's library.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteAccessory(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);

        var first = Assert.Single(language!.NewProject().Models, model => model.Name == "palette").Roots[0];
        var second = Assert.Single(language.NewProject().Models, model => model.Name == "palette").Roots[0];

        Assert.Equal(first.Id, second.Id);
        Assert.NotSame(first, second);

        first.Set("name", "changed");

        Assert.Equal("red", second.GetOrNull("name"));
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

    /// <summary>Writes the palette through the language itself, so the file is what the writer produces.</summary>
    private static void WriteAccessory(string directory)
    {
        var language = LanguageProject.Open(directory, out var error)
            ?? throw new InvalidOperationException(error);

        var palette = language.NewProject().NewModel("palette");
        var red = palette.NewNode("Doc.Item");

        red.Set("name", "red");
        palette.AddRoot(red);

        var accessory = Path.Combine(directory, LanguageProject.AccessoryFolder);

        Directory.CreateDirectory(accessory);
        ModelFile.Save(palette, Path.Combine(accessory, "palette.mps"));
    }
}
