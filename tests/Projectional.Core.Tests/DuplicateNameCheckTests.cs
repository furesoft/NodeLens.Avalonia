using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD002</c>: two children of one slot that answer to the same name.
/// </summary>
/// <remarks>
/// The rule has three parts a test has to keep apart - the group is one slot, the later child is the one
/// reported, and a name something points at is left alone - so each of them is pinned on its own. The rules
/// language supplies the list of named children; the state machine supplies the references, because a state is
/// what a transition points at.
/// </remarks>
public sealed class DuplicateNameCheckTests
{
    private static DuplicateNameCheck Check => new();

    [Fact]
    public void TheLaterOfTwoRulesUnderOneNameIsReported()
    {
        var (model, rules) = RulesUnderOneName("Check", "Check");

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(DuplicateNameCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal("name", finding.Role);
        Assert.Equal("another rule in this ruleset is already called \"Check\".", finding.Message);
        Assert.Equal(rules[1].Id, finding.Target);
        Assert.Equal("Rename to \"Check2\"", fix.Label);
        Assert.Equal(EditSafety.Plain, fix.Safety);
        Assert.False(fix.IsDisabled);
    }

    [Fact]
    public void TwoNamesInDifferentSlotsAreNotOneGroup()
    {
        // Both models are roots, and each owns a state called "Idle". A root is not a slot, so the two lists
        // are two groups and neither repeats a name of its own.
        var model = StateMachineLanguage.NewProject().NewModel("sm");
        AddMachine(model, "first", "Idle");
        AddMachine(model, "second", "Idle");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ThreeRulesUnderOneNameAreOfferedDifferentNames()
    {
        var (model, _) = RulesUnderOneName("Check", "Check", "Check");

        Assert.Equal(
            ["Rename to \"Check2\"", "Rename to \"Check3\""],
            Check.Find(model).Select(fix => fix.Label));
    }

    [Fact]
    public void ANameThatSomethingPointsAtIsLeftAlone()
    {
        var (model, states) = StatesUnderOneName("Idle", "Idle");
        var transition = AddTransition(model, model.Roots[0], source: states[1]);

        Assert.Empty(Check.Find(model));

        transition.SetReference("source", target: null);

        Assert.Single(Check.Find(model));
    }

    [Fact]
    public void AReferenceFromAnotherDocumentInTheProjectCounts()
    {
        var (model, states) = StatesUnderOneName("Idle", "Idle");
        var other = model.Project.NewModel("other");
        var machine = AddMachine(other, "Other", "Idle");

        AddTransition(other, machine, source: states[1]);

        // The reference crosses a model boundary, which the model allows; the rename would still leave it
        // pointing at a name no node has, so the check must see it.
        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TwoPlaceholderNamesAreNotOneName()
    {
        // Both rules still hold the concept's own placeholder, which is STD001's finding rather than a
        // collision: nobody has named either of them yet.
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AConceptWithoutANameSlotCannotCollide()
    {
        var (body, _) = TestEditor.BodyWithSteps("first", "second");

        Assert.Empty(Check.Find(body.Model));
    }

    [Fact]
    public void TheRepairRenamesTheLaterRuleAndUndoTakesItBack()
    {
        var (model, rules) = RulesUnderOneName("Check", "Check");
        var fix = Assert.Single(Check.Find(model));

        using var session = Open(model);
        Publish(session, fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("Check", rules[0].GetOrNull("name"));
        Assert.Equal("Check2", rules[1].GetOrNull("name"));
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Equal("Check", rules[1].GetOrNull("name"));
        Assert.Single(Check.Find(model));
    }

    [Fact]
    public void TheProposalIsRespelledByTheConceptsNameRule()
    {
        // A rename may not write a name the language would report. The shared name is already no snake_case, so
        // the offer is `order_item2` - from the same NameRule.Propose the spelling check offers.
        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Test.Item",
                [new PropertySlot("name", PropertyType.String)],
                nameSlot: "name",
                nameRule: NameRule.Snake),
            new ConceptDeclaration("Test.List", [new ChildSlot("items", "Test.Item", Cardinality.Many)]),
        ]);

        var model = new Project(registry).NewModel("document");
        var list = model.NewNode("Test.List");
        model.AddRoot(list);

        foreach (var _ in new[] { 1, 2 })
        {
            model.NewChild(list, "items", "Test.Item").Set("name", "orderItem");
        }

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal("Rename to \"order_item2\"", fix.Label);
        Assert.True(NameRule.Snake.IsValid("order_item2"));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    /// <summary>A ruleset holding one rule per name, in that order.</summary>
    private static (Model Model, IReadOnlyList<Node> Rules) RulesUnderOneName(params string[] names)
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        return (model, [.. names.Select(name =>
        {
            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", name);

            return rule;
        })]);
    }

    /// <summary>A machine holding one state per name, in that order.</summary>
    private static (Model Model, IReadOnlyList<Node> States) StatesUnderOneName(params string[] names)
    {
        var model = StateMachineLanguage.NewProject().NewModel("sm");
        var machine = AddMachine(model, "Release");

        return (model, [.. names.Select(name =>
        {
            var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
            state.Set("name", name);

            return state;
        })]);
    }

    /// <summary>A machine root called <paramref name="name"/>, holding an optional first state.</summary>
    private static Node AddMachine(Model model, string name, string? stateName = null)
    {
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", name);
        model.AddRoot(machine);

        if (stateName is not null)
        {
            model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State).Set("name", stateName);
        }

        return machine;
    }

    /// <summary>A transition of <paramref name="machine"/> that starts in <paramref name="source"/>.</summary>
    private static Node AddTransition(Model model, Node machine, Node source)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.SetReference("source", source);

        return transition;
    }

    private static EditorSession Open(Model model) =>
        new(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));

    private static void Publish(EditorSession session, DiagnosticFix fix)
    {
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);
    }
}
