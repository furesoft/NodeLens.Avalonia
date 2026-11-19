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
        }
        finally
        {
            temporary.Delete(recursive: true);
        }

        static void AssertRoundTrips(string folder, Project project, string target)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.mps"))
            {
                var path = Path.Combine(target, Path.GetFileName(file));

                ModelFile.Save(ModelFile.Load(project, file), path);

                Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(path));
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
