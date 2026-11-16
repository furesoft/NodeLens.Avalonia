using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The two intentions the expression vocabulary ships with, at the carets where they mean something and at
/// the ones where they do not.
/// </summary>
/// <remarks>
/// The fixture is the sample document - <c>rule Approved when amount &gt; 1000 then Accept</c> - so the
/// wrappers are exercised where a real language meets them: around the condition, and around one of its
/// operands.
/// </remarks>
public sealed class ExpressionsIntentionsTests
{
    [Fact]
    public void AnExpressionOffersBothIntentions()
    {
        var (session, _, condition) = Document();

        Caret(session, condition);

        Assert.Equal(
            [ExpressionsIntentions.WrapInParenthesesId, ExpressionsIntentions.NegateId],
            IntentionList.For(session).Select(offer => offer.Id));

        // Both are plain, so the gesture has to ask rather than pick one.
        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);
        Assert.Equal(2, session.Offered!.Count);
    }

    [Fact]
    public void WrappingTheConditionPutsParenthesesAroundIt()
    {
        var (session, rule, condition) = Document();

        Caret(session, condition);

        Assert.Equal(EditOutcome.Applied, session.Apply(Offer(session, ExpressionsIntentions.WrapInParenthesesId)).Outcome);

        var grouped = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.Parenthesized, grouped.Concept.Key);
        Assert.Same(condition, grouped.GetChild("inner"));

        Assert.True(session.Undo());
        Assert.Same(condition, Assert.Single(rule.GetChildren("condition")));
    }

    [Fact]
    public void WrappingAnOperandPutsParenthesesInsideIt()
    {
        // The caret is on the operand, so the parentheses go around the operand - not around the condition.
        var (session, _, condition) = Document();
        var left = condition.GetChild("left")!;

        Caret(session, left);

        session.Apply(Offer(session, ExpressionsIntentions.WrapInParenthesesId));

        var operand = Assert.Single(condition.GetChildren("left"));

        Assert.Equal(ExpressionsConceptKeys.Parenthesized, operand.Concept.Key);
        Assert.Same(left, operand.GetChild("inner"));
    }

    [Fact]
    public void NegatingWrapsWithTheBangOperator()
    {
        var (session, rule, condition) = Document();

        Caret(session, condition);

        session.Apply(Offer(session, ExpressionsIntentions.NegateId));

        var unary = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.Unary, unary.Concept.Key);
        Assert.Equal("!", unary.GetOrNull("operator"));
        Assert.Same(condition, unary.GetChild("operand"));

        Assert.True(session.Undo());
        Assert.Same(condition, Assert.Single(rule.GetChildren("condition")));
    }

    [Fact]
    public void AnAlreadyGroupedExpressionOffersOnlyTheNegation()
    {
        // Wrapping it again would draw a second pair of parentheses without changing the meaning, so only
        // the negation is left.
        var (session, rule, condition) = Document();

        Caret(session, condition);
        session.Apply(Offer(session, ExpressionsIntentions.WrapInParenthesesId));

        Caret(session, Assert.Single(rule.GetChildren("condition")));

        Assert.Equal(
            [ExpressionsIntentions.NegateId],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AnAlreadyNegatedExpressionOffersOnlyTheParentheses()
    {
        var (session, rule, condition) = Document();

        Caret(session, condition);
        session.Apply(Offer(session, ExpressionsIntentions.NegateId));

        Caret(session, Assert.Single(rule.GetChildren("condition")));

        Assert.Equal(
            [ExpressionsIntentions.WrapInParenthesesId],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void SomethingThatIsNotAnExpressionOffersNothing()
    {
        var model = PayrollSample.NewSampleModel();

        using var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        session.Select(Selection.At(CellAddress.OfSlot(model.Roots[0].Id, "name")));

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void AnExpressionWithNowhereToPutTheWrapperOffersNothing()
    {
        // A root expression has no slot for the wrapper to stand in, so there is nothing to wrap it with.
        var model = ExpressionsLanguage.NewProject().NewModel("root");

        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);
        literal.Set("value", 1);
        model.AddRoot(literal);

        using var session = new EditorSession(literal, ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts));
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        Assert.Empty(IntentionList.For(session));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);
        Assert.Null(session.Offered);
    }

    [Fact]
    public void TheGestureOpensAListForALoneIntention()
    {
        // A negated condition offers only the parentheses, and the gesture still shows them: an intention is
        // never run unasked, because nothing on screen said it was there.
        var (session, rule, condition) = Document();

        Caret(session, condition);
        session.Apply(Offer(session, ExpressionsIntentions.NegateId));

        Caret(session, Assert.Single(rule.GetChildren("condition")));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);
        Assert.Equal(ExpressionsIntentions.WrapInParenthesesId, Assert.Single(session.Offered!.Entries).Id);

        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Null(session.Offered);
        Assert.Equal(
            ExpressionsConceptKeys.Parenthesized,
            Assert.Single(rule.GetChildren("condition")).Concept.Key);
    }

    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        return (session, rule, rule.GetChild("condition")!);
    }

    /// <summary>Puts the caret on the first cell <paramref name="node"/> owns.</summary>
    /// <remarks>
    /// A node address is not a caret place by itself: only some notations draw a cell that carries the node's
    /// own address (a keyword, a space), and asking for one that does not exist falls back to the start of
    /// the document. Walking the places and taking the first whose owner is the node is the honest way to say
    /// "the caret is in this node", whatever its notation draws first.
    /// </remarks>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);
}
