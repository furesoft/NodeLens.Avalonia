using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a <see cref="TriggerRegistry"/> answers when the editor asks which triggers apply to a node.
/// </summary>
public sealed class TriggerRegistryTests
{
    [Fact]
    public void AnEmptyRegistryIsEmptyAndClaimsNothing()
    {
        Assert.True(TriggerRegistry.Empty.IsEmpty);
        Assert.Empty(TriggerRegistry.Empty.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)));
        Assert.Equal("no triggers", TriggerRegistry.Empty.ToString());
    }

    [Fact]
    public void AddingATriggerLeavesTheOlderRegistryAlone()
    {
        var one = TriggerRegistry.Empty.Add(Right("Expressions.Binary", ExpressionsConceptKeys.Expression));

        Assert.True(TriggerRegistry.Empty.IsEmpty);
        Assert.False(one.IsEmpty);

        var two = one.Add(Right("Expressions.Unary", ExpressionsConceptKeys.Expression));

        Assert.Single(one.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)));
        Assert.Equal(2, two.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)).Count);
    }

    [Fact]
    public void ATriggerAppliesToTheAnchorConceptAndToItsSubConcepts()
    {
        // The trigger names the abstract Expression, so it is asked about every concrete expression.
        var registry = TriggerRegistry.Empty.Add(Right("Expressions.Binary", ExpressionsConceptKeys.Expression));

        Assert.Single(registry.Find(Concepts.Get(ExpressionsConceptKeys.Expression)));
        Assert.Single(registry.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)));
        Assert.Single(registry.Find(Concepts.Get(ExpressionsConceptKeys.Parenthesized)));
    }

    [Fact]
    public void ATriggerDoesNotApplyToAConceptThatIsNotKindOfItsAnchor()
    {
        // A binary is not a literal: the trigger declared for literals has nothing to say about it.
        var registry = TriggerRegistry.Empty.Add(Right("Expressions.Binary", ExpressionsConceptKeys.IntLiteral));

        Assert.Empty(registry.Find(Concepts.Get(ExpressionsConceptKeys.Binary)));
        Assert.Single(registry.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)));
    }

    [Fact]
    public void TriggersAreAnsweredInTheOrderTheyWereDeclared()
    {
        var unary = Right("Expressions.Unary", ExpressionsConceptKeys.Expression);
        var binary = Right("Expressions.Binary", ExpressionsConceptKeys.Expression);

        var registry = TriggerRegistry.Empty.Add(unary).Add(binary);

        Assert.Equal(
            ["Expressions.Unary", "Expressions.Binary"],
            registry.Find(Concepts.Get(ExpressionsConceptKeys.IntLiteral)).Select(trigger => trigger.ConceptKey));
    }

    [Fact]
    public void TwoTriggersMayShareAnAnchorAndASide()
    {
        // The negation and the subtraction both start with a minus; which one a language means is its
        // declaration order, not a rule of the registry.
        var registry = TriggerRegistry.Empty
            .Add(Right("Expressions.Unary", ExpressionsConceptKeys.Expression))
            .Add(Right("Expressions.Binary", ExpressionsConceptKeys.Expression));

        Assert.Equal(2, registry.Find(Concepts.Get(ExpressionsConceptKeys.BoolLiteral)).Count);
    }

    [Fact]
    public void AddingNothingIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => TriggerRegistry.Empty.Add(null!));
        Assert.Throws<ArgumentNullException>(() => TriggerRegistry.Empty.Find(null!));
    }

    [Fact]
    public void TheRegistryPrintsWhatItHolds()
    {
        var registry = TriggerRegistry.Empty
            .Add(Right("Expressions.Binary", ExpressionsConceptKeys.Expression));

        Assert.Equal("Expressions.Binary <- Expressions.Expression (right)", registry.ToString());
    }

    private static ConceptRegistry Concepts => ExpressionsLanguage.Concepts;

    private static ConceptTrigger Right(string conceptKey, string anchorConcept) =>
        new(conceptKey, anchorConcept, TriggerSide.Right, text => text == "+", "left");
}
