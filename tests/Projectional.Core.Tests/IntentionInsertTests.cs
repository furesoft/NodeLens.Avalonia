using Projectional.Core;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The second declared body: an entry that inserts a sibling, which is what most languages want beside their own node
/// ("another rule after this one").
/// </summary>
public sealed class IntentionInsertTests
{
    [Fact]
    public void ADeclaredInsertIsOfferedOnItsConcept()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];

        using var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));

        session.AddIntentions(
            IntentionLowering.CreateProvider([Insert()], RulesLanguage.Concepts),
            IntentionSource.Language);

        Caret(session, ruleset.GetChildren(RulesLanguage.RulesSlot)[0]);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Add a rule", offer.Label);
        Assert.Equal("intent.Rules.Rule.Add-a-rule", offer.Id);
    }

    [Fact]
    public void ASideTheLanguageDoesNotHaveIsRefusedByTheModel()
    {
        // The side is an enumerated property, so a value outside the two symbols never reaches the lowering.
        Assert.Throws<ModelInvariantException>(() => Insert("sideways"));
    }

    private static Node Insert(string side = IntentionSiblings.After)
    {
        var model = IntentionLanguage.NewProject().NewModel("insert");
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        rule.Set(IntentionLanguage.ConceptKeySlot, RulesConceptKeys.Rule);
        rule.Set(IntentionLanguage.LabelSlot, "Add a rule");
        rule.Set(IntentionLanguage.DescriptionSlot, "Adds another rule beside this one.");

        var action = model.NewChild(rule, IntentionLanguage.ActionSlot, IntentionConceptKeys.InsertSibling);

        action.Set(IntentionLanguage.SideSlot, side);

        return rule;
    }

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
