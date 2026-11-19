using Avalonia.Input;

using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples.StateMachine;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Going to the declaration a reference names: which target, where the caret lands, and what it refuses.</summary>
/// <remarks>
/// The state machine is the smallest fixture whose reference points at a declaration that is on the page - the test
/// notation's own reference points at a tag the notation does not project, which is exactly the case that must not
/// navigate.
/// </remarks>
public sealed class GoToDefinitionTests
{
    [Fact]
    public void AReferenceGoesToTheDeclarationItNames()
    {
        var (machine, idle, _, _) = Machine();
        using var session = Open(machine);

        SelectSource(session);

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(CellAddress.OfSlot(idle.Id, "name"), session.Selection.Caret.Address);

        // The declaration's name is selected, so what the caret landed on is readable at a glance.
        Assert.True(session.Selection.HasText);
    }

    [Fact]
    public void GoingToADefinitionIsNotAnUndoStep()
    {
        var (machine, _, _, _) = Machine();
        using var session = Open(machine);

        SelectSource(session);

        var before = session.History.UndoDepth;

        session.Apply(new GoToDefinitionAction());

        Assert.Equal(before, session.History.UndoDepth);
    }

    [Fact]
    public void ACaretThatIsNotOnAReferenceDoesNothing()
    {
        var (machine, _, _, _) = Machine();
        using var session = Open(machine);

        session.Select(Selection.At(CellAddress.OfSlot(machine.Id, "name")));
        var before = session.Selection;

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, session.Selection);
        Assert.Null(GoToDefinition.TargetOf(session));
    }

    [Fact]
    public void AnUnsetReferenceHasNothingToGoTo()
    {
        var (machine, _, _, _) = Machine(withSource: false);
        using var session = Open(machine);

        SelectSource(session, ReferenceState.Unset);

        var before = session.Selection;

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, session.Selection);
    }

    [Fact]
    public void AnUnresolvedReferenceHasNothingToGoTo()
    {
        var (machine, _, _, _) = Machine();
        var transition = machine.GetChildren(StateMachineLanguage.TransitionsSlot)[0];
        transition.SetReference("source", NodeId.New(), resolveInfo: "gone");

        using var session = Open(machine);

        SelectSource(session, ReferenceState.Unresolved);

        var before = session.Selection;

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, session.Selection);
        Assert.Null(GoToDefinition.TargetOf(session));
    }

    [Fact]
    public void ADefinitionThatIsNotDrawnIsRejected()
    {
        var (machine, _, _, _) = Machine();
        var orphan = machine.Model.NewNode(SmConceptKeys.State);
        orphan.Set("name", "Orphan");
        machine.Model.AddRoot(orphan);

        machine.GetChildren(StateMachineLanguage.TransitionsSlot)[0].SetReference("source", orphan);

        using var session = Open(machine);

        SelectSource(session);

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Contains("not drawn", result.Reason);
        Assert.False(GoToDefinition.CanNavigate(session));
    }

    [Fact]
    public void ADefinitionInAnotherModelIsRejected()
    {
        var project = StateMachineLanguage.NewProject();
        var first = project.NewModel("First");
        var second = project.NewModel("Second");

        var machine = NewMachine(first);
        var (idle, running) = States(machine);
        NewTransition(machine, idle, running);

        var foreign = second.NewNode(SmConceptKeys.State);
        foreign.Set("name", "Foreign");
        second.AddRoot(foreign);
        machine.GetChildren(StateMachineLanguage.TransitionsSlot)[0].SetReference("source", foreign);

        using var session = Open(machine);

        SelectSource(session);

        var result = session.Apply(new GoToDefinitionAction());

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Contains("not drawn", result.Reason);
        Assert.Same(foreign, GoToDefinition.TargetOf(session));
    }

    [Fact]
    public void TheMenuOffersGoToDefinitionOnlyWhereItCanGo()
    {
        var (machine, _, _, _) = Machine();
        using var session = Open(machine);

        SelectSource(session);

        var enabled = Entry(session);
        Assert.True(enabled.IsEnabled);
        Assert.Equal("Ctrl+B", enabled.Gesture);

        session.Select(Selection.At(CellAddress.OfSlot(machine.Id, "name")));

        Assert.False(Entry(session).IsEnabled);
    }

    [Fact]
    public void CtrlBIsTheGestureAndTheRouterKnowsIt()
    {
        Assert.Equal(EditorKey.GoToDefinition, EditorKeyMap.Map(Key.B, KeyModifiers.Control));
        Assert.Null(EditorKeyMap.Map(Key.B, KeyModifiers.None));
        Assert.IsType<GoToDefinitionAction>(KeyRouter.For(EditorKey.GoToDefinition));
    }

    private static EditorSession Open(Node machine) =>
        new(machine, StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts));

    private static void SelectSource(EditorSession session, ReferenceState state = ReferenceState.Resolved)
    {
        var reference = session.Tree.Cells()
            .OfType<ReferenceCell>()
            .First(cell => cell.Slot == "source" && cell.State == state);

        session.Select(Selection.At(reference.Address));
    }

    private static EditorMenuItem Entry(EditorSession session) =>
        EditorMenu.For(session)
            .OfType<EditorMenuItem>()
            .First(item => item.Label == "Go to Definition");

    private static (Node Machine, Node Idle, Node Running, Node Transition) Machine(bool withSource = true)
    {
        var model = StateMachineLanguage.NewProject().NewModel("Release");
        var machine = NewMachine(model);
        var (idle, running) = States(machine);

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);

        return (machine, idle, running, NewTransition(machine, withSource ? idle : null, running));
    }

    private static Node NewMachine(Model model)
    {
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Release");
        model.AddRoot(machine);

        return machine;
    }

    private static (Node Idle, Node Running) States(Node machine)
    {
        var idle = State(machine, "Idle");
        var running = State(machine, "Running");

        return (idle, running);
    }

    private static Node State(Node machine, string name)
    {
        var state = machine.Model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", false);

        return state;
    }

    private static Node NewTransition(Node machine, Node? source, Node target)
    {
        var transition = machine.Model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", "go");
        transition.SetReference("target", target);

        if (source is not null)
        {
            transition.SetReference("source", source);
        }

        return transition;
    }
}
