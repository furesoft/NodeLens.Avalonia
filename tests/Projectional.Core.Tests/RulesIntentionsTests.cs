using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Samples.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The one intention the sample language ships with, which is the proof that a host adds no plumbing: a
/// language package declares it and a host registers it.
/// </summary>
public sealed class RulesIntentionsTests
{
    [Fact]
    public void ARuleThatDoesNotAcceptOffersToAccept()
    {
        var (session, rule) = Document();

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal(RulesIntentions.AcceptId, offer.Id);
        Assert.Equal("Accept this rule", offer.Label);
        Assert.False(string.IsNullOrWhiteSpace(offer.Description));
        Assert.True(offer.Availability.IsApplicable);
        Assert.Equal(EditSafety.Plain, offer.Safety);
        Assert.Equal(RuleOutcomes.Review, rule.GetOrNull("outcome"));
    }

    [Fact]
    public void ApplyingItSetsTheOutcomeInOneUndoStep()
    {
        var (session, rule) = Document();

        var result = session.Apply(Assert.Single(IntentionList.For(session)));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(RuleOutcomes.Accept, rule.GetOrNull("outcome"));

        Assert.True(session.Undo());
        Assert.Equal(RuleOutcomes.Review, rule.GetOrNull("outcome"));
    }

    [Fact]
    public void TheGestureRunsItWithoutAsking()
    {
        var (session, rule) = Document();

        Assert.Equal(EditOutcome.Applied, session.Apply(new OfferAction()).Outcome);
        Assert.Null(session.Offered);
        Assert.Equal(RuleOutcomes.Accept, rule.GetOrNull("outcome"));
    }

    [Fact]
    public void ARuleThatAlreadyAcceptsOffersNothing()
    {
        // The action would write the value the rule already holds, which is an unchanged edit rather than a
        // result - and a bulb for it would ask about something nobody needs.
        var (session, rule) = Document();
        rule.Set("outcome", RuleOutcomes.Accept);

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void ACaretOutsideARuleOffersNothing()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];

        using var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);

        session.Select(Selection.At(CellAddress.OfSlot(ruleset.Id, "name")));

        Assert.Empty(IntentionList.For(session));
    }

    private static (EditorSession Session, Node Rule) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var rule = Assert.Single(ruleset.GetChildren(RulesLanguage.RulesSlot));

        rule.Set("outcome", RuleOutcomes.Review);

        var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);

        session.Select(Selection.At(new CellAddress(rule.Id)));

        return (session, rule);
    }
}
