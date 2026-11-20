using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the interpreter itself promises: values and their trace, a budget that stops a runaway model,
/// cancellation, and a failure that names the node it happened at.
/// </summary>
/// <remarks>
/// The rules here are a test language's rather than a real one's, because what is under test is the runtime, not
/// a DSL: a step evaluates its <c>detail</c> when it has one and its <c>index</c> otherwise, which is enough to
/// produce nesting, depth, values and failures on demand.
/// </remarks>
public sealed class InterpreterTests
{
    private static readonly EvaluationRegistry Rules = EvaluationRegistry.Empty
        .Add(TestLanguage.Step, new DelegateEvaluationRule(StepValue));

    [Fact]
    public void ARunAnswersTheValueOfTheRootAndCountsItsSteps()
    {
        var step = Indexed(5);

        var result = new Interpreter(Rules).Run(step, cancellationToken: Token);

        Assert.True(result.Succeeded);
        Assert.Equal(new IntValue(5), result.Value);
        Assert.Equal(1, result.Steps);

        var entry = Assert.Single(result.Trace);

        Assert.Equal(0, entry.Depth);
        Assert.Equal(step.Id, entry.Node);
        Assert.Equal(new IntValue(5), entry.Value);
    }

    [Fact]
    public void AFloatPropertyIsReadAsAFloatValue()
    {
        // A property is read by parsing whatever the model stored, so the value kinds have to cover every property
        // type - otherwise a generator over a float slot would report that it has "no value here".
        var rules = EvaluationRegistry.Empty
            .Add(TestLanguage.Step, new DelegateEvaluationRule(context => context.PropertyValue("weight")));
        var step = IndexedStep();

        step.Set("weight", 1.5);

        var result = new Interpreter(rules).Run(step, cancellationToken: Token);

        Assert.True(result.Succeeded);
        Assert.Equal(new FloatValue(1.5), result.Value);
        Assert.Equal("1.5", result.Value!.Display);
    }

    [Fact]
    public void TheTraceIsPostOrderSoAChildComesBeforeTheNodeThatAskedForIt()
    {
        var (outer, inner) = Nested(outer: 1, inner: 7);

        var result = new Interpreter(Rules).Run(outer, cancellationToken: Token);

        Assert.Equal(new IntValue(7), result.Value);
        Assert.Equal([inner.Id, outer.Id], result.Trace.Select(entry => entry.Node));
        Assert.Equal([1, 0], result.Trace.Select(entry => entry.Depth));
        Assert.Equal(2, result.Steps);
    }

    [Fact]
    public void ABudgetThatRunsOutStopsTheRunWithoutAValue()
    {
        var root = Chain(10);

        var result = new Interpreter(Rules).Run(root, budget: 4, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.BudgetExceeded, result.Failure!.Kind);
        Assert.Equal(4, result.Steps);
        Assert.Contains("4 step", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACancelledRunStopsBeforeTheNextNode()
    {
        var step = Indexed(5);
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        var result = new Interpreter(Rules).Run(step, cancellationToken: cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.Cancelled, result.Failure!.Kind);
        Assert.Equal(0, result.Steps);
        Assert.Empty(result.Trace);
    }

    [Fact]
    public void AFailureNamesTheNodeItHappenedAt()
    {
        // No index and no detail: the rule has nothing to read, so the failure is about this very node.
        var step = IndexedStep();
        step.Set("text", "no index");

        var result = new Interpreter(Rules).Run(step, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.Evaluation, result.Failure!.Kind);
        Assert.Equal(step.Id, result.Failure.Node!.Value);

        var entry = Assert.Single(result.Trace);

        Assert.Null(entry.Value);
        Assert.Equal(step.Id, entry.Node);
    }

    [Fact]
    public void AFailureInsideAChildIsReportedAtTheChildNotAtTheParent()
    {
        var outer = Indexed(1);
        var broken = outer.Model.NewNode(TestLanguage.Step);

        // The parent asks for its only child; the child has neither detail nor index, so it fails.
        outer.InsertChild("detail", 0, broken);

        var result = new Interpreter(Rules).Run(outer, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Equal(broken.Id, result.Failure!.Node!.Value);

        // The failing node is traced once, and its parent not at all: it produced no value.
        var entry = Assert.Single(result.Trace);

        Assert.Equal(broken.Id, entry.Node);
    }

    [Fact]
    public void AVariableTheRunDoesNotBindIsAFailureOfItsOwn()
    {
        var registry = EvaluationRegistry.Empty.Add(
            TestLanguage.Note,
            new DelegateEvaluationRule(context => context.Variable("amount")));

        var note = Model().NewNode(TestLanguage.Note);

        var result = new Interpreter(registry).Run(note, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Contains("amount", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABoundVariableIsReadByAName()
    {
        var registry = EvaluationRegistry.Empty.Add(
            TestLanguage.Note,
            new DelegateEvaluationRule(context => context.Variable("amount")));

        var note = Model().NewNode(TestLanguage.Note);
        var variables = new Dictionary<string, Value>(StringComparer.Ordinal) { ["amount"] = new IntValue(42) };

        var result = new Interpreter(registry).Run(note, variables, cancellationToken: Token);

        Assert.Equal(new IntValue(42), result.Value);
    }

    [Fact]
    public void ARunResetsTheStateOfThePreviousOne()
    {
        var step = Indexed(5);
        var interpreter = new Interpreter(Rules);

        var first = interpreter.Run(step, cancellationToken: Token);
        var second = interpreter.Run(step, cancellationToken: Token);

        Assert.Equal(first.Steps, second.Steps);
        Assert.Equal(first.Trace.Count, second.Trace.Count);
    }

    [Fact]
    public void ANonPositiveBudgetIsRefused()
    {
        var step = Indexed(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Interpreter(Rules).Run(step, budget: 0, cancellationToken: Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Value StepValue(EvaluationContext context) =>
        context.Node.GetChild("detail") is { } inner
            ? context.Eval(inner)
            : context.PropertyValue("index");

    private static Model Model() => TestLanguage.NewModel();

    private static Node IndexedStep()
    {
        var step = Model().NewNode(TestLanguage.Step);
        step.Model.AddRoot(step);

        return step;
    }

    private static Node Indexed(int index)
    {
        var step = IndexedStep();
        step.Set("index", index);

        return step;
    }

    private static (Node Outer, Node Inner) Nested(int outer, int inner)
    {
        var root = Indexed(outer);
        var child = root.Model.NewNode(TestLanguage.Step);

        child.Set("index", inner);
        root.InsertChild("detail", 0, child);

        return (root, child);
    }

    /// <summary>A chain of <paramref name="depth"/> steps, each holding the next one in its <c>detail</c>.</summary>
    private static Node Chain(int depth)
    {
        var root = Indexed(0);
        var current = root;

        for (var level = 1; level < depth; level++)
        {
            var next = root.Model.NewNode(TestLanguage.Step);

            next.Set("index", level);
            current.InsertChild("detail", 0, next);
            current = next;
        }

        return root;
    }
}
