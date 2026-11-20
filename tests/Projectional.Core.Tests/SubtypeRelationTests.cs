using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The subtype relation, against the two questions it must not be confused with.</summary>
/// <remarks>
/// The value of this relation is not that it can say "yes" - a per-type <c>Accepts</c> could - but that it says it
/// about a <em>pair</em>, so a language declares one edge instead of editing two types. What these tests pin is
/// mostly what the relation refuses: an unknown type stands in no edge, and the edges a language declares are
/// transitive without being declared twice.
/// </remarks>
public sealed class SubtypeRelationTests
{
    [Fact]
    public void ATypesIsASubtypeOfItself()
    {
        var relation = SubtypeRelation.Primitives;
        var type = Named("Amount");

        Assert.True(relation.IsSubtype(type, type));

        // Two declarations of one name are two instances, and a named type is compared by identity here: a name is
        // a classifier, and whether two declarations with one name are one type is the language's question, not the
        // relation's.
        Assert.False(relation.IsSubtype(type, Named("Amount")));
    }

    [Fact]
    public void TwoPrimitivesWithOneNameAreOneType()
    {
        // A declaration writes the name and the instances are made where they are needed, so identity cannot be the
        // test - and this is the only value equality the framework has.
        var relation = SubtypeRelation.Primitives;

        Assert.True(relation.IsSubtype(PrimitiveType.Of(PropertyType.String), PrimitiveType.Of(PropertyType.String)));
        Assert.False(relation.IsSubtype(PrimitiveType.Of(PropertyType.Integer), PrimitiveType.Of(PropertyType.String)));
    }

    [Fact]
    public void ADeclaredEdgeMakesASubtype()
    {
        var amount = PrimitiveType.Of(PropertyType.Integer);
        var money = Named("Money");

        var relation = SubtypeRelation.Primitives.Add(amount, money);

        Assert.True(relation.IsSubtype(amount, money));
        Assert.False(SubtypeRelation.Primitives.IsSubtype(amount, money));
        Assert.Equal(1, relation.Count);
    }

    [Fact]
    public void DeclaredEdgesAreTransitive()
    {
        // "a may stand as b, b may stand as c" means "a may stand as c": that is what a subtype relation is, not a
        // rule this class chose, so the second edge is not declared and the answer must still be yes.
        var a = Named("A");
        var b = Named("B");
        var c = Named("C");

        var relation = SubtypeRelation.Primitives.Add(a, b).Add(b, c);

        Assert.True(relation.IsSubtype(a, c));
        Assert.False(relation.IsSubtype(c, a));
    }

    [Fact]
    public void DeclaringTheSameEdgeTwiceHoldsItOnce()
    {
        var a = Named("A");
        var b = Named("B");

        var relation = SubtypeRelation.Primitives.Add(a, b).Add(a, b);

        Assert.Equal(1, relation.Count);
    }

    [Fact]
    public void AnUnknownTypeStandsInNoEdge()
    {
        // The whole reason `Accepts` and `IsSubtype` are two questions. Conformance is lenient so that one mistake
        // does not become fifty findings; subtyping is strict so that a rule is never told a guess is a proof.
        var relation = SubtypeRelation.Primitives;
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.False(relation.IsSubtype(UnknownType.Instance, text));
        Assert.False(relation.IsSubtype(text, UnknownType.Instance));
        Assert.True(relation.IsSubtype(UnknownType.Instance, UnknownType.Instance));

        Assert.True(UnknownType.Instance.Accepts(text));
        Assert.True(text.Accepts(UnknownType.Instance));
    }

    [Fact]
    public void AnEdgeMentioningUnknownIsRefused()
    {
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.Throws<ArgumentException>(() => SubtypeRelation.Primitives.Add(UnknownType.Instance, text));
        Assert.Throws<ArgumentException>(() => SubtypeRelation.Primitives.Add(text, UnknownType.Instance));
    }

    [Fact]
    public void ARelationIsImmutable()
    {
        var before = SubtypeRelation.Primitives;
        var a = Named("A");
        var b = Named("B");

        var after = before.Add(a, b);

        Assert.False(before.IsSubtype(a, b));
        Assert.True(after.IsSubtype(a, b));
        Assert.NotSame(before, after);
    }

    [Fact]
    public void ATypesConformanceDefaultsToTheRelation()
    {
        // A type a language adds needs only a name: `Accepts` is the relation's answer unless the type says
        // something more lenient of its own. This is what the default implementation on IType is for - every type
        // the framework ships overrides it, so only a bare one can show the default.
        var type = (IType)new BareType("Amount");
        var other = (IType)new BareType("Amount");

        Assert.True(type.Accepts(type));
        Assert.False(type.Accepts(other));
        Assert.False(type.Accepts(UnknownType.Instance));
    }

    private static NamedType Named(string name) => new(name, PrimitiveType.Of(PropertyType.String));
}
