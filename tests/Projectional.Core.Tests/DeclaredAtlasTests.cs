using Projectional.Core;
using Projectional.Editors;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The full sample: one language folder that declares every aspect the framework knows, so a new aspect has a place
/// to be shown and a reader has one folder to look at instead of seven.
/// </summary>
/// <remarks>
/// <para>
/// This first slice is the shape of the language - a root that says what it is for, six concepts including an
/// abstract one and two roots, a notation for each, and two documents. The remaining aspects arrive one slice at a
/// time and are recorded in <c>docs/architecture/full-sample.md</c>; the tests that pin them come with them.
/// </para>
/// <para>
/// Atlas declares a dependency on <c>Units</c> and borrows <c>Units.Quantity</c> for a place's height, so loading it
/// needs the base the same way the app's example menu does - which is the point of the sample having a dependency at
/// all rather than a comment saying dependencies work.
/// </para>
/// </remarks>
public sealed class DeclaredAtlasTests
{
    [Fact]
    public void TheFolderLoadsWithItsRootAndItsConcepts()
    {
        var language = Language();

        Assert.Equal("Atlas", language.Name);
        Assert.Equal("1.0.0", language.Version);
        Assert.Equal("A map of places and the routes that stop at them.", language.Description);
        Assert.Equal([new LanguageDependency("Units", "1.0.0")], language.Dependencies);

        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Map");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Route");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Place");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Landmark");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Stop");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Atlas.Spot");

        // The inheritance the sample demonstrates: `Spot` is the set a map's places are declared over, and an empty
        // slot declared over it therefore fills with a concrete kind of spot rather than with the abstract concept -
        // which of the two depends on the order the folder declares them, and that order is the author's answer.
        Assert.True(language.Concepts.Get("Atlas.Spot").IsAbstract);

        var filled = language.Concepts.DefaultInstantiableFor("Atlas.Spot");

        Assert.NotNull(filled);
        Assert.True(filled.IsKindOf("Atlas.Spot"));

        // The reference the route's stop is made of, declared over the abstract set rather than over one kind.
        Assert.Equal("Atlas.Place", language.Concepts.Get("Atlas.Stop").FindReference("to")?.TargetConceptKey);

        // And the concept it borrows from the language it stands on is in the registry that opened it.
        Assert.True(language.Concepts.TryGet("Units.Quantity", out _));
    }

    [Fact]
    public void TheMapIsDrawnAsAHeaderOverItsPlaces()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Document("map.mps"));

        var rendered = LayoutSnapshot.Render(model, language.Notations, width: 120);

        Assert.Contains("map alpen", rendered, StringComparison.Ordinal);

        // One line per place, indented under the header, each drawn by the notation of the concept it is - the
        // inherited one for `Landmark` differs from the one `Place` declares.
        Assert.Contains("place peak at 1500m", rendered, StringComparison.Ordinal);
        Assert.Contains("landmark lake at 410m", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRouteIsDrawnAsAHeaderOverItsStops()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Document("route.mps"));

        var rendered = LayoutSnapshot.Render(model, language.Notations, width: 120);

        Assert.Contains("route loop", rendered, StringComparison.Ordinal);

        // Two stops, and every stop starts with the keyword its notation puts in front of the reference.
        Assert.Equal(2, rendered.Split("to ", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void TheDefinitionsSaveBackByteIdentically()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Atlas");
        var temporary = Directory.CreateTempSubdirectory("declared-atlas");

        try
        {
            AssertRoundTrips(Path.Combine(folder, LanguageProject.StructureFolder), new Project(StructureLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.EditorFolder), new Project(EditorLanguage.Concepts), temporary.FullName);

            // The root sits beside the aspect folders rather than in one, so it needs its own call.
            AssertRoundTrips(folder, new Project(StructureLanguage.Concepts), temporary.FullName, LanguageProject.LanguageFile);

            // The documents are the language's own model, which is what a user opens and edits.
            var documents = Language().NewProject();

            AssertRoundTrips(folder, documents, temporary.FullName, "map.mps");
            AssertRoundTrips(folder, documents, temporary.FullName, "route.mps");
        }
        finally
        {
            temporary.Delete(recursive: true);
        }

        static void AssertRoundTrips(string folder, Project project, string target, string? file = null)
        {
            var files = file is null
                ? Directory.EnumerateFiles(folder, "*.mps")
                : [Path.Combine(folder, file)];

            foreach (var source in files)
            {
                var path = Path.Combine(target, Path.GetFileName(source));

                ModelFile.Save(ModelFile.Load(project, source), path);

                Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(path));
            }
        }
    }

    private static string Document(string name) => Path.Combine(AppContext.BaseDirectory, "Atlas", name);

    private static LanguageProject Language()
    {
        var units = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Units"), out var unitError);

        Assert.Null(unitError);
        Assert.NotNull(units);

        var language = LanguageProject.Open(
            Path.Combine(AppContext.BaseDirectory, "Atlas"),
            new ResolvedLanguages(units),
            out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return language;
    }
}
