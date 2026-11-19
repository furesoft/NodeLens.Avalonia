using Projectional.Core;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The chain a language gets when it says nothing: local, then the enclosing blocks, then the model,
/// then the project.
/// </summary>
public sealed class DefaultScopeProviderTests
{
    [Fact]
    public void ASiblingIsVisible()
    {
        var (ruleset, first, second) = Ruleset("Approved", "Rejected");

        var scope = DefaultScopeProvider.Instance.ScopeOf(first, "inherits");

        Assert.Equal(second.Id, scope.Resolve("Rejected").Entry.Node);
    }

    [Fact]
    public void TheNodeItselfIsNotInItsOwnScope()
    {
        var (_, first, _) = Ruleset("Approved", "Rejected");

        // A reference that could point at the thing holding it is a cycle the model already refuses.
        Assert.Equal(ScopeResolutionKind.Missing, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Approved").Kind);
    }

    [Fact]
    public void AnEnclosingNodeIsVisible()
    {
        var (ruleset, first, _) = Ruleset("Approved", "Rejected");

        Assert.Equal(ruleset.Id, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Payroll").Entry.Node);
    }

    [Fact]
    public void AModelRootIsVisibleFromInsideIt()
    {
        var (ruleset, first, _) = Ruleset("Approved", "Rejected");

        // The ruleset is both an ancestor and a root; the point is that a root is reachable at all.
        Assert.Equal(ruleset.Id, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Payroll").Entry.Node);
    }

    [Fact]
    public void ARootOfAnotherModelInTheProjectIsVisible()
    {
        var project = RulesLanguage.NewProject();
        var first = project.NewModel("first");
        var second = project.NewModel("second");

        var here = first.NewNode(RulesConceptKeys.Ruleset);
        here.Set("name", "Here");

        var there = second.NewNode(RulesConceptKeys.Ruleset);
        there.Set("name", "There");

        Assert.Equal(there.Id, DefaultScopeProvider.Instance.ScopeOf(here, "inherits").Resolve("There").Entry.Node);
    }

    [Fact]
    public void TwoSiblingsWithOneNameMakeItAmbiguousForAThirdNode()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        foreach (var name in new[] { "Approved", "Approved", "Other" })
        {
            model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", name);
        }

        var other = ruleset.GetChildren(RulesLanguage.RulesSlot)[2];

        // From the first "Approved" the name is unambiguous - it means the other one. It is ambiguous
        // for a node that is neither of them, which is the case a silent pick would get wrong.
        Assert.Equal(ScopeResolutionKind.Ambiguous, DefaultScopeProvider.Instance.ScopeOf(other, "inherits").Resolve("Approved").Kind);
    }

    [Fact]
    public void AnUnnamedNodeContributesNothing()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        // The rule's name is its concept's default, which is a name - so clear it to prove the rule.
        rule.Set("name", string.Empty);

        Assert.DoesNotContain(
            DefaultScopeProvider.Instance.ScopeOf(rule, "inherits").Entries,
            entry => entry.Node == rule.Id);
    }

    [Fact]
    public void ARenamedNodeIsFoundUnderItsNewName()
    {
        var (_, first, second) = Ruleset("Approved", "Rejected");

        // The first lookup is what builds the index, so the assertion afterwards is about a stale one.
        Assert.Equal(second.Id, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Rejected").Entry.Node);

        second.Set("name", "Declined");

        var scope = DefaultScopeProvider.Instance.ScopeOf(first, "inherits");

        Assert.Equal(ScopeResolutionKind.Missing, scope.Resolve("Rejected").Kind);
        Assert.Equal(second.Id, scope.Resolve("Declined").Entry.Node);
    }

    [Fact]
    public void ANodeAddedAfterTheFirstLookupIsVisible()
    {
        var (ruleset, first, _) = Ruleset("Approved", "Rejected");

        Assert.Equal(ScopeResolutionKind.Missing, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Later").Kind);

        var later = ruleset.Model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        later.Set("name", "Later");

        Assert.Equal(later.Id, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Later").Entry.Node);
    }

    [Fact]
    public void ADeletedNodeIsNoLongerVisible()
    {
        var (ruleset, first, second) = Ruleset("Approved", "Rejected");

        Assert.Equal(second.Id, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Rejected").Entry.Node);

        ruleset.RemoveChild(RulesLanguage.RulesSlot, second);

        Assert.Equal(ScopeResolutionKind.Missing, DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Resolve("Rejected").Kind);
    }

    [Fact]
    public void AModelAddedAfterTheFirstLookupIsVisibleAtTheProjectLevel()
    {
        var project = RulesLanguage.NewProject();
        var first = project.NewModel("first");
        var here = first.NewNode(RulesConceptKeys.Ruleset);
        here.Set("name", "Here");

        Assert.Equal(ScopeResolutionKind.Missing, DefaultScopeProvider.Instance.ScopeOf(here, "inherits").Resolve("There").Kind);

        var there = project.NewModel("second").NewNode(RulesConceptKeys.Ruleset);
        there.Set("name", "There");

        Assert.Equal(there.Id, DefaultScopeProvider.Instance.ScopeOf(here, "inherits").Resolve("There").Entry.Node);
    }

    private static (Node Ruleset, Node First, Node Second) Ruleset(string firstName, string secondName)
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var first = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        first.Set("name", firstName);

        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        second.Set("name", secondName);

        return (ruleset, first, second);
    }
}
