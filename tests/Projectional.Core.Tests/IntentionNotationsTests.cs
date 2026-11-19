using Projectional.IntentionRules;
using Projectional.IntentionRules.Notation;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The intention language drawn as the sentence a rule is: the definition is a model like any other, so it is read
/// with the same renderer a user DSL is.
/// </summary>
public sealed class IntentionNotationsTests
{
    [Fact]
    public void ARuleIsDrawnAsOneSentence()
    {
        var model = IntentionLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(IntentionLanguage.LabelSlot, "Negate");
        rule.Set(IntentionLanguage.ConceptKeySlot, "Expressions.Expression");

        var action = Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot));

        action.Set(IntentionLanguage.WrapperSlot, "Expressions.Unary");
        action.Set(IntentionLanguage.WrapperChildSlot, "operand");

        var rendered = LayoutSnapshot.Render(model, Notations(), width: 200);

        Assert.Contains("intention Negate on Expressions.Expression does", rendered, StringComparison.Ordinal);
        Assert.Contains("wrap in Expressions.Unary.operand with", rendered, StringComparison.Ordinal);

        // An empty preset list is a decision ("born with nothing"), not a value the author forgot.
        Assert.Contains("<none>", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void APresetIsDrawnAsTheValueItWrites()
    {
        var model = IntentionLanguage.NewModel();
        var action = Assert.Single(model.Roots[0].GetChildren(IntentionLanguage.ActionSlot));
        var preset = model.NewChild(action, IntentionLanguage.PresetSlot, IntentionConceptKeys.Preset);

        preset.Set(IntentionLanguage.PresetPropertySlot, "operator");
        preset.Set(IntentionLanguage.PresetValueSlot, "!");

        Assert.Contains("operator = !", LayoutSnapshot.Render(model, Notations(), width: 200), StringComparison.Ordinal);
    }

    private static NotationRegistry Notations()
    {
        var notations = new NotationRegistry(IntentionLanguage.Concepts);

        IntentionNotations.AddTo(notations);

        return notations;
    }
}
