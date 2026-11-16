using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The action that writes a property of a node the caller names, which is what a language needs when the value
/// has no cell of its own.
/// </summary>
/// <remarks>
/// The fixture's <c>enabled</c> property is declared and not drawn by <see cref="TestNotations"/>, so it is exactly
/// the situation the action exists for - and the situation the cell-addressed action cannot serve.
/// </remarks>
public sealed class SetNodePropertyActionTests
{
    [Fact]
    public void ItWritesAPropertyTheNotationDoesNotDraw()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(rule.Id, "enabled", "false"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("false", rule.GetOrNull("enabled"));

        // One step, and it is the only one: the value is written once, not once per cell it is not drawn in.
        Assert.True(session.Undo());
        Assert.Equal("true", rule.GetOrNull("enabled"));
        Assert.False(session.Undo());
    }

    [Fact]
    public void WritingTheValueItAlreadyHoldsIsNotAnEdit()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(rule.Id, "enabled", "true"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(session.Undo());
    }

    [Fact]
    public void AnUndeclaredSlotIsRefused()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(rule.Id, "nope", "x"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("'nope'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueTheTypeRefusesIsRefused()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(rule.Id, "enabled", "perhaps"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("true", rule.GetOrNull("enabled"));
    }

    [Fact]
    public void AChildSlotIsNotAProperty()
    {
        // A slot that is not a property has no value to write, and the refusal has to come from the action rather
        // than from an exception on the way to the model.
        var (rule, body) = TestEditor.RuleWithBody();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(rule.Id, "body", "x"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("Test.Body", body.Concept.Key);
    }

    [Fact]
    public void ANodeThatIsNoLongerThereIsRefused()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetNodePropertyAction(NodeId.New(), "enabled", "false"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer holds", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ItNeedsTheValueToBeTheCallersBusiness()
    {
        Assert.Throws<ArgumentNullException>(() => new SetNodePropertyAction(NodeId.New(), "enabled", null!));
        Assert.Throws<ArgumentException>(() => new SetNodePropertyAction(NodeId.New(), string.Empty, "x"));
    }
}
