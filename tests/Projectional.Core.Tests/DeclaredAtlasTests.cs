using Projectional.ConstraintRules;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.IntentionRules;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.ScopeRules;
using Projectional.Semantics;
using Projectional.Structures;
using Projectional.TextGen;
using Projectional.TypingRules;
using Projectional.TypeRules;
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

        // The route is a child of the map rather than a second document, which is what lets its stops see the
        // places around them.
        Assert.Contains("route loop", rendered, StringComparison.Ordinal);
        Assert.Contains("to peak", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStandaloneRouteNamesTheMapItLiesOn()
    {
        // The second document is a route of its own, and the map it belongs to is named across documents - which is
        // the reference a project-wide scope exists for.
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Document("route.mps"));

        var rendered = LayoutSnapshot.Render(model, language.Notations, width: 120);

        Assert.Contains("route detour on alpen", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLanguageDeclaresTypesAndTypingClaims()
    {
        var language = Language();

        Assert.True(language.HasTypes);
        Assert.True(language.HasTyping);

        var map = ModelFile.Load(language.NewProject(), Document("map.mps"));
        var places = map.Roots[0].GetChildren("places");

        // Three concepts, one type: a place, a landmark and a stop are the same thing to anything that reads a
        // route, which is what separating a type from a concept is for.
        Assert.All(places, place => Assert.Equal("place", language.Types.TypeOf(place).Name));
        var route = ModelFile.Load(language.NewProject(), Document("map.mps")).Roots[0].GetChildren("routes")[0];

        Assert.Equal("place", language.Types.TypeOf(route.GetChildren("stops")[0]).Name);

        // The claim is what makes typing create the node: letters in an empty slot are a place's name.
        var claim = Assert.IsType<TypingRule>(language.Typing.ClaimFor("Atlas.Place", "peak"));

        Assert.Equal("name", claim.PropertySlot);

        // A landmark claims the words a landmark is usually called, and nothing else claims them.
        Assert.IsType<TypingRule>(language.Typing.ClaimFor("Atlas.Landmark", "lake"));
        Assert.Null(language.Typing.ClaimFor("Atlas.Landmark", "summit"));
    }

    [Fact]
    public void TheLanguageStatesTwoFindingsAndTheGoodDocumentsHaveNeither()
    {
        var language = Language();

        Assert.True(language.HasConstraints);

        var engine = new ConstraintEngine(language.Constraints);

        // The sample's own documents are clean: every place is named and every stop points somewhere.
        Assert.Empty(engine.Check(ModelFile.Load(language.NewProject(), Document("map.mps"))));
        Assert.Empty(engine.Check(ModelFile.Load(language.NewProject(), Document("route.mps"))));
        Assert.True(language.HasScopes);

        // Two things it refuses, one of each severity, both of them declared rather than built in.
        var project = language.NewProject();
        var model = project.NewModel("findings");
        var map = model.NewNode("Atlas.Map");

        model.AddRoot(map);

        var place = model.NewChild(map, "places", "Atlas.Place");

        place.Set("name", "unnamed");

        var route = model.NewNode("Atlas.Route");

        model.AddRoot(route);
        model.NewChild(route, "stops", "Atlas.Stop");

        var findings = engine.Check(model).ToDictionary(finding => finding.Code!, StringComparer.Ordinal);

        Assert.Equal(2, findings.Count);
        Assert.Equal(DiagnosticSeverity.Hint, findings["atlas.place.name"].Severity);
        Assert.Equal(DiagnosticSeverity.Error, findings["atlas.stop.target"].Severity);
    }

    [Fact]
    public void TheThreeNameSourcesResolveWhatTheySay()
    {
        var language = Language();

        Assert.True(language.HasScopes);

        // One document, because two of the three sources are about it: the map holds its places and a route whose
        // stops point at them.
        var project = language.NewProject();
        var maps = ModelFile.Load(project, Document("map.mps"));
        var routes = ModelFile.Load(project, Document("route.mps"));

        var map = maps.Roots[0];
        var places = map.GetChildren("places");
        var landmark = places[1];
        var stop = map.GetChildren("routes")[0].GetChildren("stops")[0];

        // `local`: the entry is one of the map's own places, so the node's children are enough.
        Assert.Equal(landmark.Id, language.Scopes.ScopeOf(map, "entry").Resolve("lake").Entry.Node);

        // A name the map does not hold is not in its scope, which is what makes `local` local.
        Assert.Equal(ScopeResolutionKind.Missing, language.Scopes.ScopeOf(map, "entry").Resolve("alp").Kind);

        // `ancestors`: a stop is nested in a route which is nested in the map, so the places of that map are what its
        // enclosing nodes hold - the source a nested reference almost always wants.
        Assert.Equal(places[0].Id, language.Scopes.ScopeOf(stop, "to").Resolve("peak").Entry.Node);
        Assert.Equal(landmark.Id, language.Scopes.ScopeOf(stop, "to").Resolve("lake").Entry.Node);

        // `project`: the route of the other document names the map, and a project-wide scope is what makes a name
        // of another document visible. Its roots are what it offers, which is why the map is reachable and a place
        // inside it is not.
        Assert.Equal(map.Id, language.Scopes.ScopeOf(routes.Roots[0], "onMap").Resolve("alpen").Entry.Node);
        Assert.Equal(ScopeResolutionKind.Missing, language.Scopes.ScopeOf(routes.Roots[0], "onMap").Resolve("peak").Kind);
    }

    [Fact]
    public void TheLanguageOffersTwoIntentions()
    {
        var language = Language();

        Assert.True(language.HasIntentions);

        var model = language.NewProject().NewModel("intentions");
        var route = model.NewNode("Atlas.Route");

        model.AddRoot(route);
        route.Set("name", "loop");

        // An empty `stops` slot is the one place "Add the first stop" means something.
        Assert.Contains("Add the first stop", Offers(language, route, atEmptySlot: true));

        var stop = model.NewChild(route, "stops", "Atlas.Stop");

        Assert.Contains("Delete this stop", Offers(language, stop));
        Assert.DoesNotContain("Add the first stop", Offers(language, route));
    }
    [Fact]
    public void TheGeneratorWritesTheMapAsLines()
    {
        var language = Language();
        var generator = Assert.Single(language.Generators);
        var model = ModelFile.Load(language.NewProject(), Document("map.mps"));

        var text = new Projectional.Semantics.Generator().Generate(model.Roots[0], generator).Text;

        // The nested route has no map of its own, so its `on` clause is not written - a conditional part, not an
        // empty reference in the text.
        Assert.Equal(
            """
            map alpen at lake
            place peak at 1500m
            landmark lake at 410m
            route loop
            to peak
            to lake
            """,
            text);
    }

    [Fact]
    public void TheGeneratorWritesTheStandaloneRouteWithTheMapItLiesOn()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Document("route.mps"));

        var text = new Projectional.Semantics.Generator().Generate(model.Roots[0], Assert.Single(language.Generators)).Text;

        // The route's template ends with a line break, because a route usually has stops under it; this one has none.
        Assert.Equal("route detour on alpen\n", text);
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
            AssertRoundTrips(Path.Combine(folder, LanguageProject.TypesFolder), new Project(TypeRuleLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.TypingFolder), new Project(TypingLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.ConstraintsFolder), new Project(ConstraintLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.ScopesFolder), new Project(ScopeLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.IntentionsFolder), new Project(IntentionLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.GeneratorFolder), new Project(TextGenLanguage.Concepts), temporary.FullName);

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

    private static IReadOnlyList<string> Offers(LanguageProject language, Node node, bool atEmptySlot = false)
    {
        using var session = new EditorSession(node, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id && (!atEmptySlot || each.IsEmptySlot));

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        return [.. IntentionList.For(session).Select(offer => offer.Label)];
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
