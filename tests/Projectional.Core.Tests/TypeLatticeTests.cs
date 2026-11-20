using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Joins, meets, and the walks up and down the relation they need.</summary>
/// <remarks>
/// A join is only interesting where the answer is <em>not</em> one of the two sides, so most of these build a small
/// lattice out of declared edges: `amount &lt;: number` and `quantity &lt;: number` make `number` the join of the two.
/// The rest is what the computation refuses - two incomparable supertypes, or none - because that is where a guess
/// would otherwise creep in.
/// </remarks>
public sealed class TypeLatticeTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    private static readonly NamedType Amount = new("Amount", PrimitiveType.Of(PropertyType.Integer));
    private static readonly NamedType Quantity = new("Quantity", PrimitiveType.Of(PropertyType.Integer));
    private static readonly NamedType Number = new("Number", PrimitiveType.Of(PropertyType.Integer));

    [Fact]
    public void TheWalksGoBothWaysAndStartAtTheTypeItself()
    {
        var relation = SubtypeRelation.Primitives.Add(Amount, Number).Add(Number, Int);

        Assert.Equal(["Amount", "Number", "int"], relation.SupertypesOf(Amount).Select(type => type.Name));
        Assert.Equal(["int", "Number", "Amount"], relation.SubtypesOf(Int).Select(type => type.Name));
    }

    [Fact]
    public void AJoinOfTwoSidesIsOneOfThemWhenOneIsBelowTheOther()
    {
        var relation = SubtypeRelation.Primitives.Add(Amount, Number);

        Assert.Same(Number, TypeLattice.Join(relation, Amount, Number));
        Assert.Same(Number, TypeLattice.Join(relation, Number, Amount));
        Assert.Same(Amount, TypeLattice.Join(relation, Amount, Amount));
    }

    [Fact]
    public void AJoinOfTwoSiblingsIsTheirCommonSupertype()
    {
        // The case the cookbook's `join` exists for: neither side is the answer, and the relation names one.
        var relation = SubtypeRelation.Primitives.Add(Amount, Number).Add(Quantity, Number);

        Assert.Same(Number, TypeLattice.Join(relation, Amount, Quantity));
    }

    [Fact]
    public void AJoinWithNoCommonSupertypeIsUnknown()
    {
        // Two types of one language that nothing relates: the answer is a reported "cannot say", not a guess.
        var relation = SubtypeRelation.Primitives;

        Assert.Same(UnknownType.Instance, TypeLattice.Join(relation, Amount, Quantity));
        Assert.Same(UnknownType.Instance, TypeLattice.Join(relation, Amount, Text));
    }

    [Fact]
    public void AJoinOfTwoIncomparableSupertypesIsUnknown()
    {
        // Both are above `amount`, and neither is below the other, so there is no *least* upper bound. Picking the
        // one that was declared first would be exactly the guess this layer refuses.
        var relation = SubtypeRelation.Primitives.Add(Amount, Number).Add(Amount, Text);

        Assert.Same(UnknownType.Instance, TypeLattice.Join(relation, Number, Text));

        // The two supertypes are genuinely incomparable, which is what makes the answer above honest.
        Assert.False(relation.IsSubtype(Number, Text));
        Assert.False(relation.IsSubtype(Text, Number));
    }

    [Fact]
    public void AMeetFindsTheTypeBothSidesAreAbove()
    {
        // Two *incomparable* sides with a common subtype: `narrow` is below both, so it is the greatest type that
        // may stand as each. With comparable sides the answer is simply the narrower one, which the fast path
        // already returns - this is the case that needs the walk.
        var narrow = new NamedType("Narrow", PrimitiveType.Of(PropertyType.Integer));
        var other = new NamedType("Other", PrimitiveType.Of(PropertyType.Integer));
        var relation = SubtypeRelation.Primitives.Add(narrow, Amount).Add(narrow, other);

        Assert.False(relation.IsSubtype(Amount, other));
        Assert.Same(narrow, TypeLattice.Meet(relation, Amount, other));
    }

    [Fact]
    public void AMeetOfComparableSidesIsTheNarrowerOne()
    {
        var relation = SubtypeRelation.Primitives.Add(Amount, Number);

        Assert.Same(Amount, TypeLattice.Meet(relation, Amount, Number));
        Assert.Same(Amount, TypeLattice.Meet(relation, Number, Amount));
    }

    [Fact]
    public void AMeetOfTwoSiblingsIsUnknown()
    {
        var relation = SubtypeRelation.Primitives.Add(Amount, Number).Add(Quantity, Number);

        Assert.Same(UnknownType.Instance, TypeLattice.Meet(relation, Amount, Quantity));
    }


    [Fact]
    public void AJoinExpressesEitherSidesType()
    {
        var either = new JoinType(Int, Text);

        Assert.Equal("int|string", either.Name);
        Assert.True(either.Accepts(Int));
        Assert.True(either.Accepts(PrimitiveType.Of(PropertyType.String)));
        Assert.False(either.Accepts(PrimitiveType.Of(PropertyType.Boolean)));

        // A union is as lenient as its sides: one mistake must not become a finding on a slot that says "either".
        Assert.True(either.Accepts(UnknownType.Instance));
    }

    [Fact]
    public void AMeetExpressesBothSidesTypes()
    {
        var both = new MeetType(Int, new NamedType("Amount", PrimitiveType.Of(PropertyType.Integer)));

        Assert.Equal("int&Amount", both.Name);

        // The alias expands to an int, so it satisfies both sides; a string satisfies neither.
        Assert.True(both.Accepts(Amount));
        Assert.False(both.Accepts(Text));

        // A meet is as lenient as both its sides: a primitive accepts an unknown type so that one mistake does not
        // become a finding, and an alias expands to one - so the meet accepts it too. The strictness a meet adds is
        // over the *sides*, not over `unknown`.
        Assert.True(both.Accepts(UnknownType.Instance));
    }
}
