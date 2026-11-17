using Projectional.Core;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The template walk: what each part emits, and how a generator says it cannot.</summary>
public sealed class GeneratorTests
{
    [Fact]
    public void APropertyEmitsTheValueTheModelHolds()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property("name")), Sample());

        Assert.True(result.Succeeded);
        Assert.Equal("Payroll", result.Text);
    }

    [Fact]
    public void ALiteralIsEmittedAsWritten()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Literal("// payroll")), Sample());

        Assert.True(result.Succeeded);
        Assert.Equal("// payroll", result.Text);
    }

    [Fact]
    public void AChildIsExpandedWithItsOwnTemplate()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("Approved", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void ChildrenAreJoinedByTheSeparator()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        second.Set("name", "Rejected");

        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new ChildrenPart(RulesLanguage.RulesSlot, Literal(" | "))))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("Approved | Rejected", Generate(templates, ruleset).Text);
    }

    [Fact]
    public void IndentationAppliesToEveryLineBreakInside()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new TextPart("a"),
                new IndentPart(TextTemplate.Of(LineBreakPart.Instance, new ChildPart(RulesLanguage.RulesSlot)))))
            .Add(RulesConceptKeys.Rule, Literal("b"));

        Assert.Equal("a\n  b", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void TheIndentWidthBelongsToTheGenerator()
    {
        var templates = Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
            new IndentPart(TextTemplate.Of(LineBreakPart.Instance, new TextPart("b")))));

        var result = new Generator(indentWidth: 4).Generate(Sample(), new TextGenerator("test", templates));

        Assert.Equal("\n    b", result.Text);
    }

    [Fact]
    public void AConditionalPicksTheTemplateThePropertySelects()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))
            .Add(RulesConceptKeys.Rule, TextTemplate.Of(new ConditionalPart(
                "outcome",
                RuleOutcomes.Accept,
                Literal("accepts"),
                Literal("does not accept"))));

        Assert.Equal("accepts", Generate(templates, Sample()).Text);

        var model = PayrollSample.NewSampleModel();
        model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0].Set("outcome", RuleOutcomes.Reject);

        Assert.Equal("does not accept", Generate(templates, model.Roots[0]).Text);
    }

    [Fact]
    public void ALoopAppliesItsBodyToEachChild()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(new LoopPart(
                RulesLanguage.RulesSlot,
                TextTemplate.Of(new TextPart("<"), SelfPart.Instance, new TextPart(">")))))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("<Approved>", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void AMissingTemplateFailsNamingTheConcept()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("Rules.Rule", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyChildSlotFailsNamingTheSlot()
    {
        var model = PayrollSample.NewEmptyModel();

        var result = Generate(
            Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot)),
            model.Roots[0]);

        Assert.False(result.Succeeded);
        Assert.Contains(RulesLanguage.RulesSlot, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyTheConceptDoesNotDeclareFails()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property(RulesLanguage.RulesSlot)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("not a property", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASelfExpansionStopsAtTheDepthLimit()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(SelfPart.Instance)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("expands itself", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyTheModelDefaultedEmitsTheDefault()
    {
        // The model materialises a declared default, so a generator sees "unnamed" rather than nothing: a template
        // cannot tell a default from a value the user typed, and an engine that guessed would disagree with what the
        // document shows.
        var model = PayrollSample.NewEmptyModel();

        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property("name")), model.Roots[0]);

        Assert.True(result.Succeeded);
        Assert.Equal("unnamed", result.Text);
        Assert.Single(result.Regions);
    }

    [Fact]
    public void TwoRunsAgreeAndTheSecondStartsClean()
    {
        var generator = new Generator();

        var failing = generator.Generate(
            Sample(),
            new TextGenerator("test", Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))));

        var clean = generator.Generate(
            Sample(),
            new TextGenerator("other", Registry().Add(RulesConceptKeys.Ruleset, Property("name"))));

        // A run is independent of the one before it: the failure of the first leaks neither its text nor its state
        // into the second, which is what makes one generator usable for a session that generates many documents.
        Assert.False(failing.Succeeded);
        Assert.True(clean.Succeeded);
        Assert.Equal("Payroll", clean.Text);
    }

    [Fact]
    public void RegionsSayWhichNodeProducedWhichText()
    {
        var result = Generate(
            Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new TextPart("// "),
                new PropertyPart("name"))),
            Sample());

        // Both stretches are recorded: the literal carries the node but no slot, the property carries both - a
        // literal is still text of that node, so "go to source" on a keyword must find the node that emitted it.
        Assert.Equal(2, result.Regions.Count);
        Assert.Null(result.Regions[0].Slot);
        Assert.Equal(0, result.Regions[0].Start);
        Assert.Equal("name", result.Regions[1].Slot);
        Assert.Equal(3, result.Regions[1].Start);
        Assert.Equal(result.Regions[1], result.RegionAt(5));
        Assert.Equal(result.Regions[0], result.RegionAt(0));
    }

    private static TemplateRegistry Registry() => TemplateRegistry.Empty;

    private static Node Sample() => PayrollSample.NewSampleModel().Roots[0];

    private static GeneratedText Generate(TemplateRegistry templates, Node node) =>
        new Generator().Generate(node, new TextGenerator("test", templates));

    private static TextTemplate Property(string slot) => TextTemplate.Of(new PropertyPart(slot));

    private static TextTemplate Child(string slot) => TextTemplate.Of(new ChildPart(slot));

    private static TextTemplate Literal(string text) => TextTemplate.Of(new TextPart(text));
}
