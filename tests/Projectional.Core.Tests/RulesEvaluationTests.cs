using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a ruleset means when it is run: a rule is a predicate, and the ruleset is the first rule that applies.
/// </summary>
/// <remarks>
/// The sample document is <c>rule Approved when amount &gt; 1000 then Accept</c>, so the interesting runs are the
/// two inputs either side of the threshold - and the third, a table whose conditions leave a gap, because
/// inventing an outcome there would hide the gap rather than report it.
/// </remarks>
public sealed class RulesEvaluationTests
{
    [Fact]
    public void ARuleThatAppliesDecidesTheOutcome()
    {
        var model = PayrollSample.NewSampleModel();

        var result = Run(model, ("amount", new IntValue(1500)));

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue(RuleOutcomes.Accept), result.Value);
    }

    [Fact]
    public void ARuleThatDoesNotApplyLeavesTheRulesetWithoutAnOutcome()
    {
        var model = PayrollSample.NewSampleModel();

        var result = Run(model, ("amount", new IntValue(500)));

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.Evaluation, result.Failure!.Kind);
        Assert.Contains("No rule matched", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstRuleThatAppliesWins()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];

        // A second rule that also applies, placed after the first: a ruleset is a decision list, so order decides.
        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        second.Set("name", "Manual");
        second.Set("outcome", RuleOutcomes.Review);

        var always = model.NewChild(second, "condition", ExpressionsConceptKeys.BoolLiteral);
        always.Set("value", true);

        var result = Run(model, ("amount", new IntValue(1500)));

        Assert.Equal(new StringValue(RuleOutcomes.Accept), result.Value);
    }

    [Fact]
    public void ARuleWithoutAConditionIsAFailureAboutThatRule()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        var result = Run(model, ("amount", new IntValue(1500)));

        Assert.False(result.Succeeded);
        Assert.Equal(rule.Id, result.Failure!.Node!.Value);
        Assert.Contains("condition", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionThatIsNotATruthValueIsAFailure()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.IntLiteral);

        condition.Set("value", 5);

        var result = Run(model, ("amount", new IntValue(1500)));

        Assert.False(result.Succeeded);
        Assert.Contains("truth value", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTraceSaysWhichRuleAppliedAndWhy()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));
        var comparison = rule.GetChild("condition")!;

        var result = Run(model, ("amount", new IntValue(1500)));
        var entries = result.Trace;

        // The name is read, the threshold is read, the comparison decides, the rule reports that it applies,
        // and the ruleset answers with the outcome.
        Assert.Equal(
            [
                ExpressionsConceptKeys.Name,
                ExpressionsConceptKeys.IntLiteral,
                ExpressionsConceptKeys.Binary,
                RulesConceptKeys.Rule,
                RulesConceptKeys.Ruleset,
            ],
            entries.Select(entry => entry.ConceptKey));

        Assert.Equal([3, 3, 2, 1, 0], entries.Select(entry => entry.Depth));
        Assert.Equal(new IntValue(1500), entries[0].Value);
        Assert.Equal(new BoolValue(true), entries[3].Value);
        Assert.Equal(new StringValue(RuleOutcomes.Accept), entries[4].Value);
        Assert.Equal(comparison.Id, entries[2].Node);
    }

    [Fact]
    public void TheRulesRegistryRunsTheExpressionsItIsBuiltOn()
    {
        // A condition is an expression, so the rules registry carries the expression rules; without them a
        // ruleset would fail at its first comparison.
        var model = PayrollSample.NewSampleModel();
        var comparison = model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0].GetChild("condition")!;

        Assert.NotNull(RulesEvaluation.Registry.RuleFor(comparison.Concept));
        Assert.Same(
            ExpressionsEvaluation.Registry.RuleFor(comparison.Concept),
            RulesEvaluation.Registry.RuleFor(comparison.Concept));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InterpretationResult Run(Model model, params (string Name, Value Value)[] variables)
    {
        Dictionary<string, Value> bindings = new(StringComparer.Ordinal);

        foreach (var (name, value) in variables)
        {
            bindings[name] = value;
        }

        return new Interpreter(RulesEvaluation.Registry).Run(model.Roots[0], bindings, cancellationToken: Token);
    }
}
