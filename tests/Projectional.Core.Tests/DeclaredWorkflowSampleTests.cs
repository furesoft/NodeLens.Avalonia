using Projectional.Core;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A second sample language folder: two concepts with a reference, a notation, a scope rule, two intentions and one
/// document.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline sample proves the M2 gate and has no reference in it, so the scope and intension aspects had no data
/// demonstrator. This folder is that: <c>Workflow.Step.onFailure</c> is a reference to another step, the
/// <c>scopes</c> document says the chain above a step is what it may point at, and the <c>intentions</c> documents
/// declare what the language offers at a step.
/// </para>
/// <para>
/// The definitions are generated with <c>ModelFile.Save</c> rather than written by hand, so the folder holds what the
/// writer writes; the tests read exactly the files the app reads.
/// </para>
/// </remarks>
public sealed class DeclaredWorkflowSampleTests
{
    [Fact]
    public void TheFolderLoadsWithItsScopeRuleAndItsIntentions()
    {
        var language = Language();

        Assert.True(language.HasNotation);
        Assert.True(language.HasScopes);
        Assert.True(language.HasIntentions);

        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Workflow.Workflow");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Workflow.Step");
    }

    [Fact]
    public void TheReferenceSeesTheEnclosingSteps()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));

        var build = model.Roots[0].GetChild("first")!;
        var test = build.GetChild("next")!;

        // `in ancestors`: a step is nested in the step before it, so the chain above is what `onFailure` may point at.
        var scope = language.Scopes.ScopeOf(test, "onFailure");

        Assert.Equal(build.Id, scope.Resolve("build").Entry.Node);

        // A name that is not a step above it is not visible.
        Assert.Equal(Projectional.Semantics.ScopeResolutionKind.Missing, scope.Resolve("Release").Kind);
    }

    [Fact]
    public void TheDocumentIsDrawnAsOneLineOfSteps()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));

        var rendered = LayoutSnapshot.Render(model, language.Notations, width: 120);

        Assert.Contains("workflow Release", rendered, StringComparison.Ordinal);
        Assert.Contains("dotnet test", rendered, StringComparison.Ordinal);

        // The reference is unset in the sample, which draws its unset text rather than nothing.
        Assert.Contains("<none>", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredIntentionsAreOfferedAtAStep()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));
        var build = model.Roots[0].GetChild("first")!;

        using var session = new EditorSession(build, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == build.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        var offers = IntentionList.For(session).ToDictionary(offer => offer.Label, offer => offer);

        Assert.Contains("Delete this step", offers.Keys);
        Assert.Contains("Mark as a test", offers.Keys);
        Assert.Equal(EditSafety.Destructive, offers["Delete this step"].Safety);
        Assert.Equal(EditSafety.Plain, offers["Mark as a test"].Safety);
    }

    [Fact]
    public void TheDeclaredInlineReplacesACallWithACopyOfTheStepItCalls()
    {
        // The use-shape the refactoring plan asked for, in a shipped language: `release.mps` ends with a call to
        // `build`, and inlining it runs those commands here instead - a copy, so `build` keeps its own place in the
        // chain. It is the one case where the two halves of R2 meet: a declaration in `intentions/` and the framework's
        // copy-then-delete.
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));
        var workflow = model.Roots[0];
        var build = workflow.GetChild("first")!;
        var test = build.GetChild("next")!;
        var call = test.GetChild("next")!;
        var shared = workflow.GetChildren("library")[0];

        Assert.Equal("Workflow.Call", call.Concept.Key);

        using var session = new EditorSession(workflow, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == call.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        var offer = Assert.Single(IntentionList.For(session), each => each.Label == "Inline this call");

        Assert.Equal(EditSafety.Destructive, offer.Safety);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);

        // What stood where the call stood is now a step running the commands the shared step runs - a copy, with its
        // own id.
        var inlined = test.GetChild("next")!;

        Assert.Equal("Workflow.Step", inlined.Concept.Key);
        Assert.Equal("dotnet build", inlined.GetOrNull("command"));
        Assert.NotEqual(shared.Id, inlined.Id);

        // And the definition is untouched, in the library where it belongs: a copy, not a move.
        Assert.Same(shared, Assert.Single(workflow.GetChildren("library")));

        session.History.Undo();

        Assert.Equal("Workflow.Call", test.GetChild("next")!.Concept.Key);
    }
    [Fact]
    public void TheInsertEntryIsOfferedOnTheEmptySlotAndFillsIt()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "draft.mps"));
        var workflow = model.Roots[0];

        using var session = new EditorSession(workflow, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        // `first` is empty in this document, so the only place the entry means something is that cell.
        var place = CaretPlaces.Of(session.Tree).First(each => each.IsEmptySlot);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        var offer = Assert.Single(IntentionList.For(session), each => each.Label == "Add the first step");

        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Equal("Workflow.Step", Assert.Single(workflow.GetChildren("first")).Concept.Key);
    }

    [Fact]
    public void TheInsertEntryIsAbsentWhereTheSlotIsFilled()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));
        var workflow = model.Roots[0];

        using var session = new EditorSession(workflow, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == workflow.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        Assert.DoesNotContain(IntentionList.For(session), each => each.Label == "Add the first step");
    }

    [Fact]
    public void TheFolderNamesItself()
    {
        // The sample shipped without a root, so the language answered with the folder's name on this machine and the
        // app's example menu read `declared-workflow`. The root is what makes the folder a language with a name.
        var language = Language();

        Assert.Equal("Workflow", language.Name);
        Assert.Equal("1.0.0", language.Version);
    }

    [Fact]
    public void TheDefinitionsSaveBackByteIdentically()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Workflow");
        var temporary = Directory.CreateTempSubdirectory("declared-workflow");

        try
        {
            AssertRoundTrips(Path.Combine(folder, "structure"), new Project(Projectional.Structures.StructureLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, "editor"), new Project(Projectional.Editors.EditorLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, "scopes"), new Project(Projectional.ScopeRules.ScopeLanguage.Concepts), temporary.FullName);
            AssertRoundTrips(Path.Combine(folder, "intentions"), new Project(Projectional.IntentionRules.IntentionLanguage.Concepts), temporary.FullName);

            // The root sits beside the aspect folders rather than in one, so it needs its own call.
            AssertRoundTrips(
                folder,
                new Project(Projectional.Structures.StructureLanguage.Concepts),
                temporary.FullName,
                LanguageProject.LanguageFile);
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

    private static LanguageProject Language()
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Workflow"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return language;
    }
}
