using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples.StateMachine;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// How the state machine is drawn: the block form, and the same machine as a table of its transitions.
/// </summary>
/// <remarks>
/// The snapshots are exact text on purpose - the notation is a contract with every test and every user, and a
/// projection change that moves a cell has to fail a test that names this language rather than a pixel diff.
/// </remarks>
public sealed class StateMachineNotationsTests
{
    [Fact]
    public void AMachineIsABracedBlockOfStatesAndTransitions()
    {
        var machine = Machine();

        var drawn = LayoutSnapshot.Render(machine, StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts), width: 120);

        Assert.Equal(
            """
            machine Release {
              initial: Idle
              states:
                state Idle
                state Running
                state Done final
              transitions:
                on start from Idle to Running when attempts > 0
                on finish from Running to Done when <empty>
            }
            """,
            drawn);
    }

    /// <summary>
    /// The same machine read as a table of its transitions - the second notation, chosen at projection time.
    /// </summary>
    /// <remarks>
    /// The columns are the properties a row owns. `source` and `target` are references and therefore not
    /// columns: a table column names a property or a single child, so a reference has no cell shape the grid
    /// can give it. That is a gap in the table cell rather than a decision of this language, and the snapshot
    /// records it so closing the gap is a visible change to this test.
    /// </remarks>
    [Fact]
    public void TheSameMachineCanBeReadAsATableOfItsTransitions()
    {
        var machine = Machine();
        var notations = StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts);
        var choice = NotationChoice.Default.With(SmConceptKeys.Machine, StateMachineLanguage.TableVariant);

        var drawn = LayoutSnapshot.Render(machine, notations, width: 120, choice: choice);

        Assert.Equal(
            """
            machine Release
              initial: Idle
              event  from    to      when
              start  Idle    Running attempts > 0
              finish Running Done    <empty>
            """,
            drawn);
    }

    /// <summary>
    /// A state that is not final draws its name and no word at all - the conditional cell emits no cells, not an
    /// empty one a caret would stop in.
    /// </summary>
    [Fact]
    public void AStateThatIsNotFinalDrawsNoFinalWord()
    {
        var machine = Machine();
        var notations = StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts);
        var running = machine.GetChildren(StateMachineLanguage.StatesSlot)[1];

        var drawn = LayoutSnapshot.Render(running, notations, width: 40);

        Assert.Equal("state Running", drawn);
    }

    /// <summary>
    /// A machine with three states, one of them final, and two transitions - one guarded, one not.
    /// </summary>
    /// <remarks>
    /// Built through the model API rather than loaded from a file, so the fixture cannot drift from what the
    /// declarations accept; the corpus test is what proves a written document reads back.
    /// </remarks>
    private static Node Machine()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Release");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Release");
        model.AddRoot(machine);

        var idle = State(model, machine, "Idle");
        var running = State(model, machine, "Running");
        var done = State(model, machine, "Done", isFinal: true);

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);

        Transition(model, machine, "start", idle, running, guard: "attempts > 0");
        Transition(model, machine, "finish", running, done, guard: null);

        return machine;
    }

    private static Node State(Model model, Node machine, string name, bool isFinal = false)
    {
        var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", isFinal);

        return state;
    }

    private static void Transition(Model model, Node machine, string @event, Node source, Node target, string? guard)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", @event);
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
}
