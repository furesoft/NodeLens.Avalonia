using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Per-language scope overrides: a concept that declares a provider answers for itself and for every
/// concept below it, and everything else falls through to the default chain.
/// </summary>
public sealed class ScopeProviderRegistryTests
{
    [Fact]
    public void AnEmptyRegistryAnswersWithTheDefaultChain()
    {
        var (rule, _) = Ruleset("Approved", "Rejected");

        Assert.Equal(0, ScopeProviderRegistry.Empty.Count);
        Assert.Null(ScopeProviderRegistry.Empty.ProviderFor(rule.Concept));

        // The default chain sees the sibling, which is what "nothing declared" means.
        Assert.Equal(
            ScopeResolutionKind.Found,
            ScopeProviderRegistry.Empty.ScopeOf(rule, "inherits").Resolve("Rejected").Kind);
    }

    [Fact]
    public void ADeclaredProviderAnswersForItsConcept()
    {
        var (rule, _) = Ruleset("Approved", "Rejected");
        var registry = ScopeProviderRegistry.Empty.Add(RulesConceptKeys.Rule, new NothingVisible());

        Assert.NotNull(registry.ProviderFor(rule.Concept));
        Assert.Equal(ScopeResolutionKind.Missing, registry.ScopeOf(rule, "inherits").Resolve("Rejected").Kind);
    }

    [Fact]
    public void AProviderIsInheritedByTheConceptsBelowIt()
    {
        var (rule, _) = Ruleset("Approved", "Rejected");

        // Declared on the abstract Expression, asked for a concrete one: the lookup walks the super
        // concepts, the same way notation and triggers resolve.
        var registry = ScopeProviderRegistry.Empty.Add("Expressions.Expression", new NothingVisible());
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode("Expressions.IntLiteral");

        Assert.NotNull(registry.ProviderFor(literal.Concept));
    }

    [Fact]
    public void AConceptWithNoProviderFallsThroughToTheDefault()
    {
        var (rule, _) = Ruleset("Approved", "Rejected");
        var registry = ScopeProviderRegistry.Empty.Add("Something.Else", new NothingVisible());

        Assert.Null(registry.ProviderFor(rule.Concept));
        Assert.Equal(ScopeResolutionKind.Found, registry.ScopeOf(rule, "inherits").Resolve("Rejected").Kind);
    }

    [Fact]
    public void AddingAnswersANewRegistryAndLeavesTheOldOneAlone()
    {
        var first = ScopeProviderRegistry.Empty.Add(RulesConceptKeys.Rule, new NothingVisible());
        var second = first.Add(RulesConceptKeys.Ruleset, new NothingVisible());

        Assert.Equal(1, first.Count);
        Assert.Equal(2, second.Count);
    }

    private static (Node Rule, Node Ruleset) Ruleset(string firstName, string secondName)
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var first = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        first.Set("name", firstName);

        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", secondName);

        return (first, ruleset);
    }

    /// <summary>A provider that sees nothing, which is the smallest thing that is not the default.</summary>
    private sealed class NothingVisible : IScopeProvider
    {
        public IScope ScopeOf(Node node, string referenceSlot) => Scope.Empty;
    }
}
