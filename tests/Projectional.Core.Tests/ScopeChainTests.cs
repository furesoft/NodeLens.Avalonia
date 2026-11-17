using Projectional.Core;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The chain: the first level that knows a name decides what it means, and a nearer level shadows a
/// farther one.
/// </summary>
public sealed class ScopeChainTests
{
    private static readonly NodeId Near = NodeId.New();
    private static readonly NodeId Far = NodeId.New();

    [Fact]
    public void TheNearestLevelThatKnowsTheNameWins()
    {
        var chain = ScopeChain.Of(
            Scope.Of(new ScopeEntry("x", Near, "Rules.Rule")),
            Scope.Of(new ScopeEntry("x", Far, "Rules.Rule")));

        var resolution = chain.Resolve("x");

        Assert.Equal(ScopeResolutionKind.Found, resolution.Kind);
        Assert.Equal(Near, resolution.Entry.Node);
    }

    [Fact]
    public void ALevelThatDoesNotKnowTheNameIsSkipped()
    {
        var chain = ScopeChain.Of(
            Scope.Of(new ScopeEntry("y", Near, "Rules.Rule")),
            Scope.Of(new ScopeEntry("x", Far, "Rules.Rule")));

        Assert.Equal(Far, chain.Resolve("x").Entry.Node);
    }

    [Fact]
    public void AnAmbiguousNearerLevelDoesNotFallThroughToAUniqueFartherOne()
    {
        var chain = ScopeChain.Of(
            Scope.Of(
                [
                    new ScopeEntry("x", Near, "Rules.Rule"),
                    new ScopeEntry("x", NodeId.New(), "Rules.Rule"),
                ]),
            Scope.Of(new ScopeEntry("x", Far, "Rules.Rule")));

        // Falling through would make a nearer declaration lose to a farther one, which is the opposite
        // of what a chain is for.
        Assert.Equal(ScopeResolutionKind.Ambiguous, chain.Resolve("x").Kind);
    }

    [Fact]
    public void EntriesAreEveryLevelNearestFirst()
    {
        var chain = ScopeChain.Of(
            Scope.Of(new ScopeEntry("near", Near, "Rules.Rule")),
            Scope.Of(new ScopeEntry("far", Far, "Rules.Rule")));

        Assert.Equal(["near", "far"], chain.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void EmptyLevelsAreDroppedAndASingleLevelIsThatLevel()
    {
        var only = Scope.Of(new ScopeEntry("x", Near, "Rules.Rule"));

        Assert.Same(only, ScopeChain.Of(Scope.Empty, only, Scope.Empty));
        Assert.Same(Scope.Empty, ScopeChain.Of(Scope.Empty, Scope.Empty));
    }
}
