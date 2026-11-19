using Projectional.Core;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The bodies that act on the caret's node rather than give it a value: delete, which is marked destructive, and
/// move, which names the direction the node ends up in.
/// </summary>
public sealed class IntentionCatalogueTests
{
    [Fact]
    public void ADeclaredDeleteIsDestructiveAndRemovesTheNode()
    {
        var (ruleset, rules) = TwoRules();

        using var session = Session(ruleset, Body(IntentionConceptKeys.Delete, "Delete this rule"));

        Caret(session, rules[0]);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal(EditSafety.Destructive, offer.Safety);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Single(ruleset.GetChildren(RulesLanguage.RulesSlot));
    }

    [Fact]
    public void ADeclaredMovePutsTheNodeWhereTheRuleSays()
    {
        var (ruleset, rules) = TwoRules();
        var second = rules[1].GetOrNull("name");

        using var session = Session(ruleset, Move(IntentionMoves.Up));

        Caret(session, rules[1]);

        session.Apply(Assert.Single(IntentionList.For(session)));

        Assert.Equal(second, ruleset.GetChildren(RulesLanguage.RulesSlot)[0].GetOrNull("name"));
    }

    [Fact]
    public void ADirectionOutsideTheFourSymbolsIsRefusedByTheModel()
    {
        // The direction is an enumerated property, so a value outside the four symbols never reaches the lowering.
        Assert.Throws<ModelInvariantException>(() => Move("sideways"));
    }

    private static (Node Ruleset, IReadOnlyList<Node> Rules) TwoRules()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);

        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        var first = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        first.Set("name", "First");
        second.Set("name", "Second");

        return (ruleset, ruleset.GetChildren(RulesLanguage.RulesSlot));
    }

    private static EditorSession Session(Node ruleset, Node body)
    {
        var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));

        session.AddIntentions(
            IntentionLowering.CreateProvider([body], RulesLanguage.Concepts),
            IntentionSource.Language);

        return session;
    }

    private static Node Body(string bodyKey, string label)
    {
        var model = IntentionLanguage.NewProject().NewModel("entry");
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        rule.Set(IntentionLanguage.ConceptKeySlot, RulesConceptKeys.Rule);
        rule.Set(IntentionLanguage.LabelSlot, label);
        rule.Set(IntentionLanguage.DescriptionSlot, label + ".");

        model.NewChild(rule, IntentionLanguage.ActionSlot, bodyKey);

        return rule;
    }

    private static Node Move(string direction)
    {
        var rule = Body(IntentionConceptKeys.Move, "Move the rule");

        Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot)).Set(IntentionLanguage.MoveSlot, direction);

        return rule;
    }

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
