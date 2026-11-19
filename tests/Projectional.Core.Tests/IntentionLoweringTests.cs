using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language's own intentions, written as documents: the rule lowers into the same entry a hand-written package
/// registers, and applying it produces the same edit.
/// </summary>
/// <remarks>
/// The fixture is the negation, because it is exactly the entry a declaration cannot derive: a <c>Unary</c> born
/// with <c>operator = "!"</c>.
/// </remarks>
public sealed class IntentionLoweringTests
{
    [Fact]
    public void ADeclaredNegationIsOfferedWhereTheHandWrittenOneIs()
    {
        var (session, _, condition) = Document();

        Caret(session, condition);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Negate", offer.Label);
        Assert.Equal("intent.Expressions.Expression.Negate", offer.Id);
    }

    [Fact]
    public void ApplyingItWrapsTheNodeWithThePresetValue()
    {
        var (session, rule, condition) = Document();

        Caret(session, condition);

        session.Apply(IntentionList.For(session).Single());

        var unary = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.Unary, unary.Concept.Key);
        Assert.Equal("!", unary.GetOrNull("operator"));
        Assert.Same(condition, unary.GetChild("operand"));
    }

    [Fact]
    public void ARuleOnAConceptIsNotOfferedElsewhere()
    {
        var (session, _, _) = Document();

        // The rule is about expressions; the ruleset's name is not one.
        session.Select(Selection.At(CellAddress.OfSlot(session.Document.Id, "name")));

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void AConceptThatIsNotDeclaredIsRefused()
    {
        // The rule names a concept of another language, which this registry cannot answer for.
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(RulesConceptKeys.Rule));

        Assert.Contains("not a concept", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APresetThatNamesNoPropertyOfTheWrapperIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(preset: ("nothing", "x")));

        Assert.Contains("no property named 'nothing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoIntentionsWithOneLabelOnOneConceptAreRefused()
    {
        var first = Rule().Rule;
        var second = Rule().Rule;

        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([first, second], ExpressionsLanguage.Concepts));

        Assert.Contains("one entry per label", exception.Message, StringComparison.Ordinal);
    }

    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(
            IntentionLowering.CreateProvider([Rule().Rule], ExpressionsLanguage.Concepts),
            IntentionSource.Language);

        return (session, rule, rule.GetChild("condition")!);
    }

    /// <summary>The negation as a document: wrap any expression in a <c>Unary</c> born with <c>operator = "!"</c>.</summary>
    private static (Model Model, Node Rule) Rule(
        string conceptKey = ExpressionsConceptKeys.Expression,
        (string Slot, string Value)? preset = null)
    {
        var model = IntentionLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(IntentionLanguage.ConceptKeySlot, conceptKey);
        rule.Set(IntentionLanguage.LabelSlot, "Negate");
        rule.Set(IntentionLanguage.DescriptionSlot, "Surrounds this expression with a negation.");

        var action = Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot));

        action.Set(IntentionLanguage.WrapperSlot, ExpressionsConceptKeys.Unary);
        action.Set(IntentionLanguage.WrapperChildSlot, "operand");

        var (slot, value) = preset ?? ("operator", "!");

        var entry = model.NewChild(action, IntentionLanguage.PresetSlot, IntentionConceptKeys.Preset);
        entry.Set(IntentionLanguage.PresetPropertySlot, slot);
        entry.Set(IntentionLanguage.PresetValueSlot, value);

        return (model, rule);
    }

    private static void Lower(string conceptKey = ExpressionsConceptKeys.Expression, (string Slot, string Value)? preset = null) =>
        IntentionLowering.CreateProvider([Rule(conceptKey, preset).Rule], ExpressionsLanguage.Concepts);

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
