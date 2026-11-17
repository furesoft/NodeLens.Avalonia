using Projectional.Core;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// One level of a scope chain: what it holds, and what a name means inside it.
/// </summary>
public sealed class ScopeTests
{
    private static readonly NodeId First = NodeId.New();
    private static readonly NodeId Second = NodeId.New();

    [Fact]
    public void AnEmptyScopeKnowsNoName()
    {
        Assert.Empty(Scope.Empty.Entries);
        Assert.Equal(ScopeResolutionKind.Missing, Scope.Empty.Resolve("anything").Kind);
    }

    [Fact]
    public void ANameThatOneEntryCarriesIsFound()
    {
        var scope = Scope.Of(new ScopeEntry("Approved", First, "Rules.Rule"));

        var resolution = scope.Resolve("Approved");

        Assert.Equal(ScopeResolutionKind.Found, resolution.Kind);
        Assert.Equal(First, resolution.Entry.Node);
    }

    [Fact]
    public void ANameThatTwoEntriesCarryIsAmbiguousAndNamesBoth()
    {
        var scope = Scope.Of(
            [
                new ScopeEntry("Approved", First, "Rules.Rule"),
                new ScopeEntry("Approved", Second, "Rules.Rule"),
            ]);

        var resolution = scope.Resolve("Approved");

        Assert.Equal(ScopeResolutionKind.Ambiguous, resolution.Kind);
        Assert.Equal([First, Second], resolution.Candidates.Select(candidate => candidate.Node));
    }

    [Fact]
    public void NamesAreComparedOrdinally()
    {
        var scope = Scope.Of(new ScopeEntry("Approved", First, "Rules.Rule"));

        Assert.Equal(ScopeResolutionKind.Missing, scope.Resolve("approved").Kind);
    }

    [Fact]
    public void EntriesKeepTheOrderTheyWereGivenIn()
    {
        var scope = Scope.Of(
            [
                new ScopeEntry("b", First, "Rules.Rule"),
                new ScopeEntry("a", Second, "Rules.Rule"),
            ]);

        Assert.Equal(["b", "a"], scope.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void AnAmbiguousResolutionNeedsTwoCandidates()
    {
        Assert.Throws<ArgumentException>(() => ScopeResolution.Ambiguous([new ScopeEntry("a", First, "Rules.Rule")]));
    }

    [Fact]
    public void AnEntryFitsASlotDeclaredOverItsOwnConceptOrASuperConcept()
    {
        var entry = new ScopeEntry("Approved", First, "Rules.Rule");

        Assert.True(entry.Fits("Rules.Rule", RulesLanguage.Concepts));
        Assert.False(entry.Fits("Rules.Ruleset", RulesLanguage.Concepts));
    }
}
