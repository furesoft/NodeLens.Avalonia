using Projectional.Languages;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The examples a build places beside the executable: which folders count, what they are called, and what has to be
/// opened before one of them can be.
/// </summary>
/// <remarks>
/// The catalogue is asserted headlessly because the menu is a one-line caller. What can go wrong is a folder that is
/// not a language, an order, a name nobody declared - and the dependency: <c>declared-invoice</c> extends
/// <c>Units</c>, whose example sorts after it.
/// </remarks>
public sealed class ExampleProjectsTests
{
    [Fact]
    public void OnlyAFolderThatHoldsAStructureIsAnExample()
    {
        using var folder = new TemporaryDirectory();

        Directory.CreateDirectory(Path.Combine(folder.Root, "a-language", LanguageProject.StructureFolder));
        Directory.CreateDirectory(Path.Combine(folder.Root, "not-a-language"));
        File.WriteAllText(Path.Combine(folder.Root, "README.md"), "not an example");

        var example = Assert.Single(ExampleProjects.Catalogue(folder.Root));

        Assert.Equal(Path.Combine(folder.Root, "a-language"), example.Directory);
    }

    [Fact]
    public void TheExamplesComeInOrdinalOrderOfTheirFolders()
    {
        using var folder = new TemporaryDirectory();

        CreateExample(folder.Root, "b");
        CreateExample(folder.Root, "a");
        CreateExample(folder.Root, "c");

        Assert.Equal(
            ["a", "b", "c"],
            ExampleProjects.Catalogue(folder.Root).Select(example => Path.GetFileName(example.Directory)));
    }

    [Fact]
    public void NoFolderIsNoExamples()
    {
        // A publish ships none of them, and the menu asks this before it decides whether to show itself.
        using var folder = new TemporaryDirectory();

        Assert.Empty(ExampleProjects.Catalogue(Path.Combine(folder.Root, "missing")));
    }

    [Fact]
    public void AnExampleIsNamedByItsLanguageRoot()
    {
        var catalogue = SampleCatalogue();

        Assert.Equal("Pipeline", Name(catalogue, "Declared"));
        Assert.Equal("Expressions", Name(catalogue, "Expressions"));
        Assert.Equal("Invoice", Name(catalogue, "Invoice"));
        Assert.Equal("Units", Name(catalogue, "Units"));
        Assert.Equal("Workflow", Name(catalogue, "Workflow"));
    }

    [Fact]
    public void AnExampleWithoutARootIsNamedByItsFolder()
    {
        // The fallback, and the reason it exists: a folder is a language the moment it holds a `structure` folder, and
        // the prefix the samples share is not part of the name a reader wants to meet.
        using var folder = new TemporaryDirectory();

        CreateExample(folder.Root, "declared-demo");

        var example = Assert.Single(ExampleProjects.Catalogue(folder.Root));

        Assert.Equal("Demo", example.Name);
        Assert.Empty(example.StandsOn);
    }

    [Fact]
    public void TheDeclarationSaysWhichExampleStandsOnWhich()
    {
        var catalogue = SampleCatalogue();

        Assert.Contains("Units", Example(catalogue, "Invoice").StandsOn);
        Assert.Empty(Example(catalogue, "Units").StandsOn);
        Assert.Empty(Example(catalogue, "Expressions").StandsOn);
    }

    [Fact]
    public void AnExampleBringsWhatItStandsOn()
    {
        // Dependencies first, because `LanguageProject.Open` resolves a declared language through the set: invoice
        // cannot load before units is in it.
        var catalogue = SampleCatalogue();
        var needed = ExampleProjects.Needed(Example(catalogue, "Invoice"), catalogue);

        Assert.Equal(["Units", "Invoice"], needed.Select(example => example.Name));

        var language = new WorkbenchLanguage();
        var opened = ExampleProjects.Load(language, needed, out var refused);

        Assert.Empty(refused);
        Assert.Equal(2, opened.Count);

        var invoice = opened.Single(project => project.Name == "Invoice");
        var units = opened.Single(project => project.Name == "Units");

        Assert.Contains(invoice.Inherited, inherited => inherited.Name == units.Name);

        // The vocabulary of the base is in the set, which is what makes the dependent usable and not merely loaded.
        Assert.True(language.Concepts.TryGet("Units.Quantity", out _));
    }

    [Fact]
    public void AnExampleWithoutItsBaseIsRefused()
    {
        // The user-visible half of the ordering: a catalogue that does not hold units has to leave invoice refusing to
        // load, and say which language it is missing rather than throwing.
        var catalogue = SampleCatalogue();
        var invoice = Example(catalogue, "Invoice");

        var needed = ExampleProjects.Needed(invoice, [invoice]);
        var opened = ExampleProjects.Load(new WorkbenchLanguage(), needed, out var refused);

        Assert.Empty(opened);
        var reason = Assert.Single(refused);

        Assert.Contains("Units", reason, StringComparison.Ordinal);
    }

    /// <summary>The example folders this test build copied, which is the set the app ships.</summary>
    private static IReadOnlyList<ExampleProject> SampleCatalogue() => ExampleProjects.Catalogue(AppContext.BaseDirectory);

    private static ExampleProject Example(IReadOnlyList<ExampleProject> catalogue, string folder) =>
        catalogue.Single(example => Path.GetFileName(example.Directory) == folder);

    private static string Name(IReadOnlyList<ExampleProject> catalogue, string folder) => Example(catalogue, folder).Name;

    private static void CreateExample(string root, string name) =>
        Directory.CreateDirectory(Path.Combine(root, name, LanguageProject.StructureFolder));
}
