using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a state machine means when it is run: a transition is a predicate, and the machine walks from its
/// initial state taking the first transition that applies.
/// </summary>
/// <remarks>
/// The machine under test is `Idle -start[attempts &gt; 0]-&gt; Running -finish-&gt; Done`, so the interesting
/// runs are the one that reaches `Done` and the two that stop early - a guard that refuses and a machine that
/// never settles.
/// </remarks>
public sealed class StateMachineEvaluationTests
{
    [Fact]
    public void TheMachineWalksFromTheInitialStateToTheFinalOne()
    {
        var model = Machine();

        var result = Run(model, 3);

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue("Idle -> Running -> Done"), result.Value);
    }

    [Fact]
    public void ATransitionWithoutAGuardAppliesUnconditionally()
    {
        // `finish` has no guard, so reaching Running is enough - the walk does not ask the second arrow for
        // anything the author did not write.
        var model = Machine();

        Assert.True(Run(model, 3).Succeeded);
    }

    [Fact]
    public void AGuardThatRefusesLeavesTheMachineStuckAndSaysWhere()
    {
        var model = Machine();

        var result = Run(model, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.Evaluation, result.Failure!.Kind);
        Assert.Contains("\"Idle\"", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMachineWithoutAnInitialStateIsAFailure()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Machine");
        var machine = BuildMachine(model);

        Assert.Contains("initial", Run(machine).Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMachineThatNeverSettlesIsAFailureRatherThanAHang()
    {
        // `Idle -> Running -> Idle` is an ordinary design, so the walk has to be bounded: the bound is a fact
        // about a run, not about the document.
        var model = StateMachineLanguage.NewProject().NewModel("Machine");
        var machine = BuildMachine(model);
        var idle = State(model, machine, "Idle");
        var running = State(model, machine, "Running");

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);
        Transition(model, machine, idle, running);
        Transition(model, machine, running, idle);

        var result = Run(machine);

        Assert.False(result.Succeeded);
        Assert.Contains($"{StateMachineEvaluation.MaxSteps}", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTraceShowsTheArrowsThatWereTaken()
    {
        var model = Machine();
        var machine = model.Roots[0];

        var result = Run(model, 3);

        // The guard is evaluated before the arrow that carries it, and the walk reports last - the post-order a
        // reader follows back to the answer.
        Assert.Equal(
            [
                ExpressionsConceptKeys.Name,
                ExpressionsConceptKeys.IntLiteral,
                ExpressionsConceptKeys.Binary,
                SmConceptKeys.Transition,
                SmConceptKeys.Transition,
                SmConceptKeys.Machine,
            ],
            result.Trace.Select(entry => entry.ConceptKey));

        Assert.Equal(new BoolValue(true), result.Trace[3].Value);
        Assert.Equal(new StringValue("Idle -> Running -> Done"), result.Trace[^1].Value);
        Assert.Equal(machine.Id, result.Trace[^1].Node);
    }

    [Fact]
    public void TheRegistryRunsTheExpressionsAGuardIsWrittenIn()
    {
        // A guard is an expression, so the machine's registry carries the expression rules - otherwise the first
        // guard would fail with "declares no evaluation rule". The same composition the pilot's decision list
        // makes, asserted the same way: one rule object, reached from both registries.
        var comparison = StateMachineLanguage.Concepts.Get(ExpressionsConceptKeys.Binary);

        Assert.NotNull(StateMachineEvaluation.Registry.RuleFor(comparison));
        Assert.Same(ExpressionsEvaluation.Registry.RuleFor(comparison), StateMachineEvaluation.Registry.RuleFor(comparison));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The machine used by most of the tests, as a model with one root.</summary>
    private static Model Machine()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Machine");
        var machine = BuildMachine(model);
        var idle = State(model, machine, "Idle");
        var running = State(model, machine, "Running");
        var done = State(model, machine, "Done", isFinal: true);

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);
        Transition(model, machine, idle, running, guard: "attempts > 0");
        Transition(model, machine, running, done);

        return model;
    }

    private static Node BuildMachine(Model model)
    {
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Machine");
        model.AddRoot(machine);

        return machine;
    }

    private static Node State(Model model, Node machine, string name, bool isFinal = false)
    {
        var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", isFinal);

        return state;
    }

    private static void Transition(Model model, Node machine, Node source, Node target, string? guard = null)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", "go");
        transition.SetReference("source", source);
        transition.SetReference("target", target);

        if (guard is null)
        {
            return;
        }

        var condition = model.NewChild(transition, "guard", ExpressionsConceptKeys.Binary);
        condition.Set("operator", ">");
        model.NewChild(condition, "left", ExpressionsConceptKeys.Name).Set("name", "attempts");
        model.NewChild(condition, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 0);
    }

    private static InterpretationResult Run(Model model, int attempts)
    {
        Dictionary<string, Value> bindings = new(StringComparer.Ordinal) { ["attempts"] = new IntValue(attempts) };

        return new Interpreter(StateMachineEvaluation.Registry)
            .Run(model.Roots[0], bindings, cancellationToken: Token);
    }

    private static InterpretationResult Run(Node machine) =>
        new Interpreter(StateMachineEvaluation.Registry).Run(machine, cancellationToken: Token);
}
