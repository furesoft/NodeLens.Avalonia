using Projectional.Core;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The provider a language declares: one place for the slot it names, the default chain for everything else.
/// </summary>
/// <remarks>
/// The equivalence test is the important one: "everywhere, no target" has to answer exactly what the default
/// provider answers, or a language could not tell the two apart and the shared walk would have drifted.
/// </remarks>
public sealed class NamedScopeProviderTests
{
    [Fact]
    public void EverywhereWithNoTargetIsTheDefaultChain()
    {
        var (_, first, _) = Ruleset("Approved", "Rejected");

        var named = NamedScopeProvider.For("inherits", ScopePlace.Everywhere);

        Assert.Equal(
            DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Entries,
            named.ScopeOf(first, "inherits").Entries);
    }

    [Fact]
    public void ALocalTargetSeesTheNeighboursOfThatConceptOnly()
    {
        var (_, first, second) = Ruleset("Approved", "Rejected");

        var scope = NamedScopeProvider
            .For("inherits", ScopePlace.Local, target: RulesConceptKeys.Rule)
            .ScopeOf(first, "inherits");

        Assert.Equal(second.Id, scope.Resolve("Rejected").Entry.Node);

        // The ruleset encloses the rule; it is a neighbour nobody asked for.
        Assert.Equal(ScopeResolutionKind.Missing, scope.Resolve("Payroll").Kind);
        Assert.All(scope.Entries, entry => Assert.Equal(RulesConceptKeys.Rule, entry.ConceptKey));

        // And the node itself is still not in its own scope.
        Assert.DoesNotContain(scope.Entries, entry => entry.Node == first.Id);
    }

    [Fact]
    public void AModelPlaceSeesTheDocumentsRootsAndNothingInsideThem()
    {
        var (ruleset, first, _) = Ruleset("Approved", "Rejected");

        var scope = NamedScopeProvider.For("inherits", ScopePlace.Model).ScopeOf(first, "inherits");

        Assert.Equal([ruleset.Id], scope.Entries.Select(entry => entry.Node));
    }

    [Fact]
    public void AProjectPlaceSeesTheRootsOfAnotherModel()
    {
        var project = RulesLanguage.NewProject();
        var here = project.NewModel("here");
        var there = project.NewModel("there");

        var subject = here.NewNode(RulesConceptKeys.Ruleset);
        subject.Set("name", "Here");

        var foreign = there.NewNode(RulesConceptKeys.Ruleset);
        foreign.Set("name", "There");

        Assert.Equal(
            foreign.Id,
            NamedScopeProvider.For("inherits", ScopePlace.Project).ScopeOf(subject, "inherits").Resolve("There").Entry.Node);
    }

    [Fact]
    public void ASlotTheProviderDoesNotNameFallsBackToTheDefaultChain()
    {
        var (_, first, _) = Ruleset("Approved", "Rejected");

        // A language usually says something about one reference; the concept's other references are not its business.
        var provider = NamedScopeProvider.For("condition", ScopePlace.Model);

        Assert.Equal(
            DefaultScopeProvider.Instance.ScopeOf(first, "inherits").Entries,
            provider.ScopeOf(first, "inherits").Entries);
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
