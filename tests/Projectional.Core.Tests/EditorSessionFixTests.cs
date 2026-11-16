using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Applying a fix: one undo step, a pick re-resolved against the current set, and nothing kept when a step
/// of the repair fails.
/// </summary>
/// <remarks>
/// The re-resolution test is the one that carries the design. A fix is a closure over the model of the
/// moment its set was published, so running the copy a caller is holding - after an undo, a reload or a
/// second run of the checks - is how a repair ends up applied to a document that has moved on.
/// </remarks>
public sealed class EditorSessionFixTests
{
    [Fact]
    public void ApplyingAFixIsOneUndoStep()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var fix = Publish(session, rule, "Rule1");

        var result = session.Apply(fix);

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("Rule1", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("unnamed", rule.GetOrNull("name"));

        // One step, not two: the repair did not leave a second one behind.
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AFixIsItsOwnStepEvenAfterATypingBurst()
    {
        // A repair is an undo boundary: the burst before it must not swallow it, and pressing undo once
        // has to take the repair back rather than half of the typing.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        session.Select(Selection.At(CaretPosition.At(name, 4)));
        session.Apply(new TypeTextAction("ing"));

        var fix = Publish(session, rule, "walked");

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("walked", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("walking", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void TheCurrentFixIsTheOneThatRunsNotTheCallersCopy()
    {
        // The same finding, the same id, a different repair: the pick has to run the object from the set
        // that is current now, because that is the one built against the model that exists now.
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var stale = Publish(session, rule, "Stale");

        session.Fixes = new FixSet(RenameFix(stale.Diagnostic, "Current"));

        var result = session.Apply(stale);

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("Current", rule.GetOrNull("name"));
    }

    [Fact]
    public void APickThatNoLongerResolvesIsRejected()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var fix = Publish(session, rule, "Rule1");

        // The checks ran again and no longer offer this repair.
        session.Fixes = FixSet.Empty;

        var result = session.Apply(fix);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Reason));
        Assert.Equal("unnamed", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ADisabledFixIsRejectedWithItsOwnReason()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var diagnostic = Finding(rule);
        var fix = new DiagnosticFix(
            diagnostic,
            "rename",
            "Rename to \"Rule1\"",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Rule1"),
            availability: FixAvailability.Disabled("the document is open read-only"));

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        var result = session.Apply(fix);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("the document is open read-only", result.Reason);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void AFixThatBuildsNothingIsRejected()
    {
        // A repair may decline once it looks at the node - the finding may be moot by the time the user
        // picks it. Declining is not a crash and not a silent no-op.
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var diagnostic = Finding(rule);
        var fix = new DiagnosticFix(diagnostic, "rename", "Rename", _ => null);

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        var result = session.Apply(fix);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    [Fact]
    public void AFixWhoseTargetIsGoneDeclinesInsteadOfThrowing()
    {
        // The stale case: the diagnostic outlived its node, so the context carries no node and the repair
        // gets to answer "nothing to do" rather than throwing inside a menu. A node of another project
        // models exactly that - the id is real, the project it is asked about does not hold it.
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var diagnostic = new Diagnostic(DiagnosticSeverity.Warning, TestEditor.BareRule().Id, "elsewhere");
        var seen = "not called";
        var fix = new DiagnosticFix(
            diagnostic,
            "rename",
            "Rename",
            context =>
            {
                seen = context.IsStale ? "stale" : "fresh";

                return null;
            });

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Rejected, session.Apply(fix).Outcome);
        Assert.Equal("stale", seen);
    }

    [Fact]
    public void AProgramAppliesItsStepsInOrder()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        var program = EditProgram.Of(
            new SetPropertyAction(name, "first"),
            new SetPropertyAction(name, "second"));
        var diagnostic = Finding(rule);
        var fix = new DiagnosticFix(diagnostic, "rename", "Rename", _ => program);

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("second", rule.GetOrNull("name"));

        // Both steps commit under one step of history, so one undo takes the whole repair back.
        Assert.True(session.Undo());
        Assert.Equal("unnamed", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ARejectedProgramStepTakesTheWholeProgramBack()
    {
        // A repair that cannot finish must not leave the first half applied: the model, the tree and the
        // history all describe the state before the pick.
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var program = EditProgram.Of(
            new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "renamed"),
            new SetPropertyAction(CellAddress.OfSlot(rule.Id, "nope"), "x"));
        var diagnostic = Finding(rule);
        var fix = new DiagnosticFix(diagnostic, "rename", "Rename", _ => program);

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        var result = session.Apply(fix);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);

        // The session was rebuilt against the restored model, so the document is still usable.
        Assert.Equal(EditOutcome.Applied, session.Apply(new SetPropertyAction(
            CellAddress.OfSlot(rule.Id, "name"),
            "afterwards")).Outcome);
        Assert.Equal("afterwards", rule.GetOrNull("name"));
    }

    [Fact]
    public void AnEmptyProgramChangesNothing()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        var diagnostic = Finding(rule);
        var fix = new DiagnosticFix(diagnostic, "nothing", "Nothing", _ => EditProgram.Of());

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(fix).Outcome);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AProgramReachesTheSessionThroughTheActionOverloadToo()
    {
        // A fix builds its repair as an EditAction, so the program arrives at the overload that takes one.
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);
        EditAction program = EditProgram.Of(
            new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "via action"));

        Assert.Equal(EditOutcome.Applied, session.Apply(program).Outcome);
        Assert.Equal("via action", rule.GetOrNull("name"));
    }

    [Fact]
    public void ANullFixIsRejected()
    {
        var rule = RuleNamed("unnamed");
        using var session = TestEditor.Open(rule);

        Assert.Throws<ArgumentNullException>(() => session.Apply((DiagnosticFix)null!));
    }

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }

    private static Diagnostic Finding(Node rule) =>
        new(DiagnosticSeverity.Warning, rule.Id, "the name says nothing", role: "name");

    /// <summary>A repair that renames the finding's node, built the way a real one is: from the context.</summary>
    private static DiagnosticFix RenameFix(Diagnostic diagnostic, string name) =>
        new(
            diagnostic,
            "rename",
            $"Rename to \"{name}\"",
            context => context.Node is { } node
                ? new SetPropertyAction(CellAddress.OfSlot(node.Id, "name"), name)
                : null,
            "Gives the rule the name the finding suggests.");

    /// <summary>Publishes one finding and one repair for it, and answers the repair.</summary>
    private static DiagnosticFix Publish(EditorSession session, Node rule, string name)
    {
        var diagnostic = Finding(rule);
        var fix = RenameFix(diagnostic, name);

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(fix);

        return fix;
    }
}
