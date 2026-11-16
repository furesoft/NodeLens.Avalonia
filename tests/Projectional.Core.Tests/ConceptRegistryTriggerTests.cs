using Projectional.Expressions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the registry answers about the gestures a language declares, and what it refuses to accept when a
/// declaration could never run.
/// </summary>
/// <remarks>
/// The gestures live on the concepts now, so the lookup is a scan with a super-concept test and the order is
/// declaration order - the same order completion uses. The validation exists because none of what a trigger names
/// is visible to the compiler: its anchor concept is a foreign key and its slots are strings.
/// </remarks>
public sealed class ConceptRegistryTriggerTests
{
    [Fact]
    public void ATriggerAppliesToTheAnchorConceptAndToItsSubConcepts()
    {
        var concepts = ExpressionsLanguage.Concepts;
        var intLiteral = concepts.Get(ExpressionsConceptKeys.IntLiteral);

        var triggered = concepts.TriggersFor(intLiteral);

        Assert.Contains(triggered, each => each.Concept.Key == ExpressionsConceptKeys.Binary);
        Assert.Contains(triggered, each => each.Concept.Key == ExpressionsConceptKeys.Unary);
        Assert.All(triggered, each => Assert.Equal(ExpressionsConceptKeys.Expression, each.Trigger.AnchorConcept));
    }

    [Fact]
    public void AnAnchorNothingIsDeclaredForAnswersWithNothing()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
            new ConceptDeclaration("Test.Unrelated"),
        ]);

        // The wrapper's gesture names Test.Anchor as the node it grows from, so it has nothing to say about a
        // concept that is not one.
        Assert.Empty(concepts.TriggersFor(concepts.Get("Test.Unrelated")));
        Assert.Single(concepts.TriggersFor(concepts.Get("Test.Anchor")));
    }

    [Fact]
    public void TheAnswerIsTheLanguageAndNotTheCaller()
    {
        // The expression language has four gestures: two for a binary, one for a negation and one for a
        // parenthesis. The count is what makes "the catalogue is declared on the concepts" checkable.
        var triggered = ExpressionsLanguage.Concepts.TriggersFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.Expression));

        Assert.Equal(4, triggered.Count);
    }

    [Fact]
    public void ATriggerOnAnUnknownAnchorConceptIsRefusedAtRegistration()
    {
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Wrapper", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Nope", TriggerSide.Left, _ => true, "inner")]),
        ]));
    }

    [Fact]
    public void ATriggerWhoseAnchorSlotIsMissingOrNotASingleChildIsRefused()
    {
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new PropertySlot("name", PropertyType.String)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
        ]));

        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.Many)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
        ]));
    }

    [Fact]
    public void ATriggerWhoseAnchorCannotStandInItsSlotIsRefused()
    {
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration("Test.Other"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Other", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
        ]));
    }

    [Fact]
    public void ATriggerNamingAPropertyOrCaretSlotItDoesNotDeclareIsRefused()
    {
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner", propertySlot: "operator")]),
        ]));

        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner", caretSlot: "right")]),
        ]));
    }

    [Fact]
    public void ATriggerOnAnAbstractConceptIsRefused()
    {
        // No node of an abstract concept can exist, so its gesture could never fire - and triggers are not
        // inherited, because a trigger creates the concept it is declared on.
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.One)],
                isAbstract: true,
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
        ]));
    }

    [Fact]
    public void ASubConceptDoesNotInheritTheGesture()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Anchor"),
            new ConceptDeclaration(
                "Test.Wrapper",
                [new ChildSlot("inner", "Test.Anchor", Cardinality.One)],
                triggers: [new ConceptTrigger("Test.Anchor", TriggerSide.Left, _ => true, "inner")]),
            new ConceptDeclaration("Test.Special", superConcepts: ["Test.Wrapper"]),
        ]);

        Assert.Single(concepts.Get("Test.Wrapper").Triggers);
        Assert.Empty(concepts.Get("Test.Special").Triggers);
    }
}
