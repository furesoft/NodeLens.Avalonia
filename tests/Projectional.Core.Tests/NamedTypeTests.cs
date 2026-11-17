using Projectional.Core;
using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A declared name for a type: the name is what it is called, the expansion is what it means.
/// </summary>
public sealed class NamedTypeTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    [Fact]
    public void AnAliasIsCalledByItsNameAndMeansItsExpansion()
    {
        var amount = new NamedType("Amount", Int);

        Assert.Equal("Amount", amount.Name);
        Assert.Same(Int, amount.Expansion);
    }

    [Fact]
    public void AnAliasAcceptsItsOwnExpansionAndTheOtherWayRound()
    {
        // A name is a spelling, not a distinct kind of value - otherwise `Amount` could not stand where
        // `int` is expected, and every alias would be a nominal type by accident.
        var amount = new NamedType("Amount", Int);

        Assert.True(amount.Accepts(Int));
        Assert.True(Int.Accepts(amount));
    }

    [Fact]
    public void TwoNamesForOneTypeAcceptEachOther()
    {
        var left = new NamedType("Amount", Int);
        var right = new NamedType("Quantity", Int);

        Assert.True(left.Accepts(right));
        Assert.True(right.Accepts(left));
    }

    [Fact]
    public void AnAliasOfAnotherTypeIsRefused()
    {
        var amount = new NamedType("Amount", Int);

        Assert.False(amount.Accepts(Text));
        Assert.False(amount.Accepts(new NamedType("Label", Text)));
    }

    [Fact]
    public void AnAliasStillAcceptsUnknown()
    {
        // The "one mistake is one finding" rule holds through an alias: what it accepts is what it means.
        Assert.True(new NamedType("Amount", Int).Accepts(UnknownType.Instance));
    }

    [Fact]
    public void AnAliasIsNamed()
    {
        Assert.Throws<ArgumentException>(() => new NamedType(string.Empty, Int));
    }
}
