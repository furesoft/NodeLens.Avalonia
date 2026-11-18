using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples.StateMachine;
using Projectional.Samples.StateMachine.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The unreachable-state rule: a state nothing leads to can never be entered.
/// </summary>
/// <remarks>
/// The rule reports and repairs nothing, which is why it is a constraint rather than a check - "wire it up",
/// "delete it" and "make it the initial state" are three different design decisions and none of them is the
/// language's to make.
/// </remarks>
public sealed class StateMachineConstraintsTests
{
    [Fact]
    public void AStateNoTransitionLeadsToIsReported()
    {
        var (model, machine) = Machine();
        var orphan = State(model, machine, "Orphan");

        var finding = Assert.Single(new ConstraintEngine(StateMachineConstraints.All).Check(model));

        Assert.Equal(orphan.Id, finding.Target);
        Assert.Equal(StateMachineConstraints.UnreachableCode, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Contains("Orphan", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInitialStateAndEveryTransitionTargetAreQuiet()
    {
        var (model, machine) = Machine();

        var idle = State(model, machine, "Idle", initial: true);
        var running = State(model, machine, "Running");
        var done = State(model, machine, "Done", isFinal: true);

        Transition(model, machine, idle, running);
        Transition(model, machine, running, done);

        // Done is nobody's source, and that is a final state's normal shape - reachability asks what leads *into*
        // a state, not what leaves it, or every final state would be reported on every machine.
        Assert.Empty(new ConstraintEngine(StateMachineConstraints.All).Check(model));
    }

    [Fact]
    public void AStateOutsideAMachineIsQuiet()
    {
        // A state on its own has no initial state and no transitions to compare against, which is the ordinary
        // state of a document being written - and the root of a file that holds one definition per document.
        var model = StateMachineLanguage.NewProject().NewModel("state");
        var state = model.NewNode(SmConceptKeys.State);
        state.Set("name", "Idle");
        model.AddRoot(state);

        Assert.Empty(new ConstraintEngine(StateMachineConstraints.All).Check(model));
    }

    /// <summary>A machine with three states and a transition from the first to the second.</summary>
    private static (Model Model, Node Machine) Machine()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Machine");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Machine");
        model.AddRoot(machine);

        return (model, machine);
    }

    private static Node State(Model model, Node machine, string name, bool initial = false, bool isFinal = false)
    {
        var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", isFinal);

        if (initial)
        {
            machine.SetReference(StateMachineLanguage.InitialSlot, state);
        }

        return state;
    }

    private static void Transition(Model model, Node machine, Node source, Node target)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", "go");
        transition.SetReference("source", source);
        transition.SetReference("target", target);
    }
}
