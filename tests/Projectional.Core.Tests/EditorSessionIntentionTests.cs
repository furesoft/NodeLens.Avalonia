using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Applying an offer: one undo step, the action built at the moment it runs, and nothing kept when a program
/// of steps is refused.
/// </summary>
/// <remarks>
/// The two rules worth pinning are that an offer is refused rather than applied when it no longer holds (the
/// list described a document that may have been edited since), and that an offer carrying a quick fix goes
/// back through the fix's own path so its finding is re-resolved. Everything else here is the same undo
/// contract the keystrokes already have.
/// </remarks>
public sealed class EditorSessionIntentionTests
{
    [Fact]
    public void ApplyingAnIntentionIsOneUndoStep()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        var offer = Offer(session, TestIntentions.RenameId);

        var result = session.Apply(offer);

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("renamed", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("walk", rule.GetOrNull("name"));

        // One step, not two: the offer did not leave a second one behind.
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AnIntentionIsItsOwnStepEvenAfterATypingBurst()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);
        var name = CellAddress.OfSlot(rule.Id, "name");

        session.Select(Selection.At(CaretPosition.At(name, 4)));
        session.Apply(new TypeTextAction("ing"));

        var offer = Offer(session, TestIntentions.RenameId);

        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Equal("renamed", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("walking", rule.GetOrNull("name"));

        Assert.True(session.Undo());
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void AnAbsentIntentionIsRejectedWithoutEditing()
    {
        // The offer named a caret that has moved onto a step, so the rule's intentions are absent by now.
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        var stale = TestIntentions.ForRules.At(session.CreateIntentionContext())[0];

        var result = session.Apply(stale);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ADisabledIntentionIsRejectedWithItsReason()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var intention = new Intention(
            "test.blocked",
            "Blocked",
            "Something that cannot run now.",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "other"),
            availability: IntentionAvailability.Disabled("nothing to wrap here"));

        var result = session.Apply(intention);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("nothing to wrap here", result.Reason);
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void AnIntentionThatHasNothingToDoIsRejected()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var intention = new Intention("test.noop", "No-op", "Declines after all.", _ => null);

        var result = session.Apply(intention);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AnIntentionBuildsItsActionWhenItRunsAndNotWhenItIsDeclared()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var builds = 0;

        var intention = new Intention(
            "test.counted",
            "Counted",
            "Writes whatever the name is when it runs.",
            context =>
            {
                builds++;

                return new SetPropertyAction(
                    CellAddress.OfSlot(rule.Id, "name"),
                    context.Node?.Name is { } name ? name + "!" : "none");
            });

        Assert.Equal(0, builds);

        session.Apply(intention);
        Assert.Equal(1, builds);
        Assert.Equal("walk!", rule.GetOrNull("name"));
    }

    [Fact]
    public void AProgramIsOneStepAndRollsBackCompletely()
    {
        // The second step names a property the notation does not draw, so it is refused - and the first step
        // has to come back with it.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var intention = new Intention(
            "test.program",
            "Two steps",
            "Renames and then writes somewhere impossible.",
            _ => EditProgram.Of(
                new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "renamed"),
                new SetPropertyAction(CellAddress.OfSlot(rule.Id, "nonsense"), "x")));

        var result = session.Apply(intention);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AnEntryFromAFixRunsTheRepairThatIsCurrentNow()
    {
        // The entry was built from the set that was published when the list was derived; the checks have run
        // again since, so the object it carries is stale by the time it runs.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule);
        var offered = RenameFix(finding, CellAddress.OfSlot(rule.Id, "name"), "first");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(offered);

        var entry = Offer(session, "test.rename");

        session.Fixes = new FixSet(RenameFix(finding, CellAddress.OfSlot(rule.Id, "name"), "second"));

        Assert.Equal(EditOutcome.Applied, session.Apply(entry).Outcome);
        Assert.Equal("second", rule.GetOrNull("name"));
    }

    [Fact]
    public void AnEntryFromAFixWhoseFindingIsGoneIsRejected()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule);
        var fix = RenameFix(finding, "first");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(fix);

        var entry = Offer(session, "test.rename");

        session.Fixes = FixSet.Empty;

        var result = session.Apply(entry);

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void ApplyingAnIntentionClosesAnOpenQuickFixList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        var first = Finding(rule, "one");
        var second = Finding(rule, "two", range: CharacterRange.Of(1, 3));

        session.Markers = new MarkerSet(first, second);
        session.Fixes = new FixSet(RenameFix(first, "first"), RenameFix(second, "second"));

        session.Apply(new QuickFixAction());
        Assert.NotNull(session.OfferedFixes);

        session.AddIntentions(TestIntentions.ForRules);
        session.Apply(Offer(session, TestIntentions.RenameId));

        Assert.Null(session.OfferedFixes);
        Assert.Equal("renamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void ANullIntentionIsRefused()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Throws<ArgumentNullException>(() => session.Apply((Intention)null!));
    }

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);

    private static DiagnosticFix RenameFix(Diagnostic finding, string text) =>
        new(finding, "test.rename", $"Rename to \"{text}\"", _ => null);

    /// <summary>A repair that actually writes the name, for the tests that apply it.</summary>
    private static DiagnosticFix RenameFix(Diagnostic finding, CellAddress name, string text) =>
        new(finding, "test.rename", $"Rename to \"{text}\"", _ => new SetPropertyAction(name, text));

    private static Diagnostic Finding(Node node, string message = "the name says nothing", CharacterRange? range = null) =>
        new(DiagnosticSeverity.Warning, node.Id, message, role: "name", range: range ?? CharacterRange.Of(0, 4));

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
