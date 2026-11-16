using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Filling a named empty slot, which is Enter's behaviour with the caret taken out of the question.
/// </summary>
/// <remarks>
/// A repair has to name the hole it fills, because a check runs on a model nobody is drawing. The action
/// therefore shares its creation path with <see cref="InsertNodeAction"/>, and these tests pin the answers
/// that path gives: the language's default concept, the caret inside what was created, and a quiet refusal
/// where the language has nothing to offer.
/// </remarks>
public sealed class InsertChildActionTests
{
    [Fact]
    public void TheEmptySlotGetsTheConceptEnterWouldCreate()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new InsertChildAction(rule.Id, "body"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var body = Assert.Single(rule.GetChildren("body"));

        Assert.Equal(TestLanguage.Body, body.Concept.Key);
        Assert.Equal(rule.Id, body.Parent?.Id);
        Assert.Equal("body", body.ParentSlot);
        Assert.Equal(CellAddress.OfElement(rule.Id, "body", 0), result.Selection.Caret.Address);
    }

    [Fact]
    public void TheCaretLandsInsideWhatWasCreated()
    {
        // Not on the element address, which is what the action reports, but on the first cell inside it that
        // can be typed into - the same landing Enter produces, because it is the same session handler.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Apply(new InsertChildAction(rule.Id, "body"));
        var body = rule.GetChild("body")!;

        session.Apply(new InsertChildAction(body.Id, "steps"));
        var step = body.GetChild("steps")!;

        session.Apply(new InsertChildAction(step.Id, "detail"));
        var detail = step.GetChild("detail")!;

        // The element address is what the action reports; the session then lands on the first typeable
        // place inside it, which is a property of the created node rather than the node's own address.
        Assert.Equal(detail.Id, session.Selection.Caret.Address.Owner);
        Assert.False(session.Selection.Caret.Address.IsNode);
    }

    [Fact]
    public void AFilledSlotIsRefusedRatherThanFilledAgain()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new InsertChildAction(rule.Id, "body"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Single(rule.GetChildren("body"));
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsRefused()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new InsertChildAction(rule.Id, "nonsense"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void ASlotWithNoConcreteConceptDoesNothing()
    {
        // The language declared the set and nothing in it, so there is no node to invent - and the answer is
        // "no edit", not a refusal, exactly as Enter answers at that caret.
        var holder = AbstractSlotLanguage.NewHolder();
        using var session = AbstractSlotLanguage.Open(holder);

        var result = session.Apply(new InsertChildAction(holder.Id, "sealed"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(holder.GetChildren("sealed"));
    }

    [Fact]
    public void AnOwnerThatIsGoneIsRefused()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new InsertChildAction(NodeId.New(), "body"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Empty(rule.GetChildren("body"));
    }

    [Fact]
    public void TheSlotIsNamedRatherThanInferred()
    {
        Assert.Throws<ArgumentException>(() => new InsertChildAction(NodeId.Empty, "body"));
        Assert.Throws<ArgumentException>(() => new InsertChildAction(NodeId.New(), string.Empty));
        Assert.Throws<ArgumentNullException>(() => new InsertChildAction(NodeId.New(), null!));
    }
}
