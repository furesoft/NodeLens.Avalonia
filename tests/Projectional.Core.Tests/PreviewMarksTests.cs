using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a ghost draws differently: the cell marks a diff produces, and the counts a badge reads.
/// </summary>
/// <remarks>
/// The marks are always computed from a real sandbox edit, because the mapping is the whole content of this
/// slice: a difference names a node or a slot, and the marks have to land on the cells a user sees. A hand-made
/// diff would test the arithmetic and prove nothing about the projection.
/// </remarks>
public sealed class PreviewMarksTests
{
    [Fact]
    public void AWrapperIsMarkedAsAddedAndTheNodeItWrappedAsChanged()
    {
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);
        var differences = sandbox!.Apply(
            new WrapInAction(condition.Id, ExpressionsConceptKeys.Parenthesized, "inner"));

        var marks = PreviewMarks.Of(differences!, sandbox.Tree);

        Assert.Equal(1, marks.Added);
        Assert.Equal(1, marks.Changed);
        Assert.Equal(0, marks.Removed);
        Assert.Equal(0, marks.Truncated);

        // The parenthesis cells belong to the wrapper, the number's own cell to the number.
        Assert.Contains(marks.Changes, each => each.Kind == CellChangeKind.Added);

        // The wrapped node keeps its identity, so its own cells - whichever slots its notation draws - are the
        // ones marked as changed.
        Assert.Contains(
            marks.Changes,
            each => each.Kind == CellChangeKind.Changed && each.Address.Owner == condition.Id);
    }

    [Fact]
    public void ARewrittenPropertyIsMarkedOnTheCellThatShowsIt()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));
        var marks = PreviewMarks.Of(differences!, sandbox.Tree);

        var mark = Assert.Single(marks.Changes);

        Assert.Equal(CellChangeKind.Changed, mark.Kind);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), mark.Address);

        // The old value travels with the mark, which is how the ghost can show what it replaced.
        Assert.Equal("Approved", mark.OldValue);
        Assert.Equal(1, marks.Changed);
    }

    [Fact]
    public void ARemovalIsMarkedWhereItUsedToBe()
    {
        // A removed node has no cells in the ghost, so the mark goes on the slot cell of the parent it was taken
        // out of - the only place a user can still see the hole.
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);

        // The condition is found again in the copy: an action that reached for the live node would be a "cannot
        // be previewed", which is a different test.
        var differences = sandbox!.Apply(new DelegateAction(context =>
        {
            var copiedRule = context.Document.GetChildren(RulesLanguage.RulesSlot)[0];
            var copied = copiedRule.GetChild("condition")!;

            context.DeleteChild(copiedRule, "condition", copied);

            return EditResult.Applied(context.Selection);
        }));

        var marks = PreviewMarks.Of(differences!, sandbox.Tree);

        // The diff names every node of the removed subtree - the condition and the two operands it was made of -
        // but only the outermost one has a place left to mark, because its children's slot cells went with it.
        Assert.Equal(3, marks.Removed);

        var mark = Assert.Single(marks.Changes);

        Assert.Equal(CellChangeKind.Removed, mark.Kind);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "condition"), mark.Address);
    }

    [Fact]
    public void AnEditThatChangesNothingMarksNothing()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Approved"));
        var marks = PreviewMarks.Of(differences!, sandbox.Tree);

        Assert.True(marks.IsEmpty);
        Assert.Empty(marks.Changes);
        Assert.Equal(0, marks.Truncated);
    }

    [Fact]
    public void TheBudgetKeepsTheFirstMarksAndCountsTheRest()
    {
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);
        var differences = sandbox!.Apply(
            new WrapInAction(condition.Id, ExpressionsConceptKeys.Parenthesized, "inner"));

        var all = PreviewMarks.Of(differences!, sandbox.Tree);
        var clipped = PreviewMarks.Of(differences!, sandbox.Tree, budget: 1);

        Assert.Single(clipped.Changes);
        Assert.Equal(all.Changes.Count - 1, clipped.Truncated);

        // The counts are about the edit, not about the marks, so clipping changes nothing about them.
        Assert.Equal(all.Added, clipped.Added);
        Assert.Equal(all.Changed, clipped.Changed);
    }

    [Fact]
    public void ADifferenceWhoseNodeIsNotDrawnIsCountedAndNotMarked()
    {
        // A concept with no notation produces no cells, so there is nothing to mark - but the edit did happen, and
        // a badge that stayed silent about it would describe a different edit than the one previewed.
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = new[]
        {
            new NodeDifference(NodeId.New(), NodeDifferenceKind.Added, "rules", After: new NodePlace(rule.Id, "rules", 0)),
        };

        var marks = PreviewMarks.Of(differences, sandbox!.Tree);

        Assert.Empty(marks.Changes);
        Assert.Equal(1, marks.Added);
        Assert.False(marks.IsEmpty);
    }

    [Fact]
    public void ABudgetOfZeroKeepsNoMarksAndStillCountsTheEdit()
    {
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);
        var differences = sandbox!.Apply(
            new WrapInAction(condition.Id, ExpressionsConceptKeys.Parenthesized, "inner"));

        var marks = PreviewMarks.Of(differences!, sandbox.Tree, budget: 0);

        Assert.Empty(marks.Changes);
        Assert.True(marks.Truncated > 0);
        Assert.Equal(1, marks.Added);
    }

    [Fact]
    public void ANegativeBudgetIsRefused()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreviewMarks.Of([], sandbox!.Tree, budget: -1));
    }

    /// <summary>The payroll sample opened the way the app opens it, with its notation and its rules.</summary>
    private static EditorSession Live(out Node rule)
    {
        var model = PayrollSample.NewSampleModel();

        rule = model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0];

        return new EditorSession(
            model.Roots[0],
            RulesLanguage.CreateNotations(model.Concepts),
            emphasis: RulesLanguage.Emphasis,
            typing: RulesLanguage.TypingRules);
    }
}
