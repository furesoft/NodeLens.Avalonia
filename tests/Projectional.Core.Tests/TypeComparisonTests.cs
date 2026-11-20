using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The third question a type asks: may each of two types be used for the other?</summary>
/// <remarks>
/// Conformance (<see cref="IType.Accepts"/>) asks whether one value may stand in one slot; subtyping asks whether
/// one type may stand for another in one direction. Comparison asks both directions at once, which is what makes
/// two types interchangeable - and it is neither of the other two answers, because a one-way edge is a subtype
/// without being a comparison. What these tests pin is mostly that the third question stays derived from the
/// relation and stays as structural as acceptance: a list compares its elements, a record its fields, a name its
/// expansion.
/// </remarks>
public sealed class TypeComparisonTests
{
    [Fact]
    public void ATypeIsComparableWithItself()
    {
        var relation = SubtypeRelation.Primitives;
        var type = Named("Amount");

        Assert.True(relation.IsComparableTo(type, type));

        // Two declarations of one name are two instances, the same reading the relation takes for subtyping.
        Assert.False(relation.IsComparableTo(type, Named("Amount")));
    }

    [Fact]
    public void AOneWayEdgeIsASubtypeAndNotAComparison()
    {
        // The distinction the third question exists for: "a may stand as b" is a subtype edge, and "a and b are
        // interchangeable" needs the edge in both directions. A language that declares one direction has said
        // something narrower than a comparison.
        var amount = Named("Amount");
        var money = Named("Money");

        var oneWay = SubtypeRelation.Primitives.Add(amount, money);

        Assert.True(oneWay.IsSubtype(amount, money));
        Assert.False(oneWay.IsComparableTo(amount, money));

        var bothWays = oneWay.Add(money, amount);

        Assert.True(bothWays.IsComparableTo(amount, money));
        Assert.True(bothWays.IsComparableTo(money, amount));
    }

    [Fact]
    public void TheComparisonIsSymmetric()
    {
        var relation = SubtypeRelation.Primitives;
        var text = PrimitiveType.Of(PropertyType.String);
        var number = PrimitiveType.Of(PropertyType.Integer);

        Assert.Equal(relation.IsComparableTo(text, number), relation.IsComparableTo(number, text));
        Assert.True(relation.IsComparableTo(text, PrimitiveType.Of(PropertyType.String)));
    }

    [Fact]
    public void APrimitiveIsComparableWithAnUnknownType()
    {
        // The same leniency acceptance has, for the same reason: a comparison against a type that could not be
        // computed must not become a finding about the comparison.
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.True(text.IsComparableTo(UnknownType.Instance));
        Assert.True(UnknownType.Instance.IsComparableTo(text));
    }

    [Fact]
    public void AListsComparabilityIsItsElements()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.True(new ListType(number).IsComparableTo(new ListType(number)));
        Assert.False(new ListType(number).IsComparableTo(new ListType(text)));
        Assert.False(new ListType(number).IsComparableTo(number));
    }

    [Fact]
    public void AnOptionalsComparabilityIsItsInner()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.True(new OptionalType(number).IsComparableTo(new OptionalType(number)));
        Assert.False(new OptionalType(number).IsComparableTo(new OptionalType(text)));
    }

    [Fact]
    public void ARecordsComparabilityIsItsFields()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);
        var text = PrimitiveType.Of(PropertyType.String);

        var amount = new RecordType([("amount", number)]);

        Assert.True(amount.IsComparableTo(new RecordType([("amount", number)])));
        Assert.False(amount.IsComparableTo(new RecordType([("amount", text)])));
    }

    [Fact]
    public void ANamedTypeIsComparableWithWhatItsExpansionIs()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);

        // A declared name is transparent for comparison, exactly as it is for acceptance: `Amount` over `int` is
        // comparable with `int`, in either direction.
        Assert.True(new NamedType("Amount", number).IsComparableTo(number));
        Assert.True(number.IsComparableTo(new NamedType("Amount", number)));
    }

    [Fact]
    public void AJoinIsComparableWhenEitherSideIs()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);
        var text = PrimitiveType.Of(PropertyType.String);
        var join = new JoinType(number, text);

        Assert.True(join.IsComparableTo(number));
        Assert.True(join.IsComparableTo(text));
        Assert.False(join.IsComparableTo(PrimitiveType.Of(PropertyType.Boolean)));
    }

    [Fact]
    public void AMeetIsComparableWhenBothSidesAre()
    {
        var number = PrimitiveType.Of(PropertyType.Integer);
        var text = PrimitiveType.Of(PropertyType.String);

        Assert.False(new MeetType(number, text).IsComparableTo(number));
        Assert.True(new MeetType(number, PrimitiveType.Of(PropertyType.Integer)).IsComparableTo(number));
    }

    [Fact]
    public void ATypeThatSaysNothingExtraComparesOnlyWithItself()
    {
        // The default on IType: a type that overrides nothing is comparable with itself and nothing else, because
        // the relation it asks holds a type equal to itself only. Reached through the interface, since a default
        // member is not visible on a variable typed as the implementing class.
        var type = (IType)new BareType("Amount");
        var other = (IType)new BareType("Amount");

        Assert.True(type.IsComparableTo(type));
        Assert.False(type.IsComparableTo(other));
        Assert.False(type.IsComparableTo(PrimitiveType.Of(PropertyType.String)));
    }

    private static NamedType Named(string name) => new(name, PrimitiveType.Of(PropertyType.String));
}
