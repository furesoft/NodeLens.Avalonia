using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a duplicate refuses: a node that is gone, and the only child of a single-valued slot.
/// </summary>
/// <remarks>
/// Both are the ordinary "the document moved on" answers rather than errors - an offer outlives the document it was
/// derived from, so it declines with a reason the host can show instead of throwing inside a menu. The happy path is
/// covered where the entry is derived and applied (<see cref="StandardIntentionsTests"/>).
/// </remarks>
public sealed class DuplicateNodeActionTests
{
    [Fact]
    public void ANodeThatIsGoneIsRejected()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();

        step.Set("text", "walk");

        using var session = TestEditor.Open(rule);
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        session.Apply(new DeleteNodeAction());

        var result = session.Apply(new DuplicateNodeAction(step.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(0, session.History.RedoDepth);
    }

    [Fact]
    public void TheOnlyChildOfASingleValuedSlotIsRejected()
    {
        var outer = TestEditor.BareRule();
        var inner = outer.Model.NewChild(outer, "extends", TestLanguage.Rule);

        using var session = TestEditor.Open(outer);

        var result = session.Apply(new DuplicateNodeAction(inner.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);

        // Nothing was copied and nothing entered the history.
        Assert.False(session.History.CanUndo);
        Assert.Equal(inner.Id, Assert.Single(outer.GetChildren("extends")).Id);
    }
}
