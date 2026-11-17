using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sample's second representation is the proof that variants work on a real language rather than
/// on a test fixture: the same ruleset, read as a table with one row per rule.
/// </summary>
/// <remarks>
/// A representation is view state, so these tests also hold the line the ADR draws - switching one
/// changes nothing the model remembers, nothing the caret stands on and nothing an edit does.
/// </remarks>
public sealed class SampleTableVariantTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    private static NotationChoice AsTable =>
        NotationChoice.Default.With(RulesConceptKeys.Ruleset, RulesLanguage.TableVariant);

    [Fact]
    public void ARulesetReadAsATablePutsItsRulesInRowsUnderTheirHeaders()
    {
        Assert.Equal(
            """
            ruleset Payroll
              rule     then   when
              Approved Accept amount > 1000
            """,
            LayoutSnapshot.Render(PayrollSample.NewSampleModel(), Notations, width: 60, AsTable));
    }

    [Fact]
    public void AnEmptyRulesetReadAsATableShowsItsHeaderAndTheAddRowAffordance()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Empty");
        model.AddRoot(ruleset);

        Assert.Equal(
            """
            ruleset Empty
              rule   then when
              + rule
            """,
            LayoutSnapshot.Render(model, Notations, width: 60, AsTable));
    }

    [Fact]
    public void TheSameModelReadsAsABlockWhenTheChoiceExpressesNoOpinion()
    {
        Assert.Equal(
            """
            ruleset Payroll {
              rule Approved when amount > 1000 then Accept
            }
            """,
            LayoutSnapshot.Render(PayrollSample.NewSampleModel(), Notations, width: 60, NotationChoice.Default));
    }

    [Fact]
    public void TheRulesetDeclaresBothRepresentationsAndTheBlockStaysTheDefault()
    {
        Assert.Equal(["default", RulesLanguage.TableVariant], Notations.VariantsOf(RulesConceptKeys.Ruleset));

        Assert.Equal(
            Notation.DefaultVariant,
            Notations.Get(RulesConceptKeys.Ruleset).Variant);
    }

    [Fact]
    public void SwitchingRepresentationEditsNothingAndKeepsTheCaretWhereItWas()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = model.Roots[0].GetChildren("rules")[0];

        using var session = new EditorSession(model.Roots[0], Notations);

        var name = CellAddress.OfSlot(rule.Id, "name");
        session.Select(Selection.At(CaretPosition.At(name, 2)));

        session.Choose(AsTable);

        // The variant hides no slot of the rule, so the address the caret holds still exists.
        Assert.Equal(name, session.Selection.Caret.Address);
        Assert.Equal(2, session.Selection.Caret.Offset);
        Assert.Equal(0, session.History.Revision);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ARuleReadInATableIsStillEditedThroughTheSameCell()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = model.Roots[0].GetChildren("rules")[0];

        using var session = new EditorSession(model.Roots[0], Notations);

        session.Choose(AsTable);
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 8)));
        session.Apply(new TypeTextAction("!"));

        Assert.Equal("Approved!", rule.Get("name"));
        Assert.Contains("Approved!", LayoutSnapshot.Render(session.Layout, width: 60), StringComparison.Ordinal);
    }
}
