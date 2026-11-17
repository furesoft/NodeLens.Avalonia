using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Replacing one occurrence: the text around it survives, and a refused value is a rejection.</summary>
public sealed class ReplaceMatchActionTests
{
    [Fact]
    public void OnlyTheOccurrenceIsReplaced()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milkshake");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("milk")));

        Assert.Equal(EditOutcome.Applied, session.Apply(new ReplaceMatchAction(match, "cream")).Outcome);
        Assert.Equal("creamshake", steps[0].GetOrNull("text"));
    }

    [Fact]
    public void AnEmptyReplacementDeletesTheOccurrence()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("milk")));

        session.Apply(new ReplaceMatchAction(match, string.Empty));

        Assert.Equal(string.Empty, steps[0].GetOrNull("text"));
    }

    [Fact]
    public void AReplacementIsOneUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("milk")));

        session.Apply(new ReplaceMatchAction(match, "cream"));

        Assert.True(session.Undo());
        Assert.Equal("milk", steps[0].GetOrNull("text"));
    }

    [Fact]
    public void AValueTheTypeRefusesIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        steps[0].Set("index", "42");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("42")));

        var result = session.Apply(new ReplaceMatchAction(match, "not a number"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal("42", steps[0].GetOrNull("index"));
    }

    [Fact]
    public void ATextThatHasMovedOnIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("milk")));
        var cell = CellAddress.OfSlot(steps[0].Id, "text");

        // Through the session, so the tree is rebuilt and the match really is stale: the value is now shorter
        // than the range the match names.
        session.Select(new Selection(CaretPosition.At(cell, 0), CaretPosition.At(cell, 4)));
        session.Apply(new TypeTextAction("br"));

        Assert.Equal("br", steps[0].GetOrNull("text"));

        var result = session.Apply(new ReplaceMatchAction(match, "cream"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal("br", steps[0].GetOrNull("text"));
    }
}