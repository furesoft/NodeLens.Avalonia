using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Semantics;
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
    public void TheWorkflowStatesWhatItsLibraryMeans()
    {
        // The folder's evaluation document is the sample's worked example of the bodies that pick a value out of a list
        // and refuse when there is none: `first` over the library, `when` a step runs a command, `then` that command,
        // and `otherwise` a sentence. Two documents the folder already ships prove both halves, so the rule is checked
        // against the language as a reader meets it rather than against a model built for the test.
        var language = Language();
        var rules = Assert.IsType<EvaluationRegistry>(language.Evaluation);

        // `release.mps` keeps one step in its library, "shared build", and that step runs dotnet build.
        var released = Document(language, "release.mps");

        Assert.Equal(new StringValue("dotnet build"), Run(rules, released));

        // `draft.mps` has no library at all, so nothing is tried and the refusal speaks instead of the framework's
        // "no rule for this concept".
        var failure = new Interpreter(rules)
            .Run(Document(language, "draft.mps"), cancellationToken: TestContext.Current.CancellationToken)
            .Failure;

        Assert.NotNull(failure);
        Assert.Equal("no step in the library runs a command, so there is nothing to do.", failure.Message);
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
    public void TheDeclaredExtractMovesAStepIntoTheLibraryAndLeavesACallBehind()
    {
        // The other direction of the same pair, on the same document: `test` and the chain it carries move into the
        // library, where the workflow keeps what it reuses, and a call to it stays where it stood.
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));
        var workflow = model.Roots[0];
        var build = workflow.GetChild("first")!;
        var test = build.GetChild("next")!;
        var id = test.Id;

        using var session = new EditorSession(workflow, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == test.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        var offer = Assert.Single(IntentionList.For(session), each => each.Label == "Extract to the library");

        Assert.Equal(EditSafety.Plain, offer.Safety);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);

        // The step itself, with the rest of the chain it carried and its identity intact.
        var library = workflow.GetChildren("library");

        Assert.Equal(2, library.Count);
        Assert.Equal(id, library[1].Id);
        Assert.Equal("dotnet test", library[1].GetOrNull("command"));
        Assert.Equal("Workflow.Call", library[1].GetChild("next")!.Concept.Key);

        // What stands where it stood is a call named after it, pointing back at it.
        var call = build.GetChild("next")!;

        Assert.Equal("Workflow.Call", call.Concept.Key);
        Assert.Equal("test", call.GetOrNull("name"));
        Assert.Equal(id, call.GetReference("calls")!.Value.Target);
        Assert.Null(call.GetChild("next"));

        // The caret followed the node into its new place: the id did not change, so the cell it named still exists.
        Assert.Equal(id, session.Selection.Caret.Address.Owner);

        session.History.Undo();

        Assert.Equal(id, build.GetChild("next")!.Id);
        Assert.Single(workflow.GetChildren("library"));
    }

    [Fact]
    public void InliningWhatAnExtractLeftBehindPutsTheChainBack()
    {
        // The two declared bodies are inverses on this document: the call extract left behind is exactly what the
        // inline consumes. The definition stays in the library, which is the plan's answer to its own open question.
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));
        var workflow = model.Roots[0];
        var build = workflow.GetChild("first")!;

        using var session = new EditorSession(workflow, language.Notations);

        session.AddIntentions(language.Intentions, IntentionSource.Language);

        Caret(session, build.GetChild("next")!);
        session.Apply(IntentionList.For(session).Single(each => each.Label == "Extract to the library"));

        Caret(session, build.GetChild("next")!);
        session.Apply(IntentionList.For(session).Single(each => each.Label == "Inline this call"));

        // The chain runs the commands itself again, and the library is what the extract left in it.
        var restored = build.GetChild("next")!;

        Assert.Equal("Workflow.Step", restored.Concept.Key);
        Assert.Equal("dotnet test", restored.GetOrNull("command"));
        Assert.Equal("Workflow.Call", restored.GetChild("next")!.Concept.Key);
        Assert.Equal(2, workflow.GetChildren("library").Count);
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
            AssertRoundTrips(Path.Combine(folder, "evaluation"), new Project(Projectional.EvaluationRules.EvaluationRuleLanguage.Concepts), temporary.FullName);

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

    /// <summary>The root of the sample document <paramref name="name"/>, read from the folder the app reads.</summary>
    private static Node Document(LanguageProject language, string name) =>
        ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Workflow", name)).Roots[0];

    /// <summary>What the sample's declared rules answer for <paramref name="node"/>.</summary>
    private static Value Run(EvaluationRegistry rules, Node node)
    {
        var result = new Interpreter(rules).Run(node, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Failure is null, result.Failure?.Message);

        return result.Value!;
    }

    /// <summary>Puts the caret in the first place of <paramref name="node"/>, which is how a gesture starts.</summary>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
