using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Writing a property in one step: the action a repair uses when it knows the value it wants.
/// </summary>
/// <remarks>
/// The interesting cases are the two refusals, because they are what keeps a fix inside the language:
/// an address the document no longer draws, and a value the property's type rejects.
/// </remarks>
public sealed class SetPropertyActionTests
{
    [Fact]
    public void ItWritesTheValueAndMovesTheCaretOntoIt()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var result = session.Apply(new SetPropertyAction(name, "walk"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.Equal(name, result.Selection.Caret.Address);
    }

    [Fact]
    public void AnAddressTheDocumentDoesNotDrawIsRejected()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "nope"), "walk"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Reason));
        Assert.Equal("unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void AValueTheTypeRejectsIsRejectedRatherThanWritten()
    {
        // A fix may not do what a keystroke may not do: the cell's own type stays the authority.
        var (_, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(step);
        var index = CellAddress.OfSlot(step.Id, "index");

        var result = session.Apply(new SetPropertyAction(index, "not a number"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    [Fact]
    public void WritingTheValueItAlreadyHoldsIsNotAChange()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "unnamed"));

        // The model is unchanged, so there is nothing to undo - and a fix that turned out to change
        // nothing must not look like an edit that happened.
        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void NullsAreRejected()
    {
        var address = CellAddress.OfSlot(TestEditor.BareRule().Id, "name");

        Assert.Throws<ArgumentNullException>(() => new SetPropertyAction(address, null!));
    }
}
