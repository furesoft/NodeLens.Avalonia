using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Walking the occurrences: forward, backward, wrapping, and what a miss does.</summary>
/// <remarks>
/// Two rules are pinned, both of them things a naive implementation gets wrong. The caret's <em>own</em> occurrence is
/// passed over, or the second press would select what the first one did. And a search wraps, because the alternative -
/// stopping silently at the end - makes a user believe there is nothing further when there is.
/// </remarks>
public sealed class FindNextActionTests
{
    [Fact]
    public void TheFirstPressSelectsTheFirstOccurrence()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "milk");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new FindNextAction(new FindQuery("milk")));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(CellAddress.OfSlot(steps[0].Id, "text"), session.Selection.Caret.Address);
        Assert.Equal(0, session.Selection.Start);
        Assert.Equal(4, session.Selection.End);
    }

    [Fact]
    public void RepeatedPressesWalkForward()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "milk");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new FindNextAction(new FindQuery("milk")));
        session.Apply(new FindNextAction(new FindQuery("milk")));

        Assert.Equal(CellAddress.OfSlot(steps[1].Id, "text"), session.Selection.Caret.Address);
    }

    [Fact]
    public void TheSearchWrapsAroundAtTheEnd()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "milk");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new FindNextAction(new FindQuery("milk")));
        session.Apply(new FindNextAction(new FindQuery("milk")));
        session.Apply(new FindNextAction(new FindQuery("milk")));

        Assert.Equal(CellAddress.OfSlot(steps[0].Id, "text"), session.Selection.Caret.Address);
    }

    [Fact]
    public void BackwardSearchSelectsTheOccurrenceBefore()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "milk");

        using var session = TestEditor.Open(body.Parent!);

        // What a forward find just selected must not be found again: "previous" moves to the occurrence before it.
        var second = CellAddress.OfSlot(steps[1].Id, "text");
        session.Select(new Selection(CaretPosition.At(second, 0), CaretPosition.At(second, 4)));

        session.Apply(new FindNextAction(new FindQuery("milk"), FindDirection.Backward));

        Assert.Equal(CellAddress.OfSlot(steps[0].Id, "text"), session.Selection.Caret.Address);
    }

    [Fact]
    public void AQueryThatMatchesNothingLeavesTheCaretWhereItIs()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);
        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));

        var before = session.Selection.Caret;
        var result = session.Apply(new FindNextAction(new FindQuery("nothing")));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before.Address, session.Selection.Caret.Address);
        Assert.Equal(before.Offset, session.Selection.Caret.Offset);
    }

    [Fact]
    public void AFindingIsNotAnUndoStep()
    {
        var (body, _) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new FindNextAction(new FindQuery("milk")));

        Assert.False(session.Undo());
    }
}