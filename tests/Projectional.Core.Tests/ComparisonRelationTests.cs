using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The comparison relation: which types a language declared interchangeable, and what that does not claim.</summary>
/// <remarks>
/// The relation exists to say "these two may each be used for the other" without also saying "each is a subtype of
/// the other", so the tests are as much about what it leaves alone as about what it answers.
/// </remarks>
public sealed class ComparisonRelationTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);
    private static readonly PrimitiveType Flag = PrimitiveType.Of(PropertyType.Boolean);

    [Fact]
    public void ATypeIsComparableWithItselfAndNothingElseWithoutADeclaration()
    {
        Assert.True(ComparisonRelation.Empty.IsComparableTo(Int, PrimitiveType.Of(PropertyType.Integer)));
        Assert.False(ComparisonRelation.Empty.IsComparableTo(Int, Text));
    }

    [Fact]
    public void ADeclaredPairIsComparableBothWays()
    {
        // One declaration, one fact: "interchangeable" is symmetric, so it is never written twice.
        var comparisons = ComparisonRelation.Empty.Add(Int, Text);

        Assert.True(comparisons.IsComparableTo(Int, Text));
        Assert.True(comparisons.IsComparableTo(Text, Int));
    }

    [Fact]
    public void ComparabilityIsTransitive()
    {
        // It has to be, or two spellings of one fact would answer differently: two declared edges, one each way,
        // already make interchange transitive, and this relation must mean the same thing.
        var comparisons = ComparisonRelation.Empty.Add(Int, Text).Add(Text, Flag);

        Assert.True(comparisons.IsComparableTo(Int, Flag));
        Assert.True(comparisons.IsComparableTo(Flag, Int));
    }

    [Fact]
    public void ADeclaredPairReachesNothingElse()
    {
        var comparisons = ComparisonRelation.Empty.Add(Int, Text);

        Assert.False(comparisons.IsComparableTo(Int, Flag));
        Assert.Equal(1, comparisons.Count);
    }

    [Fact]
    public void DeclaringTheSamePairTwiceHoldsItOnce()
    {
        var once = ComparisonRelation.Empty.Add(Int, Text);

        Assert.Same(once, once.Add(Int, Text));
        Assert.Same(once, once.Add(Text, Int));
    }

    [Fact]
    public void ATypesOwnSamenessNeedsNoDeclaration()
    {
        // A declared pair that is already true is not stored, the same way a subtype edge a type already satisfies
        // is not: a language stating a fact twice does not have to know it did.
        Assert.Same(ComparisonRelation.Empty, ComparisonRelation.Empty.Add(Int, PrimitiveType.Of(PropertyType.Integer)));
    }

    [Fact]
    public void UnknownStandsInNoComparison()
    {
        // The refusal the edges already make, for the same reason: "unknown may be used for int" is how a guess
        // becomes a proof.
        var exception = Assert.Throws<ArgumentException>(() => ComparisonRelation.Empty.Add(UnknownType.Instance, Int));

        Assert.Contains("unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRelationIsKeptApartFromTheEdges()
    {
        // The reason the pair is not just two edges: a comparison leaves the lattice alone, so the two types are
        // neither of them below the other.
        var comparisons = ComparisonRelation.Empty.Add(Int, Text);

        Assert.True(comparisons.IsComparableTo(Int, Text));
        Assert.False(SubtypeRelation.Primitives.IsSubtype(Int, Text));
        Assert.False(SubtypeRelation.Primitives.IsSubtype(Text, Int));
    }

    [Fact]
    public void ASolverAsksTheComparisonBeforeCallingAContradiction()
    {
        // Two types declared interchangeable may be one type even though neither is a subtype of the other, which is
        // the claim a language makes when it says "these are the same for my purposes" without ordering them.
        var comparisons = ComparisonRelation.Empty.Add(Int, Text);

        Assert.False(new TypeSolver().Solve([TypeEquation.Equal(Int, Text)]).Succeeded);

        var solution = new TypeSolver(comparisons: comparisons).Solve([TypeEquation.Equal(Int, Text)]);

        Assert.True(solution.Succeeded);
    }

    [Fact]
    public void AComparisonDoesNotWidenSubtyping()
    {
        // The other half of the same decision: a pair says nothing about which type may stand where the other is
        // required, so a subtyping claim between them is still refused.
        var comparisons = ComparisonRelation.Empty.Add(Int, Text);

        Assert.False(new TypeSolver(comparisons: comparisons).Solve([TypeEquation.Subtype(Int, Text)]).Succeeded);
    }
}
