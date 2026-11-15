using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The seam between the two languages is a slot, so this is where an edit crosses it: pressing Enter
/// in an empty condition has to create a concept the rules language never declared, and the rule about
/// which one is the expression language's to make.
/// </summary>
public sealed class RulesLanguageEditingTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    [Fact]
    public void EnterInAnEmptyConditionCreatesTheDefaultOfTheBorrowedConcept()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "condition")));

            var result = session.Apply(KeyRouter.For(EditorKey.Enter));

            Assert.Equal(EditOutcome.Applied, result.Outcome);
            Assert.Equal(ExpressionsConceptKeys.IntLiteral, rule.GetChild("condition")?.Concept.Key);
        }
    }

    [Fact]
    public void TypingAfterEnterFillsTheNewLiteralAndUndoUnwindsBothSteps()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "condition")));
            session.Apply(KeyRouter.For(EditorKey.Enter));
            session.Apply(KeyRouter.Type("42"));

            Assert.Equal(42, rule.GetChild("condition")?.GetInt("value"));
            Assert.Equal(2, session.History.UndoDepth);

            Assert.True(session.Undo());
            Assert.Equal(ExpressionsConceptKeys.IntLiteral, rule.GetChild("condition")?.Concept.Key);

            Assert.True(session.Undo());
            Assert.Null(rule.GetChild("condition"));
        }
    }

    [Fact]
    public void EnterInTheRuleListLeavesTheCaretWhereTheNewRulesNameCanBeTyped()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];

        using var session = new EditorSession(ruleset, Notations);

        session.Select(Selection.At(CellAddress.OfSlot(ruleset.Id, "rules")));

        var created = session.Apply(KeyRouter.For(EditorKey.Enter));
        var rule = ruleset.GetChildren("rules")[0];

        // The caret ends up in the new rule's name rather than on the keyword in front of it, so the
        // very next keystroke is part of naming the rule instead of being refused by a keyword.
        Assert.Equal(EditOutcome.Applied, created.Outcome);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);

        var typed = session.Apply(KeyRouter.Type("Bonus"));

        Assert.Equal(EditOutcome.Applied, typed.Outcome);
        Assert.NotEqual("unnamed", rule.Get("name"));
    }

    private static (EditorSession Session, Node Rule) OpenRuleWithEmptyCondition()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        var session = new EditorSession(ruleset, Notations);

        return (session, rule);
    }
}
