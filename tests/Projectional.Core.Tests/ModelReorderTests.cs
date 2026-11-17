using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Reordering the children of a list slot: the model owns the mutation, the caller owns the order.
/// </summary>
/// <remarks>
/// What is pinned here is the part the model promised rather than the part a caller chose: the slot holds the same
/// children afterwards, the whole rearrangement is one undo step, an order that already holds is not an edit at all,
/// and an order that is not a rearrangement is refused without touching anything.
/// </remarks>
public sealed class ModelReorderTests
{
    [Fact]
    public void TheChildrenComeOutInTheGivenOrder()
    {
        var (body, steps) = BodyWithSteps("a", "b", "c");

        body.Model.ReorderChildren(body, "steps", [steps[2], steps[0], steps[1]]);

        Assert.Equal(["c", "a", "b"], Texts(body));
    }

    [Fact]
    public void AReorderIsOneUndoStepThatPutsTheOldOrderBack()
    {
        var (body, steps) = BodyWithSteps("a", "b", "c");

        using var history = new UndoHistory(body.Model);

        body.Model.ReorderChildren(body, "steps", [steps[2], steps[1], steps[0]]);

        Assert.Equal(1, history.UndoDepth);
        Assert.Equal(["c", "b", "a"], Texts(body));

        Assert.True(history.Undo());
        Assert.Equal(["a", "b", "c"], Texts(body));

        Assert.True(history.Redo());
        Assert.Equal(["c", "b", "a"], Texts(body));
    }

    [Fact]
    public void AnOrderThatAlreadyHoldsIsNotAnEdit()
    {
        var (body, steps) = BodyWithSteps("a", "b");

        using var history = new UndoHistory(body.Model);

        body.Model.ReorderChildren(body, "steps", [steps[0], steps[1]]);

        Assert.Equal(0, history.UndoDepth);
        Assert.Equal(["a", "b"], Texts(body));
    }

    [Fact]
    public void AnOrderThatLeavesAChildOutIsRefused()
    {
        var (body, steps) = BodyWithSteps("a", "b", "c");

        Assert.Throws<ModelInvariantException>(
            () => body.Model.ReorderChildren(body, "steps", [steps[0], steps[1]]));

        Assert.Equal(["a", "b", "c"], Texts(body));
    }

    [Fact]
    public void AnOrderThatNamesAChildTwiceIsRefused()
    {
        var (body, steps) = BodyWithSteps("a", "b", "c");

        Assert.Throws<ModelInvariantException>(
            () => body.Model.ReorderChildren(body, "steps", [steps[0], steps[0], steps[1]]));

        Assert.Equal(["a", "b", "c"], Texts(body));
    }

    [Fact]
    public void AnOrderThatNamesANodeFromElsewhereIsRefused()
    {
        var (body, steps) = BodyWithSteps("a", "b");
        var (_, others) = BodyWithSteps("x", "y");

        Assert.Throws<ModelInvariantException>(
            () => body.Model.ReorderChildren(body, "steps", [steps[0], others[0]]));

        Assert.Equal(["a", "b"], Texts(body));
    }

    [Fact]
    public void ASlotThatHoldsOneChildHasNoOrderToChange()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        Assert.Throws<ModelInvariantException>(
            () => model.ReorderChildren(rule, "body", [body]));
    }

    /// <summary>A body holding one step per entry of <paramref name="texts"/>, with the text set.</summary>
    private static (Node Body, IReadOnlyList<Node> Steps) BodyWithSteps(params string[] texts)
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        List<Node> steps = [];

        foreach (var text in texts)
        {
            var step = model.NewChild(body, "steps", TestLanguage.Step);
            step.Set("text", text);
            steps.Add(step);
        }

        return (body, steps);
    }

    /// <summary>The text of each step of <paramref name="body"/>, in document order.</summary>
    private static IReadOnlyList<string?> Texts(Node body) =>
        [.. body.GetChildren("steps").Select(step => step.GetOrNull("text"))];
}
