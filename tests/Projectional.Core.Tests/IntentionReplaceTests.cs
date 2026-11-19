using Projectional.Core;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The replace body: an entry that swaps the node's concept, which is the other way a language says "this could have
/// been that".
/// </summary>
public sealed class IntentionReplaceTests
{
    [Fact]
    public void ADeclaredReplaceIsOfferedAndMarkedDestructive()
    {
        var (ruleset, rules) = TwoRules();

        using var session = Session(ruleset, Replace(RulesConceptKeys.Rule, RulesConceptKeys.Ruleset));

        Caret(session, rules[0]);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Swap the rule", offer.Label);
        Assert.Equal(EditSafety.Destructive, offer.Safety);
    }

    [Fact]
    public void AReplacementThatIsTheNodeItselfIsRefusedAtLoad()
    {
        var rule = Replace(RulesConceptKeys.Rule, RulesConceptKeys.Rule);

        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([rule], RulesLanguage.Concepts));

        Assert.Contains("would be the node again", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReplacementThatIsNotAConceptIsRefusedAtLoad()
    {
        var rule = Replace(RulesConceptKeys.Rule, "Rules.Nothing");

        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([rule], RulesLanguage.Concepts));

        Assert.Contains("Rules.Nothing", exception.Message, StringComparison.Ordinal);
    }

    private static (Node Ruleset, IReadOnlyList<Node> Rules) TwoRules()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);

        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", "First");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", "Second");

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

    private static Node Replace(string conceptKey, string with)
    {
        var model = IntentionLanguage.NewProject().NewModel("replace");
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        rule.Set(IntentionLanguage.ConceptKeySlot, conceptKey);
        rule.Set(IntentionLanguage.LabelSlot, "Swap the rule");
        rule.Set(IntentionLanguage.DescriptionSlot, "Puts another concept in the place of this rule.");

        var action = model.NewChild(rule, IntentionLanguage.ActionSlot, IntentionConceptKeys.Replace);

        action.Set(IntentionLanguage.WithSlot, with);

        return rule;
    }

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
