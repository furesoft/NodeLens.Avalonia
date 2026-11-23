using System.Reflection;
using System.Runtime.Loader;
using Projectional.Behaviours;
using Projectional.ConstraintRules;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.ImportRules;
using Projectional.IntentionRules;
using Projectional.Language;
using Projectional.LanguageExport;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Testing;
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

        Assert.Contains("route detour on alpen from summit", rendered, StringComparison.Ordinal);
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
    public void TheGeneratedTextIsWriteOnlyBecauseATemplateCannotReadAReferenceBack()
    {
        // The limit the sample ran into, pinned as a fact: the importer that a generator gives refuses a template that
        // writes a reference, because the text has nowhere to resolve into. Atlas writes its map entry and its stops as
        // references, so its own text is not the format it reads back.
        var language = Language();

        Assert.False(language.TryCreateImporter("AtlasText", out var importer, out var error));
        Assert.Null(importer);
        Assert.Contains("names a reference", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredImporterReadsTheReferenceFreeFormat()
    {
        // What import can do today: a declared format that fills properties and children. It names no place, so the
        // map's entry and the route's stops stay unsaid - which is the honest shape of the gap rather than a format
        // that silently drops them.
        var language = Language();

        Assert.True(language.TryCreateImporter("places", out var importer, out var error), error);
        Assert.NotNull(importer);

        var model = language.NewProject().NewModel("imported");
        var result = importer.Import(model, "map alpen\nplace peak at 1500m\nlandmark lake at 410m");

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var map = Assert.Single(result.Roots);

        Assert.Equal("Atlas.Map", map.Concept.Key);
        Assert.Equal("alpen", map.GetOrNull("name"));
        Assert.Equal(
            ["Atlas.Place/peak/1500m", "Atlas.Landmark/lake/410m"],
            map.GetChildren("places").Select(place => $"{place.Concept.Key}/{place.GetOrNull("name")}/{place.GetOrNull("at")}"));

        // Nothing said where the map is entered, and the importer does not invent it.
        Assert.Null(map.GetReference("entry"));
    }
    [Fact]
    public void TheLanguageShipsALibraryOfPlaces()
    {
        // An `accessory` folder is not a definition but an ordinary model of the language, and every project of it
        // starts with that model - which is what lets a document point at a library node and keep the pointer across a
        // save and a load.
        var language = Language();
        var library = Assert.Single(language.Accessory, model => model.Name == "known-places");

        // The base's library travels too, because an accessory is a model of the vocabulary and Atlas can name a
        // `Units.Quantity` - which is why the assertion above selects by name instead of expecting one document.
        Assert.Contains(language.Accessory, model => model.Name == "quantities");
        Assert.Equal(["summit", "valley", "ridge"], library.Roots.Select(place => place.GetOrNull("name")));

        // Two projects of the same language hold the library with the same identities, which is the whole point: a
        // reference stores an id, so a document's pointer into the library has to survive a reload.
        var first = Assert.Single(language.NewProject().Models, model => model.Name == "known-places");
        var second = Assert.Single(language.NewProject().Models, model => model.Name == "known-places");

        Assert.Equal(first.Roots.Select(place => place.Id), second.Roots.Select(place => place.Id));
        Assert.Equal(first.Roots[0].Id, library.Roots[0].Id);
    }

    [Fact]
    public void TheRouteStartsAtAPlaceFromTheLibrary()
    {
        var language = Language();
        var library = Assert.Single(language.Accessory, model => model.Name == "known-places");
        var summit = library.Roots[0];
        var model = ModelFile.Load(language.NewProject(), Document("route.mps"));
        var route = model.Roots[0];

        // The text the route was written with says which library place it starts at, and the reference itself resolves
        // through the project - which is the source the library is visible from.
        Assert.Equal(summit.Id, route.GetReference("start")?.Target);
        Assert.Equal(summit.Id, language.Scopes.ScopeOf(route, "start").Resolve("summit").Entry.Node);
    }

    [Fact]
    public void TheLanguagesOwnTestsPass()
    {
        // The `tests` aspect, run by the framework's own runner: these are the sample's assertions about itself, so a
        // change to the notation, the generator or the findings fails here rather than in a reader's head.
        var language = Language();
        var report = TestRunner.Run(language);

        Assert.True(report.Ran >= 3, $"only {report.Ran} assertions ran");
        Assert.True(report.Succeeded, report.ToString());

        Assert.Contains(report.Results, result => result.Succeeded);
    }

    [Fact]
    public void TheTypesExportAsCodeThatAnswersWhatTheFolderDoes()
    {
        // The aspect the export could not write for longest, proved the only way that counts: the generated source is
        // compiled, loaded, and asked the same question the folder's registry was asked. A named record and three
        // rules that name it survive the trip, which is what makes an exported language a language rather than a
        // vocabulary.
        var language = Language();
        var result = LanguageExporter.Export(language, "Sample.Atlas", ExportOutput.Assembly);
        var source = result.Files.Single(file => file.Name.StartsWith("Atlas", StringComparison.Ordinal)).Text;

        Assert.Contains("public static TypeRegistry Types { get; } = TypeRegistry.Empty", source, StringComparison.Ordinal);
        Assert.Contains(".Add(\"Atlas.Place\", new FixedTypeRule(Place))", source, StringComparison.Ordinal);
        Assert.Contains("new NamedType(\"place\", new RecordType([(\"name\", PrimitiveType.Of(PropertyType.String))", source, StringComparison.Ordinal);

        var context = new AssemblyLoadContext("atlas-types", isCollectible: true);
        var assembly = context.LoadFromStream(new MemoryStream(result.Assembly!.Bytes));
        var type = assembly.GetType("Sample.Atlas.AtlasLanguage", throwOnError: true)!;

        var exported = (TypeRegistry)type.GetProperty("Types", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        Assert.Equal(language.Types.Count, exported.Count);

        var model = ModelFile.Load(language.NewProject(), Document("map.mps"));
        var place = model.Roots[0].GetChildren("places")[0];
        var landmark = model.Roots[0].GetChildren("places")[1];

        Assert.Equal(language.Types.TypeOf(place).Name, exported.TypeOf(place).Name);
        Assert.Equal("place", exported.TypeOf(place).Name);
        Assert.Equal("place", exported.TypeOf(landmark).Name);
    }

    [Fact]
    public void TheGeneratorIsDrawnOnePartPerLine()
    {
        // The staircase this replaced: the parts of a template were drawn side by side, so every part's own indent
        // pushed the rest of the line right and a generator read as a diagonal. A template is a sequence of parts, so
        // each one gets its own line and the nesting stays one level per step.
        var model = ModelFile.Load(
            new Project(TextGenLanguage.Concepts),
            Path.Combine(AppContext.BaseDirectory, "Atlas", "generator", "atlas-text.mps"));

        var lines = LayoutSnapshot.Render(model, TextGenNotations.Registry, width: 120).Split('\n');

        Assert.Equal(
            [
                "  generate AtlasText",
                "      map Atlas.Map to",
                "        template",
                "          \"map \"",
                "          $name",
                "          when entry is filled:",
                "            template",
                "              \" at \"",
                "              <entry>",
                "            otherwise:",
                "              template",
                "                << ... >>",
                "          newline",
                "          [places] separated by",
                "            template",
                "              newline",
            ],
            lines.Take(16));
    }

    [Fact]
    public void TheFullSampleDeclaresEveryAspectTheFrameworkKnows()
    {
        // The rule the plan in docs/architecture/full-sample.md states and this test enforces: an aspect added to the
        // framework has to arrive here, or the sample stops being the folder that shows everything.
        var language = Language();
        var folder = Path.Combine(AppContext.BaseDirectory, "Atlas");

        // The one list, so an aspect added to `AspectFolders` is an aspect this sample must carry.
        Assert.DoesNotContain(LanguageProject.AspectFolders, aspect => !Directory.Exists(Path.Combine(folder, aspect)));

        // And the other direction: a folder the sample ships that the framework does not name is a folder nothing
        // opens, which is how `tests` would be forgotten if it were not named below.
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(folder).Select(Path.GetFileName),
            aspect => aspect != TestRunner.Folder && !LanguageProject.AspectFolders.Contains(aspect!, StringComparer.Ordinal));

        // A folder is not an aspect: each one has to have lowered into something the language uses, which is what a
        // `types/` directory holding nothing would not.
        Assert.True(language.HasNotation);
        Assert.True(language.HasTypes);
        Assert.True(language.HasTyping);
        Assert.True(language.HasConstraints);
        Assert.True(language.HasScopes);
        Assert.True(language.HasIntentions);
        Assert.True(language.HasGenerators);
        Assert.True(language.HasDeclaredImporters);
        Assert.NotEmpty(language.Accessory);

        // The tests folder is beside the aspects rather than in that list, and it is an aspect all the same.
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(folder, TestRunner.Folder), "*.mps"));
        Assert.True(TestRunner.Run(language).Succeeded);

        // And the root states the one language this one was built on.
        Assert.Equal(["Units"], language.Dependencies.Select(dependency => dependency.Name));
    }

    [Fact]
    public void TheDefinitionsSaveBackByteIdentically()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Atlas");
        var temporary = Directory.CreateTempSubdirectory("declared-atlas");

        try
        {
            AssertRoundTrips(Path.Combine(folder, LanguageProject.StructureFolder), new Project(StructureLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.BehaviourFolder), new Project(BehaviourLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.EditorFolder), new Project(EditorLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.TypesFolder), new Project(TypeRuleLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.TypingFolder), new Project(TypingLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.ConstraintsFolder), new Project(ConstraintLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.ScopesFolder), new Project(ScopeLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.IntentionsFolder), new Project(IntentionLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.GeneratorFolder), new Project(TextGenLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.ImportFolder), new Project(ImportLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, LanguageProject.AccessoryFolder), new Project(Language().Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, Projectional.Testing.TestRunner.Folder), new Project(TestingLanguage.Concepts), temporary.FullName);

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

    [Fact]
    public void TheBehaviourFolderNamesAStopAfterThePlaceItStopsAt()
    {
        var language = Language();

        // The one behaviour the sample declares, lowered into the concept itself: a Stop has no name slot, so the name a
        // reference to it displays is the name of the place it stops at.
        Assert.Single(language.BehaviourDocuments);
        Assert.NotNull(language.Concepts.Get("Atlas.Stop").NameFunction);

        var map = ModelFile.Load(language.NewProject(), Document("map.mps")).Roots[0];
        var stops = map.SelfAndDescendants().Where(node => node.Concept.Key == "Atlas.Stop").ToList();

        // The document's two stops point at `peak` and at `lake`, and neither names itself.
        Assert.Equal(["peak", "lake"], stops.Select(stop => stop.Name));
    }

    [Fact]
    public void TheFolderStatesWhatAMapAndAStopMean()
    {
        var language = Language();
        var rules = Assert.IsType<EvaluationRegistry>(language.Evaluation);
        var map = ModelFile.Load(language.NewProject(), Document("map.mps")).Roots[0];

        // A map answers how many places it holds - the document it ships with holds two, a place and a landmark, and both
        // are spots in the slot that is counted.
        Assert.Equal(new IntValue(2), Run(rules, map));

        // A stop answers with what the place it points at answers - the reference is followed and that node's own rule
        // decides - and a place answers with a label built out of two of its properties, or with the word the language uses
        // while it has no name at all.
        var route = map.GetChildren("routes")[0];
        var stops = route.GetChildren("stops");

        Assert.Equal(new StringValue("peak at 1500m"), Run(rules, stops[0]));
        Assert.Equal(new StringValue("lake at 410m"), Run(rules, stops[1]));

        // A route answers the list of what its stops answer, in the order it stops there: the list body composes the reference,
        // the label and the place's own rule into one value.
        Assert.Equal(
            new ListValue([new StringValue("peak at 1500m"), new StringValue("lake at 410m")]),
            Run(rules, route));

        // A place without a position answers with its name alone, and a fresh one is called `unnamed` because that is the
        // language's own default for the property - which is why the guard asks about `at` and not about `name`.
        var unplaced = map.Model.NewChild(map, "places", "Atlas.Place");

        Assert.Equal(new StringValue("unnamed"), Run(rules, unplaced));
        Assert.Equal(new StringValue("unnamed at 900m"), Run(rules, Placed(unplaced, "900m")));

        // A stop that points nowhere is refused, with the sentence that says so rather than an invented name.
        var unlinked = map.Model.NewChild(route, "stops", "Atlas.Stop");
        var failure = new Interpreter(rules).Run(unlinked, cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("points nowhere", failure.Message, StringComparison.Ordinal);
    }
    /// <summary>Gives the place no position… and then one, so a rule that guards on it has both sides.</summary>
    private static Node Placed(Node place, string at)
    {
        place.Set("at", at);

        return place;
    }
    /// <summary>What the sample's declared rules answer for <paramref name="node"/>.</summary>
    private static Value Run(EvaluationRegistry rules, Node node)
    {
        var result = new Interpreter(rules).Run(node, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Failure is null, result.Failure?.Message);

        return result.Value!;
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
