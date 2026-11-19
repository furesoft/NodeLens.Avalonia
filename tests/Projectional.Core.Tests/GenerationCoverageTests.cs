using Projectional.Core;
using Projectional.Json;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What a generator cannot print, as findings a host can show before anyone presses Generate.</summary>
/// <remarks>
/// A generation run stops at the first concept nobody mapped, which is the right behaviour and a poor way to find out.
/// This is the same information as a list, so the editor can mark the document instead of surprising its author.
/// </remarks>
public sealed class GenerationCoverageTests
{
    [Fact]
    public void AConceptWithoutATemplateIsReportedAtItsFirstNode()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var first = model.NewChild(body, "notes", TestLanguage.Note);
        model.NewChild(body, "notes", TestLanguage.Note);

        var templates = TemplateRegistry.Empty
            .Add(TestLanguage.Rule, TextTemplate.Empty)
            .Add(TestLanguage.Body, TextTemplate.Empty);

        var finding = Assert.Single(GenerationCoverage.Of(model, new TextGenerator("test", templates)));

        // One finding per concept, not per node: the defect belongs to the language, and five hundred repetitions would
        // bury every other finding in the panel. The node is what makes it a place the user can be taken to.
        Assert.Equal(GenerationCoverage.MissingTemplateCode, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal(first.Id, finding.Target);
        Assert.Contains(TestLanguage.Note, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACoveredDocumentReportsNothing()
    {
        var model = TextProject();
        var root = model.NewNode(JsonConceptKeys.Null);

        var templates = TemplateRegistry.Empty.Add(root.Concept.Key, TextTemplate.Empty);

        Assert.Empty(GenerationCoverage.Of(model, new TextGenerator("test", templates)));
    }

    [Fact]
    public void ATemplateIsInheritedTheWayARunInheritsIt()
    {
        // The report must agree with the run, so it asks the same question: the concept's own template or the nearest
        // super concept's. A second lookup here would be a second answer to "can this be printed".
        var model = TextProject();
        model.NewNode(JsonConceptKeys.Array);

        var inherited = TemplateRegistry.Empty.Add(JsonConceptKeys.Value, TextTemplate.Empty);

        Assert.Empty(GenerationCoverage.Of(model, new TextGenerator("test", inherited)));
        Assert.Single(GenerationCoverage.Of(model, new TextGenerator("test", TemplateRegistry.Empty)));
    }

    [Fact]
    public void AConceptThatEmitsNothingIsCoveredRatherThanMissing()
    {
        // The distinction the whole thing rests on: Json.Comment has a template - the empty one - so it is a decided
        // silence, not a hole. A checker that reported it would tell an author to fix something that is correct.
        var model = TextProject();
        var container = model.NewNode(JsonConceptKeys.Object);
        model.NewChild(container, JsonLanguage.MembersSlot, JsonConceptKeys.Comment)
            .Set(JsonLanguage.TextSlot, "why");

        Assert.Empty(GenerationCoverage.Of(model, JsonTextGenerators.Json));
    }

    [Fact]
    public void EveryGeneratorIsAskedSeparately()
    {
        // A language has as many generators as it declares, so "can this be printed" has one answer per target. A
        // document covered for one and not for the other must say so, and name the one that stops: one merged sentence
        // would leave the user guessing which menu entry is the broken one.
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Rule);

        var covered = new TextGenerator("json", TemplateRegistry.Empty.Add(TestLanguage.Rule, TextTemplate.Empty));
        var uncovered = new TextGenerator("csharp", TemplateRegistry.Empty);

        var finding = Assert.Single(GenerationCoverage.Of(model, [covered, uncovered]));

        Assert.Contains("csharp", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("json", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageWithoutGeneratorsReportsNothing()
    {
        // "Cannot be printed" is not a defect of a document whose language declares no generator - the shell offers no
        // Generate entry at all, so a warning about it would be advice the user cannot act on.
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Rule);

        Assert.Empty(GenerationCoverage.Of(model, Array.Empty<TextGenerator>()));
    }

    [Fact]
    public void TheShippedSampleIsCoveredByBothOfItsGenerators()
    {
        // What the shell now asks on every change, over what the sample language actually declares: a healthy document
        // must not grow marks just because coverage is reported. A gap in RulesTextGenerators shows up here rather than
        // as a warning on the sample nobody can explain.
        var model = PayrollSample.NewSampleModel();

        Assert.Empty(GenerationCoverage.Of(model, RulesTextGenerators.All));
    }

    private static Model TextProject() => JsonLanguage.NewProject().NewModel("json");
}
