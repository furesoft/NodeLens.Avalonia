using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Replacing a node through the edit layer: the new node lands in the old one's place, the caret lands on it, and
/// every refusal is a reason rather than an exception.
/// </summary>
/// <remarks>
/// The refusals are what an offer that outlived its document meets: the node is gone, the node is the document
/// itself, the concept is unknown or abstract, or it cannot stand where the old one stood. Each is checked before
/// anything is deleted, so a refusal always leaves the document it refused in.
/// </remarks>
public sealed class ReplaceNodeActionTests
{
    [Fact]
    public void TheOldNodeIsReplacedByANewOneAtItsPlace()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new ReplaceNodeAction(steps[0].Id, TestLanguage.Step));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var after = body.GetChildren("steps");

        Assert.Equal(2, after.Count);
        Assert.NotEqual(steps[0].Id, after[0].Id);
        Assert.Equal("b", after[1].GetOrNull("text"));

        // What the old node held is gone, and the caret is on the node that took its place.
        Assert.Null(after[0].GetOrNull("text"));
        Assert.Equal(after[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void AnOnlyChildIsReplacedInItsSlot()
    {
        var rule = TestEditor.BareRule();
        var body = rule.Model.NewChild(rule, "body", TestLanguage.Body);

        using var session = TestEditor.Open(rule);

        Assert.Equal(
            EditOutcome.Applied,
            session.Apply(new ReplaceNodeAction(body.Id, TestLanguage.Body)).Outcome);

        var after = Assert.Single(rule.GetChildren("body"));

        Assert.NotEqual(body.Id, after.Id);
        Assert.Equal("body", after.ParentSlot);
    }

    [Fact]
    public void AReplacementIsOneUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new ReplaceNodeAction(steps[0].Id, TestLanguage.Step));

        Assert.True(session.Undo());
        Assert.Equal(steps[0].Id, body.GetChildren("steps")[0].Id);
        Assert.Equal("a", body.GetChildren("steps")[0].GetOrNull("text"));
    }

    [Fact]
    public void ANodeThatIsGoneIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = TestEditor.Open(body.Parent!);

        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));
        session.Apply(new DeleteNodeAction());

        var result = session.Apply(new ReplaceNodeAction(steps[0].Id, TestLanguage.Step));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void TheDocumentItselfIsRejected()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new ReplaceNodeAction(rule.Id, TestLanguage.Tag));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void AnUnknownConceptIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new ReplaceNodeAction(steps[0].Id, "Nope.Missing"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(steps[0].Id, Assert.Single(body.GetChildren("steps")).Id);
    }

    [Fact]
    public void AConceptThatCannotStandThereIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        // `steps` holds steps; a note may not stand there, and the refusal must not delete what it cannot replace.
        var result = session.Apply(new ReplaceNodeAction(steps[0].Id, TestLanguage.Note));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(steps[0].Id, Assert.Single(body.GetChildren("steps")).Id);
    }
}
