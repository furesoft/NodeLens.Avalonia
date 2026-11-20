using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A replacement: what it matches, what it binds, and what it says to prove instead.</summary>
/// <remarks>
/// The rule exists to turn a claim about two compound types into a claim about their parts, so the tests are about
/// the three things that can go wrong with that: a pattern that matches too much, a variable that is bound twice
/// and then forgotten, and a relation handed back with its variables still in it.
/// </remarks>
public sealed class TypeReplacementTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    [Fact]
    public void AReplacementStatesWhatToProveInsteadWithItsVariablesBound()
    {
        var element = new TypeVariable("element");
        var otherElement = new TypeVariable("otherElement");
        var rule = new TypeReplacement(
            TypeEquationKind.Subtype,
            new ListType(element),
            new ListType(otherElement),
            [TypeEquation.Subtype(element, otherElement)]);

        Assert.True(rule.TryApply(new ListType(Int), new ListType(Text), out var instead));

        var relation = Assert.Single(instead);

        Assert.Equal(TypeEquationKind.Subtype, relation.Kind);
        Assert.Same(Int, relation.Left);
        Assert.Same(Text, relation.Right);
    }

    [Fact]
    public void AReplacementSaysNothingWhenTheShapesDoNotMatch()
    {
        var element = new TypeVariable("element");
        var rule = new TypeReplacement(
            TypeEquationKind.Subtype,
            new ListType(element),
            new ListType(element),
            []);

        Assert.False(rule.TryApply(new OptionalType(Int), new ListType(Int), out var instead));
        Assert.Empty(instead);
    }

    [Fact]
    public void AReplacementThatStatesNothingDischargesTheClaim()
    {
        // MPS' null type is a subtype of everything a classifier is, so the inequation is removed rather than
        // proven. An empty replacement is that removal, and it still counts as having applied.
        var rule = new TypeReplacement(TypeEquationKind.Subtype, new OptionalType(Int), Int, []);

        Assert.True(rule.TryApply(new OptionalType(Int), Int, out var instead));
        Assert.Empty(instead);
    }

    [Fact]
    public void OneVariableWrittenTwiceHasToOneType()
    {
        // "[?a] <: [?a]" relates a sequence of a type to a sequence of the same type, so a sequence of int against
        // a sequence of string must not match it - binding the variable twice to two types is the mistake that
        // would make the rule mean "any two sequences".
        var element = new TypeVariable("element");
        var rule = new TypeReplacement(
            TypeEquationKind.Subtype,
            new ListType(element),
            new ListType(element),
            [TypeEquation.Equal(element, element)]);

        Assert.True(rule.TryApply(new ListType(Int), new ListType(Int), out _));
        Assert.False(rule.TryApply(new ListType(Int), new ListType(Text), out _));
    }

    [Fact]
    public void AVariableIsReplacedAllTheWayDown()
    {
        // A relation a rule states instead is a template its author wrote, so a part of a part has to be filled in
        // too - the reason this substitution descends where the solver's own does not.
        var element = new TypeVariable("element");
        var otherElement = new TypeVariable("otherElement");
        var rule = new TypeReplacement(
            TypeEquationKind.Subtype,
            new ListType(element),
            new ListType(otherElement),
            [TypeEquation.Subtype(new ListType(element), new OptionalType(otherElement))]);

        Assert.True(rule.TryApply(new ListType(new OptionalType(Int)), new ListType(Text), out var instead));

        var relation = Assert.Single(instead);

        Assert.Equal("[int?]", relation.Left.Name);
        Assert.Equal("string?", relation.Right.Name);
    }

    [Fact]
    public void ARecordPatternMatchesTheFieldsItNames()
    {
        var amount = new TypeVariable("amount");
        var rule = new TypeReplacement(
            TypeEquationKind.Subtype,
            new RecordType([("amount", amount)]),
            new NamedType("Money", Int),
            []);

        var money = new NamedType("Money", Int);

        Assert.True(rule.TryApply(new RecordType([("amount", Int), ("currency", Text)]), money, out _));
        Assert.False(rule.TryApply(new RecordType([("currency", Text)]), money, out _));
    }

    [Fact]
    public void ADeclaredNamePatternMatchesByName()
    {
        var rule = new TypeReplacement(TypeEquationKind.Subtype, new NamedType("Amount", Int), Int, []);

        Assert.True(rule.TryApply(new NamedType("Amount", Int), Int, out _));
        Assert.False(rule.TryApply(new NamedType("Money", Int), Int, out _));
    }

    [Fact]
    public void ASupertypeClaimIsNotASecondClaim()
    {
        // ':>' is '<:' with the sides swapped, so accepting it here would let one rule be written two ways and
        // read as two rules.
        Assert.Throws<ArgumentException>(() => new TypeReplacement(
            TypeEquationKind.Supertype,
            Int,
            Text,
            []));
    }

    [Fact]
    public void TheCovarianceDefaultsCoverBothShapesAndBothClaims()
    {
        var claims = TypeReplacements.Covariance
            .Select(each => (each.Claim, Pattern: each.Left.GetType()))
            .ToList();

        Assert.Equal(
            [
                (TypeEquationKind.Equal, typeof(ListType)),
                (TypeEquationKind.Equal, typeof(OptionalType)),
                (TypeEquationKind.Subtype, typeof(ListType)),
                (TypeEquationKind.Subtype, typeof(OptionalType)),
            ],
            claims);
    }
}
