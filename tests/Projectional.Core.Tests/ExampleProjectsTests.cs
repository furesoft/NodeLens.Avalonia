using Projectional.CSharp;
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
        Assert.Equal("DeclaredExpressions", Name(catalogue, "Expressions"));
        Assert.Equal("Invoice", Name(catalogue, "Invoice"));
        Assert.Equal("Units", Name(catalogue, "Units"));
        Assert.Equal("Workflow", Name(catalogue, "Workflow"));
    }

    [Fact]
    public void AnExampleCarriesWhatItsRootSaysItIsFor()
    {
        // The menu shows the sentence on hover, which is the one place a user decides whether this is the language
        // they were looking for.
        var catalogue = SampleCatalogue();

        Assert.Equal("A build pipeline, as the steps it runs.", Example(catalogue, "Declared").Description);

        // A root that writes no sentence leaves the menu with nothing to show, rather than an empty tooltip.
        Assert.Null(Example(catalogue, "Units").Description);
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

    [Fact]
    public void EverySampleDeclaresAnAccessoryDocument()
    {
        // An accessory document is a model of the language itself - the vocabulary a reader of the documentation meets as
        // an example, and the one thing a language can show without a document of its syntax. Every sample ships one, so
        // a new sample that forgets arrives with a failing test instead of with a documentation page that is empty.
        var catalogue = SampleCatalogue();
        var language = new WorkbenchLanguage();

        var needed = catalogue
            .SelectMany(example => ExampleProjects.Needed(example, catalogue))
            .DistinctBy(example => example.Name, StringComparer.Ordinal)
            .ToArray();

        var opened = ExampleProjects.Load(language, needed, out var refused);

        Assert.Empty(refused);
        Assert.NotEmpty(opened);

        // C# is shipped rather than sampled: the shell builds it from its declarations (`LanguageProject.Shipped`, with no
        // accessory), and the folder the C# package ships holds its definitions, not a sample of a language someone
        // wrote. It is the one language the rule does not reach.
        var samples = opened
            .Where(sample => !string.Equals(sample.Name, CSharpLanguage.Name, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(samples);

        foreach (var sample in samples)
        {
            Assert.True(
                sample.Accessory.Count > 0,
                $"{sample.Name} declares no accessory document; it needs an 'accessory' folder with a model of itself");
        }
    }

    /// <summary>The example folders this test build copied, which is the set the app ships.</summary>
    private static IReadOnlyList<ExampleProject> SampleCatalogue() => ExampleProjects.Catalogue(AppContext.BaseDirectory);

    private static ExampleProject Example(IReadOnlyList<ExampleProject> catalogue, string folder) =>
        catalogue.Single(example => Path.GetFileName(example.Directory) == folder);

    private static string Name(IReadOnlyList<ExampleProject> catalogue, string folder) => Example(catalogue, folder).Name;

    private static void CreateExample(string root, string name) =>
        Directory.CreateDirectory(Path.Combine(root, name, LanguageProject.StructureFolder));
}
