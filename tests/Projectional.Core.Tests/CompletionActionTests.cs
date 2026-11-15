using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Completion driven the way a user drives it: through <see cref="KeyRouter"/> and
/// <see cref="EditorSession.Apply"/>, never by calling an action directly.
/// </summary>
/// <remarks>
/// The empty <c>condition</c> of a rule is the case worth pinning, because the slot is declared over an
/// abstract concept the rules language did not write - so the proposals come from the language below it,
/// which is exactly the seam the feature exists for.
/// </remarks>
public sealed class CompletionActionTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    [Fact]
    public void AnEmptySlotOffersEveryConcreteConceptTheLanguageOffers()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            var result = session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));

            Assert.Equal(EditOutcome.Unchanged, result.Outcome);

            var list = Assert.IsType<CompletionList>(session.Completion);

            // Seven concrete Expression concepts, minus the three literals the language hides; the
            // abstract base is not a candidate either, because no node can be an instance of it.
            Assert.Equal(4, list.Count);
            Assert.DoesNotContain(ExpressionsConceptKeys.Expression, list.Matches.Select(item => item.ConceptKey));

            // Declaration order, so the first proposal is the first concept worth choosing.
            Assert.Equal(ExpressionsConceptKeys.Name, list.Selected?.ConceptKey);
        }
    }

    [Fact]
    public void TheLiteralsAreReachableByEnterButNotOfferedInTheList()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));

            var keys = session.Completion?.Matches.Select(item => item.ConceptKey).ToArray() ?? [];

            Assert.DoesNotContain(ExpressionsConceptKeys.IntLiteral, keys);
            Assert.DoesNotContain(ExpressionsConceptKeys.BoolLiteral, keys);
            Assert.DoesNotContain(ExpressionsConceptKeys.StringLiteral, keys);
        }

        // Hiding a concept from the menu must not make it impossible to create: the ordinary Enter path
        // still materializes the language's default, which is the integer literal.
        var (enterSession, enterRule) = OpenRuleWithEmptyCondition();

        using (enterSession)
        {
            enterSession.Apply(KeyRouter.For(enterSession, EditorKey.Enter));

            Assert.Equal(ExpressionsConceptKeys.IntLiteral, enterRule.GetChild("condition")?.Concept.Key);
        }
    }

    [Fact]
    public void OpeningAListIsNotAnUndoStep()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.Type(session, "Name"));

            Assert.Equal(0, session.History.UndoDepth);
        }
    }

    [Fact]
    public void TypingNarrowsTheListAndEnterFillsTheSlot()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.Type(session, "Name"));

            Assert.Equal(1, session.Completion?.Count);

            var result = session.Apply(KeyRouter.For(session, EditorKey.Enter));

            Assert.Equal(EditOutcome.Applied, result.Outcome);
            Assert.Null(session.Completion);
            Assert.Equal(ExpressionsConceptKeys.Name, rule.GetChild("condition")?.Concept.Key);
        }
    }

    [Fact]
    public void AcceptingIsOneUndoStepThatRemovesTheNodeAgain()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.For(session, EditorKey.Enter));

            Assert.Equal(1, session.History.UndoDepth);

            Assert.True(session.Undo());

            Assert.Null(rule.GetChild("condition"));
        }
    }

    [Fact]
    public void TheCaretLandsInWhatWasCreated()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.For(session, EditorKey.Enter));

            var created = rule.GetChild("condition");

            Assert.NotNull(created);

            // Inside the new node rather than on the slot that held it, so the user can keep typing: the
            // first editable cell of a Name is its name.
            Assert.Equal(
                CellAddress.OfSlot(created.Id, "name"),
                session.Selection.Caret.Address);
        }
    }

    [Fact]
    public void MovingTheHighlightAcceptsTheOneBelow()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.For(session, EditorKey.Down));

            session.Apply(KeyRouter.For(session, EditorKey.Enter));

            Assert.Equal(ExpressionsConceptKeys.Binary, rule.GetChild("condition")?.Concept.Key);
        }
    }

    [Fact]
    public void TabAcceptsJustLikeEnter()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.For(session, EditorKey.Tab));

            Assert.Equal(ExpressionsConceptKeys.Name, rule.GetChild("condition")?.Concept.Key);
        }
    }

    [Fact]
    public void EscapeLeavesTheModelAlone()
    {
        var (session, rule) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));

            var result = session.Apply(KeyRouter.For(session, EditorKey.Escape));

            Assert.Equal(EditOutcome.Unchanged, result.Outcome);
            Assert.Null(session.Completion);
            Assert.Null(rule.GetChild("condition"));
            Assert.Equal(0, session.History.UndoDepth);
        }
    }

    [Fact]
    public void BackspaceWidensTheListAndThenClosesIt()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Apply(KeyRouter.Type(session, "Na"));

            Assert.Equal(1, session.Completion?.Count);

            session.Apply(KeyRouter.For(session, EditorKey.Backspace));

            Assert.Equal("N", session.Completion?.Prefix);

            session.Apply(KeyRouter.For(session, EditorKey.Backspace));

            Assert.Equal(4, session.Completion?.Count);

            // With no prefix left there is nothing to widen, so the key falls through to the ordinary
            // path - which is a non-completion action, and therefore closes the list.
            session.Apply(KeyRouter.For(session, EditorKey.Backspace));

            Assert.Null(session.Completion);
        }
    }

    [Fact]
    public void ANonCompletionKeyClosesTheList()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));

            // Right is not a completion gesture, so it takes the ordinary path - and an ordinary edit
            // action cannot leave a list open over a slot the caret has left.
            session.Apply(KeyRouter.For(session, EditorKey.Right));

            Assert.Null(session.Completion);
        }
    }

    [Fact]
    public void MovingTheCaretClosesTheList()
    {
        var (session, _) = OpenRuleWithEmptyCondition();

        using (session)
        {
            session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));
            session.Select(Selection.At(session.Selection.Caret));

            Assert.Null(session.Completion);
        }
    }

    [Fact]
    public void AFilledSlotHasNothingToComplete()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        model.NewChild(rule, "condition", ExpressionsConceptKeys.IntLiteral);

        using var session = new EditorSession(ruleset, Notations);

        session.Select(Selection.At(CellAddress.OfElement(rule.Id, "condition", 0)));
        session.Apply(KeyRouter.For(session, EditorKey.ControlSpace));

        // The caret is inside the created expression, where the slot at hand is a property - so there is
        // no list rather than a list of everything the language owns.
        Assert.Null(session.Completion);
    }

    private static (EditorSession Session, Node Rule) OpenRuleWithEmptyCondition()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);

        var session = new EditorSession(ruleset, Notations);

        // The session starts with the caret at the beginning of the document, so standing in the slot
        // that is actually being completed is part of building the fixture.
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "condition")));

        return (session, rule);
    }
}
