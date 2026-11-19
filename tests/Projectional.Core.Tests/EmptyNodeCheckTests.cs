using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD012</c>: a list entry that holds nothing but the defaults its concept gives it.
/// </summary>
/// <remarks>
/// The three exclusions the check makes are the three cases that matter: a single-child slot is not a list, a
/// leaf is its value rather than an empty node, and one value, one child or one reference anywhere is content.
/// The repair is destructive, so the last test also pins that the node comes back with its own identity.
/// </remarks>
public sealed class EmptyNodeCheckTests
{
    private static EmptyNodeCheck Check => new();

    [Fact]
    public void AStepWithNothingInItIsReported()
    {
        var (body, _) = TestEditor.BodyWithSteps("first");
        var empty = body.Model.NewChild(body, "steps", TestLanguage.Step);

        var fix = Assert.Single(Check.Find(body.Model));
        var finding = fix.Diagnostic;

        Assert.Equal(EmptyNodeCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal(empty.Id, finding.Target);
        Assert.Null(finding.Role);
        Assert.Equal("this step holds nothing but its defaults.", finding.Message);
        Assert.Equal("Delete the empty step", fix.Label);
        Assert.Equal(EditSafety.Destructive, fix.Safety);
    }

    [Fact]
    public void OneValueOneChildOrOneReferenceIsContent()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first");
        body.Model.NewChild(steps[0], "detail", TestLanguage.Step);

        Assert.Empty(Check.Find(body.Model));

        var (model, machine, states, transitions) = Machine(states: 1, transitions: 2);

        transitions[0].Set("event", "start");
        transitions[1].SetReference("source", states[0]);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ATransitionThatLeadsNowhereIsReported()
    {
        var (_, _, _, transitions) = Machine(transitions: 1);

        var fix = Assert.Single(Check.Find(transitions[0].Model));

        Assert.Equal(transitions[0].Id, fix.Diagnostic.Target);
        Assert.Equal("Delete the empty transition", fix.Label);
    }

    [Fact]
    public void TheOnlyChildOfASingleValuedSlotIsNotReported()
    {
        // A body holds nothing either, but it stands in a slot rather than in a list: emptying that slot is
        // STD010's business, not a deletion.
        var (_, body) = TestEditor.RuleWithBody();

        Assert.Empty(Check.Find(body.Model));
    }

    [Fact]
    public void ALeafThatIsOneValueIsNotReported()
    {
        var (model, _, states, _) = Machine(states: 1);

        // A state's whole content is its name and its final flag, and a fresh one holds both by default.
        Assert.Single(states);
        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheRootIsNotReported()
    {
        var rule = TestEditor.BareRule();

        Assert.Empty(Check.Find(rule.Model));
    }

    [Fact]
    public void TheRepairDeletesTheNodeAndUndoTakesItBack()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first");
        var empty = body.Model.NewChild(body, "steps", TestLanguage.Step);
        var fix = Assert.Single(Check.Find(body.Model));

        using var session = TestEditor.Open(body);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Same(steps[0], Assert.Single(body.GetChildren("steps")));
        Assert.Empty(Check.Find(body.Model));

        Assert.True(session.Undo());
        Assert.Same(empty, body.GetChildren("steps")[1]);
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    /// <summary>
    /// A machine holding <paramref name="states"/> states and <paramref name="transitions"/> transitions, so a
    /// transition can hold a reference that resolves.
    /// </summary>
    private static (Model Model, Node Machine, IReadOnlyList<Node> States, IReadOnlyList<Node> Transitions) Machine(
        int states = 1,
        int transitions = 0)
    {
        var model = StateMachineLanguage.NewProject().NewModel("sm");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Release");
        model.AddRoot(machine);

        List<Node> stateNodes = [.. Enumerable.Range(0, states).Select(_ =>
            model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State))];

        List<Node> transitionNodes = [.. Enumerable.Range(0, transitions).Select(_ =>
            model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition))];

        return (model, machine, stateNodes, transitionNodes);
    }
}
