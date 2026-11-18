using Projectional.Core;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The range a child slot declares: the storage shape from <see cref="Cardinality"/> and the lower end from
/// <see cref="ChildSlot.Minimum"/>.
/// </summary>
/// <remarks>
/// The four ranges an author writes - <c>0..1</c>, <c>0..n</c>, <c>1</c>, <c>1..n</c> - are the four combinations, and
/// the upper end is never a declaration: a single child holds one and a list holds as many as there are.
/// </remarks>
public sealed class ChildSlotTests
{
    [Fact]
    public void ASingleChildSlotRequiresOneByDefault()
    {
        // What every slot declared before ranges existed meant: a slot holding one child is a hole while it is empty.
        var slot = new ChildSlot("body", "Test.Body", Cardinality.One);

        Assert.Equal(1, slot.Minimum);
        Assert.Equal(1, slot.Maximum);
    }

    [Fact]
    public void AListSlotRequiresNothingByDefault()
    {
        var slot = new ChildSlot("steps", "Test.Step");

        Assert.Equal(Cardinality.Many, slot.Cardinality);
        Assert.Equal(0, slot.Minimum);
        Assert.Null(slot.Maximum);
    }

    [Fact]
    public void ASingleChildSlotMayAllowNothing()
    {
        // `0..1`: one child at most, none required - a legal empty slot, so no check reports it.
        var slot = new ChildSlot("extra", "Test.Step", Cardinality.One, minimum: 0);

        Assert.Equal(0, slot.Minimum);
        Assert.Equal(1, slot.Maximum);
    }

    [Fact]
    public void AListSlotMayRequireOne()
    {
        // `1..n`: a list with at least one element, which is a different statement from `0..n`.
        var slot = new ChildSlot("steps", "Test.Step", Cardinality.Many, minimum: 1);

        Assert.Equal(1, slot.Minimum);
        Assert.Null(slot.Maximum);
    }

    [Fact]
    public void ANegativeMinimumIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ChildSlot("steps", "Test.Step", Cardinality.Many, minimum: -1));
    }

    [Fact]
    public void ASingleChildSlotCannotRequireTwoChildren()
    {
        // The storage shape decides the upper end, so a minimum above one describes a list.
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ChildSlot("body", "Test.Body", Cardinality.One, minimum: 2));

        Assert.Contains("list", error.Message, StringComparison.Ordinal);
    }
}
