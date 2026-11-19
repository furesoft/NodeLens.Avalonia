using Projectional.Core;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The insert body: the rule names the concept, the caret supplies the empty slot, and the entry is absent anywhere
/// the slot is not empty.
/// </summary>
public sealed class IntentionInsertChildTests
{
    [Fact]
    public void TheEntryIsOfferedOnAnEmptySlotAndFillsIt()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);

        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        using var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));

        session.AddIntentions(
            IntentionLowering.CreateProvider([Insert(RulesConceptKeys.Rule)], RulesLanguage.Concepts),
            IntentionSource.Language);

        // The empty list is the only caret place the ruleset has, and it is the slot the rule asks for.
        EmptySlot(session);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Add a rule", offer.Label);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Single(ruleset.GetChildren(RulesLanguage.RulesSlot));
    }

    [Fact]
    public void TheEntryIsAbsentWhereTheSlotIsFilled()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);

        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        using var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));

        session.AddIntentions(
            IntentionLowering.CreateProvider([Insert(RulesConceptKeys.Rule)], RulesLanguage.Concepts),
            IntentionSource.Language);

        Caret(session, rule);

        // The slot is filled, so an insertion there would be a refusal - and an offer that comes back rejected is not
        // an offer.
        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void AConceptThatIsNotDeclaredIsRefusedAtLoad()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([Insert("Rules.Nothing")], RulesLanguage.Concepts));

        Assert.Contains("Rules.Nothing", exception.Message, StringComparison.Ordinal);
    }

    private static Node Insert(string conceptKey)
    {
        var model = IntentionLanguage.NewProject().NewModel("insert");
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        rule.Set(IntentionLanguage.ConceptKeySlot, RulesConceptKeys.Ruleset);
        rule.Set(IntentionLanguage.LabelSlot, "Add a rule");
        rule.Set(IntentionLanguage.DescriptionSlot, "Puts a rule into the empty list.");

        var action = model.NewChild(rule, IntentionLanguage.ActionSlot, IntentionConceptKeys.Insert);

        action.Set(IntentionLanguage.ConceptSlot, conceptKey);

        return rule;
    }

    private static void EmptySlot(EditorSession session)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.IsEmptySlot);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
