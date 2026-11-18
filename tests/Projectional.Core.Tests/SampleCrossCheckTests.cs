using System.Text.Json;
using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The pilot DSL's two derivations of one document must answer the same thing: the interpreter, which decides
/// from the model, and the generated text, which a consumer reads instead of the model.
/// </summary>
/// <remarks>
/// <para>
/// This is the cheapest correctness net a language framework can have, and it is the one the roadmap asks for as
/// M4's "cross-check tests": a generator and an interpreter read the same tree and must agree about every input.
/// The two sides are deliberately derived differently, or the test would only prove that a routine equals itself:
/// the decision comes from <see cref="RulesEvaluation"/> (first rule whose condition holds), the reason comes from
/// <b>the generated JSON</b>, and the rule that fired is found by evaluating each condition with
/// <see cref="ExpressionsEvaluation"/> - the shipped expression rules, assembled the other way round.
/// </para>
/// <para>
/// Two things this cannot do, named so nobody reads more into a green run. The generated C# fragment is pinned as
/// text by <see cref="RulesTextGeneratorsTests"/> and never executed, because this repository compiles nothing;
/// the cross-check therefore runs over the JSON generator, which is parseable. And the comparison is about
/// <em>decisions</em> - which rule fires and what it returns - not about the condition's text, because text would
/// have to be parsed back into a tree and ADR 0002 (`docs/adr/0002-model-as-source-of-truth.md`) is exactly the
/// decision not to own a parser.
/// </para>
/// </remarks>
public sealed class SampleCrossCheckTests
{
    [Theory]
    [InlineData(1500, RuleOutcomes.Accept)]
    [InlineData(700, RuleOutcomes.Review)]
    [InlineData(100, RuleOutcomes.Reject)]
    [InlineData(500, RuleOutcomes.Reject)]
    public void TheGeneratedTableDecidesWhatTheInterpreterDecides(int amount, string expected)
    {
        var ruleset = DecisionTable();
        var variables = Inputs(amount);

        var decision = InterpreterSays(ruleset, variables);

        Assert.Equal(expected, decision);

        var rows = GeneratedRows(ruleset);
        var fired = FirstRuleWhoseConditionHolds(ruleset, rows.Length, variables);

        Assert.True(fired >= 0, "No rule applied, so the table has a gap the interpreter should have failed on.");
        Assert.Equal(decision, rows[fired].GetProperty("outcome").GetString());
    }

    [Fact]
    public void TheGeneratedTableHoldsEveryRuleInDocumentOrder()
    {
        var ruleset = DecisionTable();

        var rows = GeneratedRows(ruleset);

        Assert.Equal(
            ruleset.GetChildren(RulesLanguage.RulesSlot).Select(rule => rule.GetOrNull("name")),
            rows.Select(row => row.GetProperty("rule").GetString()));
    }

    /// <summary>
    /// Three rules, because a one-rule table cannot show that the <em>first</em> match decides: the sample's own
    /// rule comes first, a narrower one after it, and a catch-all last, so each of the three inputs stops at a
    /// different rule.
    /// </summary>
    private static Node DecisionTable()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];

        RuleAbove(model, ruleset, name: "Escalate", outcome: RuleOutcomes.Review, threshold: 500);
        Always(model, ruleset, name: "Rejected", outcome: RuleOutcomes.Reject);

        return ruleset;
    }

    private static Node RuleAbove(Model model, Node ruleset, string name, string outcome, int threshold)
    {
        var rule = NewRule(model, ruleset, name, outcome);
        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.Binary);
        condition.Set("operator", ">");

        model.NewChild(condition, "left", ExpressionsConceptKeys.Name).Set("name", "amount");
        model.NewChild(condition, "right", ExpressionsConceptKeys.IntLiteral).Set("value", threshold);

        return rule;
    }

    private static Node Always(Model model, Node ruleset, string name, string outcome)
    {
        var rule = NewRule(model, ruleset, name, outcome);

        model.NewChild(rule, "condition", ExpressionsConceptKeys.BoolLiteral).Set("value", true);

        return rule;
    }

    private static Node NewRule(Model model, Node ruleset, string name, string outcome)
    {
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", name);
        rule.Set("outcome", outcome);

        return rule;
    }

    private static Dictionary<string, Value> Inputs(int amount) =>
        new(StringComparer.Ordinal) { ["amount"] = new IntValue(amount) };

    /// <summary>The decision the language's own rules reach, which is one side of the comparison.</summary>
    private static string InterpreterSays(Node ruleset, IReadOnlyDictionary<string, Value> variables)
    {
        var run = new Interpreter(RulesEvaluation.Registry)
            .Run(ruleset, variables, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(run.Succeeded, run.Failure?.ToString());

        return Assert.IsType<StringValue>(run.Value).Content;
    }

    /// <summary>The other side: what a consumer of the generated text sees, read as data.</summary>
    private static JsonElement[] GeneratedRows(Node ruleset)
    {
        var generated = new Generator().Generate(ruleset, RulesTextGenerators.Json);

        Assert.True(generated.Succeeded, generated.Failure?.ToString());

        using var document = JsonDocument.Parse(generated.Text);

        // Cloned, because a JsonElement is a view into the document: the rows outlive this method's `using`.
        return [.. document.RootElement.GetProperty("rules").EnumerateArray().Select(row => row.Clone())];
    }

    /// <summary>
    /// Which rule the generated table would fire, found without <see cref="RulesEvaluation"/>: every condition is
    /// evaluated on its own with the expression rules, and the table's own order decides the first match.
    /// </summary>
    private static int FirstRuleWhoseConditionHolds(
        Node ruleset,
        int rowCount,
        IReadOnlyDictionary<string, Value> variables)
    {
        var rules = ruleset.GetChildren(RulesLanguage.RulesSlot);

        Assert.Equal(rules.Count, rowCount);

        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index].GetChild("condition") is not { } condition)
            {
                continue;
            }

            var applies = new Interpreter(ExpressionsEvaluation.Registry)
                .Run(condition, variables, cancellationToken: TestContext.Current.CancellationToken);

            if (applies is { Succeeded: true, Value: BoolValue { Content: true } })
            {
                return index;
            }
        }

        return -1;
    }
}
