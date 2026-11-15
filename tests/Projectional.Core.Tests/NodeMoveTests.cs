using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Reordering is an edit of the model, not of the picture: the node keeps its identity, the journal sees
/// one step, and the caret stays in the node that moved. These tests pin all three, because a move that
/// rebuilt the node would take every reference to it with it.
/// </summary>
/// <remarks>
/// The rules of a ruleset are the cheapest list to reorder: they are the children of one slot, so their
/// order is the order of <c>rules</c> and nothing else. Each rule is named here so that the assertions
/// can talk about which one went where.
/// </remarks>
public sealed class NodeMoveTests
{
    [Fact]
    public void AltUpSwapsTheNodeWithTheOneBeforeIt()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[1].Id, "name")));

            var result = session.Apply(KeyRouter.For(EditorKey.AltUp));

            Assert.Equal(EditOutcome.Applied, result.Outcome);
            Assert.Equal("b,a,c", Order(ruleset));

            // The address names the node, not the line, so the caret is still in the rule it was in.
            Assert.Equal(CellAddress.OfSlot(rules[1].Id, "name"), session.Selection.Caret.Address);
            Assert.Equal(rules[1].Id, ruleset.GetChildren("rules")[0].Id);
        }
    }

    [Fact]
    public void AltDownSwapsTheNodeWithTheOneAfterIt()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[1].Id, "name")));

            var result = session.Apply(KeyRouter.For(EditorKey.AltDown));

            Assert.Equal(EditOutcome.Applied, result.Outcome);
            Assert.Equal("a,c,b", Order(ruleset));
            Assert.Equal(CellAddress.OfSlot(rules[1].Id, "name"), session.Selection.Caret.Address);
        }
    }

    [Fact]
    public void MovingTheFirstNodeUpDoesNothing()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[0].Id, "name")));
            var before = session.Selection;

            var result = session.Apply(KeyRouter.For(EditorKey.AltUp));

            // Nothing to trade places with, so the host gets the same answer a Tab at the end gives:
            // unchanged, which is how a control knows it may pass the key on.
            Assert.Equal(EditOutcome.Unchanged, result.Outcome);
            Assert.Equal("a,b,c", Order(ruleset));
            Assert.Equal(0, session.History.UndoDepth);
            Assert.Equal(before, session.Selection);
        }
    }

    [Fact]
    public void MovingTheLastNodeDownDoesNothing()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[2].Id, "name")));

            var result = session.Apply(KeyRouter.For(EditorKey.AltDown));

            Assert.Equal(EditOutcome.Unchanged, result.Outcome);
            Assert.Equal("a,b,c", Order(ruleset));
        }
    }

    [Fact]
    public void AMoveIsOneUndoStepThatPutsTheNodeBack()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[1].Id, "name")));
            session.Apply(KeyRouter.For(EditorKey.AltUp));

            // The move is one mutation, so it is one step even though the journal sees the node leave
            // and arrive.
            Assert.Equal(1, session.History.UndoDepth);

            Assert.True(session.Undo());

            Assert.Equal("a,b,c", Order(ruleset));
            Assert.Equal(rules[1].Id, ruleset.GetChildren("rules")[1].Id);
        }
    }

    [Fact]
    public void MovingBackAndForthUndoesAsTwoSteps()
    {
        var (session, ruleset, rules) = OpenThreeRules();

        using (session)
        {
            session.Select(Selection.At(CellAddress.OfSlot(rules[0].Id, "name")));

            session.Apply(KeyRouter.For(EditorKey.AltDown));
            session.Apply(KeyRouter.For(EditorKey.AltDown));

            Assert.Equal("b,c,a", Order(ruleset));
            Assert.Equal(2, session.History.UndoDepth);

            session.Undo();
            session.Undo();

            Assert.Equal("a,b,c", Order(ruleset));
        }
    }

    private static string Order(Node ruleset) =>
        string.Join(",", ruleset.GetChildren("rules").Select(rule => rule.Get("name")));

    private static (EditorSession Session, Node Ruleset, Node[] Rules) OpenThreeRules()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        string[] names = ["a", "b", "c"];

        // The names are set before the session exists, so that the history these tests count holds the
        // moves and nothing else.
        var rules = names
            .Select(name =>
            {
                var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
                rule.Set("name", name);

                return rule;
            })
            .ToArray();

        return (new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts)), ruleset, rules);
    }
}
