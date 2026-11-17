using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Replacing every occurrence: one undo step, one write per cell, and refusals that do not abort the run.</summary>
public sealed class ReplaceAllActionTests
{
    [Fact]
    public void EveryOccurrenceIsReplaced()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "bread", "milk");

        using var session = TestEditor.Open(body.Parent!);

        Assert.Equal(
            EditOutcome.Applied,
            session.Apply(new ReplaceAllAction(new FindQuery("milk"), "cream")).Outcome);

        Assert.Equal(["cream", "bread", "cream"], [.. steps.Select(step => step.GetOrNull("text"))]);
    }

    [Fact]
    public void TwoOccurrencesInOneCellAreBothReplaced()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a a a");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new ReplaceAllAction(new FindQuery("a"), "bb"));

        // Right to left, so the offsets of the earlier ones stay valid.
        Assert.Equal("bb bb bb", steps[0].GetOrNull("text"));
    }

    [Fact]
    public void AReplaceAllIsOneUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "milk");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new ReplaceAllAction(new FindQuery("milk"), "cream"));

        Assert.True(session.Undo());
        Assert.Equal(["milk", "milk"], [.. steps.Select(step => step.GetOrNull("text"))]);
    }

    [Fact]
    public void AQueryThatMatchesNothingIsNotAnEdit()
    {
        var (body, _) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new ReplaceAllAction(new FindQuery("nothing"), "cream"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(session.Undo());
    }

    [Fact]
    public void AValueTheTypeRefusesLeavesTheDocumentAndTheHistoryAlone()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        steps[0].Set("index", "42");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new ReplaceAllAction(new FindQuery("42"), "not a number"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal("42", steps[0].GetOrNull("index"));
        Assert.False(session.Undo());
    }
}