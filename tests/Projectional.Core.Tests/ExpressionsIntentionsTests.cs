using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The one intention the expression vocabulary still ships with - negate - at the carets where it means something
/// and at the ones where it does not.
/// </summary>
/// <remarks>
/// <para>
/// The fixture is the sample document - <c>rule Approved when amount &gt; 1000 then Accept</c> - so the wrap is
/// exercised where a real language meets it: around the condition, and around one of its operands.
/// </para>
/// <para>
/// "Wrap in parentheses" is deliberately absent here: it is the standard catalogue's <c>Wrap in "Parenthesized"</c>
/// now, and <see cref="StandardIntentionsTests"/> is where that is proven. What this file still owns is the part a
/// generic entry cannot express - the operator the negation is born with.
/// </para>
/// </remarks>
public sealed class ExpressionsIntentionsTests
{
    [Fact]
    public void AnExpressionOffersTheNegation()
    {
        var (session, _, condition) = Document();

        Caret(session, condition);

        Assert.Equal([ExpressionsIntentions.NegateId], IntentionList.For(session).Select(offer => offer.Id));
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
    public void AnAlreadyNegatedExpressionOffersNothing()
    {
        // A second negation would draw another level without changing the meaning.
        var (session, rule, condition) = Document();

        Caret(session, condition);
        session.Apply(Offer(session, ExpressionsIntentions.NegateId));

        Caret(session, Assert.Single(rule.GetChildren("condition")));

        Assert.Empty(IntentionList.For(session));
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
        // One entry is the ordinary case, not an exception: the gesture never applies an offer by itself.
        var (session, rule, condition) = Document();

        Caret(session, condition);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);

        Assert.Equal(ExpressionsIntentions.NegateId, Assert.Single(session.Offered!.Entries).Id);

        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Null(session.Offered);

        var unary = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.Unary, unary.Concept.Key);
        Assert.Same(condition, unary.GetChild("operand"));
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
