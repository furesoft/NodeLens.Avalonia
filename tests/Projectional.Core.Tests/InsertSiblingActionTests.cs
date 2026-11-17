using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What an inserted sibling refuses: a node that is gone, a root, and the only child of a single-valued slot.
/// </summary>
/// <remarks>
/// Each is the ordinary "there is no room" answer rather than an error, because the offer that produced the action
/// outlives the document it was derived from. The happy path - both sides, and the caret landing in the new node -
/// is covered where the entry is derived and applied (<see cref="StandardIntentionsTests"/>).
/// </remarks>
public sealed class InsertSiblingActionTests
{
    [Fact]
    public void ANodeThatIsGoneIsRejected()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();

        step.Set("text", "walk");

        using var session = TestEditor.Open(rule);
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        session.Apply(new DeleteNodeAction());

        var result = session.Apply(new InsertSiblingAction(step.Id, SiblingPlace.After));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void ARootIsRejected()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new InsertSiblingAction(rule.Id, SiblingPlace.Before));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void TheOnlyChildOfASingleValuedSlotIsRejected()
    {
        var outer = TestEditor.BareRule();
        var inner = outer.Model.NewChild(outer, "extends", TestLanguage.Rule);

        using var session = TestEditor.Open(outer);

        var result = session.Apply(new InsertSiblingAction(inner.Id, SiblingPlace.After));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(inner.Id, Assert.Single(outer.GetChildren("extends")).Id);
    }
}
