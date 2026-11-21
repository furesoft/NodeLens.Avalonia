using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Walking a finished run's trace, which is what stepping is.</summary>
/// <remarks>
/// The run itself is <see cref="InterpreterTests"/>' business; these tests are about the one thing stepping adds -
/// a place to stand - so they use the same two-node model and read the order off it rather than building a third.
/// </remarks>
public sealed class TraceCursorTests
{
    private static readonly EvaluationRegistry Rules = EvaluationRegistry.Empty
        .Add(TestLanguage.Step, new DelegateEvaluationRule(StepValue));

    [Fact]
    public void ACursorStandsBeforeTheFirstStepAndWalksForwardOnlyAsFarAsTheEnd()
    {
        var (outer, inner) = Nested();
        var cursor = new TraceCursor(Run(outer));

        // Nothing has been stepped yet: the reader has just opened the trace.
        Assert.Equal(-1, cursor.Position);
        Assert.Null(cursor.Current);
        Assert.True(cursor.IsAtStart);
        Assert.False(cursor.IsAtEnd);
        Assert.Equal(2, cursor.Count);

        Assert.True(cursor.Step());
        Assert.Equal(inner.Id, cursor.Current!.Value.Node);

        Assert.True(cursor.Step());
        Assert.Equal(outer.Id, cursor.Current!.Value.Node);
        Assert.True(cursor.IsAtEnd);

        // At the end a step answers "no" and does not move, which is what lets a shell drive a button from it.
        Assert.False(cursor.Step());
        Assert.Equal(1, cursor.Position);
    }

    [Fact]
    public void ACursorWalksBackToBeforeTheFirstStepAndStops()
    {
        var (outer, _) = Nested();
        var cursor = new TraceCursor(Run(outer));

        _ = cursor.Step();
        _ = cursor.Step();

        Assert.True(cursor.Back());
        Assert.Equal(0, cursor.Position);

        Assert.True(cursor.Back());
        Assert.Equal(-1, cursor.Position);
        Assert.Null(cursor.Current);
        Assert.True(cursor.IsAtStart);

        // Back at the beginning a step back answers "no" rather than wrapping to the end.
        Assert.False(cursor.Back());
        Assert.Equal(-1, cursor.Position);
    }

    [Fact]
    public void AResetPutsTheCursorBackAndASingleStepRunIsBothEndsAtOnce()
    {
        var single = Run(Indexed(5));
        var cursor = new TraceCursor(single);

        Assert.Equal(1, cursor.Count);
        Assert.False(cursor.IsAtEnd);

        _ = cursor.Step();

        // One step is the first and the last, which is what keeps "at the end" from needing a special case.
        Assert.True(cursor.IsAtEnd);
        Assert.False(cursor.IsAtStart);

        cursor.Reset();

        Assert.Equal(-1, cursor.Position);
        Assert.True(cursor.IsAtStart);
        Assert.False(cursor.IsAtEnd);
    }

    private static InterpretationResult Run(Node root) =>
        new Interpreter(Rules).Run(root, cancellationToken: TestContext.Current.CancellationToken);

    private static Value StepValue(EvaluationContext context) =>
        context.Node.GetChild("detail") is { } inner
            ? context.Eval(inner)
            : context.PropertyValue("index");

    private static Node Indexed(int index)
    {
        var step = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        step.Model.AddRoot(step);
        step.Set("index", index);

        return step;
    }

    private static (Node Outer, Node Inner) Nested()
    {
        var root = Indexed(1);
        var inner = root.Model.NewChild(root, "detail", TestLanguage.Step);

        inner.Set("index", 7);

        return (root, inner);
    }
}
