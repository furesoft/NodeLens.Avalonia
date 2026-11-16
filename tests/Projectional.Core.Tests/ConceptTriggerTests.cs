using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The one declaration a language writes per gesture: which concept, on which side, for which text.
/// </summary>
public sealed class ConceptTriggerTests
{
    [Fact]
    public void ATriggerAnswersWhatItClaims()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Binary",
            "Expressions.Expression",
            TriggerSide.Right,
            text => text == "+",
            "left");

        Assert.True(trigger.Claims("+"));
        Assert.False(trigger.Claims("-"));
        Assert.False(trigger.Claims("++"));
    }

    [Fact]
    public void ATriggerWithoutAPropertySlotOnlySelectsTheConcept()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Parenthesized",
            "Expressions.Expression",
            TriggerSide.Left,
            text => text == "(",
            "inner");

        Assert.Null(trigger.PropertySlot);
        Assert.Null(trigger.CaretSlot);
        Assert.Equal("Expressions.Parenthesized", trigger.ConceptKey);
        Assert.Equal("Expressions.Expression", trigger.AnchorConcept);
        Assert.Equal(TriggerSide.Left, trigger.Side);
        Assert.Equal("inner", trigger.AnchorSlot);
    }

    [Fact]
    public void ATriggerKeepsTheSlotsItWasGiven()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Binary",
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
    [InlineData("", "Expressions.Expression", "left")]
    [InlineData("Expressions.Binary", "", "left")]
    [InlineData("Expressions.Binary", "Expressions.Expression", "")]
    public void AnEmptyKeyOrSlotIsRefused(string conceptKey, string anchorConcept, string anchorSlot)
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            conceptKey,
            anchorConcept,
            TriggerSide.Right,
            _ => true,
            anchorSlot));
    }

    [Fact]
    public void AnOptionalSlotMayNotBeEmptyEither()
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Binary",
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left",
            propertySlot: ""));

        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Binary",
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
            "Expressions.Binary",
            "Expressions.Expression",
            TriggerSide.Right,
            null!,
            "left"));
    }

    [Fact]
    public void ASideThatIsNotASideIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ConceptTrigger(
            "Expressions.Binary",
            "Expressions.Expression",
            (TriggerSide)7,
            _ => true,
            "left"));
    }

    [Fact]
    public void ATriggerPrintsItsShape()
    {
        var trigger = new ConceptTrigger(
            "Expressions.Binary",
            "Expressions.Expression",
            TriggerSide.Right,
            _ => true,
            "left");

        Assert.Equal("Expressions.Binary <- Expressions.Expression (right)", trigger.ToString());
    }
}
