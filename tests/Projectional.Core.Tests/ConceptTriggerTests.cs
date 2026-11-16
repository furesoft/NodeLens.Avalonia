using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The one declaration a language writes per gesture: which side, for which text, and where the node and the
/// caret go.
/// </summary>
/// <remarks>
/// The concept a trigger creates is the concept that declares it, so nothing here names it - which is the point of
/// declaring the gesture on the concept. What the concept's own slots have to offer is checked by
/// <see cref="ConceptRegistry"/>, so those tests live in <c>ConceptRegistryTriggerTests</c>.
/// </remarks>
public sealed class ConceptTriggerTests
{
    [Fact]
    public void ATriggerAnswersWhatItClaims()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            text => text == "+",
            "left");

        Assert.True(trigger.Claims("+"));
        Assert.False(trigger.Claims("-"));
        Assert.False(trigger.Claims("++"));
    }

    [Fact]
    public void ATriggerWithoutASlotOnlySelectsTheConcept()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Left,
            text => text == "(",
            "inner");

        Assert.Null(trigger.PropertySlot);
        Assert.Null(trigger.CaretSlot);
        Assert.Equal("Expressions.Expression", trigger.AnchorConcept);
        Assert.Equal(TriggerSide.Left, trigger.Side);
        Assert.Equal("inner", trigger.AnchorSlot);
    }

    [Fact]
    public void ATriggerKeepsTheSlotsItWasGiven()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left",
            propertySlot: "operator",
            caretSlot: "right");

        Assert.Equal("operator", trigger.PropertySlot);
        Assert.Equal("right", trigger.CaretSlot);
    }

    [Theory]
    [InlineData("", "left")]
    [InlineData("Expressions.Expression", "")]
    public void AnEmptyKeyOrSlotIsRefused(string anchorConcept, string anchorSlot)
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            anchorConcept,
            TriggerSide.Right,
            _ => true,
            anchorSlot));
    }

    [Fact]
    public void AnOptionalSlotMayNotBeEmptyEither()
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left",
            propertySlot: ""));

        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left",
            caretSlot: ""));
    }

    [Fact]
    public void ClaimingNothingIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            null!,
            "left"));
    }

    [Fact]
    public void ASideThatIsNotASideIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Expression",
            (TriggerSide)7,
            _ => true,
            "left"));
    }

    [Fact]
    public void ATriggerPrintsItsShape()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left");

        Assert.Equal("<- Expressions.Expression (right)", trigger.ToString());
    }
}
