using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The order a list slot's children take when sorted by name, and when there is no such order.</summary>
/// <remarks>
/// Three refusals are the point of this file: a slot that holds one child, a list too short to have an order, and a
/// child whose concept declares no name. Each answers <see langword="null"/>, because "this list cannot be sorted" is
/// an ordinary answer rather than an error.
/// </remarks>
public sealed class ChildOrderTests
{
    [Fact]
    public void TheOrderIsByValueAndCaseSensitive()
    {
        var (ruleset, _, _) = Ruleset("beta", "Alpha", "alpha");

        var order = ChildOrder.ByName(ruleset, RulesLanguage.RulesSlot);

        Assert.NotNull(order);
        Assert.Equal(["Alpha", "alpha", "beta"], [.. order.Select(rule => rule.GetOrNull("name"))]);
    }

    [Fact]
    public void TwoChildrenWithOneNameKeepTheirOrder()
    {
        var (ruleset, first, second) = Ruleset("same", "same");

        var order = ChildOrder.ByName(ruleset, RulesLanguage.RulesSlot);

        Assert.NotNull(order);
        Assert.Equal([first.Id, second.Id], [.. order.Select(rule => rule.Id)]);
    }

    [Fact]
    public void ALongerNameComesAfterAShorterOneThatItStartsWith()
    {
        var (ruleset, _, _) = Ruleset("rule", "rule2", "rule10");

        var order = ChildOrder.ByName(ruleset, RulesLanguage.RulesSlot);

        Assert.NotNull(order);

        // Ordinal, not numeric: "rule10" sorts before "rule2" because that is what comparing text means.
        Assert.Equal(["rule", "rule10", "rule2"], [.. order.Select(rule => rule.GetOrNull("name"))]);
    }

    [Fact]
    public void AListInOrderWouldNotChange()
    {
        var (ruleset, _, _) = Ruleset("alpha", "beta");

        Assert.False(ChildOrder.WouldChange(ruleset, RulesLanguage.RulesSlot));
    }

    [Fact]
    public void AListOutOfOrderWouldChange()
    {
        var (ruleset, _, _) = Ruleset("beta", "alpha");

        Assert.True(ChildOrder.WouldChange(ruleset, RulesLanguage.RulesSlot));
    }

    [Fact]
    public void ASingleValuedSlotHasNoOrder()
    {
        var model = RulesLanguage.NewProject().NewModel("single");
        var rule = model.NewNode(RulesConceptKeys.Rule);
        model.AddRoot(rule);
        model.NewChild(rule, "condition", ExpressionsConceptKeys.Name);

        Assert.Null(ChildOrder.ByName(rule, "condition"));
        Assert.False(ChildOrder.WouldChange(rule, "condition"));
    }

    [Fact]
    public void OneChildIsNothingToRearrange()
    {
        var (ruleset, _, _) = Ruleset("alpha");

        Assert.Null(ChildOrder.ByName(ruleset, RulesLanguage.RulesSlot));
    }

    [Fact]
    public void AChildWithoutANameHasNoOrderToTake()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        model.NewChild(body, "steps", TestLanguage.Step);
        model.NewChild(body, "steps", TestLanguage.Step);

        // Test.Step declares no name slot, so its list cannot be sorted by one - not even partly.
        Assert.Null(ChildOrder.ByName(body, "steps"));
        Assert.False(ChildOrder.WouldChange(body, "steps"));
    }

    /// <summary>A ruleset holding one rule per entry of <paramref name="names"/>.</summary>
    private static (Node Ruleset, Node First, Node Second) Ruleset(params string[] names)
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        model.AddRoot(ruleset);

        List<Node> rules = [];

        foreach (var name in names)
        {
            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", name);
            rules.Add(rule);
        }

        return (ruleset, rules[0], rules[^1]);
    }
}
